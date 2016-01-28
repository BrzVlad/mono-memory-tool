using System.Collections.Generic;
using OxyPlot;

public class NurseryCollection : GCCollection {
	public override void Plot (PlotModel plotModel, List<double> timestamps, List<double> memoryUsage)
	{
		new PlotInterval (start_timestamp, end_timestamp, PlotInterval.green).Plot (plotModel, timestamps, memoryUsage);
	}

	public static List<NurseryCollection> ParseNurseryCollections (List<GCEvent> gcEvents)
	{
		List<NurseryCollection> nurseryCollections = new List<NurseryCollection> ();
		NurseryCollection current = null;

		foreach (GCEvent gcEvent in gcEvents) {
			if (gcEvent.Type == GCEventType.NURSERY_START) {
				current = new NurseryCollection ();
				current.start_timestamp = gcEvent.Timestamp;
			} else if (gcEvent.Type == GCEventType.NURSERY_END) {
				current.end_timestamp = gcEvent.Timestamp;
				Utils.Assert (current.start_timestamp != default(double));
				Utils.Assert (current.end_timestamp != default(double));
				nurseryCollections.Add (current);
				current = null;
			} else if (gcEvent.Type == GCEventType.CONCURRENT_START) {
				/*
				 * Ignore the previously added nursery collection. We view
				 * it as part of the major collection for now.
				 */
				nurseryCollections.RemoveAt (nurseryCollections.Count - 1);
				Utils.Assert (current == null);
			}
		}

		Utils.Assert (current == null);
		return nurseryCollections;
	}
}
