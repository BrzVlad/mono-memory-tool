BENCHMARKS_ANALYZER_SOURCES=$(wildcard src/BenchmarksAnalyzer/*.cs)
ANALYZER_SOURCES=$(wildcard src/Analyzer/*.cs)

all: analyzer.exe canalyzer.exe benchmarks-analyzer.exe benchmarks-canalyzer.exe

benchmarks-analyzer.exe: $(BENCHMARKS_ANALYZER_SOURCES)
	mcs -debug /out:$@ /r:Newtonsoft.Json.dll $^

benchmarks-canalyzer.exe: $(BENCHMARKS_ANALYZER_SOURCES)
	mcs -debug /D:CONC_VS_CONC /out:$@ /r:Newtonsoft.Json.dll $^

analyzer.exe: $(ANALYZER_SOURCES)
	mcs -debug /out:$@ /r:OxyPlot.dll $^

canalyzer.exe: $(ANALYZER_SOURCES)
	mcs -debug /D:CONC_VS_CONC /out:$@ /r:OxyPlot.dll $^

clean:
	rm -rf analyzer.exe* benchmarks-analyzer.exe* canalyzer.exe* benchmarks-canalyzer.exe*
