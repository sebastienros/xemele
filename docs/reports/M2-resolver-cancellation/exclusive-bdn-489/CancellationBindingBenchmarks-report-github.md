```

BenchmarkDotNet v0.15.8, macOS Sequoia 15.8.1 (24H32) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
.NET SDK 10.0.400
  [Host]   : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method                   | Mean      | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------- |----------:|----------:|----------:|------:|--------:|----------:|------------:|
| DirectCancellationChecks | 0.0000 ns | 0.0000 ns | 0.0000 ns |     ? |       ? |         - |           ? |
| BindExactToken           | 0.7157 ns | 0.0508 ns | 0.0028 ns |     ? |       ? |         - |           ? |
| BindAlternatingToken     | 1.8642 ns | 0.3371 ns | 0.0185 ns |     ? |       ? |         - |           ? |
