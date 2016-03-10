using System;
using System.Collections.Generic;
using OxyPlot;

public class MajorConcCollection : GCCollection {
	private double end_of_start_timestamp, start_of_end_timestamp;
	private double major_mod_union_scan_start, major_mod_union_scan_end;
	private double los_mod_union_scan_start, los_mod_union_scan_end;
	private double finish_gray_stack_start, finish_gray_stack_end;
	private double pre_major_mod_union_scan_start, pre_major_mod_union_scan_end;
	private double pre_los_mod_union_scan_start, pre_los_mod_union_scan_end;
	private int num_minor;
	private double evacuated_block_sizes;
	private double worker_finish;
	private double worker_finish_forced;

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
		if (pre_major_mod_union_scan_start != default(double)) {
			stats |= new OutputStat ("Conc M&S (ms)", (pre_major_mod_union_scan_start - end_of_start_timestamp) * 1000, CumulationType.MIN_MAX_AVG);
			stats |= new OutputStat ("Major Mod Preclean (ms)", (pre_major_mod_union_scan_end - pre_major_mod_union_scan_start) * 1000, CumulationType.MIN_MAX_AVG);
			stats |= new OutputStat ("LOS Mod Preclean (ms)", (pre_los_mod_union_scan_end - pre_los_mod_union_scan_start) * 1000, CumulationType.MIN_MAX_AVG);
			if (worker_finish_forced != default(double))
				stats |= new OutputStat ("Finish conc M&S (ms)", (worker_finish_forced - pre_los_mod_union_scan_end) * 1000, CumulationType.MIN_MAX_AVG);
			else
				stats |= new OutputStat ("Finish conc M&S (ms)", (worker_finish - pre_los_mod_union_scan_end) * 1000, CumulationType.MIN_MAX_AVG);
		} else {
			if (worker_finish_forced != default(double))
				stats |= new OutputStat ("Conc M&S (ms)", (worker_finish_forced - end_of_start_timestamp) * 1000, CumulationType.MIN_MAX_AVG);
			else
				stats |= new OutputStat ("Conc M&S (ms)", (worker_finish - end_of_start_timestamp) * 1000, CumulationType.MIN_MAX_AVG);
			stats |= new OutputStat ("Major Mod Preclean (ms)", 0, CumulationType.MIN_MAX_AVG);
			stats |= new OutputStat ("LOS Mod Preclean (ms)", 0, CumulationType.MIN_MAX_AVG);
			stats |= new OutputStat ("Finish conc M&S (ms)", 0, CumulationType.MIN_MAX_AVG);
		}
		stats ^= new OutputStat ("Major Pause (ms)", (end_timestamp - start_of_end_timestamp) * 1000, CumulationType.MIN_MAX_AVG);
		if (worker_finish_forced != default(double))
			stats |= new OutputStat ("Forced finish (ms)", (worker_finish - start_of_end_timestamp) * 1000, CumulationType.MIN_MAX_AVG);
		else
			stats |= new OutputStat ("Forced finish (ms)", 0, CumulationType.MIN_MAX_AVG);
		stats |= new OutputStat ("Mod Union Major Scan (ms)", (major_mod_union_scan_end - major_mod_union_scan_start) * 1000, CumulationType.MIN_MAX_AVG);
		stats |= new OutputStat ("Mod Union LOS Scan (ms)", (los_mod_union_scan_end - los_mod_union_scan_start) * 1000, CumulationType.MIN_MAX_AVG);
		stats |= new OutputStat ("Major Finish GS (ms)", (finish_gray_stack_end - finish_gray_stack_start) * 1000, CumulationType.MIN_MAX_AVG);
		return stats | base.GetStats ();
	}

	public static List<MajorConcCollection> ParseMajorConcCollections (List<GCEvent> gcEvents)
	{
		List<MajorConcCollection> majorConcCollections = new List<MajorConcCollection> ();
		MajorConcCollection current = null;
		double last_nursery_end = default(double);

		foreach (GCEvent gcEvent in gcEvents) {
			switch (gcEvent.Type) {
			case GCEventType.MAJOR_REQUEST_FORCE:
				current = null;
				break;
			case GCEventType.NURSERY_END:
				last_nursery_end = gcEvent.Timestamp;
				if (current != null)
					current.num_minor++;
				break;
			case GCEventType.CONCURRENT_START:
				current = new MajorConcCollection ();
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
				if (current != null) {
					if (current.major_mod_union_scan_start != default(double)) {
						current.pre_major_mod_union_scan_start = current.major_mod_union_scan_start;
					}
					current.major_mod_union_scan_start = gcEvent.Timestamp;
				}
				break;
			case GCEventType.MAJOR_MOD_UNION_SCAN_END:
				if (current != null) {
					if (current.major_mod_union_scan_end != default(double)) {
						current.pre_major_mod_union_scan_end = current.major_mod_union_scan_end;
					}
					current.major_mod_union_scan_end = gcEvent.Timestamp;
				}
				break;
			case GCEventType.LOS_MOD_UNION_SCAN_START:
				if (current != null) {
					if (current.los_mod_union_scan_start != default(double)) {
						current.pre_los_mod_union_scan_start = current.los_mod_union_scan_start;
					}
					current.los_mod_union_scan_start = gcEvent.Timestamp;
				}
				break;
			case GCEventType.LOS_MOD_UNION_SCAN_END:
				if (current != null) {
					if (current.los_mod_union_scan_end != default(double)) {
						current.pre_los_mod_union_scan_end = current.los_mod_union_scan_end;
					}
					current.los_mod_union_scan_end = gcEvent.Timestamp;
				}
				break;
			case GCEventType.WORKER_FINISH_FORCED:
				if (current != null)
					current.worker_finish_forced = gcEvent.Timestamp;
				break;
			case GCEventType.WORKER_FINISH:
				if (current != null) {
					current.worker_finish = gcEvent.Timestamp;
				}
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
					Utils.Assert (current.end_timestamp != default(double));
					Utils.Assert (current.end_of_start_timestamp != default(double));
					Utils.Assert (current.start_of_end_timestamp != default(double));
					majorConcCollections.Add (current);
					current = null;
				}
				break;
			default:
				if (current != null)
					current.ParseCustomEvent (gcEvent);
				break;
			}
		}

		Utils.Assert (current == null);
		return majorConcCollections;
	}
}

