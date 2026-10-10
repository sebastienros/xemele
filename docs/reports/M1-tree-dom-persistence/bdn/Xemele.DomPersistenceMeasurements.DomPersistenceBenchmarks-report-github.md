```

BenchmarkDotNet v0.15.8, macOS Sequoia 15.8.1 (24H32) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
.NET SDK 10.0.400
  [Host]   : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method                     | Length | Shape  | Mean        | Error        | StdDev     | Ratio | RatioSD | Gen0    | Gen1   | Allocated | Alloc Ratio |
|--------------------------- |------- |------- |------------:|-------------:|-----------:|------:|--------:|--------:|-------:|----------:|------------:|
| **CheckedPersistence**         | **16**     | **names**  |    **27.24 μs** |     **5.564 μs** |   **0.305 μs** |  **1.00** |    **0.01** | **18.3716** | **3.6621** | **150.52 KB** |        **1.00** |
| SyntaxValidatedPersistence | 16     | names  |    18.14 μs |     3.704 μs |   0.203 μs |  0.67 |    0.01 | 18.3716 | 3.6621 | 150.52 KB |        1.00 |
|                            |        |        |             |              |            |       |         |         |        |           |             |
| **CheckedPersistence**         | **16**     | **values** |    **22.49 μs** |     **1.787 μs** |   **0.098 μs** |  **1.00** |    **0.01** | **18.3716** | **3.4790** | **150.46 KB** |        **1.00** |
| SyntaxValidatedPersistence | 16     | values |    16.93 μs |     1.532 μs |   0.084 μs |  0.75 |    0.00 | 18.3716 | 3.4790 | 150.46 KB |        1.00 |
|                            |        |        |             |              |            |       |         |         |        |           |             |
| **CheckedPersistence**         | **16384**  | **names**  | **8,781.56 μs** | **4,586.704 μs** | **251.413 μs** |  **1.00** |    **0.03** | **15.6250** |      **-** | **214.45 KB** |        **1.00** |
| SyntaxValidatedPersistence | 16384  | names  | 2,868.41 μs | 1,166.918 μs |  63.963 μs |  0.33 |    0.01 | 23.4375 | 3.9063 | 214.45 KB |        1.00 |
|                            |        |        |             |              |            |       |         |         |        |           |             |
| **CheckedPersistence**         | **16384**  | **values** | **3,108.51 μs** | **1,980.668 μs** | **108.567 μs** | **1.001** |    **0.04** | **15.6250** |      **-** | **150.46 KB** |        **1.00** |
| SyntaxValidatedPersistence | 16384  | values |    16.59 μs |     9.196 μs |   0.504 μs | 0.005 |    0.00 | 18.3716 | 3.4790 | 150.46 KB |        1.00 |
