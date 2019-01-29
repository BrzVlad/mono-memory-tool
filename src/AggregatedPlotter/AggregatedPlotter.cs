using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

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
//	Uncomment for sorting based on delta
//			if (time1 < time2)
				return time1 / time2;
//			else
//				return time2 / time1;
		}).ToList ();

		Plot (results);
		Plot2 (results, results_folder);
	}

	// 'Plot' to stdout because I don't know how to plot
	// Output is ready to copy in excel
	public static void Plot (List<BenchmarkStats> results)
	{
		string Mono1 = results [0].Mono1;
		string Mono2 = results [0].Mono2;

		Console.WriteLine ("{0}\t{1}", Mono1, Mono2);

		foreach (BenchmarkStats stats in results) {
			double time1 = stats.GetStat1 (time_property);
			double time2 = stats.GetStat2 (time_property);

			Console.WriteLine ("{0}\t{1}\t{2}", stats.Name, 1.0, time1 / time2);
		}
	}

	public static void BuildPlotScript (List<BenchmarkStats> results, string data_file, string script_file, string output)
	{
		using (StreamWriter file = new StreamWriter (script_file)) {
			file.WriteLine ("set terminal jpeg size 1920,1080 enhanced font \"Helvetica,10\"");
			file.WriteLine ("set output \'{0}\'", output);

			file.Write ("set xtics rotate by -45 (");
			int x = 1;
			foreach (BenchmarkStats stats in results) {
				file.Write ("\"{0}\" {1}, ", stats.Name, x);
				x += 2; // increment * 3 from below
			}
			file.WriteLine (")");

			file.WriteLine ("set boxwidth 0.66666");
			file.WriteLine ("set style fill solid");
			file.WriteLine ("set xrange [0:*]");
			file.WriteLine ("set yrange [0:1.5]");

			file.WriteLine ("plot \'{0}\' every 2 using 1:2 with boxes ls 1 title \'{1}\', \'{0}\' every 2::1 using 1:2 with boxes ls 2 title \'{2}\'", data_file, results [0].Mono1, results [1].Mono2);
		}
	}

	public static void BuildPlotData (List<BenchmarkStats> results, string data_file)
	{
		using (StreamWriter file = new StreamWriter (data_file)) {
			float xaxis = 1.0f;
			const float increment = 0.66666f;
			foreach (BenchmarkStats stats in results) {
				double time1 = stats.GetStat1 (time_property);
				double time2 = stats.GetStat2 (time_property);

				file.WriteLine ("{0}\t{1}", xaxis, time1 / time2);
				xaxis += increment;
				file.WriteLine ("{0}\t{1}\n", xaxis, 1.0f);
				xaxis += increment * 2;
			}
		}
	}

	public static void Plot2 (List<BenchmarkStats> results, string name)
	{
		const string data_file = "tmp.data";
		const string script_file = "tmp.plot";

		BuildPlotData (results, data_file);
		BuildPlotScript (results, data_file, script_file, name + ".jpg");

		Process p = new Process ();
		p.StartInfo.FileName = "gnuplot";
		p.StartInfo.Arguments = script_file;
		p.Start ();
		p.WaitForExit ();
	}
}
