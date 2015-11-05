all: analyzer.exe canalyzer.exe benchmarks-analyzer.exe benchmarks-canalyzer.exe

benchmarks-analyzer.exe: benchmarks-analyzer.cs benchmark.cs
	mcs -debug /r:Newtonsoft.Json.dll benchmarks-analyzer.cs benchmark.cs

benchmarks-canalyzer.exe: benchmarks-analyzer.cs benchmark.cs
	mcs -debug /D:CONC_VS_CONC /out:benchmarks-canalyzer.exe /r:Newtonsoft.Json.dll benchmarks-analyzer.cs benchmark.cs

analyzer.exe: analyzer.cs
	mcs -debug /r:OxyPlot.dll analyzer.cs

canalyzer.exe: analyzer.cs
	mcs -debug /D:CONC_VS_CONC /out:canalyzer.exe /r:OxyPlot.dll analyzer.cs

clean:
	rm -rf analyzer.exe benchmarks-analyzer.exe canalyzer.exe benchmarks-canalyzer.exe
