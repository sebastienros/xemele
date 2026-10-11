```

BenchmarkDotNet v0.15.8, macOS Sequoia 15.8.1 (24H32) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a

Job=InProcess  Toolchain=InProcessEmitToolchain  IterationCount=3  
WarmupCount=3  

```
| Method         | Bytes | Mean         | Error         | StdDev     | Ratio | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|--------------- |------ |-------------:|--------------:|-----------:|------:|--------:|-------:|-------:|----------:|------------:|
| **Sync**           | **32**    |     **7.931 μs** |     **1.2157 μs** |  **0.0666 μs** |  **1.00** |    **0.01** | **8.5144** | **0.6409** |  **69.81 KB** |        **1.00** |
| AsyncImmediate | 32    |     9.549 μs |     0.8276 μs |  0.0454 μs |  1.20 |    0.01 | 8.5144 | 0.6409 |  69.81 KB |        1.00 |
| AsyncSuspended | 32    |    18.169 μs |    12.1565 μs |  0.6663 μs |  2.29 |    0.07 | 9.4604 | 1.3123 |  74.52 KB |        1.07 |
|                |       |              |               |            |       |         |        |        |           |             |
| **Sync**           | **65536** | **2,679.284 μs** | **1,122.0294 μs** | **61.5022 μs** |  **1.00** |    **0.03** | **7.8125** |      **-** |  **69.82 KB** |        **1.00** |
| AsyncImmediate | 65536 | 2,723.494 μs |    35.2371 μs |  1.9315 μs |  1.02 |    0.02 | 7.8125 |      - |  69.81 KB |        1.00 |
| AsyncSuspended | 65536 | 2,917.332 μs |   979.8304 μs | 53.7078 μs |  1.09 |    0.03 | 7.8125 |      - |  85.66 KB |        1.23 |
