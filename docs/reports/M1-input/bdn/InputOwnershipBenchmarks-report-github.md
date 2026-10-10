```

BenchmarkDotNet v0.15.8, macOS Sequoia 15.8.1 (24H32) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
.NET SDK 10.0.400
  [Host]   : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

```
| Method              | Mean     | Error    | StdDev   | Gen0   | Gen1   | Allocated |
|-------------------- |---------:|---------:|---------:|-------:|-------:|----------:|
| Setup               | 46.81 ns | 2.658 ns | 0.146 ns | 0.0975 | 0.0002 |     816 B |
| MalformedDiagnostic | 66.60 ns | 2.729 ns | 0.150 ns | 0.1061 | 0.0002 |     888 B |
