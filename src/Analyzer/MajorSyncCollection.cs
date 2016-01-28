using System.Collections.Generic;
using OxyPlot;

public class MajorSyncCollection : GCCollection {
	public override void Plot (PlotModel plotModel, List<double> timestamps, List<double> memoryUsage)
	{
		new PlotInterval (start_timestamp, end_timestamp, PlotInterval.red).Plot (plotModel, timestamps, memoryUsage);
	}

	public static List<MajorSyncCollection> ParseMajorSyncCollections (List<GCEvent> gcEvents)
	{
		List<MajorSyncCollection> majorSyncCollections = new List<MajorSyncCollection> ();
		MajorSyncCollection current = null;

		foreach (GCEvent gcEvent in gcEvents) {
			if (gcEvent.Type == GCEventType.MAJOR_START) {
				current = new MajorSyncCollection ();
				current.start_timestamp = gcEvent.Timestamp;
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
