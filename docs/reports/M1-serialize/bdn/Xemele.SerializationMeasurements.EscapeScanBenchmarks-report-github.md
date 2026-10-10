```

BenchmarkDotNet v0.15.8, macOS Sequoia 15.8.1 (24H32) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
.NET SDK 10.0.400
  [Host]   : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method             | Corpus       | Mean          | Error         | StdDev     | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------- |------------- |--------------:|--------------:|-----------:|------:|--------:|----------:|------------:|
| **ScalarLoop**         | **long-hit**     | **3,908.2302 ns** |   **240.0155 ns** | **13.1561 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| CachedSearchValues | long-hit     |   243.9348 ns |     8.9193 ns |  0.4889 ns |  0.06 |    0.00 |         - |          NA |
| RuntimeIndexOfAny  | long-hit     |   324.5013 ns |    63.2063 ns |  3.4645 ns |  0.08 |    0.00 |         - |          NA |
|                    |              |               |               |            |       |         |           |             |
| **ScalarLoop**         | **long-miss**    | **4,023.5286 ns** | **1,262.9673 ns** | **69.2275 ns** |  **1.00** |    **0.02** |         **-** |          **NA** |
| CachedSearchValues | long-miss    |   247.6393 ns |    26.9504 ns |  1.4772 ns |  0.06 |    0.00 |         - |          NA |
| RuntimeIndexOfAny  | long-miss    |   313.0035 ns |    23.1600 ns |  1.2695 ns |  0.08 |    0.00 |         - |          NA |
|                    |              |               |               |            |       |         |           |             |
| **ScalarLoop**         | **multilingual** | **4,060.1850 ns** |   **624.3297 ns** | **34.2216 ns** |  **1.00** |    **0.01** |         **-** |          **NA** |
| CachedSearchValues | multilingual |   249.4649 ns |    15.9072 ns |  0.8719 ns |  0.06 |    0.00 |         - |          NA |
| RuntimeIndexOfAny  | multilingual |   319.3657 ns |    11.4813 ns |  0.6293 ns |  0.08 |    0.00 |         - |          NA |
|                    |              |               |               |            |       |         |           |             |
| **ScalarLoop**         | **short-hit**    |     **0.7591 ns** |     **0.3164 ns** |  **0.0173 ns** |  **1.00** |    **0.03** |         **-** |          **NA** |
| CachedSearchValues | short-hit    |     1.1808 ns |     2.8191 ns |  0.1545 ns |  1.56 |    0.18 |         - |          NA |
| RuntimeIndexOfAny  | short-hit    |     2.2813 ns |     0.5212 ns |  0.0286 ns |  3.01 |    0.07 |         - |          NA |
