all: analyzer.exe benchmarks-analyzer.exe

benchmarks-analyzer.exe: benchmarks-analyzer.cs benchmark.cs
	mcs -debug /r:Newtonsoft.Json.dll benchmarks-analyzer.cs benchmark.cs

analyzer.exe: analyzer.cs
	mcs -debug /r:OxyPlot.dll analyzer.cs

clean:
	rm -rf analyzer.exe benchmarks-analyzer.exe
