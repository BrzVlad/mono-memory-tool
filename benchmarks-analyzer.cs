using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

using Benchmarker.Common.Models;


public class Program {
	public static void Main (string[] args)
	{
		if (args.Length < 3) {
			Console.WriteLine ("Usage : ./benchmarks-analyzer.exe mono analyzer benchmarker-folder");
			return;
		}

		string mono = args [0];
		string analyzer = args [1];
		string benchmarker_folder = args [2];
		string benchmarks_folder = Path.Combine (benchmarker_folder, "benchmarks");
		string tests_folder = Path.Combine (benchmarker_folder, "tests");

		List<Benchmark> benchmark_list = Benchmark.LoadAllFrom (benchmarks_folder);

		foreach (Benchmark b in benchmark_list) {
			ProcessStartInfo info = new ProcessStartInfo {
				UseShellExecute = false,
			};

			info.FileName = analyzer;
			info.Arguments = mono + " " + Path.Combine (tests_folder, b.TestDirectory) + " " + string.Join (" ", b.CommandLine);

			Console.WriteLine ("{0}", b.Name);
			Process ps = Process.Start (info);
			ps.WaitForExit ();
		}
	}
}
