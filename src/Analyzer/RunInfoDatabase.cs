using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using OxyPlot;
using OxyPlot.Axes;

public class RunInfoDatabase {
	public List<RunInfo> runs1 = new List<RunInfo>();
	public List<RunInfo> runs2 = new List<RunInfo>();
	private bool outliers_removed = false;

	private void RemoveOutliers (List<RunInfo> runs)
	{
		if (runs.Count > 2) {
			RunInfo min = runs [0];
			RunInfo max = runs [0];
			foreach (RunInfo run in runs) {
				if (run.Time < min.Time)
					min = run;
				if (run.Time > max.Time)
					max = run;
			}
			runs.Remove (min);
			runs.Remove (max);
		}
	}

	private void RemoveOutliers ()
	{
		if (outliers_removed)
			return;

		RemoveOutliers (runs1);
		RemoveOutliers (runs2);

		outliers_removed = true;
	}

	public void Plot (string resultsFolder, string name1, string name2)
	{
		RemoveOutliers ();

		string test_name = Path.GetFileName (resultsFolder);
		Directory.CreateDirectory (resultsFolder);
		Utils.AssertEqual<int> (runs1.Count, runs2.Count);
		for (int i = 0; i < runs1.Count; i++) {
			string svgFile = Path.Combine (resultsFolder, test_name + i + ".svg");
			PlotModel plotModel  = new PlotModel { Title = test_name };
			plotModel.Axes.Add (new LinearAxis { Position = AxisPosition.Bottom });
			plotModel.Axes.Add (new LinearAxis { Position = AxisPosition.Left });

			plotModel.LegendBackground = OxyColors.LightGray;
			plotModel.LegendBorder = OxyColors.Black;

			runs1 [i].Plot (plotModel, name1);
			runs2 [i].Plot (plotModel, name2);

			using (FileStream stream = new FileStream (svgFile, FileMode.Create)) {
				SvgExporter.Export (plotModel, stream, 1920, 1080, true);
			}
		}
	}

	private OutputStatSet GetStats (List<RunInfo> runs, string name)
	{
		OutputStatSet resultStat = new OutputStatSet (name);
		foreach (RunInfo runInfo in runs) {
			resultStat += runInfo.GetStats ();
		}
		return resultStat;
	}

	private void OutputOverallStats (string resultsFolder, string name1, string name2)
	{
		OutputStatSet stats1 = GetStats (runs1, name1);
		OutputStatSet stats2 = GetStats (runs2, name2);

		Directory.CreateDirectory (resultsFolder);
		string statsFile = Path.Combine (resultsFolder, "stats-overall");
		using (StreamWriter statsWriter = new StreamWriter (statsFile)) {
			statsWriter.Write (OutputStatSet.ToString (stats1, stats2));
		}
	}

	private void OutputStatListComparison (StreamWriter statsWriter, List<OutputStatSet> s1, List<OutputStatSet> s2)
	{
		for (int i = 0; i < Math.Max (s1.Count, s2.Count); i++) {
			OutputStatSet stat1 = OutputStatSet.EmptyStatSet;
			OutputStatSet stat2 = OutputStatSet.EmptyStatSet;
			if (i < s1.Count)
				stat1 = s1 [i];
			if (i < s2.Count)
				stat2 = s2 [i];
			statsWriter.WriteLine (OutputStatSet.ToString (stat1, stat2));
		}
	}

	private void OutputPerRunStats (string resultsFolder, string noconc, string conc)
	{
		for (int i = 0; i < runs1.Count; i++) {
			string statsFile = Path.Combine (resultsFolder, "majors" + i);
			using (StreamWriter statsWriter = new StreamWriter (statsFile)) {
				statsWriter.WriteLine ("Majors");
				OutputStatListComparison (statsWriter, runs1 [i].GetTopMajorStats (10), runs2 [i].GetTopMajorStats (10));
			}
			statsFile = Path.Combine (resultsFolder, "minors" + i);
			using (StreamWriter statsWriter = new StreamWriter (statsFile)) {
				statsWriter.WriteLine ("Minors");
				OutputStatListComparison (statsWriter, runs1 [i].GetTopMinorStats (10), runs2 [i].GetTopMinorStats (10));
			}
		}
	}

	public void OutputStats (string resultsFolder, string name1, string name2)
	{
		RemoveOutliers ();
		OutputOverallStats (resultsFolder, name1, name2);
		OutputPerRunStats (resultsFolder, name1, name2);
	}
}
