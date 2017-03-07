using System.Collections.Generic;
using OxyPlot;

class WorkerInfoNursery {
	private double worker_finish;
	private double worker_last_scan, worker_last_scan_start;
	private double total_major_scan, total_los_scan;

	/* If index is 0, this is actually the gc thread */
	private int worker_index;

	public WorkerInfoNursery (int worker_index)
	{
		this.worker_index = worker_index;
	}

	public void HandleEvent (GCEvent gcEvent) {
		switch (gcEvent.Type) {
		case GCEventType.MAJOR_CARDTABLE_SCAN_START:
			worker_last_scan_start = gcEvent.Timestamp;
			break;
		case GCEventType.MAJOR_CARDTABLE_SCAN_END:
			total_major_scan += gcEvent.Timestamp - worker_last_scan_start;
			worker_last_scan = gcEvent.Timestamp;
			break;
		case GCEventType.LOS_CARDTABLE_SCAN_START:
			worker_last_scan_start = gcEvent.Timestamp;
			break;
		case GCEventType.LOS_CARDTABLE_SCAN_END:
			total_los_scan += gcEvent.Timestamp - worker_last_scan_start;
			worker_last_scan = gcEvent.Timestamp;
			break;
		case GCEventType.WORKER_FINISH:
			worker_finish = gcEvent.Timestamp;
			break;
		}
	}

	public OutputStatSet AddFinishStats (OutputStatSet stats)
	{
		stats |= new OutputStat (string.Format ("Major Scan {0,2} (ms)", worker_index), total_major_scan * 1000, CumulationType.MIN_MAX_AVG);
		stats |= new OutputStat (string.Format ("LOS Scan {0,2} (ms)", worker_index), total_los_scan * 1000, CumulationType.MIN_MAX_AVG);

		if (worker_index == 0)
			return stats;

		if (worker_last_scan != default(double))
			stats |= new OutputStat (string.Format ("Finish Par {0,2} (ms)", worker_index), (worker_finish - worker_last_scan) * 1000, CumulationType.MIN_MAX_AVG);
		else
			stats |= new OutputStat (string.Format ("Finish Par {0,2} (ms)", worker_index), 0, CumulationType.MIN_MAX_AVG);
		return stats;
	}

}

class WorkerStatManagerNursery {
	private const int MAX_NUM_WORKERS = 16;
	private WorkerInfoNursery[] worker_infos = new WorkerInfoNursery [MAX_NUM_WORKERS];

	public void HandleEvent (GCEvent gcEvent)
	{
		if (worker_infos [gcEvent.WorkerIndex] == null)
			worker_infos [gcEvent.WorkerIndex] = new WorkerInfoNursery (gcEvent.WorkerIndex);

		worker_infos [gcEvent.WorkerIndex].HandleEvent (gcEvent);
	}

	public OutputStatSet AddFinishStats (OutputStatSet stats)
	{
		for (int i = 0; i < MAX_NUM_WORKERS; i++) {
			if (worker_infos [i] != null)
				stats = worker_infos [i].AddFinishStats (stats);
		}
		return stats;
	}
}

public class NurseryCollection : GCCollection {
	private double finish_gray_stack_start, finish_gray_stack_end;
	private double suspend_start, suspend_end, resume_start, resume_end;
	private WorkerStatManagerNursery worker_manager = new WorkerStatManagerNursery ();

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
		stats = worker_manager.AddFinishStats (stats);
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
			case GCEventType.MAJOR_CARDTABLE_SCAN_END:
			case GCEventType.LOS_CARDTABLE_SCAN_START:
			case GCEventType.LOS_CARDTABLE_SCAN_END:
			case GCEventType.WORKER_FINISH:
				if (current != null)
                                        current.worker_manager.HandleEvent (gcEvent);
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
