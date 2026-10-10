```

BenchmarkDotNet v0.15.8, macOS Sequoia 15.8.1 (24H32) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
.NET SDK 10.0.400
  [Host]   : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

```
| Method             | Length | Mean      | Error     | StdDev   | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------- |------- |----------:|----------:|---------:|------:|--------:|----------:|------------:|
| **Baseline**           | **8**      | **140.78 ns** | **10.218 ns** | **0.560 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| BorrowedDictionary | 8      |  18.62 ns |  1.038 ns | 0.057 ns |  0.13 |    0.00 |         - |          NA |
|                    |        |           |           |          |       |         |           |             |
| **Baseline**           | **80**     | **143.48 ns** | **53.299 ns** | **2.921 ns** |  **1.00** |    **0.02** |         **-** |          **NA** |
| BorrowedDictionary | 80     |  50.68 ns |  2.353 ns | 0.129 ns |  0.35 |    0.01 |         - |          NA |
