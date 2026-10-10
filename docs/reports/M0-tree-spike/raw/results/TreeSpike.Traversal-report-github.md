```

BenchmarkDotNet v0.15.8, macOS Sequoia 15.8.1 (24H32) [Darwin 24.6.0]
Unknown processor
.NET SDK 10.0.400
  [Host]      : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a
  Provisional : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a

Job=Provisional  IterationCount=5  IterationTime=100ms  
LaunchCount=1  WarmupCount=2  

```
| Method  | Count | Mean      | Error     | StdDev    | Ratio | Allocated | Alloc Ratio |
|-------- |------ |----------:|----------:|----------:|------:|----------:|------------:|
| **Objects** | **1024**  |  **1.234 μs** | **0.0132 μs** | **0.0020 μs** |  **1.00** |         **-** |          **NA** |
| Arena   | 1024  |  3.242 μs | 0.0938 μs | 0.0145 μs |  2.63 |         - |          NA |
|         |       |           |           |           |       |           |             |
| **Objects** | **16384** | **24.889 μs** | **0.3467 μs** | **0.0900 μs** |  **1.00** |         **-** |          **NA** |
| Arena   | 16384 | 52.058 μs | 1.7886 μs | 0.2768 μs |  2.09 |         - |          NA |
