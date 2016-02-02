using System.Collections.Generic;
using OxyPlot;

public class MajorSyncCollection : GCCollection {
	private double finish_gray_stack_start, finish_gray_stack_end;
	private double evacuated_block_sizes;

	public override void Plot (PlotModel plotModel, List<double> timestamps, List<double> memoryUsage)
	{
		new PlotInterval (start_timestamp, end_timestamp, PlotInterval.red).Plot (plotModel, timestamps, memoryUsage);
	}

	public override OutputStatSet GetStats ()
	{
		OutputStatSet stats = new OutputStatSet ();
		stats |= new OutputStat ("Total Major Pause (ms)", (end_timestamp - start_timestamp) * 1000, CumulationType.SUM);
		stats |= new OutputStat ("Evacuated block sizes", evacuated_block_sizes, CumulationType.MIN_MAX_AVG);
		stats ^= new OutputStat ("Major Pause (ms)", (end_timestamp - start_timestamp) * 1000, CumulationType.MIN_MAX_AVG);
		stats |= new OutputStat ("Major Finish GS (ms)", (finish_gray_stack_end - finish_gray_stack_start) * 1000, CumulationType.MIN_MAX_AVG);
		return stats;
	}

	public static List<MajorSyncCollection> ParseMajorSyncCollections (List<GCEvent> gcEvents)
	{
		List<MajorSyncCollection> majorSyncCollections = new List<MajorSyncCollection> ();
		MajorSyncCollection current = null;
		bool force = false;

		foreach (GCEvent gcEvent in gcEvents) {
			switch (gcEvent.Type) {
			case GCEventType.MAJOR_REQUEST_FORCE:
				force = true;
				break;
			case GCEventType.MAJOR_START:
				if (force) {
					force = false;
				} else {
					current = new MajorSyncCollection ();
					current.start_timestamp = gcEvent.Timestamp;
				}
				break;
			case GCEventType.MAJOR_END:
				if (current != null) {
					current.end_timestamp = gcEvent.Timestamp;
					Utils.Assert (current.start_timestamp != default(double));
					Utils.Assert (current.end_timestamp != default(double));
					majorSyncCollections.Add (current);
					current = null;
				}
				break;
			case GCEventType.CONCURRENT_FINISH:
				current = null;
				break;
			case GCEventType.FINISH_GRAY_STACK_START:
				if (current != null)
					current.finish_gray_stack_start = gcEvent.Timestamp;
				break;
			case GCEventType.FINISH_GRAY_STACK_END:
				if (current != null)
					current.finish_gray_stack_end = gcEvent.Timestamp;
				break;
			case GCEventType.EVACUATING_BLOCKS:
				if (current != null)
					current.evacuated_block_sizes += 1;
				break;
			}
		}

		Utils.Assert (current == null);
		return majorSyncCollections;
	}
}
