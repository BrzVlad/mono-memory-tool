using System;

public enum CumulationType {
	SUM,
	AVERAGE,
	MIN_MAX_AVG,
}

public class OutputStat {
	private int stat_count; /* if it's a cumulation of same stats */
	private string stat_name;
	private double stat_value, min_val, max_val;
	private CumulationType cumulation_type;
	private bool stat_hidden;

	public static readonly OutputStat EmptyStat = new OutputStat ("", default(double), CumulationType.AVERAGE);

	public string Name {
		get {
			return stat_name;
		}
	}

	public double Value {
		get {
			if (cumulation_type == CumulationType.AVERAGE || cumulation_type == CumulationType.MIN_MAX_AVG)
				return stat_value / stat_count;
			else
				return stat_value;
		}
	}

	private OutputStat ()
	{
	}

	public OutputStat (string name, double val, CumulationType cumul) : this (name, val, cumul, false)
	{
	}

	public OutputStat (string name, double val, CumulationType cumul, bool hidden)
	{
		stat_count = 1;
		stat_name = name;
		stat_value = val;
		stat_hidden = hidden;
		cumulation_type = cumul;
		if (cumulation_type == CumulationType.MIN_MAX_AVG) {
			min_val = stat_value;
			max_val = stat_value;
		}
	}

	public void Normalize ()
	{
		stat_value = Value;
		stat_count = 1;
		if (cumulation_type == CumulationType.SUM) {
			cumulation_type = CumulationType.AVERAGE;
		}
	}

	public override string ToString ()
	{
		return ToString (true);
	}

	public string ToString (bool includeCumulation)
	{
		if (this == EmptyStat || stat_hidden && stat_value == default(double)) {
			return string.Format ("{0,28}  {1,-28}", "", "");
		} else if (!includeCumulation && (cumulation_type == CumulationType.SUM || cumulation_type == CumulationType.AVERAGE)) {
			return "";
		} else if (cumulation_type == CumulationType.MIN_MAX_AVG) {
			string statVal;
			if (includeCumulation)
				statVal = string.Format ("{0:0.##} ({1:0.##}-{2:0.##})", Value, min_val, max_val);
			else
				statVal = string.Format ("{0:0.##}", Value);
			return string.Format ("{0,28}  {1,-28}", Name, statVal);
		} else {
			return string.Format ("{0,28}  {1,-28:0.##}", Name, Value);
		}
	}

	public static OutputStat operator + (OutputStat o1, OutputStat o2)
	{
		if (o1 == EmptyStat && o2 == EmptyStat)
			return EmptyStat;

		OutputStat o_result = new OutputStat ();

		o_result.stat_count = o1.stat_count + o2.stat_count;

		Utils.AssertEqual<string> (o1.stat_name, o2.stat_name, o1, o2);
		o_result.stat_name = o1.stat_name;

		Utils.AssertEqual<int> ((int)o1.cumulation_type, (int)o2.cumulation_type, o1, o2);
		o_result.cumulation_type = o1.cumulation_type;

		switch (o_result.cumulation_type) {
			case CumulationType.MIN_MAX_AVG:
				o_result.min_val = Math.Min (o1.min_val, o2.min_val);
				o_result.max_val = Math.Max (o1.max_val, o2.max_val);
				o_result.stat_value = o1.stat_value + o2.stat_value;
				break;
			case CumulationType.SUM:
			case CumulationType.AVERAGE:
				o_result.stat_value = o1.stat_value + o2.stat_value;
				break;
		}
		return o_result;
	}
}
