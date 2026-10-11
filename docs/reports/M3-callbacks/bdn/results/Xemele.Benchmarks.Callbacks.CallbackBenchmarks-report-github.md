```

BenchmarkDotNet v0.15.8, macOS Sequoia 15.8.1 (24H32) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a

Job=ShortRun  Toolchain=InProcessEmitToolchain  InvocationCount=65536  
IterationCount=5  LaunchCount=1  UnrollFactor=1  
WarmupCount=5  

```
| Method    | Mean      | Error      | StdDev     | Median    | Ratio | RatioSD | Allocated | Alloc Ratio |
|---------- |----------:|-----------:|-----------:|----------:|------:|--------:|----------:|------------:|
| Subclass  | 194.01 ns |   5.798 ns |   1.506 ns | 194.52 ns |  1.00 |    0.01 |         - |          NA |
| Delegates | 132.22 ns | 495.171 ns | 128.594 ns |  39.23 ns |  0.68 |    0.61 |         - |          NA |
| Optional  |  28.01 ns |   0.894 ns |   0.138 ns |  28.01 ns |  0.14 |    0.00 |         - |          NA |
