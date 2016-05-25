using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using OxyPlot;
using OxyPlot.Axes;

public class RunInfoDatabase {
	public List<RunInfo> noconcRuns = new List<RunInfo>();
	public List<RunInfo> concRuns = new List<RunInfo>();
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

		RemoveOutliers (noconcRuns);
		RemoveOutliers (concRuns);

		outliers_removed = true;
	}

	public void Plot (string resultsFolder, string noconc, string conc)
	{
		RemoveOutliers ();

		string test_name = Path.GetFileName (resultsFolder);
		Directory.CreateDirectory (resultsFolder);
		Utils.AssertEqual<int> (noconcRuns.Count, concRuns.Count);
		for (int i = 0; i < noconcRuns.Count; i++) {
			string svgFile = Path.Combine (resultsFolder, test_name + i + ".svg");
			PlotModel plotModel  = new PlotModel { Title = test_name };
			plotModel.Axes.Add (new LinearAxis { Position = AxisPosition.Bottom });
			plotModel.Axes.Add (new LinearAxis { Position = AxisPosition.Left });

			plotModel.LegendBackground = OxyColors.LightGray;
			plotModel.LegendBorder = OxyColors.Black;

			noconcRuns [i].Plot (plotModel, noconc);
			concRuns [i].Plot (plotModel, conc);

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

	private void OutputOverallStats (string resultsFolder, string noconc, string conc)
	{
		OutputStatSet noconcStats = GetStats (noconcRuns, noconc);
		OutputStatSet concStats = GetStats (concRuns, conc);

		Directory.CreateDirectory (resultsFolder);
		string statsFile = Path.Combine (resultsFolder, "stats-overall");
		using (StreamWriter statsWriter = new StreamWriter (statsFile)) {
			statsWriter.Write (OutputStatSet.ToString (noconcStats, concStats));
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
		for (int i = 0; i < noconcRuns.Count; i++) {
			string statsFile = Path.Combine (resultsFolder, "majors" + i);
			using (StreamWriter statsWriter = new StreamWriter (statsFile)) {
				statsWriter.WriteLine ("Majors");
				OutputStatListComparison (statsWriter, noconcRuns [i].GetTopMajorStats (10), concRuns [i].GetTopMajorStats (10));
			}
			statsFile = Path.Combine (resultsFolder, "minors" + i);
			using (StreamWriter statsWriter = new StreamWriter (statsFile)) {
				statsWriter.WriteLine ("Minors");
				OutputStatListComparison (statsWriter, noconcRuns [i].GetTopMinorStats (10), concRuns [i].GetTopMinorStats (10));
			}
		}
	}

	public void OutputStats (string resultsFolder, string noconc, string conc)
	{
		RemoveOutliers ();
		OutputOverallStats (resultsFolder, noconc, conc);
		OutputPerRunStats (resultsFolder, noconc, conc);
	}
}
