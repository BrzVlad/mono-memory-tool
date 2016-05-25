using System;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Collections.Generic;

public class Program {
	public const string binprotExec = "sgen-grep-binprot";
	public static string binprotFile;


	private static RunInfoDatabase runInfoDatabase = new RunInfoDatabase ();

	public static void ParseArguments (string[] args)
	{
		int arg = 0;

		if (args.Length < 1) {
			throw new ArgumentException ("Usage : ./parser.exe binprotoutput");
		}

		binprotFile = args [arg++];
	}

	public static void Main (string[] args)
	{
		ParseArguments (args);

		string name1 = "noconc";
		string name2 = "conc";

		runInfoDatabase.noconcRuns.Add (new RunInfo (null, null, ParseBinProtOutput ()));
		runInfoDatabase.concRuns.Add (new RunInfo (null, null, ParseBinProtOutput ()));

		string resultsFolder = Path.Combine ("results", Path.GetFileName (binprotFile));

		runInfoDatabase.OutputStats (resultsFolder, name1, name2);
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
