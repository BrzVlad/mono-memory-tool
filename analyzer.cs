using System;
using System.Threading;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Collections.ObjectModel;
using System.Linq;
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

	public const int numRuns = 5;

	public const int deltaHackConc = 4;
	public const int deltaHackNoconc = deltaHackConc;

	private static int num_minor_while_major;
	private static int num_stop_intervals;

	private static List<Point> memoryUsage;
	private static List<Interval> nurseryIntervals, majorIntervals, concurrentIntervals, stopIntervals, plotStopIntervals;
	private static PlotModel plotModel;
	private static float referenceTime;

	/* If running conc vs conc, noconc is actually conc */
	private static List<RunStats> noconcRunStats = new List<RunStats> ();
	private static List<RunStats> concRunStats = new List<RunStats> ();

#if CONC_VS_CONC
	public static void ParseArguments (string[] args, out string mono, out string mono2, out string workingDirectory, out string[] monoArguments) {
#else
	public static void ParseArguments (string[] args, out string mono, out string workingDirectory, out string[] monoArguments) {
#endif
		int arg = 0;

#if CONC_VS_CONC
		if (args.Length < 3) {
			throw new ArgumentException ("Usage : ./canalyzer.exe [--stop stop_intervals] mono1 mono2 working-directory [mono-arg1] [mono-arg2] ...");
		}
#else
		if (args.Length < 2) {
			throw new ArgumentException ("Usage : ./analyzer.exe [--stop stop_intervals] mono working-directory [mono-arg1] [mono-arg2] ...");
		}
#endif

		while (args [arg].StartsWith ("--")) {
			if (args [arg].Equals ("--stop")) {
				arg++;
				num_stop_intervals = Int32.Parse (args [arg]);
				arg++;
			} else {
				throw new ArgumentException ("Invalid argument");
			}
		}

#if CONC_VS_CONC
		mono2 = args [arg++];
#endif
		mono = args [arg++];
		workingDirectory = args [arg++];
		monoArguments = args.SubArray<string> (arg);
	}

	public static void Main (string[] args) {
		string mono, workingDirectory, target, resultsFolder;
#if CONC_VS_CONC
		string mono2;
#endif
		string[] monoArguments;
#if CONC_VS_CONC
		ParseArguments (args, out mono, out mono2, out workingDirectory, out monoArguments);
#else
		ParseArguments (args, out mono, out workingDirectory, out monoArguments);
#endif

		target = Path.GetFileNameWithoutExtension (monoArguments [0]);
		resultsFolder = Path.Combine ("results", target);

#if CONC_VS_CONC
		string name1 = mono2;
		string name2 = mono;
#else
		string name1 = "noconc";
		string name2 = "conc";
#endif

		Directory.CreateDirectory (resultsFolder);
		/* Reduce jit compilation delays */
		Console.WriteLine (DateTime.Now.AddMilliseconds (100));
		for (int i = 0; i < numRuns; i++) {
			string svgFile = Path.Combine (resultsFolder, target + i + ".svg");
			plotModel  = new PlotModel { Title = Path.GetFileNameWithoutExtension (workingDirectory) };
			plotModel.Axes.Add (new LinearAxis { Position = AxisPosition.Bottom });
			plotModel.Axes.Add (new LinearAxis { Position = AxisPosition.Left });

			plotModel.LegendBackground = OxyColors.LightGray;
			plotModel.LegendBorder = OxyColors.Black;

#if CONC_VS_CONC
			RunMono (mono2, monoArguments, workingDirectory, true);
#else
			RunMono (mono, monoArguments, workingDirectory, false);
#endif
			referenceTime = memoryUsage [memoryUsage.Count - 1].x;
			ParseBinProtOutput ();
			AddPlotData (name1);
			noconcRunStats.Add (GetStats ());

			RunMono (mono, monoArguments, workingDirectory, true);
			ParseBinProtOutput ();
			AddPlotData (name2);
			concRunStats.Add (GetStats ());

			Plot (svgFile);
		}

		string statsFile = Path.Combine (resultsFolder, "stats");
		using (StreamWriter statsWriter = new StreamWriter (statsFile)) {
			OutputStat (name1, AggregateStats (noconcRunStats), statsWriter);
			OutputStat (name2, AggregateStats (concRunStats), statsWriter);
		}
	}

	public static RunStats AggregateStats (List<RunStats> list)
	{
		RunStats aggregated = new RunStats ();
		list.Sort ();

		int numRuns = Program.numRuns;

		if (numRuns < 3) {
			/* We consider all the runs, add dummy outliers */
			list.Insert (0, null);
			list.Add (null);
			numRuns += 2;
		}

		for (int i = 1; i < numRuns - 1; i++) {
			RunStats current = list [i];

			aggregated.totalTime += current.totalTime;
			aggregated.minMinor = Math.Min (aggregated.minMinor, current.minMinor);
			aggregated.maxMinor = Math.Max (aggregated.maxMinor, current.maxMinor);
			aggregated.minMajor = Math.Min (aggregated.minMajor, current.minMajor);
			aggregated.maxMajor = Math.Max (aggregated.maxMajor, current.maxMajor);
			aggregated.avgMinor += current.avgMinor;
			aggregated.avgMajor += current.avgMajor;
			aggregated.totalPause += current.totalPause;
			aggregated.numMinor += current.numMinor;
			aggregated.numMinorStops += current.numMinorStops;
			aggregated.numMajor += current.numMajor;
			aggregated.numMajorStops += current.numMajorStops;
			aggregated.numMinorWhileMajor += current.numMinorWhileMajor;
			aggregated.memoryUsage += current.memoryUsage;
		}

		aggregated.totalTime /= numRuns - 2;
		aggregated.avgMinor /= numRuns - 2;
		aggregated.avgMajor /= numRuns - 2;
		aggregated.totalPause /= numRuns - 2;
		aggregated.numMinor /= numRuns - 2;
		aggregated.numMinorStops /= numRuns - 2;
		aggregated.numMajor /= numRuns - 2;
		aggregated.numMajorStops /= numRuns - 2;
		aggregated.numMinorWhileMajor /= numRuns - 2;
		aggregated.memoryUsage /= numRuns - 2;

		return aggregated;
	}

	public static RunStats GetStats ()
	{
		RunStats runStats = new RunStats ();

		runStats.totalTime = memoryUsage [memoryUsage.Count - 1].x;
		runStats.numMinor = nurseryIntervals.Count;
		runStats.numMajor = majorIntervals.Count;
		runStats.numMinorWhileMajor = num_minor_while_major;

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

		runStats.totalPause = totalPause * 1000;
                runStats.numMinorStops = numMinor;
                runStats.minMinor = minMinor * 1000;
                runStats.maxMinor = maxMinor * 1000;
                runStats.avgMinor = avgMinor * 1000;
                runStats.numMajorStops = numMajor;
                runStats.minMajor = minMinor * 1000;
                runStats.maxMajor = maxMajor * 1000;
                runStats.avgMajor = avgMajor * 1000;

		/* Memory Usage */
		float memoryUsageStat = 0.0f;
		for (int i = 1; i < memoryUsage.Count - 1; i++)
			memoryUsageStat += (memoryUsage [i].x - memoryUsage [i - 1].x) * (memoryUsage [i].y + memoryUsage [i - 1].y) / 2;
		runStats.memoryUsage = memoryUsageStat * referenceTime / runStats.totalTime;

		return runStats;
	}

	public static void OutputStat (string prefix, RunStats runStats, StreamWriter statsWriter) {
		statsWriter.WriteLine ("\n" + prefix);
		statsWriter.WriteLine ("Time (s)		{0}", runStats.totalTime);
		statsWriter.WriteLine ("Minor			{0}", runStats.numMinor);
		statsWriter.WriteLine ("Major			{0}", runStats.numMajor);
		statsWriter.WriteLine ("Minor While Major	{0}", runStats.numMinorWhileMajor);

		statsWriter.WriteLine ();
		statsWriter.WriteLine ("Total pause time (ms)	{0}", runStats.totalPause);
		statsWriter.WriteLine ("Minor stops		{0}", runStats.numMinorStops);
		statsWriter.WriteLine ("Min minor (ms)		{0}", runStats.minMinor);
		statsWriter.WriteLine ("Max minor (ms)		{0}", runStats.maxMinor);
		statsWriter.WriteLine ("Avg minor (ms)		{0}", runStats.avgMinor);
		statsWriter.WriteLine ("Major stops		{0}", runStats.numMajorStops);
		statsWriter.WriteLine ("Min major (ms)		{0}", runStats.minMajor);
		statsWriter.WriteLine ("Max major (ms)		{0}", runStats.maxMajor);
		statsWriter.WriteLine ("Avg major (ms)		{0}", runStats.avgMajor);

		statsWriter.WriteLine ();
		statsWriter.WriteLine ("Memory Usage (MBs)	{0}", runStats.memoryUsage);
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
				timestamp = (float)(((double)long.Parse (m.Groups [1].Value)) / 10000000);
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
				interval.start = (float)(((double)long.Parse (startRegex.Match (line).Groups [1].Value)) / 10000000);
			} else if (endRegex.IsMatch (line)) {
				interval.generation = int.Parse (endRegex.Match (line).Groups [1].Value);
				interval.end = (float)(((double)long.Parse (endRegex.Match (line).Groups [2].Value)) / 10000000);
				stopIntervals.Add (interval);
			}
		}

		plotStopIntervals = stopIntervals.OrderByDescending (ival => ival.end - ival.start).Take (num_stop_intervals).OrderBy (ival => ival.start).ToList<Interval> ();

		Console.Write ("{0} intervals", plotStopIntervals.Count ());
		for (int i = 0; i < plotStopIntervals.Count (); i++)
			Console.Write (", {0}({1})", plotStopIntervals [i].start, plotStopIntervals [i].end - plotStopIntervals [i].start);
		Console.WriteLine ();
	}

	public static void ParseBinProtOutput () {
		Process p = new Process ();
		p.StartInfo.UseShellExecute = false;
		p.StartInfo.FileName = binprotExec; 
		p.StartInfo.Arguments = "-i " + binprotFile;
		p.StartInfo.RedirectStandardOutput = true; 
		
		p.Start ();

		string stdout = p.StandardOutput.ReadToEnd ();

		p.WaitForExit ();

		ParseNurseryCollections (stdout);
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


	static byte lineColor;
	public static void AddPlotData (string name) {
		LineSeries lineSeries = new LineSeries ();
		lineSeries.Title = name;
		lineSeries.Color = OxyColor.FromRgb (lineColor, lineColor, lineColor);
		lineColor += 128;
		for (int i = 0; i < memoryUsage.Count; i++)
			lineSeries.Points.Add (new DataPoint (memoryUsage [i].x, memoryUsage [i].y));
		plotModel.Series.Add (lineSeries);

		AddSeries (plotModel.Series, majorIntervals, OxyColor.FromRgb (255, 0, 0));
		AddSeries (plotModel.Series, concurrentIntervals, OxyColor.FromRgb (0, 0, 255));
		AddSeries (plotModel.Series, nurseryIntervals, OxyColor.FromRgb (0, 255, 0));
		AddSeries (plotModel.Series, plotStopIntervals, OxyColor.FromRgb (255, 255, 0));
	}

	public static void Plot (string name) {
		using (FileStream stream = new FileStream (name, FileMode.Create)) {
			SvgExporter.Export (plotModel, stream, 1920, 1080, true);
		}
	}
}

public class RunStats : IComparable<RunStats> {
	public float totalTime;
	public float minMinor;
	public float maxMinor;
	public float minMajor;
	public float maxMajor;
	public float avgMinor;
	public float avgMajor;
	public float totalPause;
	public int numMinor, numMinorStops;
	public int numMajor, numMajorStops;
	public int numMinorWhileMajor;
	public float memoryUsage;

	public RunStats ()
	{
		totalTime = 0.0f;
		minMinor = float.MaxValue;
		maxMinor = float.MinValue;
		minMajor = float.MaxValue;
		maxMajor = float.MinValue;
		avgMinor = 0.0f;
		avgMajor = 0.0f;
		totalPause = 0.0f;
		numMinor = 0;
		numMinorStops = 0;
		numMajor = 0;
		numMajorStops = 0;
		numMinorWhileMajor = 0;
		memoryUsage = 0.0f;
	}

	public int CompareTo (RunStats other)
	{
		return totalTime.CompareTo (other.totalTime);
	}
}
