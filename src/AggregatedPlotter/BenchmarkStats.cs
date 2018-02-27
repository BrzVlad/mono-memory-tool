using System;
using System.IO;
using System.Collections.Generic;

public class BenchmarkStats {
	private string name;
	private string mono1;
	private string mono2;

	private Dictionary<string,double> stats1;
	private Dictionary<string,double> stats2;

	public string Mono1 {
		get {
			return mono1;
		}
	}

	public string Mono2 {
		get {
			return mono2;
		}
	}

	public string Name {
		get {
			return name;
		}
	}

	public double GetStat1 (string name)
	{
		return stats1 [name];
	}

	public double GetStat2 (string name)
	{
		return stats2 [name];
	}

	public BenchmarkStats (string file)
	{
		string line;
		int length;

		name = Directory.GetParent (file).Name;

		stats1 = new Dictionary<string,double> ();
		stats2 = new Dictionary<string,double> ();

		StreamReader reader = new StreamReader (file);
		line = reader.ReadLine ();
		length = line.Length;
		mono1 = line.Substring (0, length / 2).Trim ();
		mono2 = line.Substring (length / 2, length /2 ).Trim ();

		while ((line = reader.ReadLine ()) != null) {
			if (string.IsNullOrWhiteSpace (line))
				continue;
			length = line.Length;
			string line1 = line.Substring (0, length / 2).Trim ();
			string line2 = line.Substring (length / 2, length /2 ).Trim ();

			int value_index = line1.IndexOfAny ("0123456789".ToCharArray ());
			Utils.Assert (value_index == line2.IndexOfAny ("0123456789".ToCharArray ()));

			stats1.Add (line1.Substring (0, value_index).Trim (), double.Parse (line1.Substring (value_index).Split (' ') [0]));
			stats2.Add (line2.Substring (0, value_index).Trim (), double.Parse (line2.Substring (value_index).Split (' ') [0]));
		}

		reader.Close ();
	}
}
