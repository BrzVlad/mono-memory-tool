using System;
using System.Threading;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Collections.ObjectModel;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;

public enum IntervalType {
	NurseryCollection,
	MajorCollection,
	ConcurrentCollection
}

public struct Point {
	public float x, y;
	
	public Point (float x, float y)
	{
		this.x = x;
		this.y = y;
	}
};

public struct Interval {
	public float start, end;
	public bool done;
}

public static class ArrayExtensions {
	public static T[] SubArray<T>(this T[] data, int index)
	{
		T[] result = new T[data.Length - index];
		Array.Copy(data, index, result, 0, data.Length - index);
		return result;
	}
}


public class Program {
	public const string binprotFile = "/tmp/binprot";
	public const string binprotExec = "sgen-grep-binprot";

	public const int deltaHackConc = 4;
	public const int deltaHackNoconc = deltaHackConc;

	private static int num_minor_while_major;

	private static List<Point> memoryUsage;
	private static List<Interval> nurseryIntervals, majorIntervals, concurrentIntervals, stopIntervals;
	private static PlotModel plotModel;

	public static void Main (string[] args) {
		if (args.Length < 1) {
			Console.WriteLine ("Usage : ./analyzer.exe mono [mono-arg1] [mono-arg2] ...");
			return;
		}

		string target = Path.GetFileNameWithoutExtension (args[1]);
		string resultsFolder = Path.Combine ("results", target);
		string statsFile = Path.Combine (resultsFolder, "stats");
		string svgFile = Path.Combine (resultsFolder, target + ".svg");
		string mono = args [0];

		Directory.CreateDirectory (resultsFolder);
		using (StreamWriter statsWriter = new StreamWriter (statsFile)) {
			plotModel  = new PlotModel { Title = Path.GetFileNameWithoutExtension (args [1]) };
			plotModel.Axes.Add (new LinearAxis { Position = AxisPosition.Bottom });
			plotModel.Axes.Add (new LinearAxis { Position = AxisPosition.Left });

			/* Reduce jit compilation delays */
			Console.WriteLine (DateTime.Now.AddMilliseconds (100));

			RunMono (mono, args.SubArray<string> (1), false);
			ParseBinProtOutput (false);
			AddPlotData ();
			statsWriter.WriteLine ("Noconc Minor {0}, Major {1}, Minor While Major {2}", nurseryIntervals.Count, majorIntervals.Count, num_minor_while_major);
			Thread.Sleep (1000);

			RunMono (mono, args.SubArray<string> (1), true);
			ParseBinProtOutput (true);
			AddPlotData ();
			statsWriter.WriteLine ("Conc Minor {0}, Major {1}, Minor While Major {2}", nurseryIntervals.Count, majorIntervals.Count, num_minor_while_major);

			Plot (svgFile);
		}
	}



	public static void RunMono (string mono, string[] args, bool concurrent) {
		Process p = new Process ();
		p.StartInfo.UseShellExecute = false;
		p.StartInfo.FileName = mono;
		p.StartInfo.Arguments = string.Join (" ", args);

		if (concurrent)
			p.StartInfo.EnvironmentVariables.Add ("MONO_GC_PARAMS", "major=marksweep-conc");
		p.StartInfo.EnvironmentVariables.Add ("MONO_GC_DEBUG", "binary-protocol=" + binprotFile);

		p.Start ();

		DateTime startTime = DateTime.Now.AddMilliseconds (concurrent ? deltaHackConc : deltaHackNoconc);

		memoryUsage = new List<Point> ();

		while (!p.HasExited) {
			DateTime now = DateTime.Now;
			memoryUsage.Add (new Point (((float)(now - startTime).TotalMilliseconds) / 1000, (float)p.WorkingSet64 / 1000000)); 
			Thread.Sleep (1);
		}
		p.WaitForExit ();
	}

	public static void ParseCollections (List<Interval> intervals, string s, int generation) {
		StringReader reader = new StringReader (s);
		string line;

		if (generation < 0 || generation > 2)
			throw new Exception ("Wrong generation");;

		Regex nurseryRegex = new Regex (@"collection_begin index \d+ generation 0");
		Regex timestampRegex = new Regex (@"timestamp (\d+)");
		Regex startRegex, endRegex;
		if (generation == 0 || generation == 1) {
			startRegex = new Regex (@"collection_begin index \d+ generation " + generation);
			endRegex = new Regex (@"collection_end \d+ generation " + generation);
		} else {
			startRegex = new Regex ("concurrent_start");
			endRegex = new Regex ("concurrent_finish");
		}

		Interval interval = new Interval ();
		float timestamp = 0;

		while ((line = reader.ReadLine ()) != null) {
			if (startRegex.IsMatch (line)) {
				interval.start = timestamp;
			} else if (endRegex.IsMatch (line)) {
				interval.done = true;
			} else if (timestampRegex.IsMatch (line)) {
				Match m = timestampRegex.Match (line);
				/* original output is in 100ns ticks */
				timestamp = ((float)int.Parse (m.Groups [1].Value)) / 10000000;
				if (interval.done) {
					interval.end = timestamp;
					intervals.Add (interval);
					interval = new Interval ();
				}
			} else if (generation != 0 && nurseryRegex.IsMatch (line) && interval.start != default(float)) {
				/* Nursery collection while running the concurrent marksweep */
				num_minor_while_major++;
			}
		}

	}

	public static void ParseNurseryCollections (string s) {
		nurseryIntervals = new List<Interval> ();
		ParseCollections (nurseryIntervals, s, 0);
	}

	public static void ParseMajorCollections (string s) {
		majorIntervals = new List<Interval> ();
		num_minor_while_major = 0;
		ParseCollections (majorIntervals, s, 1);
	}

	public static void ParseConcurrentCollections (string s) {
		concurrentIntervals = new List<Interval> ();
		num_minor_while_major = 0;
		ParseCollections (concurrentIntervals, s, 2);
	}

	public static void ParseStopIntervals (string s) {
		stopIntervals = new List<Interval> ();
		StringReader reader = new StringReader (s);
		string line;

		Regex startRegex = new Regex (@"world_stopped generation \d+ timestamp (\d+)");
		Regex endRegex = new Regex (@"world_restarted generation \d+ timestamp (\d+)");
		Interval interval = new Interval ();

		while ((line = reader.ReadLine ()) != null) {
			if (startRegex.IsMatch (line)) {
				interval.start = ((float)int.Parse (startRegex.Match (line).Groups [1].Value)) / 10000000;
			} else if (endRegex.IsMatch (line)) {
				interval.end = ((float)int.Parse (endRegex.Match (line).Groups [1].Value)) / 10000000;
				stopIntervals.Add (interval);
			}
		}
	}

	public static void ParseBinProtOutput (bool concurrent) {
		Process p = new Process ();
		p.StartInfo.UseShellExecute = false;
		p.StartInfo.FileName = binprotExec; 
		p.StartInfo.Arguments = "-i " + binprotFile;
		p.StartInfo.RedirectStandardOutput = true; 
		
		p.Start ();

		string stdout = p.StandardOutput.ReadToEnd ();

		p.WaitForExit ();

		ParseNurseryCollections (stdout);
		if (concurrent)
			ParseConcurrentCollections (stdout);
		ParseMajorCollections (stdout);
		ParseStopIntervals (stdout);
	}

	public static void AddSeries (ElementCollection<Series> original, List<Interval> intervals, OxyColor color) {
		LineSeries series = null;
		int interval = 0;
		if (intervals == null || intervals.Count == 0)
			return;
		for (int mem = 0; mem < memoryUsage.Count && interval < intervals.Count; mem++) {
			if (memoryUsage [mem].x > intervals [interval].start &&
					memoryUsage [mem].x < intervals [interval].end) {
				if (series == null) {
					series = new LineSeries ();
					series.Color = color;
				}
				series.Points.Add (new DataPoint (memoryUsage [mem].x, memoryUsage [mem].y));
			} else if (memoryUsage [mem].x > intervals [interval].end) {
				interval++;
				mem--; /* Recheck the current point with the next interval */
				if (series != null) {
					original.Add (series);
					series = null;
				}
			}
		}
	}

	public static void AddPlotData () {
		LineSeries lineSeries = new LineSeries ();
		lineSeries.Color = OxyColor.FromRgb (0, 0, 0);
		for (int i = 0; i < memoryUsage.Count; i++)
			lineSeries.Points.Add (new DataPoint (memoryUsage [i].x, memoryUsage [i].y));
		plotModel.Series.Add (lineSeries);

		AddSeries (plotModel.Series, majorIntervals, OxyColor.FromRgb (255, 0, 0));
		AddSeries (plotModel.Series, concurrentIntervals, OxyColor.FromRgb (0, 0, 255));
		AddSeries (plotModel.Series, nurseryIntervals, OxyColor.FromRgb (0, 255, 0));
//		AddSeries (plotModel.Series, stopIntervals, OxyColor.FromRgb (192, 192, 192));
	}

	public static void Plot (string name) {
		using (FileStream stream = new FileStream (name, FileMode.Create)) {
			SvgExporter.Export (plotModel, stream, 1920, 1080, true);
		}
	}
}
