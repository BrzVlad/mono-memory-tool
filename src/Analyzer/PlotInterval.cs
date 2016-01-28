using System;
using System.Collections.Generic;
using OxyPlot;
using OxyPlot.Series;

public class PlotInterval {
	public static readonly OxyColor red = OxyColor.FromRgb (255, 0, 0);
	public static readonly OxyColor green = OxyColor.FromRgb (0, 255, 0);
	public static readonly OxyColor blue = OxyColor.FromRgb (0, 0, 255);

	private double start, end;
	private OxyColor color;

	public PlotInterval (double start, double end, OxyColor color)
	{
		this.start = start;
		this.end = end;
		this.color = color;
	}

	public void Plot (PlotModel plotModel, List<double> timestamps, List<double> memoryUsage)
	{
		LineSeries series = new LineSeries ();
		series.Color = color;

		int index = timestamps.BinarySearch (start);
		if (index < 0)
			index = ~index;

		if (index == timestamps.Count)
			throw new Exception ("Why do we have an interval that does not have memoryUsage data");

		while (timestamps [index] < end) {
			series.Points.Add (new DataPoint (timestamps [index], memoryUsage [index]));
			index++;
		}

		plotModel.Series.Add (series);
	}
}
