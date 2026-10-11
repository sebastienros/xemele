```

BenchmarkDotNet v0.15.8, macOS Sequoia 15.8.1 (24H32) [Darwin 24.6.0]
Unknown processor
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a

Job=InProcess  Toolchain=InProcessEmitToolchain  IterationCount=3  
IterationTime=100ms  WarmupCount=1  

```
| Method  | Corpus       | Mean         | Error         | StdDev      | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|-------- |------------- |-------------:|--------------:|------------:|------:|--------:|-------:|----------:|------------:|
| **Forward** | **long-ascii**   | **2,001.356 μs** | **3,068.8909 μs** | **168.2163 μs** |  **1.20** |    **0.10** |      **-** |   **1.76 KB** |        **1.01** |
| Dom     | long-ascii   | 1,665.620 μs | 1,631.0140 μs |  89.4014 μs |  1.00 |    0.06 |      - |   1.74 KB |        1.00 |
|         |              |              |               |             |       |         |        |           |             |
| **Forward** | **multilingual** | **3,382.161 μs** |   **456.1964 μs** |  **25.0057 μs** |  **1.08** |    **0.01** |      **-** |      **2 KB** |        **1.03** |
| Dom     | multilingual | 3,121.415 μs |   388.7201 μs |  21.3071 μs |  1.00 |    0.01 |      - |   1.94 KB |        1.00 |
|         |              |              |               |             |       |         |        |           |             |
| **Forward** | **short**        |     **3.737 μs** |     **0.2726 μs** |   **0.0149 μs** |  **0.84** |    **0.00** | **0.1875** |   **1.63 KB** |        **1.05** |
| Dom     | short        |     4.468 μs |     0.1353 μs |   0.0074 μs |  1.00 |    0.00 | 0.1782 |   1.55 KB |        1.00 |
