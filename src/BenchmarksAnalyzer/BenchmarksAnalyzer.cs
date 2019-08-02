using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

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
	}
}
