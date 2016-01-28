using System.Collections.Generic;
using OxyPlot;
using OxyPlot.Series;

public class RunInfo {
	private List<NurseryCollection> nurseryCollections;
	private List<MajorSyncCollection> majorSyncCollections;
	private List<MajorConcCollection> majorConcCollections;
	private List<GCEvent> gcEvents;
	private List<double> timestamps;
	private List<double> memoryUsage; /* usage for each timestamp entry */

	private static byte lineColor = 0;

	public double referenceTime;

	public RunInfo (List<double> stamps, List<double> mem, List<GCEvent> gcev)
	{
		timestamps = stamps;
		memoryUsage = mem;
		gcEvents = gcev;
		nurseryCollections = NurseryCollection.ParseNurseryCollections (gcEvents);
		majorSyncCollections = MajorSyncCollection.ParseMajorSyncCollections (gcEvents);
		majorConcCollections = MajorConcCollection.ParseMajorConcCollections (gcEvents);
	}

	public void Plot (PlotModel plotModel, string name)
	{
		LineSeries lineSeries = new LineSeries ();
		lineSeries.Title = name;
		lineSeries.Color = OxyColor.FromRgb (lineColor, lineColor, lineColor);
		lineColor += 128;

		for (int i = 0; i < timestamps.Count; i++)
			lineSeries.Points.Add (new DataPoint (timestamps [i], memoryUsage [i]));
		plotModel.Series.Add (lineSeries);

		PlotGCCollectionList<MajorSyncCollection> (plotModel, majorSyncCollections);
		PlotGCCollectionList<MajorConcCollection> (plotModel, majorConcCollections);
		PlotGCCollectionList<NurseryCollection> (plotModel, nurseryCollections);
	}

	private void PlotGCCollectionList<T> (PlotModel plotModel, List<T> collectionList) where T : GCCollection
	{
		foreach (T collection in collectionList) {
			collection.Plot (plotModel, timestamps, memoryUsage);
		}
	}
}
