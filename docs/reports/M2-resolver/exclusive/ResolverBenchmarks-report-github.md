```

BenchmarkDotNet v0.15.8, macOS Sequoia 15.8.1 (24H32) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
.NET SDK 10.0.400
  [Host]   : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

```
| Method                 | ChunkSize | Mean        | Error       | StdDev    | Ratio  | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|----------------------- |---------- |------------:|------------:|----------:|-------:|--------:|-------:|-------:|----------:|------------:|
| **DirectRead**             | **1**         |   **0.3877 ns** |   **0.4757 ns** | **0.0261 ns** |   **1.00** |    **0.08** |      **-** |      **-** |         **-** |          **NA** |
| BoundedRead            | 1         |   4.5115 ns |   0.2362 ns | 0.0129 ns |  11.67 |    0.67 |      - |      - |         - |          NA |
| BoundedAsyncRead       | 1         |  21.3620 ns |   0.5946 ns | 0.0326 ns |  55.26 |    3.17 |      - |      - |         - |          NA |
| SessionOpenReadDispose | 1         | 125.7113 ns |   1.2090 ns | 0.0663 ns | 325.22 |   18.63 | 0.1693 | 0.0005 |    1416 B |          NA |
|                        |           |             |             |           |        |         |        |        |           |             |
| **DirectRead**             | **4096**      |  **33.0506 ns** |   **7.3408 ns** | **0.4024 ns** |   **1.00** |    **0.01** |      **-** |      **-** |         **-** |          **NA** |
| BoundedRead            | 4096      |  44.8278 ns |   8.1298 ns | 0.4456 ns |   1.36 |    0.02 |      - |      - |         - |          NA |
| BoundedAsyncRead       | 4096      |  60.0368 ns |  29.6591 ns | 1.6257 ns |   1.82 |    0.05 |      - |      - |         - |          NA |
| SessionOpenReadDispose | 4096      | 166.2217 ns | 107.4762 ns | 5.8911 ns |   5.03 |    0.16 | 0.1693 | 0.0005 |    1416 B |          NA |
