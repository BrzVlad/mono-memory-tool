using System;
using System.Collections.Generic;
using OxyPlot;

class WorkerInfo {
	private class WorkerFinishStat {
		public double major_scan, los_scan, work_time;
		public double timestamp;

		public double DrainWork {
			get {
				return work_time - los_scan - major_scan;
			}
		}
	}

	private List<WorkerFinishStat> finish_stats = new List<WorkerFinishStat> ();
	private int forced_index = -1;

	/* Index should be > 0, which is actually the gc thread */
	private int worker_index;

	public bool IsForcedFinish {
		get {
			return forced_index != -1;
		}
	}

	public WorkerInfo (int worker_index)
	{
		this.worker_index = worker_index;
	}

	public void HandleEvent (GCEvent gcEvent) {
		switch (gcEvent.Type) {
		case GCEventType.MAJOR_WORKER_FINISH_STATS:
		case GCEventType.MAJOR_WORKER_FINISH_FORCED_STATS:
			Utils.Assert (worker_index == int.Parse (gcEvent.Values [0]));
			finish_stats.Add (new WorkerFinishStat {
				major_scan = ((double)long.Parse (gcEvent.Values [1])) / 10000000,
				los_scan = ((double)long.Parse (gcEvent.Values [2])) / 10000000,
				work_time = ((double)long.Parse (gcEvent.Values [3])) / 10000000,
				timestamp = gcEvent.Timestamp
				});
			if (gcEvent.Type == GCEventType.MAJOR_WORKER_FINISH_FORCED_STATS)
				forced_index = finish_stats.Count - 1;
			break;
		}
	}

	private int ComputeLastConcurrentIndex (double start_of_end)
	{
		int last_concurrent_index;

		if (forced_index != -1) {
			last_concurrent_index = forced_index;
		} else {
			last_concurrent_index = -1;
			foreach (WorkerFinishStat stat in finish_stats) {
				if (stat.timestamp >= start_of_end)
					break;
				last_concurrent_index++;
			}
		}

		return last_concurrent_index;
	}

	public OutputStatSet AddConcurrentStats (OutputStatSet stats, double start_of_end)
	{
		int last_concurrent_index = ComputeLastConcurrentIndex (start_of_end);

		/* Return if worker not part of concurrent phase */
		if (last_concurrent_index == -1)
			return stats;

		stats |= new OutputStat (string.Format ("Conc M&S {0,2} (ms)", worker_index), finish_stats [0].work_time * 1000, CumulationType.MIN_MAX_AVG, true);
		stats |= new OutputStat (string.Format ("Major Mod Preclean {0,2} (ms)", worker_index), finish_stats [last_concurrent_index].major_scan * 1000, CumulationType.MIN_MAX_AVG, true);
		stats |= new OutputStat (string.Format ("LOS Mod Preclean {0,2} (ms)", worker_index), finish_stats [last_concurrent_index].los_scan * 1000, CumulationType.MIN_MAX_AVG, true);
		stats |= new OutputStat (string.Format ("Finish conc M&S {0,2} (ms)", worker_index), (finish_stats [last_concurrent_index].DrainWork - finish_stats [0].work_time) * 1000, CumulationType.MIN_MAX_AVG);
		return stats;
	}

	public OutputStatSet AddFinishStats (OutputStatSet stats, double start_of_end)
	{
		int last_concurrent_index = ComputeLastConcurrentIndex (start_of_end);

		/* Return if worker not part of finishing pause */
		if (last_concurrent_index == finish_stats.Count - 1)
			return stats;

		stats |= new OutputStat (string.Format ("Mod Union Major Scan {0,2} (ms)", worker_index), finish_stats [finish_stats.Count - 1].major_scan * 1000, CumulationType.MIN_MAX_AVG, true);
		stats |= new OutputStat (string.Format ("Mod Union LOS Scan {0,2} (ms)", worker_index), finish_stats [finish_stats.Count - 1].los_scan, CumulationType.MIN_MAX_AVG, true);
		stats |= new OutputStat (string.Format ("Major Finish {0,2} (ms)", worker_index), finish_stats [finish_stats.Count - 1].DrainWork * 1000, CumulationType.MIN_MAX_AVG);
		return stats;
	}

}

class WorkerStatManager {
	private const int MAX_NUM_WORKERS = 16;
	private WorkerInfo[] worker_infos = new WorkerInfo [MAX_NUM_WORKERS];

	public void HandleEvent (GCEvent gcEvent, double start_timestamp_finish)
	{
		Utils.Assert (gcEvent.Type == GCEventType.MAJOR_WORKER_FINISH_STATS
			|| gcEvent.Type == GCEventType.MAJOR_WORKER_FINISH_FORCED_STATS);

		int worker_index = int.Parse (gcEvent.Values [0]);
		if (worker_infos [worker_index] == null)
			worker_infos [worker_index] = new WorkerInfo (worker_index);

		worker_infos [worker_index].HandleEvent (gcEvent);
	}

	public int NumForced {
		get {
			int num_forced = 0;
			for (int i = 0; i < MAX_NUM_WORKERS; i++) {
				if (worker_infos [i] != null && worker_infos [i].IsForcedFinish)
					num_forced++;
			}
			return num_forced;
		}
	}

	public OutputStatSet AddConcurrentStats (OutputStatSet stats, double start_of_end)
	{
		for (int i = 0; i < MAX_NUM_WORKERS; i++) {
			if (worker_infos [i] != null)
				stats = worker_infos [i].AddConcurrentStats (stats, start_of_end);
		}
		return stats;
	}

	public OutputStatSet AddFinishStats (OutputStatSet stats, double start_of_end)
	{
		for (int i = 0; i < MAX_NUM_WORKERS; i++) {
			if (worker_infos [i] != null)
				stats = worker_infos [i].AddFinishStats (stats, start_of_end);
		}
		return stats;
	}
}

public class MajorConcCollection : GCCollection {

	private double end_of_start_timestamp, start_of_end_timestamp;
	private double finish_gray_stack, major_scan, los_scan;
	private int num_minor;
	private double evacuated_block_sizes;
	private double concurrent_sweep_end, next_nursery_start;
	private WorkerStatManager worker_manager = new WorkerStatManager ();

	public MajorConcCollection () : base (true) { }

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
		stats |= new OutputStat ("Evacuated block sizes", evacuated_block_sizes, CumulationType.MIN_MAX_AVG);
		stats |= new OutputStat ("Start Pause (ms)", (end_of_start_timestamp - start_timestamp) * 1000, CumulationType.MIN_MAX_AVG);
		stats |= new OutputStat ("Minor while Conc", num_minor, CumulationType.MIN_MAX_AVG);
		stats = worker_manager.AddConcurrentStats (stats, start_of_end_timestamp);
		stats ^= new OutputStat ("Major Pause (ms)", (end_timestamp - start_of_end_timestamp) * 1000, CumulationType.MIN_MAX_AVG);
		stats |= new OutputStat ("Forced Finish", worker_manager.NumForced, CumulationType.MIN_MAX_AVG);
		stats = worker_manager.AddFinishStats (stats, start_of_end_timestamp);
		stats |= new OutputStat ("Mod Union Major Scan (ms)", major_scan * 1000, CumulationType.MIN_MAX_AVG, true);
                stats |= new OutputStat ("Mod Union LOS Scan (ms)", los_scan * 1000, CumulationType.MIN_MAX_AVG, true);
		stats |= new OutputStat ("Major Finish GS (ms)", finish_gray_stack * 1000, CumulationType.MIN_MAX_AVG);
		if (concurrent_sweep_end > end_timestamp) {
			if (next_nursery_start != default(double) && concurrent_sweep_end > next_nursery_start) {
				Utils.Assert (next_nursery_start > end_timestamp);
				stats |= new OutputStat ("Concurrent Sweep (ms)", (next_nursery_start - end_timestamp) * 1000, CumulationType.MIN_MAX_AVG);
				stats |= new OutputStat ("Forced finish Sweep (ms)", (concurrent_sweep_end - next_nursery_start) * 1000, CumulationType.MIN_MAX_AVG);
			} else {
				stats |= new OutputStat ("Concurrent Sweep (ms)", (concurrent_sweep_end - end_timestamp) * 1000, CumulationType.MIN_MAX_AVG);
				stats |= new OutputStat ("Forced finish Sweep (ms)", 0, CumulationType.MIN_MAX_AVG);
			}
		} else {
			stats |= new OutputStat ("Concurrent Sweep (ms)", 0, CumulationType.MIN_MAX_AVG);
			stats |= new OutputStat ("Forced finish Sweep (ms)", 0, CumulationType.MIN_MAX_AVG);
		}
		return stats | base.GetStats ();
	}

	public static List<MajorConcCollection> ParseMajorConcCollections (List<GCEvent> gcEvents)
	{
		List<MajorConcCollection> majorConcCollections = new List<MajorConcCollection> ();
		MajorConcCollection current = null, last_current = null;
		double last_nursery_end = default(double);

		foreach (GCEvent gcEvent in gcEvents) {
			switch (gcEvent.Type) {
			case GCEventType.MAJOR_REQUEST_FORCE:
				current = null;
				break;
			case GCEventType.NURSERY_START:
				if (current == null && last_current != null && last_current.next_nursery_start == default(double))
					last_current.next_nursery_start = gcEvent.Timestamp;
				break;
			case GCEventType.NURSERY_END:
				last_nursery_end = gcEvent.Timestamp;
				if (current != null)
					current.num_minor++;
				break;
			case GCEventType.CONCURRENT_START:
				current = new MajorConcCollection ();
				last_current = current;
				current.start_timestamp = gcEvent.Timestamp;
				/*
				 * The nursery end entry has an AFTER timestamp type whereas the concurrent
				 * start entry has a BEFORE timestamp type, which means the nursery end
				 * entry has a higher timestamp, even though it precedes the concurrent
				 * start entry.
				 */
				current.end_of_start_timestamp = last_nursery_end;
				break;
			case GCEventType.EVACUATING_BLOCKS:
				if (current != null)
					current.evacuated_block_sizes += 1;
				break;
			case GCEventType.MAJOR_WORKER_FINISH_STATS:
			case GCEventType.MAJOR_WORKER_FINISH_FORCED_STATS:
				if (current != null)
					current.worker_manager.HandleEvent (gcEvent, current.start_of_end_timestamp);
				break;
			case GCEventType.COLLECTION_END_STATS:
				if (current != null && current.start_of_end_timestamp != default (double)) {
					current.major_scan = ((double)long.Parse (gcEvent.Values [0])) / 10000000;
					current.los_scan = ((double)long.Parse (gcEvent.Values [1])) / 10000000;
					current.finish_gray_stack = ((double)long.Parse (gcEvent.Values [2])) / 10000000;
				}
				break;
			case GCEventType.CONCURRENT_FINISH:
				if (current != null) {
					current.start_of_end_timestamp = gcEvent.Timestamp;
				}
				break;
			case GCEventType.MAJOR_END:
				if (current != null) {
					current.end_timestamp = gcEvent.Timestamp;
					Utils.Assert (current.start_timestamp != default(double));
					Utils.Assert (current.end_of_start_timestamp != default(double));
					Utils.Assert (current.start_of_end_timestamp != default(double));
					if (current.end_timestamp != default(double))
						majorConcCollections.Add (current);
					last_current = current;
					current = null;
				}
				break;
			case GCEventType.CONCURRENT_SWEEP_END:
				if (current == null && last_current != null && last_current.concurrent_sweep_end == default(double))
					last_current.concurrent_sweep_end = gcEvent.Timestamp;
				break;
			default:
				if (current != null)
					current.ParseCustomEvent (gcEvent);
				break;
			}
		}

		return majorConcCollections;
	}
}

