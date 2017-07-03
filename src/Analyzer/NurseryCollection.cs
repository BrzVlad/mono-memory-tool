using System.Collections.Generic;
using OxyPlot;

class WorkerInfoNursery {
	private double total_major_scan, total_los_scan, total_work_time;

	/* Index should be > 0, which is actually the gc thread */
	private int worker_index;

	public WorkerInfoNursery (int worker_index)
	{
		this.worker_index = worker_index;
	}

	public void HandleEvent (GCEvent gcEvent) {
		switch (gcEvent.Type) {
		case GCEventType.MINOR_WORKER_FINISH_STATS:
			Utils.Assert (worker_index == int.Parse (gcEvent.Values [0]));
			total_major_scan = ((double)long.Parse (gcEvent.Values [1])) / 10000000;
			total_los_scan = ((double)long.Parse (gcEvent.Values [2])) / 10000000;
			total_work_time = ((double)long.Parse (gcEvent.Values [3])) / 10000000;
			break;
		}
	}

	public OutputStatSet AddFinishStats (OutputStatSet stats)
	{
		stats |= new OutputStat (string.Format ("Major Scan {0,2} (ms)", worker_index), total_major_scan * 1000, CumulationType.MIN_MAX_AVG);
		stats |= new OutputStat (string.Format ("LOS Scan {0,2} (ms)", worker_index), total_los_scan * 1000, CumulationType.MIN_MAX_AVG);
		stats |= new OutputStat (string.Format ("Finish Par {0,2} (ms)", worker_index), (total_work_time - total_major_scan - total_los_scan) * 1000, CumulationType.MIN_MAX_AVG);
		return stats;
	}

}

class WorkerStatManagerNursery {
	private const int MAX_NUM_WORKERS = 16;
	private WorkerInfoNursery[] worker_infos = new WorkerInfoNursery [MAX_NUM_WORKERS];

	public void HandleEvent (GCEvent gcEvent)
	{
		int worker_index;
		Utils.Assert (gcEvent.Type == GCEventType.MINOR_WORKER_FINISH_STATS);
		worker_index = int.Parse (gcEvent.Values [0]);
		if (worker_infos [worker_index] == null)
			worker_infos [worker_index] = new WorkerInfoNursery (worker_index);

		worker_infos [worker_index].HandleEvent (gcEvent);
	}

	public OutputStatSet AddFinishStats (OutputStatSet stats)
	{
		for (int i = 1; i < MAX_NUM_WORKERS; i++) {
			if (worker_infos [i] != null)
				stats = worker_infos [i].AddFinishStats (stats);
		}
		return stats;
	}
}

public class NurseryCollection : GCCollection {
	private double finish_gray_stack, major_scan, los_scan;
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
		stats |= new OutputStat ("Major Scan (ms)", major_scan * 1000, CumulationType.MIN_MAX_AVG);
		stats |= new OutputStat ("LOS Scan (ms)", los_scan * 1000, CumulationType.MIN_MAX_AVG);
		stats |= new OutputStat ("Minor Finish GS (ms)", finish_gray_stack * 1000, CumulationType.MIN_MAX_AVG);
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
			case GCEventType.MINOR_WORKER_FINISH_STATS:
				current.worker_manager.HandleEvent (gcEvent);
				break;
			case GCEventType.COLLECTION_END_STATS:
				if (current != null) {
					current.major_scan = ((double)long.Parse (gcEvent.Values [0])) / 10000000;
					current.los_scan = ((double)long.Parse (gcEvent.Values [1])) / 10000000;
					current.finish_gray_stack = ((double)long.Parse (gcEvent.Values [2])) / 10000000;
				}
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
