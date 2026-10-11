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
| **Synchronous**    | **long-ascii**   |  **1,937.63 μs** | **1,580.19 μs** |  **86.62 μs** |  **1,903.17 μs** |  **1.00** |    **0.05** |   **9.57 KB** |        **1.00** |
| ImmediateAsync | long-ascii   |  2,644.18 μs | 5,016.05 μs | 274.95 μs |  2,494.29 μs |  1.37 |    0.13 |  13.85 KB |        1.45 |
|                |              |              |             |           |              |       |         |           |             |
| **Synchronous**    | **multilingual** |  **5,096.26 μs** | **3,169.51 μs** | **173.73 μs** |  **5,074.75 μs** |  **1.00** |    **0.04** |  **13.79 KB** |        **1.00** |
| ImmediateAsync | multilingual | 26,091.46 μs | 7,751.89 μs | 424.91 μs | 25,952.62 μs |  5.12 |    0.17 |  13.85 KB |        1.00 |
|                |              |              |             |           |              |       |         |           |             |
| **Synchronous**    | **short**        |     **60.50 μs** |   **945.44 μs** |  **51.82 μs** |     **31.37 μs** |  **1.49** |    **1.43** |  **13.79 KB** |        **1.00** |
| ImmediateAsync | short        |    201.96 μs | 4,424.92 μs | 242.54 μs |     67.25 μs |  4.96 |    6.21 |  13.85 KB |        1.00 |
