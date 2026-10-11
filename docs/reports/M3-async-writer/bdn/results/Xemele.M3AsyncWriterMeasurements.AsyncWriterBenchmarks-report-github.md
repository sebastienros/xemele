```

BenchmarkDotNet v0.15.8, macOS Sequoia 15.8.1 (24H32) [Darwin 24.6.0]
Unknown processor
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a

Job=Dry  Toolchain=InProcessEmitToolchain  InvocationCount=1  
IterationCount=3  LaunchCount=1  RunStrategy=ColdStart  
UnrollFactor=1  WarmupCount=1  

```
| Method         | Corpus       | Mean         | Error       | StdDev    | Median       | Ratio | RatioSD | Allocated | Alloc Ratio |
|--------------- |------------- |-------------:|------------:|----------:|-------------:|------:|--------:|----------:|------------:|
| **Synchronous**    | **long-ascii**   |  **2,460.40 μs** | **4,611.75 μs** | **252.79 μs** |  **2,579.00 μs** |  **1.01** |    **0.13** |   **9.67 KB** |        **1.00** |
| ImmediateAsync | long-ascii   |  3,666.25 μs | 4,991.53 μs | 273.60 μs |  3,816.08 μs |  1.50 |    0.17 |  13.63 KB |        1.41 |
|                |              |              |             |           |              |       |         |           |             |
| **Synchronous**    | **multilingual** |  **5,996.65 μs** |   **913.71 μs** |  **50.08 μs** |  **6,012.42 μs** |  **1.00** |    **0.01** |  **11.27 KB** |        **1.00** |
| ImmediateAsync | multilingual | 27,045.62 μs | 5,170.59 μs | 283.42 μs | 27,066.62 μs |  4.51 |    0.05 |  13.35 KB |        1.19 |
|                |              |              |             |           |              |       |         |           |             |
| **Synchronous**    | **short**        |     **53.65 μs** |   **819.45 μs** |  **44.92 μs** |     **29.92 μs** |  **1.47** |    **1.38** |  **13.56 KB** |        **1.00** |
| ImmediateAsync | short        |    177.13 μs | 3,856.72 μs | 211.40 μs |     61.29 μs |  4.84 |    6.03 |  13.63 KB |        1.01 |
