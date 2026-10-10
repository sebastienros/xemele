```

BenchmarkDotNet v0.15.8, macOS Sequoia 15.8.1 (24H32) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
.NET SDK 10.0.400
  [Host]   : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

```
| Method                    | Corpus       | Mean          | Error        | StdDev      | Ratio | RatioSD | Allocated | Alloc Ratio |
|-------------------------- |------------- |--------------:|-------------:|------------:|------:|--------:|----------:|------------:|
| **StraightforwardStrictUtf8** | **long**         |  **7,220.297 ns** | **1,377.126 ns** |  **75.4849 ns** |  **1.00** |    **0.01** |         **-** |          **NA** |
| BoundedDecoder            | long         | 34,943.817 ns | 3,125.201 ns | 171.3028 ns |  4.84 |    0.05 |         - |          NA |
|                           |              |               |              |             |       |         |           |             |
| **StraightforwardStrictUtf8** | **multilingual** |  **1,530.262 ns** |   **144.569 ns** |   **7.9243 ns** |  **1.00** |    **0.01** |         **-** |          **NA** |
| BoundedDecoder            | multilingual |  6,764.661 ns | 1,561.689 ns |  85.6014 ns |  4.42 |    0.05 |         - |          NA |
|                           |              |               |              |             |       |         |           |             |
| **StraightforwardStrictUtf8** | **short**        |      **8.969 ns** |     **4.236 ns** |   **0.2322 ns** |  **1.00** |    **0.03** |         **-** |          **NA** |
| BoundedDecoder            | short        |     57.233 ns |    30.860 ns |   1.6915 ns |  6.38 |    0.22 |         - |          NA |
