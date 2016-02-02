using System.Collections.Generic;
using OxyPlot;

public class NurseryCollection : GCCollection {
	private double finish_gray_stack_start, finish_gray_stack_end;
	private double major_card_table_scan_start, major_card_table_scan_end;
	private double los_card_table_scan_start, los_card_table_scan_end;

	public override void Plot (PlotModel plotModel, List<double> timestamps, List<double> memoryUsage)
	{
		new PlotInterval (start_timestamp, end_timestamp, PlotInterval.green).Plot (plotModel, timestamps, memoryUsage);
	}

	public override OutputStatSet GetStats ()
	{
		OutputStatSet stats = new OutputStatSet ();
		stats |= new OutputStat ("Total Nursery Pause (ms)", (end_timestamp - start_timestamp) * 1000, CumulationType.SUM);
		stats ^= new OutputStat ("Nursery Pause (ms)", (end_timestamp - start_timestamp) * 1000, CumulationType.MIN_MAX_AVG);
		stats |= new OutputStat ("Major Scan (ms)", (major_card_table_scan_end - major_card_table_scan_start) * 1000, CumulationType.MIN_MAX_AVG);
		stats |= new OutputStat ("LOS Scan (ms)", (los_card_table_scan_end - los_card_table_scan_start) * 1000, CumulationType.MIN_MAX_AVG);
		stats |= new OutputStat ("Minor Finish GS (ms)", (finish_gray_stack_end - finish_gray_stack_start) * 1000, CumulationType.MIN_MAX_AVG);
		return stats;
	}

	public static List<NurseryCollection> ParseNurseryCollections (List<GCEvent> gcEvents)
	{
		List<NurseryCollection> nurseryCollections = new List<NurseryCollection> ();
		NurseryCollection current = null;

		foreach (GCEvent gcEvent in gcEvents) {
			switch (gcEvent.Type) {
			case GCEventType.NURSERY_START:
				current = new NurseryCollection ();
				current.start_timestamp = gcEvent.Timestamp;
				break;
			case GCEventType.MAJOR_CARDTABLE_SCAN_START:
				if (current != null)
					current.major_card_table_scan_start = gcEvent.Timestamp;
				break;
			case GCEventType.MAJOR_CARDTABLE_SCAN_END:
				if (current != null)
					current.major_card_table_scan_end = gcEvent.Timestamp;
				break;
			case GCEventType.LOS_CARDTABLE_SCAN_START:
				if (current != null)
					current.los_card_table_scan_start = gcEvent.Timestamp;
				break;
			case GCEventType.LOS_CARDTABLE_SCAN_END:
				if (current != null)
					current.los_card_table_scan_end = gcEvent.Timestamp;
				break;
			case GCEventType.FINISH_GRAY_STACK_START:
				if (current != null)
					current.finish_gray_stack_start = gcEvent.Timestamp;
				break;
			case GCEventType.FINISH_GRAY_STACK_END:
				if (current != null)
					current.finish_gray_stack_end = gcEvent.Timestamp;
				break;
			case GCEventType.NURSERY_END:
				current.end_timestamp = gcEvent.Timestamp;
				Utils.Assert (current.start_timestamp != default(double));
				Utils.Assert (current.end_timestamp != default(double));
				nurseryCollections.Add (current);
				current = null;
				break;
			case GCEventType.CONCURRENT_START:
				/*
				 * Ignore the previously added nursery collection. We view
				 * it as part of the major collection for now.
				 */
				nurseryCollections.RemoveAt (nurseryCollections.Count - 1);
				Utils.Assert (current == null);
				break;
			}
		}

		Utils.Assert (current == null);
		return nurseryCollections;
	}
}
