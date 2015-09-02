all: analyzer.exe benchmarks-analyzer.exe

benchmarks-analyzer.exe: benchmarks-analyzer.cs benchmark.cs
	mcs /r:Newtonsoft.Json.dll benchmarks-analyzer.cs benchmark.cs

analyzer.exe: analyzer.cs
	mcs /r:OxyPlot.dll analyzer.cs

clean:
	rm -rf analyzer.exe benchmarks-analyzer.exe
