```

BenchmarkDotNet v0.15.8, macOS Sequoia 15.8.1 (24H32) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
.NET SDK 10.0.400
  [Host]   : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

```
| Method         | Mean     | Error    | StdDev   | Gen0   | Gen1   | Allocated |
|--------------- |---------:|---------:|---------:|-------:|-------:|----------:|
| ParseLegacyDom | 28.01 μs | 0.515 μs | 0.028 μs | 6.9275 | 1.1292 |  56.89 KB |
