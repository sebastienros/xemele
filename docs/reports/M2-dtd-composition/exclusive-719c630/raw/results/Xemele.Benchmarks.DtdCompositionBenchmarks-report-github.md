```

BenchmarkDotNet v0.15.8, macOS Sequoia 15.8.1 (24H32) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
.NET SDK 10.0.400
  [Host]   : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method                   | CommentScalars | Mean        | Error     | StdDev   | Gen0   | Gen1   | Allocated |
|------------------------- |--------------- |------------:|----------:|---------:|-------:|-------:|----------:|
| **SeedAndFramedComposition** | **32**             |  **1,424.9 ns** |  **32.13 ns** |  **1.76 ns** | **0.8068** | **0.0095** |    **6.6 KB** |
| SeedAndModelSnapshot     | 32             |    292.6 ns |  37.42 ns |  2.05 ns | 0.4549 | 0.0033 |   3.72 KB |
| **SeedAndFramedComposition** | **4096**           | **19,158.8 ns** | **759.25 ns** | **41.62 ns** | **0.7935** |      **-** |    **6.6 KB** |
| SeedAndModelSnapshot     | 4096           |    304.7 ns |  85.79 ns |  4.70 ns | 0.4549 | 0.0033 |   3.72 KB |
