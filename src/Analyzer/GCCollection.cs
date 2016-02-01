using System.Collections.Generic;
using OxyPlot;

public abstract class GCCollection {
	/* Timestamps are measured in seconds */
	protected double start_timestamp, end_timestamp;

	public abstract void Plot (PlotModel plotModel, List<double> timestamps, List<double> memoryUsage);
	public abstract OutputStatSet GetStats ();
}

