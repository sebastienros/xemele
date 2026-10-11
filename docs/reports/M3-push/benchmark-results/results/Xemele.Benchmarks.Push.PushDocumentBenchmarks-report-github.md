```

BenchmarkDotNet v0.15.8, macOS Sequoia 15.8.1 (24H32) [Darwin 24.6.0]
Unknown processor
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a

Job=InProcess  Toolchain=InProcessEmitToolchain  InvocationCount=64  
IterationCount=3  UnrollFactor=1  WarmupCount=1  

```
| Method   | Mean     | Error   | StdDev  | Allocated |
|--------- |---------:|--------:|--------:|----------:|
| EndToEnd | 297.9 μs | 9.81 μs | 0.54 μs |   55.6 KB |
