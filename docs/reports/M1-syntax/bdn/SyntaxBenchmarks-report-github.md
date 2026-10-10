```

BenchmarkDotNet v0.15.8, macOS Sequoia 15.8.1 (24H32) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
.NET SDK 10.0.400
  [Host]   : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

```
| Method                    | Mean       | Error     | StdDev   | Gen0   | Gen1   | Allocated |
|-------------------------- |-----------:|----------:|---------:|-------:|-------:|----------:|
| WarmRepeatedTagAndUnicode | 1,012.4 ns | 165.74 ns |  9.08 ns |      - |      - |         - |
| ColdDocument              | 1,327.6 ns | 256.01 ns | 14.03 ns | 0.8659 | 0.0210 |    7248 B |
| StructuredRejection       |   472.8 ns |  22.90 ns |  1.26 ns | 0.7992 | 0.0052 |    6688 B |
