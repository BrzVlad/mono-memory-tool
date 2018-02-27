using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

public class Program {
	public const string stats_file = "stats-overall";
	public const string time_property = "Time (s)";

	public static void Main (string[] args)
	{
		if (args.Length != 1) {
			Console.WriteLine ("Usage : ./aggregated-plotter.exe results-folder");
			return;
		}

		string results_folder = args [0];
		string[] directories = Directory.GetDirectories (results_folder);
		string svgFile = results_folder + ".svg";
		var results = new List<BenchmarkStats> ();

		foreach (string directory in directories) {
			string file = Path.Combine (directory, stats_file);
			results.Add (new BenchmarkStats (file));
		}

		results = results.OrderBy (b => {
			double time1 = b.GetStat1 (time_property);
			double time2 = b.GetStat2 (time_property);
			if (time1 < time2)
				return time1 / time2;
			else
				return time2 / time1;
		}).ToList ();

		Plot (results, svgFile);
	}

	// 'Plot' to stdout because I don't know how to plot
	public static void Plot (List<BenchmarkStats> results, string file)
	{
		string Mono1 = results [0].Mono1;
		string Mono2 = results [0].Mono2;

		Console.WriteLine ("{0}\t{1}", Mono1, Mono2);

		foreach (BenchmarkStats stats in results) {
			double time1 = stats.GetStat1 (time_property);
			double time2 = stats.GetStat2 (time_property);

			Console.WriteLine ("{0}\t{1}\t{2}", stats.Name, 1.0, time2 / time1);
		}
	}
}
