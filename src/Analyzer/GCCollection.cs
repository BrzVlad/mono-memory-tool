using System.Collections.Generic;
using OxyPlot;

public abstract class GCCollection {
	/* Timestamps are measured in seconds */
	protected double start_timestamp, end_timestamp;
	private double custom_event_start, custom_event_end;
	private bool major;

	protected GCCollection (bool major)
	{
		this.major = major;
	}

	public abstract void Plot (PlotModel plotModel, List<double> timestamps, List<double> memoryUsage);

	public virtual OutputStatSet GetStats ()
	{
		OutputStatSet stats = new OutputStatSet ();
		stats |= new OutputStat (string.Format ("{0} Custom Range (ms)", major ? "Major" : "Minor"), (custom_event_end - custom_event_start) * 1000, CumulationType.MIN_MAX_AVG, true);
		return stats;
	}

	protected bool ParseCustomEvent (GCEvent gcEvent)
	{
		switch (gcEvent.Type) {
			case GCEventType.CUSTOM_EVENT_START: {
				custom_event_start = gcEvent.Timestamp;
				return true;
			}
			case GCEventType.CUSTOM_EVENT_END: {
				custom_event_end = gcEvent.Timestamp;
				return true;
			}
			default:
				return false;
		}
	}
}

