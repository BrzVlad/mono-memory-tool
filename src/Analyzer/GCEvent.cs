using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public enum GCEventType {
	NURSERY_START,
	NURSERY_END,
	MAJOR_START,
	MAJOR_END,
	CONCURRENT_START,
	CONCURRENT_FINISH,
	MAJOR_REQUEST_FORCE,
}

public class GCEvent {
	private enum GCEventTimestampType {
		BEFORE,
		INCLUDED,
		AFTER
	}

	private class GCEventTypeMatcher {
		private static Regex timestampRegex = new Regex (@"timestamp (\d+)");
		private static GCEventTypeMatcher[] matchers = {
			new GCEventTypeMatcher () { type = GCEventType.NURSERY_START, timestampType = GCEventTimestampType.BEFORE, match = new Regex (@"collection_begin index \d+ generation 0") },
			new GCEventTypeMatcher () { type = GCEventType.NURSERY_END, timestampType = GCEventTimestampType.AFTER, match = new Regex (@"collection_end \d+ generation 0") },
			new GCEventTypeMatcher () { type = GCEventType.MAJOR_START, timestampType = GCEventTimestampType.BEFORE, match = new Regex (@"collection_begin index \d+ generation 1") },
			new GCEventTypeMatcher () { type = GCEventType.MAJOR_END, timestampType = GCEventTimestampType.AFTER, match = new Regex (@"collection_end \d+ generation 1") },
			new GCEventTypeMatcher () { type = GCEventType.CONCURRENT_START, timestampType = GCEventTimestampType.BEFORE, match = new Regex ("concurrent_start") },
			new GCEventTypeMatcher () { type = GCEventType.CONCURRENT_FINISH, timestampType = GCEventTimestampType.BEFORE, match = new Regex ("concurrent_finish") },
			new GCEventTypeMatcher () { type = GCEventType.MAJOR_REQUEST_FORCE, timestampType = GCEventTimestampType.AFTER, match = new Regex (@"collection_requested generation 1 requested_size \d+ force true") },
			};

		public GCEventType type;
		public GCEventTimestampType timestampType;
		private Regex match;

		public static GCEventTypeMatcher Match (string line)
		{
			foreach (GCEventTypeMatcher matcher in matchers) {
				if (matcher.match.IsMatch (line))
					return matcher;
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
	}

	public double Timestamp { get; private set; }
	public GCEventType Type { get; private set; }

	private static GCEvent Parse (string line, Stack<GCEvent> noTimestamp, ref double timestamp)
	{
		double stamp = GCEventTypeMatcher.MatchTimestamp (line);
		GCEventTypeMatcher eventTypeMatch = GCEventTypeMatcher.Match (line);
		GCEvent gcEvent = null;

		if (stamp != default(double) && noTimestamp.Count > 0) {
			while (noTimestamp.Count > 0) {
				noTimestamp.Pop ().Timestamp = stamp;
			}
		}

		if (eventTypeMatch != null) {
			gcEvent = new GCEvent ();
			gcEvent.Type = eventTypeMatch.type;
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
