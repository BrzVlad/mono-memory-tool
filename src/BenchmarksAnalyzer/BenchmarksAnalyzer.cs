using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Text;

using Benchmarker.Common.Models;

public class Program {
	public static void Main (string[] args)
	{
		if (args.Length < 4) {
			Console.WriteLine ("Usage : ./benchmarks-analyzer.exe mode mono1 mono2 benchmarker-folder");
			Console.WriteLine ("            mode    : majors for the two monos [s|c|cp][i]v[s|c|cp][i]");
			Console.WriteLine ("            mono2   : if mono2 is '-' then mono1 is used for both runs");
			return;
		}
		int arg = 0;

		string mode = args [arg++];
		string mono1 = args [arg++];
		string mono2 = args [arg++];
		string analyzer = "analyzer.exe";
		string benchmarker_folder = args [arg++];
		string benchmarks_folder = Path.Combine (benchmarker_folder, "benchmarks");
		string tests_folder = Path.Combine (benchmarker_folder, "tests");

		List<Benchmark> benchmark_list = Benchmark.LoadAllFrom (benchmarks_folder);

		foreach (Benchmark b in benchmark_list) {
			ProcessStartInfo info = new ProcessStartInfo {
				UseShellExecute = false,
			};

			info.EnvironmentVariables ["BENCHMARK_NAME"] = b.Name;
			info.FileName = analyzer;
			info.Arguments = mode + " " + mono1 + " " + mono2 + " " + Path.Combine (tests_folder, b.TestDirectory) + " " + string.Join (" ", b.CommandLine);

			Console.WriteLine ("{0}", b.Name);
			Process ps = Process.Start (info);
			ps.WaitForExit ();
		}

		Report (benchmark_list);
	}

	private static float GetMedian (List<float> floats)
	{
		int length = floats.Count;
		floats.Sort ();
		if (length % 2 == 0) {
			return (floats [length / 2 - 1] + floats [length / 2]) / 2;
		} else {
			return floats [length / 2];
		}
	}

	private static void Report (List<Benchmark> benchmark_list)
	{
		StringBuilder report_text = new StringBuilder ();
		List<float> ratios = new List<float> ();
		foreach (Benchmark b in benchmark_list) {
			string stats_file = Path.Combine ("results", b.Name, "stats-overall");
			string line = null;
			using (StreamReader file_reader = File.OpenText (stats_file)) {
				do {
					line = file_reader.ReadLine ();
					if (line == null)
						break;
				} while (!line.Contains ("Time (s)"));
			}
			// No data for this benchmark for whatever reason
			if (line == null)
				continue;
			line = line.Replace ("Time (s)", "");
			line = line.Trim (' ');
			line = Regex.Replace (line, " {2,}", " ");
			string[] times = line.Split (' ');
			if (times.Length != 2)
				continue;
			float time1 = float.Parse (times [0]);
			float time2 = float.Parse (times [1]);

			ratios.Add (time1 / time2);
			report_text.AppendLine (string.Format ("{0}\t{1:0.00}\t{2:0.00}\t{3:0.00}", b.Name, time1, time2, time1 / time2));
		}
		string report_file = Path.Combine ("results", "report.txt");
		File.WriteAllText (report_file, report_text.ToString ());
		File.AppendAllText (report_file, string.Format ("Median\t{0:0.00}\n", GetMedian (ratios)));
	}
}
