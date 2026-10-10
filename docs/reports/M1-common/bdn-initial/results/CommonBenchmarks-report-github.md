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
| StraightforwardCounter      |  0.0000 ns | 0.0000 ns | 0.0000 ns |     ? |       ? |      - |      - |         - |           ? |
| BoundedCounter              |  2.9080 ns | 0.2064 ns | 0.0113 ns |     ? |       ? |      - |      - |         - |           ? |
| OperationSetup              | 39.7108 ns | 3.3494 ns | 0.1836 ns |     ? |       ? | 0.0812 | 0.0001 |     680 B |           ? |
| OwnedExceptionSnapshot      | 13.9728 ns | 2.4670 ns | 0.1352 ns |     ? |       ? | 0.0249 |      - |     208 B |           ? |
| BoundedDiagnosticTruncation | 34.7676 ns | 1.9426 ns | 0.1065 ns |     ? |       ? | 0.0258 |      - |     216 B |           ? |
