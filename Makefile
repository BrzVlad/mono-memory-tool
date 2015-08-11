all : analyzer.cs
	mcs /r:OxyPlot.dll analyzer.cs

clean :
	rm -rf analyzer.exe 
