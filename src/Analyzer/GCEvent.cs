using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;

public enum GCEventType {
	SUSPEND_START,
	SUSPEND_END,
	RESUME_START,
	RESUME_END,
	NURSERY_START,
	NURSERY_END,
	MAJOR_START,
	MAJOR_END,
	CONCURRENT_START,
	CONCURRENT_FINISH,
	MAJOR_REQUEST_FORCE,
	MINOR_WORKER_FINISH_STATS,
	MAJOR_WORKER_FINISH_STATS,
	MAJOR_WORKER_FINISH_FORCED_STATS,
	COLLECTION_END_STATS,
	EVACUATING_BLOCKS,
	FINISH_GRAY_STACK_START,
	FINISH_GRAY_STACK_END,
	CONCURRENT_SWEEP_END,
	CUSTOM_EVENT_START,
	CUSTOM_EVENT_END,
}

public class GCEvent {
	private enum GCEventTimestampType {
		BEFORE,
		INCLUDED,
		AFTER
	}

	private class GCEventTypeMatcher {
		private static Regex timestampRegex = new Regex (@"timestamp (\d+)");
		private static Regex workerRegex = new Regex (@" w(\d+)");
		private static GCEventTypeMatcher[] matchers = {
			new GCEventTypeMatcher () { type = GCEventType.SUSPEND_START, timestampType = GCEventTimestampType.INCLUDED, match = new Regex (@"world_stopping generation \d+ timestamp \d+") },
			new GCEventTypeMatcher () { type = GCEventType.SUSPEND_END, timestampType = GCEventTimestampType.INCLUDED, match = new Regex (@"world_stopped generation \d+ timestamp \d+") },
			new GCEventTypeMatcher () { type = GCEventType.RESUME_START, timestampType = GCEventTimestampType.INCLUDED, match = new Regex (@"world_restarting generation \d+ timestamp \d+") },
			new GCEventTypeMatcher () { type = GCEventType.RESUME_END, timestampType = GCEventTimestampType.INCLUDED, match = new Regex (@"world_restarted generation \d+ timestamp \d+") },
			new GCEventTypeMatcher () { type = GCEventType.NURSERY_START, timestampType = GCEventTimestampType.BEFORE, match = new Regex (@"collection_begin index \d+ generation 0") },
			new GCEventTypeMatcher () { type = GCEventType.NURSERY_END, timestampType = GCEventTimestampType.AFTER, match = new Regex (@"collection_end \d+ generation 0") },
			new GCEventTypeMatcher () { type = GCEventType.MAJOR_START, timestampType = GCEventTimestampType.BEFORE, match = new Regex (@"collection_begin index \d+ generation 1") },
			new GCEventTypeMatcher () { type = GCEventType.MAJOR_END, timestampType = GCEventTimestampType.AFTER, match = new Regex (@"collection_end \d+ generation 1") },
			new GCEventTypeMatcher () { type = GCEventType.CONCURRENT_START, timestampType = GCEventTimestampType.BEFORE, match = new Regex ("concurrent_start") },
			new GCEventTypeMatcher () { type = GCEventType.CONCURRENT_FINISH, timestampType = GCEventTimestampType.BEFORE, match = new Regex ("concurrent_finish") },
			new GCEventTypeMatcher () { type = GCEventType.MAJOR_REQUEST_FORCE, timestampType = GCEventTimestampType.AFTER, match = new Regex (@"collection_requested generation 1 requested_size \d+ force true") },
			new GCEventTypeMatcher () { type = GCEventType.MINOR_WORKER_FINISH_STATS, timestampType = GCEventTimestampType.AFTER, match = new Regex (@"worker_finish_stats worker_index (\d+) generation 0 forced false major_scan (\d+) los_scan (\d+) work_time (\d+)") },
			new GCEventTypeMatcher () { type = GCEventType.MAJOR_WORKER_FINISH_STATS, timestampType = GCEventTimestampType.AFTER, match = new Regex (@"worker_finish_stats worker_index (\d+) generation 1 forced false major_scan (\d+) los_scan (\d+) work_time (\d+)") },
			new GCEventTypeMatcher () { type = GCEventType.MAJOR_WORKER_FINISH_FORCED_STATS, timestampType = GCEventTimestampType.AFTER, match = new Regex (@"worker_finish_stats worker_index (\d+) generation 1 forced true major_scan (\d+) los_scan (\d+) work_time (\d+)") },
			new GCEventTypeMatcher () { type = GCEventType.COLLECTION_END_STATS, timestampType = GCEventTimestampType.AFTER, match = new Regex (@"collection_end_stats major_scan (\d+) los_scan (\d+) finish_stack (\d+)") },
			new GCEventTypeMatcher () { type = GCEventType.EVACUATING_BLOCKS, timestampType = GCEventTimestampType.BEFORE, match = new Regex (@"evacuating_blocks block_size \d+") },
			new GCEventTypeMatcher () { type = GCEventType.FINISH_GRAY_STACK_START, timestampType = GCEventTimestampType.INCLUDED, match = new Regex (@"finish_gray_stack_start timestamp \d+ generation \d+") },
			new GCEventTypeMatcher () { type = GCEventType.FINISH_GRAY_STACK_END, timestampType = GCEventTimestampType.INCLUDED, match = new Regex (@"finish_gray_stack_end timestamp \d+ generation \d+") },
			new GCEventTypeMatcher () { type = GCEventType.CONCURRENT_SWEEP_END, timestampType = GCEventTimestampType.INCLUDED, match = new Regex (@"concurrent_sweep_end timestamp \d+") },
			new GCEventTypeMatcher () { type = GCEventType.CUSTOM_EVENT_START, timestampType = GCEventTimestampType.INCLUDED, match = new Regex (@"custom_event_start timestamp \d+") },
			new GCEventTypeMatcher () { type = GCEventType.CUSTOM_EVENT_END, timestampType = GCEventTimestampType.INCLUDED, match = new Regex (@"custom_event_end timestamp \d+") },
			};

		public GCEventType type;
		public GCEventTimestampType timestampType;
		private Regex match;

		public static GCEventTypeMatcher Match (string line, out string[] groups)
		{
			groups = null;
			foreach (GCEventTypeMatcher matcher in matchers) {
				if (matcher.match.IsMatch (line)) {
					Match m = matcher.match.Match (line);
					if (m.Groups.Count > 1)
						groups = m.Groups.Cast<Group> ().Skip (1).Select<Group,string> (g => g.Value).ToArray<string> ();
					return matcher;
				}
			}

			return null;
		}

		public static double MatchTimestamp (string line)
		{
			if (timestampRegex.IsMatch (line)) {
				Match m = timestampRegex.Match (line);
				return ((double)long.Parse (m.Groups [1].Value)) / 10000000;
			}

			return default(double);
		}

		public static int MatchWorkerIndex (string line)
		{
			if (workerRegex.IsMatch (line)) {
				Match m = workerRegex.Match (line);
				return int.Parse (m.Groups [1].Value);
			}
			return 0;
		}
	}

	public double Timestamp { get; private set; }
	public GCEventType Type { get; private set; }
	public int WorkerIndex { get; private set; }
	public string[] Values { get; private set; }

	private static GCEvent Parse (string line, Stack<GCEvent> noTimestamp, ref double timestamp)
	{
		double stamp = GCEventTypeMatcher.MatchTimestamp (line);
		int worker_index = GCEventTypeMatcher.MatchWorkerIndex (line);
		string[] groups;
		GCEventTypeMatcher eventTypeMatch = GCEventTypeMatcher.Match (line, out groups);
		GCEvent gcEvent = null;

		if (stamp != default(double) && noTimestamp.Count > 0) {
			while (noTimestamp.Count > 0) {
				noTimestamp.Pop ().Timestamp = stamp;
			}
		}

		if (eventTypeMatch != null) {
			gcEvent = new GCEvent ();
			gcEvent.Type = eventTypeMatch.type;
			gcEvent.WorkerIndex = worker_index;
			gcEvent.Values = groups;
			switch (eventTypeMatch.timestampType) {
				case GCEventTimestampType.BEFORE:
					Utils.Assert (timestamp != default(double));
					gcEvent.Timestamp = timestamp;
					break;
				case GCEventTimestampType.INCLUDED:
					Utils.Assert (stamp != default(double));
					gcEvent.Timestamp = stamp;
					break;
				case GCEventTimestampType.AFTER:
					noTimestamp.Push (gcEvent);
					break;
			}
		}

		if (stamp != default(double))
			timestamp = stamp;

		return gcEvent;
	}

	public static List<GCEvent> ParseEvents (string binprotoutput)
	{
		List<GCEvent> list = new List<GCEvent> ();
		Stack<GCEvent> noTimestamp = new Stack<GCEvent> ();
		StringReader reader = new StringReader (binprotoutput);
		string line;
		double timestamp = default(double);

		while ((line = reader.ReadLine ()) != null) {
			GCEvent parsed = GCEvent.Parse (line, noTimestamp, ref timestamp);
			if (parsed != null)
				list.Add (parsed);
		}

		return list;
	}
}
