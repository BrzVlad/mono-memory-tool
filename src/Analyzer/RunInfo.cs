using System;
using System.Collections.Generic;
using System.Linq;
using OxyPlot;
using OxyPlot.Series;

public class RunInfo {
	private const double referenceRunTime = 1.0;
	private List<NurseryCollection> nurseryCollections;
	private List<MajorSyncCollection> majorSyncCollections;
	private List<MajorConcCollection> majorConcCollections;
	private List<GCEvent> gcEvents;
	private List<double> timestamps;
	private List<double> memoryUsage; /* usage for each timestamp entry */

	public double Time {
		get {
			return timestamps [timestamps.Count - 1];
		}
	}

	public RunInfo (List<double> stamps, List<double> mem, List<GCEvent> gcev)
	{
		timestamps = stamps;
		memoryUsage = mem;
		gcEvents = gcev;
		nurseryCollections = NurseryCollection.ParseNurseryCollections (gcEvents);
		majorSyncCollections = MajorSyncCollection.ParseMajorSyncCollections (gcEvents);
		majorConcCollections = MajorConcCollection.ParseMajorConcCollections (gcEvents);
	}

	private static byte lineColor = 0;
	public void Plot (PlotModel plotModel, string name)
	{
		LineSeries lineSeries = new LineSeries ();
		lineSeries.Title = name;
		lineSeries.Color = OxyColor.FromRgb (lineColor, lineColor, lineColor);
		lineColor += 128;

		for (int i = 0; i < timestamps.Count; i++)
			lineSeries.Points.Add (new DataPoint (timestamps [i], memoryUsage [i]));
		plotModel.Series.Add (lineSeries);

		PlotGCCollectionList (plotModel, majorSyncCollections);
		PlotGCCollectionList (plotModel, majorConcCollections);
		PlotGCCollectionList (plotModel, nurseryCollections);
	}

	private void PlotGCCollectionList (PlotModel plotModel, IEnumerable<GCCollection> collectionList)
	{
		foreach (GCCollection collection in collectionList) {
			collection.Plot (plotModel, timestamps, memoryUsage);
		}
	}

	private double ComputeMemoryUsage ()
	{
		double memoryUsageStat = 0.0;

		for (int i = 1; i < timestamps.Count - 1; i++)
			memoryUsageStat += (timestamps [i] - timestamps [i - 1]) * (memoryUsage [i] + memoryUsage [i - 1]) / 2;
		memoryUsageStat = memoryUsageStat * referenceRunTime / Time;

		return memoryUsageStat;
	}

	public OutputStatSet GetStats ()
	{
		OutputStatSet nurseryStat = null;
		OutputStatSet majorStat = null;
		OutputStatSet resultStat = new OutputStatSet ();
		IEnumerable<GCCollection> majorList;

		if (majorSyncCollections.Count > 0) {
			majorList = majorSyncCollections;
			Utils.Assert (majorConcCollections.Count == 0);
		} else if (majorConcCollections.Count > 0) {
			majorList = majorConcCollections;
			Utils.Assert (majorSyncCollections.Count == 0);
		} else {
			majorList = new List<GCCollection> ();
		}

		resultStat |= new OutputStat ("Time (ms)", Time, CumulationType.AVERAGE);
		resultStat |= new OutputStat ("Num Minor", nurseryCollections.Count, CumulationType.AVERAGE);
		resultStat |= new OutputStat ("Num Major", majorList.Count<GCCollection> (), CumulationType.AVERAGE);
		resultStat |= new OutputStat ("Avg Mem Usage (MB)", ComputeMemoryUsage (), CumulationType.AVERAGE);

		resultStat |= OutputStat.EmptyStat;
		foreach (NurseryCollection nurseryCollection in nurseryCollections)
			nurseryStat += nurseryCollection.GetStats ();
		nurseryStat.Normalize ();
		resultStat |= nurseryStat;

		resultStat |= OutputStat.EmptyStat;
		foreach (GCCollection majorCollection in majorList)
			majorStat += majorCollection.GetStats ();
		majorStat.Normalize ();
		resultStat |= majorStat;

		return resultStat;
	}

	public List<OutputStatSet> GetTopMinorStats (int count)
	{
		List<OutputStatSet> stats = nurseryCollections.ConvertAll (new Converter<NurseryCollection,OutputStatSet> (nrs => nrs.GetStats ()));
		stats.Sort ();
		return stats.GetRange (0, Math.Min (count, stats.Count));
	}

	public List<OutputStatSet> GetTopMajorStats (int count)
	{
		List<OutputStatSet> stats = null;
		if (majorSyncCollections.Count > 0)
			stats = majorSyncCollections.ConvertAll (new Converter<MajorSyncCollection,OutputStatSet> (mjr => mjr.GetStats ()));
		else if (majorConcCollections.Count > 0)
			stats = majorConcCollections.ConvertAll (new Converter<MajorConcCollection,OutputStatSet> (mjr => mjr.GetStats ()));
		stats.Sort ();
		return stats.GetRange (0, Math.Min (count, stats.Count));
	}
}
