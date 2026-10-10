```

BenchmarkDotNet v0.15.8, macOS Sequoia 15.8.1 (24H32) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
.NET SDK 10.0.400
  [Host]   : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a

Job=ShortRun  InvocationCount=8192  IterationCount=3
LaunchCount=1  UnrollFactor=1  WarmupCount=3

```
| Method            | Length | CrLf  | Mean         | Error       | StdDev     | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------ |------- |------ |-------------:|------------:|-----------:|------:|--------:|----------:|------------:|
| **ScalarBaseline**    | **8**      | **False** |     **92.39 ns** |    **66.43 ns** |   **3.641 ns** |  **1.00** |    **0.05** |         **-** |          **NA** |
| IncrementalFramer | 8      | False |    337.44 ns |    22.41 ns |   1.228 ns |  3.66 |    0.12 |         - |          NA |
|                   |        |       |              |             |            |       |         |           |             |
| **ScalarBaseline**    | **8**      | **True**  |     **92.38 ns** |    **25.03 ns** |   **1.372 ns** |  **1.00** |    **0.02** |         **-** |          **NA** |
| IncrementalFramer | 8      | True  |    324.28 ns |   354.93 ns |  19.455 ns |  3.51 |    0.19 |         - |          NA |
|                   |        |       |              |             |            |       |         |           |             |
| **ScalarBaseline**    | **4096**   | **False** |  **4,917.76 ns** | **1,196.88 ns** |  **65.605 ns** |  **1.00** |    **0.02** |         **-** |          **NA** |
| IncrementalFramer | 4096   | False | 18,264.87 ns | 9,905.38 ns | 542.948 ns |  3.71 |    0.10 |         - |          NA |
|                   |        |       |              |             |            |       |         |           |             |
| **ScalarBaseline**    | **4096**   | **True**  |  **5,110.49 ns** | **2,788.12 ns** | **152.826 ns** |  **1.00** |    **0.04** |         **-** |          **NA** |
| IncrementalFramer | 4096   | True  | 14,046.14 ns | 1,790.84 ns |  98.162 ns |  2.75 |    0.07 |         - |          NA |
