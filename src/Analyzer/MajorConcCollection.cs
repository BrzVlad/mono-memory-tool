using System;
using System.Collections.Generic;
using OxyPlot;

class WorkerInfo {
	private double worker_finish, worker_finish_before_scan;
	private double workers_first_scan, worker_last_scan, worker_last_scan_start;
	private double total_major_scan, total_los_scan;

	/* If index is 0, this is actually the gc thread */
	private int worker_index;

	public WorkerInfo (int worker_index)
	{
		this.worker_index = worker_index;
	}

	public void ReportScan (GCEvent gcEvent, bool concurrent)
	{
		if (workers_first_scan == default(double)) {
			workers_first_scan = gcEvent.Timestamp;
			if (gcEvent.WorkerIndex == worker_index && concurrent) {
				worker_finish = workers_first_scan;
				worker_finish_before_scan = workers_first_scan;
			}
		}
	}

	public void HandleEvent (GCEvent gcEvent) {
		switch (gcEvent.Type) {
		case GCEventType.MAJOR_MOD_UNION_SCAN_START:
			worker_last_scan_start = gcEvent.Timestamp;
			break;
		case GCEventType.MAJOR_MOD_UNION_SCAN_END:
			total_major_scan += gcEvent.Timestamp - worker_last_scan_start;
			worker_last_scan = gcEvent.Timestamp;
			break;
		case GCEventType.LOS_MOD_UNION_SCAN_START:
			worker_last_scan_start = gcEvent.Timestamp;
			break;
		case GCEventType.LOS_MOD_UNION_SCAN_END:
			total_los_scan += gcEvent.Timestamp - worker_last_scan_start;
			worker_last_scan = gcEvent.Timestamp;
			break;
		case GCEventType.WORKER_FINISH:
		case GCEventType.WORKER_FINISH_FORCED:
			worker_finish = gcEvent.Timestamp;
			if (workers_first_scan == default(double))
				worker_finish_before_scan = worker_finish;
			break;
		}
	}

	public OutputStatSet AddConcurrentStats (OutputStatSet stats, double worker_start, double start_of_end)
	{
		if (worker_finish == default(double) || worker_index == 0)
			return stats;

		double worker_cms_finish = worker_finish_before_scan;
		if (worker_finish == worker_cms_finish || worker_finish > start_of_end)
			worker_finish = start_of_end;

		stats |= new OutputStat (string.Format ("Conc M&S {0,2} (ms)", worker_index), (worker_cms_finish - worker_start) * 1000, CumulationType.MIN_MAX_AVG);
		stats |= new OutputStat (string.Format ("Major Mod Preclean {0,2} (ms)", worker_index), total_major_scan * 1000, CumulationType.MIN_MAX_AVG);
		stats |= new OutputStat (string.Format ("LOS Mod Preclean {0,2} (ms)", worker_index), total_los_scan * 1000, CumulationType.MIN_MAX_AVG);
		if (worker_last_scan != default(double))
			stats |= new OutputStat (string.Format ("Finish conc M&S {0,2} (ms)", worker_index), (worker_finish - worker_last_scan) * 1000, CumulationType.MIN_MAX_AVG);
		else
			stats |= new OutputStat (string.Format ("Finish conc M&S {0,2} (ms)", worker_index), 0, CumulationType.MIN_MAX_AVG);
		return stats;
	}

	public OutputStatSet AddForcedFinishStat (OutputStatSet stats, double start_of_finish)
	{
		if (worker_index == 0)
			return stats;

		if (worker_finish_before_scan != default(double))
			stats |= new OutputStat (string.Format ("Forced finish {0,2} (ms)", worker_index), (worker_finish_before_scan - start_of_finish) * 1000, CumulationType.MIN_MAX_AVG);
		else
			stats |= new OutputStat (string.Format ("Forced finish {0,2} (ms)", worker_index), 0, CumulationType.MIN_MAX_AVG);
		return stats;
	}

	public OutputStatSet AddFinishStats (OutputStatSet stats)
	{
		stats |= new OutputStat (string.Format ("Mod Union Major Scan {0,2} (ms)", worker_index), total_major_scan * 1000, CumulationType.MIN_MAX_AVG);
		stats |= new OutputStat (string.Format ("Mod Union LOS Scan {0,2} (ms)", worker_index), total_los_scan * 1000, CumulationType.MIN_MAX_AVG);

		if (worker_index == 0)
			return stats;

		if (worker_last_scan != default(double))
			stats |= new OutputStat (string.Format ("Major Finish Par {0,2} (ms)", worker_index), (worker_finish - worker_last_scan) * 1000, CumulationType.MIN_MAX_AVG);
		else
			stats |= new OutputStat (string.Format ("Major Finish Par {0,2} (ms)", worker_index), 0, CumulationType.MIN_MAX_AVG);
		return stats;
	}

}

class WorkerStatManager {
	private const int MAX_NUM_WORKERS = 16;
	private WorkerInfo[] worker_infos_conc = new WorkerInfo [MAX_NUM_WORKERS];
	private WorkerInfo[] worker_infos_finish = new WorkerInfo [MAX_NUM_WORKERS];
	private WorkerInfo[] worker_infos_current;

	public WorkerStatManager ()
	{
		worker_infos_current = worker_infos_conc;
	}

	public void HandleEvent (GCEvent gcEvent, double start_timestamp_finish) {
		if (start_timestamp_finish != default(double) &&
				worker_infos_current == worker_infos_conc) {
			worker_infos_current = worker_infos_finish;
		}

		if (worker_infos_current [gcEvent.WorkerIndex] == null) {
			worker_infos_current [gcEvent.WorkerIndex] = new WorkerInfo (gcEvent.WorkerIndex);
			if (gcEvent.Type == GCEventType.WORKER_FINISH_FORCED && worker_infos_conc [gcEvent.WorkerIndex] == null) {
				worker_infos_conc [gcEvent.WorkerIndex] = new WorkerInfo (gcEvent.WorkerIndex);
				worker_infos_conc [gcEvent.WorkerIndex].HandleEvent (gcEvent);
			}
		}

		if (gcEvent.Type == GCEventType.MAJOR_MOD_UNION_SCAN_START ||
				gcEvent.Type == GCEventType.LOS_MOD_UNION_SCAN_START) {
			for (int i = 0; i < MAX_NUM_WORKERS; i++) {
				if (worker_infos_current [i] != null)
					worker_infos_current [i].ReportScan (gcEvent, worker_infos_current == worker_infos_conc);
			}
		}

		worker_infos_current [gcEvent.WorkerIndex].HandleEvent (gcEvent);
	}

	public OutputStatSet AddConcurrentStats (OutputStatSet stats, double worker_start, double start_of_end)
	{
		for (int i = 0; i < MAX_NUM_WORKERS; i++) {
			if (worker_infos_conc [i] != null)
				stats = worker_infos_conc [i].AddConcurrentStats (stats, worker_start, start_of_end);
		}
		return stats;
	}

	public OutputStatSet AddForcedFinishStat (OutputStatSet stats, double start_of_finish)
	{
		for (int i = 0; i < MAX_NUM_WORKERS; i++) {
			if (worker_infos_finish [i] != null)
				stats = worker_infos_finish [i].AddForcedFinishStat (stats, start_of_finish);
		}
		return stats;
	}

	public OutputStatSet AddFinishStats (OutputStatSet stats)
	{
		for (int i = 0; i < MAX_NUM_WORKERS; i++) {
			if (worker_infos_finish [i] != null)
				stats = worker_infos_finish [i].AddFinishStats (stats);
		}
		return stats;
	}
}

public class MajorConcCollection : GCCollection {

	private double end_of_start_timestamp, start_of_end_timestamp;
	private double finish_gray_stack_start, finish_gray_stack_end;
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
		stats = worker_manager.AddConcurrentStats (stats, end_of_start_timestamp, start_of_end_timestamp);
		stats ^= new OutputStat ("Major Pause (ms)", (end_timestamp - start_of_end_timestamp) * 1000, CumulationType.MIN_MAX_AVG);
		stats = worker_manager.AddForcedFinishStat (stats, start_of_end_timestamp);
		stats = worker_manager.AddFinishStats (stats);
		stats |= new OutputStat ("Major Finish GS (ms)", (finish_gray_stack_end - finish_gray_stack_start) * 1000, CumulationType.MIN_MAX_AVG);
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
			case GCEventType.MAJOR_MOD_UNION_SCAN_START:
			case GCEventType.MAJOR_MOD_UNION_SCAN_END:
			case GCEventType.LOS_MOD_UNION_SCAN_START:
			case GCEventType.LOS_MOD_UNION_SCAN_END:
			case GCEventType.WORKER_FINISH_FORCED:
			case GCEventType.WORKER_FINISH:
				if (current != null)
					current.worker_manager.HandleEvent (gcEvent, current.start_of_end_timestamp);
				break;
			case GCEventType.FINISH_GRAY_STACK_START:
				/*
				 * The finish gray stack for the nursery collections will be
				 * overwritten by the one for the finishing pause
				 */
				if (current != null)
					current.finish_gray_stack_start = gcEvent.Timestamp;
				break;
			case GCEventType.FINISH_GRAY_STACK_END:
				if (current != null)
					current.finish_gray_stack_end = gcEvent.Timestamp;
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

