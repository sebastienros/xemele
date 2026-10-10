```

BenchmarkDotNet v0.15.8, macOS Sequoia 15.8.1 (24H32) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
.NET SDK 10.0.400
  [Host]   : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method              | Mean      | Error    | StdDev   | Gen0   | Allocated |
|-------------------- |----------:|---------:|---------:|-------:|----------:|
| BorrowedDeclaration |  96.17 ns | 5.120 ns | 0.281 ns |      - |         - |
| OwnedAttribute      | 218.52 ns | 4.338 ns | 0.238 ns | 0.1099 |     920 B |
