```

BenchmarkDotNet v0.15.8, macOS Sequoia 15.8.1 (24H32) [Darwin 24.6.0]
Unknown processor
.NET SDK 10.0.400
  [Host]      : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a
  Provisional : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a

Job=Provisional  IterationCount=5  IterationTime=100ms  
LaunchCount=1  WarmupCount=2  

```
| Method  | Count | Mean     | Error    | StdDev   | Ratio | RatioSD | Allocated | Alloc Ratio |
|-------- |------ |---------:|---------:|---------:|------:|--------:|----------:|------------:|
| **Objects** | **1024**  | **10.63 ns** | **0.560 ns** | **0.146 ns** |  **1.00** |    **0.02** |         **-** |          **NA** |
| Arena   | 1024  | 33.23 ns | 1.316 ns | 0.342 ns |  3.13 |    0.05 |         - |          NA |
|         |       |          |          |          |       |         |           |             |
| **Objects** | **16384** | **10.46 ns** | **0.307 ns** | **0.080 ns** |  **1.00** |    **0.01** |         **-** |          **NA** |
| Arena   | 16384 | 33.20 ns | 0.550 ns | 0.143 ns |  3.17 |    0.03 |         - |          NA |
