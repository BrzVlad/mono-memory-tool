using System;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Collections.Generic;
using System.Linq;

public class Program {
	public enum MajorType { MajorSerial, MajorConcurrent, MajorConcurrentPar };
	public const string binprotFile = "/tmp/binprot";
	public const string binprotExec = "sgen-grep-binprot";

	public const int numRuns = 1;
	public const bool remove_outliers = true;

	public const int deltaHack = 4;

	public static MajorType major1, major2;
	public static bool interp1, interp2;
	public static string mono1, mono2;
	public static string workingDirectory;
	public static string benchmark_name;
	public static string[] monoArguments;

	private static RunInfoDatabase runInfoDatabase = new RunInfoDatabase (remove_outliers);

	public static void PrintUsage ()
	{
		Console.WriteLine ("Usage : ./analyzer.exe mode mono1 mono2 working-directory [mono-arg1] [mono-arg2] ...");
		Console.WriteLine ("		mode	: majors for the two monos [s|c|cp][i]v[s|c|cp][i]");
		Console.WriteLine ("		mono2	: if mono2 is '-' then mono1 is used for both runs");
	}

	public static bool ParseMajor (string mode, ref MajorType major_type)
	{
		if (mode.Contains ("cp"))
			major_type = MajorType.MajorConcurrentPar;
		else if (mode.Contains ("c"))
			major_type = MajorType.MajorConcurrent;
		else if (mode.Contains ("s"))
			major_type = MajorType.MajorSerial;
		else
			return false;
		return true;
	}

	public static bool ParseMode (string mode)
	{
		string[] modes = mode.Split ('v');
		if (modes.Length != 2)
			return false;
		if (!ParseMajor (modes [0], ref major1))
			return false;
		if (!ParseMajor (modes [1], ref major2))
			return false;
		interp1 = modes [0].Contains ("i");
		interp2 = modes [1].Contains ("i");
		return true;
	}

	public static bool ParseArguments (string[] args)
	{
		int arg = 0;

		if (args.Length < 4)
			return false;

		if (!ParseMode (args [arg++]))
			return false;
		mono1 = args [arg++];
		mono2 = args [arg++];
		if (string.Compare (mono2, "-") == 0)
			mono2 = mono1;
		workingDirectory = args [arg++];
		monoArguments = args.SubArray<string> (arg);
		return true;
	}

	public static void Main (string[] args)
	{
		string target = null, resultsFolder;

		if (!ParseArguments (args)) {
			PrintUsage ();
			return;
		}

		string exec1 = Path.GetFileNameWithoutExtension (mono1);
		string exec2 = Path.GetFileNameWithoutExtension (mono2);

		string name1 = exec1 + "|" + major1 + (interp1 ? "|i" : "");
		string name2 = exec2 + "|" + major2 + (interp2 ? "|i" : "");

		bool no_comparison = name1 == name2;

		/* Reduce jit compilation delays */
		Console.WriteLine (DateTime.Now.AddMilliseconds (100));

		if (name1.StartsWith ("mono")) {
			for (int i = 0; i < monoArguments.Length; i++) {
				if (!monoArguments [i].StartsWith ("-")) {
					target = Path.GetFileNameWithoutExtension (monoArguments [i]);
					break;
				}
			}
		} else {
			/* mono launching is embedded in the executable */
			target = exec1;
		}

		benchmark_name = Environment.GetEnvironmentVariable ("BENCHMARK_NAME");
		if (benchmark_name == null)
			benchmark_name = target;

		resultsFolder = Path.Combine ("results", benchmark_name);
		Directory.CreateDirectory (resultsFolder);

		for (int i = 0; i < numRuns; i++) {
			List<double>[] memoryUsage;

			memoryUsage = RunMono (mono1, monoArguments, workingDirectory, major1, interp1);
			runInfoDatabase.runs1.Add (new RunInfo (memoryUsage [0], memoryUsage [1], ParseBinProtOutput ()));
			File.Copy (binprotFile, Path.Combine (resultsFolder, "binprot-" + name1 + i), true);

			if (!no_comparison) {
				memoryUsage = RunMono (mono2, monoArguments, workingDirectory, major2, interp2);
				runInfoDatabase.runs2.Add (new RunInfo (memoryUsage [0], memoryUsage [1], ParseBinProtOutput ()));
				File.Copy (binprotFile, Path.Combine (resultsFolder, "binprot-" + name2 + i), true);
			}
		}

		runInfoDatabase.Plot (resultsFolder, name1, no_comparison ? "" : name2);
		runInfoDatabase.OutputStats (resultsFolder, name1, no_comparison ? "" : name2);
	}

	private static void AddMonoOption (Process p, string key, string val)
	{
		string prev_val = p.StartInfo.EnvironmentVariables [key];
		if (prev_val != null && !prev_val.Contains (val))
			p.StartInfo.EnvironmentVariables [key] = string.Join (",", prev_val, val);
		else
			p.StartInfo.EnvironmentVariables [key] = val;
		Console.WriteLine ("Environment [{0}] = {1}", key, p.StartInfo.EnvironmentVariables [key]);
	}

	public static List<double>[] RunMono (string mono, string[] args, string workingDirectory, MajorType major_type, bool interpreted)
	{
		Process p = new Process ();
		p.StartInfo.UseShellExecute = false;
		p.StartInfo.FileName = mono;
		p.StartInfo.WorkingDirectory = workingDirectory;
		p.StartInfo.Arguments = (interpreted ? "\"--interpreter\" " : "") + string.Join (" ", args.Select<string, string> (arg => "\"" + arg + "\""));

		switch (major_type) {
			case MajorType.MajorSerial:
				AddMonoOption (p, "MONO_GC_PARAMS", "major=marksweep");
				break;
			case MajorType.MajorConcurrent:
				AddMonoOption (p, "MONO_GC_PARAMS", "major=marksweep-conc");
				break;
			case MajorType.MajorConcurrentPar:
				AddMonoOption (p, "MONO_GC_PARAMS", "major=marksweep-conc-par");
				break;
		}
		AddMonoOption (p, "MONO_GC_DEBUG", "binary-protocol=" + binprotFile);

		Console.WriteLine ("Run {0}, Major Type {1}, Interpreted = {2}", mono, major_type, interpreted);
		Thread.Sleep (1000);
		p.Start ();

		DateTime startTime = DateTime.Now.AddMilliseconds (deltaHack);

		List<double> timestamps = new List<double> ();
		List<double> memoryUsage = new List<double> ();

		while (!p.HasExited) {
			DateTime now = DateTime.Now;
			timestamps.Add (((double)(now - startTime).TotalMilliseconds) / 1000);
			memoryUsage.Add ((double)p.WorkingSet64 / 1000000);
			Thread.Sleep (1);
		}
		p.WaitForExit ();

		return new List<double>[] {timestamps, memoryUsage};
	}

	public static List<GCEvent> ParseBinProtOutput ()
	{
		Process p = new Process ();
		p.StartInfo.UseShellExecute = false;
		p.StartInfo.FileName = binprotExec;
		p.StartInfo.Arguments = "-i " + binprotFile;
		p.StartInfo.RedirectStandardOutput = true;

		p.Start ();

		string stdout = p.StandardOutput.ReadToEnd ();

		p.WaitForExit ();

		return GCEvent.ParseEvents (stdout);
	}
}
