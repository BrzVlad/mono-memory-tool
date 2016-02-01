using System;

public enum CumulationType {
	MIN,
	MAX,
	SUM,
	AVERAGE
}

public class OutputStat {
	private int stat_count; /* if it's a cumulation of same stats */
	private string stat_name;
	private double stat_value;
	private CumulationType cumulation_type;

	public static readonly OutputStat EmptyStat = new OutputStat ("", default(double), CumulationType.AVERAGE);

	public string Name {
		get {
			return stat_name;
		}
	}

	public double Value {
		get {
			if (cumulation_type == CumulationType.AVERAGE)
				return stat_value / stat_count;
			else
				return stat_value;
		}
	}

	private OutputStat ()
	{
	}

	public OutputStat (string name, double val, CumulationType cumul)
	{
		stat_count = 1;
		stat_name = name;
		stat_value = val;
		cumulation_type = cumul;
	}

	public void Normalize ()
	{
		stat_value = Value;
		stat_count = 1;
		cumulation_type = CumulationType.AVERAGE;
	}

	public override string ToString ()
	{
		if (this == EmptyStat)
			return string.Format ("{0,25} {1,-10:.##}", "", "");
		else
			return string.Format ("{0,25} {1,-10:.##}", Name, Value);
	}

	public static OutputStat operator + (OutputStat o1, OutputStat o2)
	{
		if (o1 == EmptyStat && o2 == EmptyStat)
			return EmptyStat;

		OutputStat o_result = new OutputStat ();

		o_result.stat_count = o1.stat_count + o2.stat_count;

		Utils.AssertEqual<string> (o1.stat_name, o2.stat_name);
		o_result.stat_name = o1.stat_name;

		Utils.AssertEqual<int> ((int)o1.cumulation_type, (int)o2.cumulation_type);
		o_result.cumulation_type = o1.cumulation_type;

		switch (o_result.cumulation_type) {
			case CumulationType.MIN:
				o_result.stat_value = Math.Min (o1.stat_value, o2.stat_value);
				break;
			case CumulationType.MAX:
				o_result.stat_value = Math.Max (o1.stat_value, o2.stat_value);
				break;
			case CumulationType.SUM:
			case CumulationType.AVERAGE:
				o_result.stat_value = o1.stat_value + o2.stat_value;
				break;
		}
		return o_result;
	}
}
