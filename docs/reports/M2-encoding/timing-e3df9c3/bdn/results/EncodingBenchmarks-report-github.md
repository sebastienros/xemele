```

BenchmarkDotNet v0.15.8, macOS Sequoia 15.8.1 (24H32) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

```
| Method          | Label      | Length | Mean | Error | Ratio | RatioSD | Alloc Ratio |
|---------------- |----------- |------- |-----:|------:|------:|--------:|------------:|
| Straightforward | ISO-8859-1 | 8      |   NA |    NA |     ? |       ? |           ? |

Benchmarks with issues:
  EncodingBenchmarks.Straightforward: ShortRun(IterationCount=3, LaunchCount=1, WarmupCount=3) [Label=ISO-8859-1, Length=8]
