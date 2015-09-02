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
	public int generation;
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
	private static float referenceTime;

	public static void Main (string[] args) {
		if (args.Length < 2) {
			Console.WriteLine ("Usage : ./analyzer.exe mono working-directory [mono-arg1] [mono-arg2] ...");
			return;
		}

		string mono = args [0];
		string workingDirectory = args [1];
		string target = Path.GetFileNameWithoutExtension (args[2]);
		string resultsFolder = Path.Combine ("results", target);
		string statsFile = Path.Combine (resultsFolder, "stats");
		string svgFile = Path.Combine (resultsFolder, target + ".svg");

		Directory.CreateDirectory (resultsFolder);
		using (StreamWriter statsWriter = new StreamWriter (statsFile)) {
			plotModel  = new PlotModel { Title = Path.GetFileNameWithoutExtension (args [1]) };
			plotModel.Axes.Add (new LinearAxis { Position = AxisPosition.Bottom });
			plotModel.Axes.Add (new LinearAxis { Position = AxisPosition.Left });

			/* Reduce jit compilation delays */
			Console.WriteLine (DateTime.Now.AddMilliseconds (100));

			RunMono (mono, args.SubArray<string> (2), workingDirectory, false);
			referenceTime = memoryUsage [memoryUsage.Count - 1].x;
			ParseBinProtOutput (false);
			AddPlotData ();
			OutputStats ("noconc", statsWriter);

			RunMono (mono, args.SubArray<string> (2), workingDirectory, true);
			ParseBinProtOutput (true);
			AddPlotData ();
			OutputStats ("conc", statsWriter);

			Plot (svgFile);
		}
	}

	public static void OutputStats (string prefix, StreamWriter statsWriter) {
		float totalTime = memoryUsage [memoryUsage.Count - 1].x;
		statsWriter.WriteLine ("\n" + prefix);
		/* Number of collections */
		statsWriter.WriteLine ("Time (s)		{0}", totalTime);
		statsWriter.WriteLine ("Minor			{0}", nurseryIntervals.Count);
		statsWriter.WriteLine ("Major			{0}", majorIntervals.Count);
		statsWriter.WriteLine ("Minor While Major	{0}", num_minor_while_major);
		/* Pause times */
		float minMinor = float.MaxValue, maxMinor = float.MinValue, minMajor = float.MaxValue, maxMajor = float.MinValue, avgMinor = 0.0f, avgMajor = 0.0f, totalPause = 0.0f;
		int numMinor = 0, numMajor = 0;

		foreach (Interval interval in stopIntervals) {
			float intervalDuration = interval.end - interval.start;
			totalPause += intervalDuration;
			if (interval.generation == 0) {
				numMinor++;
				if (intervalDuration < minMinor)
					minMinor = intervalDuration;
				if (intervalDuration > maxMinor)
					maxMinor = intervalDuration;
				avgMinor += intervalDuration;
			} else if (interval.generation == 1) {
				numMajor++;
				if (intervalDuration < minMajor)
					minMajor = intervalDuration;
				if (intervalDuration > maxMajor)
					maxMajor = intervalDuration;
				avgMajor += intervalDuration;
			} else {
				throw new Exception ("Invalid genration");
			}
		}
		avgMinor /= numMinor;
		avgMajor /= numMajor;

		statsWriter.WriteLine ();
		statsWriter.WriteLine ("Total pause time (ms)	{0}", totalPause * 1000);
		statsWriter.WriteLine ("Minor stops		{0}", numMinor);
		statsWriter.WriteLine ("Min minor (ms)		{0}", minMinor * 1000);
		statsWriter.WriteLine ("Max minor (ms)		{0}", maxMinor * 1000);
		statsWriter.WriteLine ("Avg minor (ms)		{0}", avgMinor * 1000);
		statsWriter.WriteLine ("Major stops		{0}", numMajor);
		statsWriter.WriteLine ("Min major (ms)		{0}", minMajor * 1000);
		statsWriter.WriteLine ("Max major (ms)		{0}", maxMajor * 1000);
		statsWriter.WriteLine ("Avg major (ms)		{0}", avgMajor * 1000);

		/* Memory Usage */
		float memoryUsageStat = 0.0f;
		for (int i = 1; i < memoryUsage.Count - 1; i++)
			memoryUsageStat += (memoryUsage [i].x - memoryUsage [i - 1].x) * (memoryUsage [i].y + memoryUsage [i - 1].y) / 2;
		memoryUsageStat *= referenceTime / totalTime;
		statsWriter.WriteLine ();
		statsWriter.WriteLine ("Memory Usage (MBs)	{0}", memoryUsageStat);
	}

	public static void RunMono (string mono, string[] args, string workingDirectory, bool concurrent) {
		Process p = new Process ();
		p.StartInfo.UseShellExecute = false;
		p.StartInfo.FileName = mono;
		p.StartInfo.WorkingDirectory = workingDirectory;
		p.StartInfo.Arguments = string.Join (" ", args);

		if (concurrent)
			p.StartInfo.EnvironmentVariables.Add ("MONO_GC_PARAMS", "major=marksweep-conc");
		p.StartInfo.EnvironmentVariables.Add ("MONO_GC_DEBUG", "binary-protocol=" + binprotFile);

		Thread.Sleep (1000);
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
		Regex endRegex = new Regex (@"world_restarted generation (\d+) timestamp (\d+)");
		Interval interval = new Interval ();

		while ((line = reader.ReadLine ()) != null) {
			if (startRegex.IsMatch (line)) {
				interval.start = ((float)int.Parse (startRegex.Match (line).Groups [1].Value)) / 10000000;
			} else if (endRegex.IsMatch (line)) {
				interval.generation = int.Parse (endRegex.Match (line).Groups [1].Value);
				interval.end = ((float)int.Parse (endRegex.Match (line).Groups [2].Value)) / 10000000;
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
