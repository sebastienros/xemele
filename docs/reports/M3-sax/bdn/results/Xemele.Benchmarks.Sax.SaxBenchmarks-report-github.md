```

BenchmarkDotNet v0.15.8, macOS Sequoia 15.8.1 (24H32) [Darwin 24.6.0]
Unknown processor
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a

Job=Dry  Toolchain=InProcessEmitToolchain  IterationCount=1  
LaunchCount=1  RunStrategy=ColdStart  UnrollFactor=1  
WarmupCount=1  

```
| Method    | Characters | Mean       | Error | Ratio | Allocated | Alloc Ratio |
|---------- |----------- |-----------:|------:|------:|----------:|------------:|
| **StringSax** | **32**         | **9,364.3 μs** |    **NA** |  **1.00** |   **50.2 KB** |        **1.00** |
| BytesSax  | 32         | 4,040.7 μs |    NA |  0.43 |  57.83 KB |        1.15 |
| StreamSax | 32         |   292.4 μs |    NA |  0.03 |   58.5 KB |        1.17 |
| StringDom | 32         | 1,704.5 μs |    NA |  0.18 |     15 KB |        0.30 |
|           |            |            |       |       |           |             |
| **StringSax** | **16384**      | **5,497.9 μs** |    **NA** |  **1.00** |  **53.76 KB** |        **1.00** |
| BytesSax  | 16384      | 2,534.2 μs |    NA |  0.46 |  58.44 KB |        1.09 |
| StreamSax | 16384      | 2,451.7 μs |    NA |  0.45 |   58.5 KB |        1.09 |
| StringDom | 16384      | 1,995.2 μs |    NA |  0.36 | 133.02 KB |        2.47 |
