```

BenchmarkDotNet v0.15.8, macOS Sequoia 15.8.1 (24H32) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
.NET SDK 10.0.400
  [Host]   : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

```
| Method             | Mean        | Error        | StdDev    | Ratio | RatioSD | Gen0    | Gen1    | Allocated | Alloc Ratio |
|------------------- |------------:|-------------:|----------:|------:|--------:|--------:|--------:|----------:|------------:|
| Indexed            |  2,629.1 ns |     47.92 ns |   2.63 ns |  1.00 |    0.00 |       - |       - |         - |          NA |
| Linked             |    175.3 ns |     15.04 ns |   0.82 ns |  0.07 |    0.00 |       - |       - |         - |          NA |
| ConstructOwnedTree | 10,293.2 ns |    319.46 ns |  17.51 ns |  3.92 |    0.01 |  6.2714 |  0.5188 |   52560 B |          NA |
| CloneOwnedDocument |  9,596.5 ns |    235.05 ns |  12.88 ns |  3.65 |    0.01 |  6.3019 |  0.5188 |   52784 B |          NA |
| ImportOwnedTree    |  9,642.6 ns |    638.39 ns |  34.99 ns |  3.67 |    0.01 |  6.3171 |  0.6256 |   52928 B |          NA |
| ImportMemberHeavy  | 81,833.3 ns | 13,329.61 ns | 730.64 ns | 31.13 |    0.24 | 51.8799 | 19.2871 |  435240 B |          NA |
