```

BenchmarkDotNet v0.15.8, macOS Sequoia 15.8.1 (24H32) [Darwin 24.6.0]
Unknown processor
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a

Toolchain=InProcessEmitToolchain  IterationCount=3  WarmupCount=3  

```
| Method            | Mean      | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------ |----------:|----------:|----------:|------:|--------:|----------:|------------:|
| NoOp              | 0.5347 ns | 1.0037 ns | 0.0550 ns |  1.01 |    0.12 |         - |          NA |
| PumpIdleSelection | 4.2956 ns | 0.5566 ns | 0.0305 ns |  8.09 |    0.69 |         - |          NA |
| CoreIdleSelection | 2.4159 ns | 0.3883 ns | 0.0213 ns |  4.55 |    0.39 |         - |          NA |
