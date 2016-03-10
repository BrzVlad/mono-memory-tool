using System.Collections.Generic;
using OxyPlot;

public abstract class GCCollection {
	/* Timestamps are measured in seconds */
	protected double start_timestamp, end_timestamp;
	private double custom_event_start, custom_event_end;
	private bool has_custom_event;
	private bool major;

	protected GCCollection (bool major)
	{
		this.major = major;
	}

	public abstract void Plot (PlotModel plotModel, List<double> timestamps, List<double> memoryUsage);

	public virtual OutputStatSet GetStats ()
	{
		OutputStatSet stats = new OutputStatSet ();
		if (has_custom_event)
			stats |= new OutputStat (string.Format ("{0} Custom Range (ms)", major ? "Major" : "Minor"), (custom_event_end - custom_event_start) * 1000, CumulationType.MIN_MAX_AVG);
		return stats;
	}

	protected bool ParseCustomEvent (GCEvent gcEvent)
	{
		switch (gcEvent.Type) {
			case GCEventType.CUSTOM_EVENT_START: {
				has_custom_event = true;
				custom_event_start = gcEvent.Timestamp;
				return true;
			}
			case GCEventType.CUSTOM_EVENT_END: {
				has_custom_event = true;
				custom_event_end = gcEvent.Timestamp;
				return true;
			}
			default:
				return false;
		}
	}
}

