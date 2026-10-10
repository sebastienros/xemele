```

BenchmarkDotNet v0.15.8, macOS Sequoia 15.8.1 (24H32) [Darwin 24.6.0]
Unknown processor
.NET SDK 10.0.400
  [Host]      : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a
  Provisional : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a

Job=Provisional  IterationCount=5  IterationTime=100ms  
LaunchCount=1  WarmupCount=2  

```
| Method       | Length | Mean        | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------- |------- |------------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| **Owned**        | **32**     |   **5.3628 ns** |  **0.2392 ns** | **0.0621 ns** | **1.000** |    **0.02** | **0.0105** |      **88 B** |        **1.00** |
| RetainedView | 32     |   0.0000 ns |  0.0000 ns | 0.0000 ns | 0.000 |    0.00 |      - |         - |        0.00 |
|              |        |             |            |           |       |         |        |           |             |
| **Owned**        | **8192**   | **527.0214 ns** | **32.5250 ns** | **5.0333 ns** | **1.000** |    **0.01** | **1.9553** |   **16408 B** |        **1.00** |
| RetainedView | 8192   |   0.0000 ns |  0.0000 ns | 0.0000 ns | 0.000 |    0.00 |      - |         - |        0.00 |
