using System.Collections.Generic;
using OxyPlot;

public class MajorSyncCollection : GCCollection {
	public override void Plot (PlotModel plotModel, List<double> timestamps, List<double> memoryUsage)
	{
		new PlotInterval (start_timestamp, end_timestamp, PlotInterval.red).Plot (plotModel, timestamps, memoryUsage);
	}

	public override OutputStatSet GetStats ()
	{
		OutputStatSet stats = new OutputStatSet ();
		stats |= new OutputStat ("Total Major Pause (ms)", (end_timestamp - start_timestamp) * 1000, CumulationType.SUM);
		stats |= new OutputStat ("Avg Major Pause (ms)", (end_timestamp - start_timestamp) * 1000, CumulationType.AVERAGE);
		stats |= new OutputStat ("Max Major Pause (ms)", (end_timestamp - start_timestamp) * 1000, CumulationType.MAX);
		return stats;
	}

	public static List<MajorSyncCollection> ParseMajorSyncCollections (List<GCEvent> gcEvents)
	{
		List<MajorSyncCollection> majorSyncCollections = new List<MajorSyncCollection> ();
		MajorSyncCollection current = null;
		bool force = false;

		foreach (GCEvent gcEvent in gcEvents) {
			if (gcEvent.Type == GCEventType.MAJOR_REQUEST_FORCE) {
				force = true;
			} else if (gcEvent.Type == GCEventType.MAJOR_START) {
				if (force) {
					force = false;
				} else {
					current = new MajorSyncCollection ();
					current.start_timestamp = gcEvent.Timestamp;
				}
			} else if (gcEvent.Type == GCEventType.MAJOR_END && current != null) {
				current.end_timestamp = gcEvent.Timestamp;
				Utils.Assert (current.start_timestamp != default(double));
				Utils.Assert (current.end_timestamp != default(double));
				majorSyncCollections.Add (current);
				current = null;
			} else if (gcEvent.Type == GCEventType.CONCURRENT_FINISH) {
				current = null;
			}
		}

		Utils.Assert (current == null);
		return majorSyncCollections;
	}
}
