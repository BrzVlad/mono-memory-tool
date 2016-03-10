using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

using Benchmarker.Common.Models;

public class Program {
	public static void Main (string[] args)
	{
#if CONC_VS_CONC
		if (args.Length < 3) {
			Console.WriteLine ("Usage : ./benchmarks-canalyzer.exe mono1 mono2 benchmarker-folder");
			return;
		}
#else
		if (args.Length < 2) {
			Console.WriteLine ("Usage : ./benchmarks-analyzer.exe mono benchmarker-folder");
			return;
		}
#endif
		int arg = 0;

		string mono = args [arg++];
#if CONC_VS_CONC
		string mono2 = args [arg++];
#endif
#if CONC_VS_CONC
		string analyzer = "canalyzer.exe";
#else
		string analyzer = "analyzer.exe";
#endif
		string benchmarker_folder = args [arg++];
		string benchmarks_folder = Path.Combine (benchmarker_folder, "benchmarks");
		string tests_folder = Path.Combine (benchmarker_folder, "tests");

		List<Benchmark> benchmark_list = Benchmark.LoadAllFrom (benchmarks_folder);

		foreach (Benchmark b in benchmark_list) {
			ProcessStartInfo info = new ProcessStartInfo {
				UseShellExecute = false,
			};

			info.FileName = analyzer;
#if CONC_VS_CONC
			info.Arguments = mono + " " + mono2 + " " + Path.Combine (tests_folder, b.TestDirectory) + " " + string.Join (" ", b.CommandLine);
#else
			info.Arguments = mono + " " + Path.Combine (tests_folder, b.TestDirectory) + " " + string.Join (" ", b.CommandLine);
#endif

			Console.WriteLine ("{0}", b.Name);
			Process ps = Process.Start (info);
			ps.WaitForExit ();
		}
	}
}
