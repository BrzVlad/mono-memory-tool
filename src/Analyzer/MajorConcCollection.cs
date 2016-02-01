using System;
using System.Collections.Generic;
using OxyPlot;

public class MajorConcCollection : GCCollection {
	private double end_of_start_timestamp, start_of_end_timestamp;
	private int num_minor;

	public override void Plot (PlotModel plotModel, List<double> timestamps, List<double> memoryUsage)
	{
		new PlotInterval (start_timestamp, end_of_start_timestamp, PlotInterval.red).Plot (plotModel, timestamps, memoryUsage);
		new PlotInterval (end_of_start_timestamp, start_of_end_timestamp, PlotInterval.blue).Plot (plotModel, timestamps, memoryUsage);
		new PlotInterval (start_of_end_timestamp, end_timestamp, PlotInterval.red).Plot (plotModel, timestamps, memoryUsage);
	}

	public override OutputStatSet GetStats ()
	{
		OutputStatSet stats = new OutputStatSet ();
		stats |= new OutputStat ("Total Major Pause (ms)", (end_timestamp - start_of_end_timestamp + end_of_start_timestamp - start_timestamp) * 1000, CumulationType.SUM);
		stats |= new OutputStat ("Avg Major Pause (ms)", (end_timestamp - start_of_end_timestamp) * 1000, CumulationType.AVERAGE);
		stats |= new OutputStat ("Max Major Pause (ms)", (end_timestamp - start_of_end_timestamp) * 1000, CumulationType.MAX);
		stats |= new OutputStat ("Avg Conc M&S (ms)", (start_of_end_timestamp - end_of_start_timestamp) * 1000, CumulationType.AVERAGE);
		stats |= new OutputStat ("Max Conc M&S (ms)", (start_of_end_timestamp - end_of_start_timestamp) * 1000, CumulationType.MAX);
		stats |= new OutputStat ("Avg Start Pause (ms)", (end_of_start_timestamp - start_timestamp) * 1000, CumulationType.AVERAGE);
		stats |= new OutputStat ("Max Start Pause (ms)", (end_of_start_timestamp - start_timestamp) * 1000, CumulationType.MAX);
		stats |= new OutputStat ("Avg Minor while Conc", num_minor, CumulationType.AVERAGE);
		stats |= new OutputStat ("Max Minor while Conc", num_minor, CumulationType.MAX);
		return stats;
	}

	public static List<MajorConcCollection> ParseMajorConcCollections (List<GCEvent> gcEvents)
	{
		List<MajorConcCollection> majorConcCollections = new List<MajorConcCollection> ();
		MajorConcCollection current = null;
		double last_nursery_end = default(double);

		foreach (GCEvent gcEvent in gcEvents) {
			if (gcEvent.Type == GCEventType.MAJOR_REQUEST_FORCE) {
				current = null;
			} else if (gcEvent.Type == GCEventType.NURSERY_END) {
				last_nursery_end = gcEvent.Timestamp;
				if (current != null)
					current.num_minor++;
			} else if (gcEvent.Type == GCEventType.CONCURRENT_START) {
				current = new MajorConcCollection ();
				current.start_timestamp = gcEvent.Timestamp;
				/*
				 * The nursery end entry has an AFTER timestamp type whereas the concurrent
				 * start entry has a BEFORE timestamp type, which means the nursery end
				 * entry has a higher timestamp, even though it precedes the concurrent
				 * start entry.
				 */
				current.end_of_start_timestamp = last_nursery_end;
			} else if (gcEvent.Type == GCEventType.CONCURRENT_FINISH && current != null) {
				current.start_of_end_timestamp = gcEvent.Timestamp;
			} else if (gcEvent.Type == GCEventType.MAJOR_END && current != null) {
				current.end_timestamp = gcEvent.Timestamp;
				Utils.Assert (current.start_timestamp != default(double));
				Utils.Assert (current.end_timestamp != default(double));
				Utils.Assert (current.end_of_start_timestamp != default(double));
				Utils.Assert (current.start_of_end_timestamp != default(double));
				majorConcCollections.Add (current);
				current = null;
			}
		}

		Utils.Assert (current == null);
		return majorConcCollections;
	}
}

