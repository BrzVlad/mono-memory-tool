using System;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Collections.Generic;

public class Program {
	public const string binprotFile = "/tmp/binprot";
	public const string binprotExec = "sgen-grep-binprot";

	public const int numRuns = 5;

	public const int deltaHackConc = 4;
	public const int deltaHackNoconc = deltaHackConc;

	private static RunInfoDatabase runInfoDatabase = new RunInfoDatabase ();

#if CONC_VS_CONC
	public static void ParseArguments (string[] args, out string mono, out string mono2, out string workingDirectory, out string[] monoArguments)
#else
	public static void ParseArguments (string[] args, out string mono, out string workingDirectory, out string[] monoArguments)
#endif
	{
		int arg = 0;

#if CONC_VS_CONC
		if (args.Length < 3) {
			throw new ArgumentException ("Usage : ./canalyzer.exe mono1 mono2 working-directory [mono-arg1] [mono-arg2] ...");
		}
#else
		if (args.Length < 2) {
			throw new ArgumentException ("Usage : ./analyzer.exe mono working-directory [mono-arg1] [mono-arg2] ...");
		}
#endif

#if CONC_VS_CONC
		mono2 = args [arg++];
#endif
		mono = args [arg++];
		workingDirectory = args [arg++];
		monoArguments = args.SubArray<string> (arg);
	}

	public static void Main (string[] args)
	{
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

#if CONC_VS_CONC
		string name1 = Path.GetFileNameWithoutExtension (mono2);
		string name2 = Path.GetFileNameWithoutExtension (mono);
#else
		string name1 = "noconc";
		string name2 = "conc";
#endif

		/* Reduce jit compilation delays */
		Console.WriteLine (DateTime.Now.AddMilliseconds (100));
		for (int i = 0; i < numRuns; i++) {
			List<double>[] memoryUsage;
#if CONC_VS_CONC
			memoryUsage = RunMono (mono2, monoArguments, workingDirectory, true);
#else
			memoryUsage = RunMono (mono, monoArguments, workingDirectory, false);
#endif
			runInfoDatabase.noconcRuns.Add (new RunInfo (memoryUsage [0], memoryUsage [1], ParseBinProtOutput ()));

			memoryUsage = RunMono (mono, monoArguments, workingDirectory, true);
			runInfoDatabase.concRuns.Add (new RunInfo (memoryUsage [0], memoryUsage [1], ParseBinProtOutput ()));
		}

		target = Path.GetFileNameWithoutExtension (monoArguments [0]);
		resultsFolder = Path.Combine ("results", target);

		runInfoDatabase.Plot (resultsFolder, name1, name2);
		runInfoDatabase.OutputStats (resultsFolder, name1, name2);
	}

	public static List<double>[] RunMono (string mono, string[] args, string workingDirectory, bool concurrent)
	{
		Process p = new Process ();
		p.StartInfo.UseShellExecute = false;
		p.StartInfo.FileName = mono;
		p.StartInfo.WorkingDirectory = workingDirectory;
		p.StartInfo.Arguments = string.Join (" ", args);

		if (concurrent)
			p.StartInfo.EnvironmentVariables.Add ("MONO_GC_PARAMS", "major=marksweep-conc");
		p.StartInfo.EnvironmentVariables.Add ("MONO_GC_DEBUG", "binary-protocol=" + binprotFile);

		Console.WriteLine ("Run {0}, concurrent {1}", mono, concurrent);
		Thread.Sleep (1000);
		p.Start ();

		DateTime startTime = DateTime.Now.AddMilliseconds (concurrent ? deltaHackConc : deltaHackNoconc);

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
