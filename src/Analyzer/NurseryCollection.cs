using System.Collections.Generic;
using OxyPlot;

public class NurseryCollection : GCCollection {
	private double finish_gray_stack_start, finish_gray_stack_end;
	private double major_card_table_scan_start, major_card_table_scan_end;
	private double los_card_table_scan_start, los_card_table_scan_end;
	private double suspend_start, suspend_end, resume_start, resume_end;

	public NurseryCollection () : base (false) { }

	public override void Plot (PlotModel plotModel, List<double> timestamps, List<double> memoryUsage)
	{
		new PlotInterval (start_timestamp, end_timestamp, PlotInterval.green).Plot (plotModel, timestamps, memoryUsage);
	}

	public override OutputStatSet GetStats ()
	{
		OutputStatSet stats = new OutputStatSet ();
		stats |= new OutputStat ("Total Suspend World (ms)", (suspend_end - suspend_start) * 1000, CumulationType.SUM);
		stats |= new OutputStat ("Total Resume World (ms)", (resume_end - resume_start) * 1000, CumulationType.SUM);
		stats |= new OutputStat ("Total Nursery Pause (ms)", (end_timestamp - start_timestamp) * 1000, CumulationType.SUM);
		stats |= new OutputStat ("Suspend World (ms)", (suspend_end - suspend_start) * 1000, CumulationType.MIN_MAX_AVG);
		stats |= new OutputStat ("Resume World (ms)", (resume_end - resume_start) * 1000, CumulationType.MIN_MAX_AVG);
		stats ^= new OutputStat ("Nursery Pause (ms)", (end_timestamp - start_timestamp) * 1000, CumulationType.MIN_MAX_AVG);
		stats |= new OutputStat ("Major Scan (ms)", (major_card_table_scan_end - major_card_table_scan_start) * 1000, CumulationType.MIN_MAX_AVG);
		stats |= new OutputStat ("LOS Scan (ms)", (los_card_table_scan_end - los_card_table_scan_start) * 1000, CumulationType.MIN_MAX_AVG);
		stats |= new OutputStat ("Minor Finish GS (ms)", (finish_gray_stack_end - finish_gray_stack_start) * 1000, CumulationType.MIN_MAX_AVG);
		return stats | base.GetStats ();
	}

	public static List<NurseryCollection> ParseNurseryCollections (List<GCEvent> gcEvents)
	{
		List<NurseryCollection> nurseryCollections = new List<NurseryCollection> ();
		NurseryCollection current = new NurseryCollection ();

		foreach (GCEvent gcEvent in gcEvents) {
			switch (gcEvent.Type) {
			case GCEventType.SUSPEND_START:
				current.suspend_start = gcEvent.Timestamp;
				break;
			case GCEventType.SUSPEND_END:
				current.suspend_end = gcEvent.Timestamp;
				break;
			case GCEventType.RESUME_START: {
				NurseryCollection prev = nurseryCollections.Count > 1 ? nurseryCollections [nurseryCollections.Count - 1] : null;
				if (prev != null && prev.resume_start == default(double))
					prev.resume_start = gcEvent.Timestamp;
				break;
			}
			case GCEventType.RESUME_END: {
				NurseryCollection prev = nurseryCollections.Count > 1 ? nurseryCollections [nurseryCollections.Count - 1] : null;
				if (prev != null && prev.resume_end == default(double))
					prev.resume_end = gcEvent.Timestamp;
				break;
			}
			case GCEventType.NURSERY_START:
				current.start_timestamp = gcEvent.Timestamp;
				break;
			case GCEventType.MAJOR_CARDTABLE_SCAN_START:
				current.major_card_table_scan_start = gcEvent.Timestamp;
				break;
			case GCEventType.MAJOR_CARDTABLE_SCAN_END:
				current.major_card_table_scan_end = gcEvent.Timestamp;
				break;
			case GCEventType.LOS_CARDTABLE_SCAN_START:
				current.los_card_table_scan_start = gcEvent.Timestamp;
				break;
			case GCEventType.LOS_CARDTABLE_SCAN_END:
				current.los_card_table_scan_end = gcEvent.Timestamp;
				break;
			case GCEventType.FINISH_GRAY_STACK_START:
				current.finish_gray_stack_start = gcEvent.Timestamp;
				break;
			case GCEventType.FINISH_GRAY_STACK_END:
				current.finish_gray_stack_end = gcEvent.Timestamp;
				break;
			case GCEventType.NURSERY_END:
				current.end_timestamp = gcEvent.Timestamp;
				Utils.Assert (current.start_timestamp != default(double));
				/* We lack end_timestamp, probably due to crash and flush fail */
				if (current.end_timestamp != default(double))
					nurseryCollections.Add (current);
				current = new NurseryCollection ();
				break;
			case GCEventType.CONCURRENT_START:
				/*
				 * Ignore the previously added nursery collection. We view
				 * it as part of the major collection for now.
				 */
				nurseryCollections.RemoveAt (nurseryCollections.Count - 1);
				break;
			default:
				current.ParseCustomEvent (gcEvent);
				break;
			}
		}

		return nurseryCollections;
	}
}
