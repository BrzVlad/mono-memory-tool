using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using OxyPlot;
using OxyPlot.Axes;

public class RunInfoDatabase {
	public List<RunInfo> noconcRuns = new List<RunInfo>();
	public List<RunInfo> concRuns = new List<RunInfo>();

	public void Plot (string resultsFolder, string noconc, string conc)
	{
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

	public void OutputStats (string resultsFolder, string noconc, string conc)
	{
	}
}
