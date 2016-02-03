using System;
using System.Text;
using System.Collections.Generic;

public class OutputStatSet : IComparable<OutputStatSet> {
	private List<OutputStat> stats = new List<OutputStat> ();
	private OutputStat sort_stat;

	public static readonly OutputStatSet EmptyStatSet = new OutputStatSet ();

	private OutputStat this [int i] {
		get { return stats [i]; }
		set { stats [i] = value; }
	}

	private int Count {
		get { return stats.Count; }
	}

	private double SortValue {
		get {
			Utils.Assert (sort_stat != null);
			return sort_stat.Value;
		}
	}

	private string name = null;

	private int FindStatIndex (OutputStat stat)
	{
		if (stat == OutputStat.EmptyStat)
			return -1;
		for (int i = 0; i < stats.Count; i++) {
			if (stats [i].Name.Equals (stat.Name))
				return i;
		}
		return -1;
	}

	public OutputStatSet ()
	{
	}

	public OutputStatSet (string s)
	{
		name = s;
	}

	public override string ToString ()
	{
		StringBuilder builder = new StringBuilder ();

		if (name != null) {
			builder.Append (name.Center (OutputStat.EmptyStat.Name.Length));
			builder.AppendLine ();
		}
		foreach (OutputStat stat in stats) {
			builder.Append (stat.ToString ());
			builder.AppendLine ();
		}

		return builder.ToString ();
	}

	public void Normalize ()
	{
		foreach (OutputStat stat in stats)
			stat.Normalize ();
	}

	public int CompareTo (OutputStatSet other)
	{
		/* Descending sort by default */
		return other.SortValue.CompareTo (SortValue);
	}

	public static string ToString (OutputStatSet s1, OutputStatSet s2) {
		StringBuilder builder = new StringBuilder ();
		List<Tuple<int,int>> sharedStats = new List<Tuple<int,int>> ();

		/*
		 * We want to display the two sets by having the common stats
		 * displayed side by side on each line. We enforce the code
		 * that uses this to add the common stats in the same order in
		 * the two sets.
		 */
		sharedStats.Add (Tuple.Create<int,int> (0, 0));
		for (int i = 0; i < s1.Count; i++) {
			int index1 = i;
			int index2 = s2.FindStatIndex (s1 [i]);

			if (index2 != -1) {
				if (sharedStats.Count > 0)
					Utils.Assert (index2 >= sharedStats [sharedStats.Count - 1].Item2);
				sharedStats.Add (Tuple.Create<int,int> (index1, index2));
			}
		}
		sharedStats.Add (Tuple.Create<int,int> (s1.Count, s2.Count));

		if (s1.name != null || s2.name != null) {
			builder.Append (s1.name.Center (OutputStat.EmptyStat.ToString ().Length));
			builder.Append (s2.name.Center (OutputStat.EmptyStat.ToString ().Length));
			builder.AppendLine ();
			builder.AppendLine ();
		}
		for (int i = 0; i < (sharedStats.Count - 1); i++) {
			int start1 = sharedStats [i].Item1;
			int end1 = sharedStats [i + 1].Item1;
			int start2 = sharedStats [i].Item2;
			int end2 = sharedStats [i + 1].Item2;
			int count = Math.Max (end1 - start1, end2 - start2);
			for (int k = 0; k < count; k++) {
				string display1 = OutputStat.EmptyStat.ToString ();
				string display2 = OutputStat.EmptyStat.ToString ();
				if (start1 + k < end1)
					display1 = s1 [start1 + k].ToString ();
				if (start2 + k < end2)
					display2 = s2 [start2 + k].ToString ();

				builder.Append (display1 + display2);
				builder.AppendLine ();
			}
		}

		return builder.ToString ();
	}

	/* Combines two stat sets that have 1 to 1 mapping of stats */
	public static OutputStatSet operator + (OutputStatSet s1, OutputStatSet s2)
	{
		if (s1 == null)
			return s2;
		OutputStatSet stat_result = new OutputStatSet (s1.name);

		if (s1.Count == 0) {
			stat_result.stats = new List<OutputStat> (s2.stats); 
		} else {
			Utils.AssertEqual<int> (s1.Count, s2.Count);

			for (int i = 0; i < s1.Count; i++) {
				OutputStat sum_stat = s1 [i] + s2 [i];
				if (s1.sort_stat == s1 [i]) {
					Utils.AssertEqualRef (s2.sort_stat, s2 [i]);
					stat_result.sort_stat = sum_stat;
				}
				stat_result.stats.Add (sum_stat);
			}
		}

		return stat_result;
	}

	/* Reunion of two stat sets. No common stats accepted */
	public static OutputStatSet operator | (OutputStatSet s1, OutputStatSet s2)
	{
		if (s1 == null)
			return s2;
		else if (s2 == null)
			return s1;
		OutputStatSet stat_result = new OutputStatSet (s1.name);

		foreach (OutputStat stat in s1.stats)
			stat_result.stats.Add (stat);

		foreach (OutputStat stat in s2.stats) {
			stat_result = stat_result | stat;
		}

		return stat_result;
	}

	/* Warning! This operator mutates, only use += */
	public static OutputStatSet operator + (OutputStatSet set, OutputStat stat)
	{
		if (stat == OutputStat.EmptyStat)
			return set;
		int index = set.FindStatIndex (stat);
		Utils.AssertNotEqual<int> (index, -1);
		set.stats [index] = set.stats [index] + stat;
		return set;
	}

	/* Warning! This operator mutates, only use |= */
	public static OutputStatSet operator | (OutputStatSet set, OutputStat stat)
	{
		if (set == null)
			set = new OutputStatSet ();
		int index = set.FindStatIndex (stat);
		Utils.AssertEqual<int> (index, -1);
		set.stats.Add (stat);
		return set;
	}

	/* Warning! This operator mutates, only use ^= */
	public static OutputStatSet operator ^ (OutputStatSet set, OutputStat stat)
	{
		set |= stat;
		set.sort_stat = stat;
		return set;
	}
}
