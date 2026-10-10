```

BenchmarkDotNet v0.15.8, macOS Sequoia 15.8.1 (24H32) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
.NET SDK 10.0.400
  [Host]   : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

```
| Method                      | Mean       | Error     | StdDev    | Ratio | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|---------------------------- |-----------:|----------:|----------:|------:|--------:|-------:|-------:|----------:|------------:|
| StraightforwardCounter      |  0.5540 ns | 1.7580 ns | 0.0964 ns |  1.02 |    0.23 |      - |      - |         - |          NA |
| BoundedCounter              |  0.9931 ns | 0.1644 ns | 0.0090 ns |  1.83 |    0.31 |      - |      - |         - |          NA |
| OperationSetup              | 38.7991 ns | 6.6580 ns | 0.3649 ns | 71.64 |   12.01 | 0.0812 | 0.0001 |     680 B |          NA |
| OwnedExceptionSnapshot      | 13.8417 ns | 0.5573 ns | 0.0305 ns | 25.56 |    4.28 | 0.0249 |      - |     208 B |          NA |
| BoundedDiagnosticTruncation | 34.9343 ns | 3.0245 ns | 0.1658 ns | 64.50 |   10.80 | 0.0258 |      - |     216 B |          NA |
