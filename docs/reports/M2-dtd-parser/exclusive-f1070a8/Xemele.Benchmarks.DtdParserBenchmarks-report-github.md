```

BenchmarkDotNet v0.15.8, macOS Sequoia 15.8.1 (24H32) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
.NET SDK 10.0.400
  [Host]   : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

```
| Method         | Declarations | Mean          | Error        | StdDev       | Gen0    | Gen1   | Allocated |
|--------------- |------------- |--------------:|-------------:|-------------:|--------:|-------:|----------:|
| **BorrowedScan**   | **1**            |      **56.11 ns** |     **26.90 ns** |     **1.475 ns** |       **-** |      **-** |         **-** |
| OwnedGrammar   | 1            |   2,405.18 ns |     92.37 ns |     5.063 ns |  1.0567 | 0.0153 |    8840 B |
| ChunkedGrammar | 1            |   2,605.13 ns |     83.19 ns |     4.560 ns |  1.0567 | 0.0153 |    8840 B |
| **BorrowedScan**   | **64**           |      **57.61 ns** |     **13.90 ns** |     **0.762 ns** |       **-** |      **-** |         **-** |
| OwnedGrammar   | 64           |  92,743.93 ns |  2,472.75 ns |   135.540 ns | 19.7754 | 3.4180 |  166221 B |
| ChunkedGrammar | 64           | 105,072.62 ns | 21,113.03 ns | 1,157.276 ns | 19.7754 | 3.4180 |  166221 B |
