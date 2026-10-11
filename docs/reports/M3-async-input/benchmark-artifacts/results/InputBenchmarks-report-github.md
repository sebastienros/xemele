```

BenchmarkDotNet v0.15.8, macOS Sequoia 15.8.1 (24H32) [Darwin 24.6.0]
Unknown processor
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a

Job=AsyncInputBounded  Toolchain=InProcessNoEmitToolchain  InvocationCount=1024  
IterationCount=3  LaunchCount=1  UnrollFactor=1  
WarmupCount=3  

```
| Method             | Mean          | Error         | StdDev      | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------- |--------------:|--------------:|------------:|------:|--------:|----------:|------------:|
| SyncBufferedText   |   341.5391 ns | 1,231.3565 ns |  67.4948 ns | 1.024 |    0.24 |         - |          NA |
| AsyncBufferedText  |   598.5648 ns | 2,707.9320 ns | 148.4309 ns | 1.794 |    0.48 |       2 B |          NA |
| AsyncSuspendedText | 2,724.9349 ns | 1,049.2466 ns |  57.5127 ns | 8.169 |    1.26 |     320 B |          NA |
| NoOp               |     0.1628 ns |     0.0103 ns |   0.0006 ns | 0.000 |    0.00 |         - |          NA |
