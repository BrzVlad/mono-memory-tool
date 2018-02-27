BENCHMARKS_ANALYZER_SOURCES=$(wildcard src/BenchmarksAnalyzer/*.cs)
ANALYZER_SOURCES=$(wildcard src/Analyzer/*.cs)
PARSER_SOURCES=$(wildcard src/Parser/*cs)
AGGREGATED_PLOTTER_SOURCES=$(wildcard src/AggregatedPlotter/*cs)

all: analyzer.exe benchmarks-analyzer.exe parser.exe aggregated-plotter.exe

benchmarks-analyzer.exe: $(BENCHMARKS_ANALYZER_SOURCES)
	mcs -debug /out:$@ /r:Newtonsoft.Json.dll $^

analyzer.exe: $(ANALYZER_SOURCES)
	mcs -debug /out:$@ /r:OxyPlot.dll $^

parser.exe : $(PARSER_SOURCES)
	mcs -debug /out:$@ /r:OxyPlot.dll $^

aggregated-plotter.exe: $(AGGREGATED_PLOTTER_SOURCES)
	mcs -debug /out:$@ $^

clean:
	rm -rf analyzer.exe* benchmarks-analyzer.exe* parser.exe*
