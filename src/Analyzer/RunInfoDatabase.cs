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

	public void OutputStats (string resultsFolder, string noconc, string conc)
	{
		RemoveOutliers ();

		OutputStatSet noconcStats = GetStats (noconcRuns, noconc);
		OutputStatSet concStats = GetStats (concRuns, conc);

		string statsFile = Path.Combine (resultsFolder, "stats");
		using (StreamWriter statsWriter = new StreamWriter (statsFile)) {
			statsWriter.Write (OutputStatSet.ToString (noconcStats, concStats));
		}
	}
}
