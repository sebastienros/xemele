```

BenchmarkDotNet v0.15.8, macOS Sequoia 15.8.1 (24H32) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
.NET SDK 10.0.400
  [Host]   : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

```
| Method            | Mean     | Error     | StdDev    | Gen0   | Gen1   | Allocated |
|------------------ |---------:|----------:|----------:|-------:|-------:|----------:|
| EffectiveDefaults | 5.003 μs | 0.0912 μs | 0.0050 μs | 1.9608 | 0.0458 |  16.05 KB |
