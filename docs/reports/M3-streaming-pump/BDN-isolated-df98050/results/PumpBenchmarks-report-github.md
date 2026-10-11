```

BenchmarkDotNet v0.15.8, macOS Sequoia 15.8.1 (24H32) [Darwin 24.6.0]
Unknown processor
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a

Job=InProcess  Toolchain=InProcessEmitToolchain  IterationCount=3  
WarmupCount=3  

```
| Method             | Mean     | Error     | StdDev  | Gen0   | Gen1   | Allocated |
|------------------- |---------:|----------:|--------:|-------:|-------:|----------:|
| BorrowedBytes      | 185.7 μs |  74.28 μs | 4.07 μs |      - |      - |         - |
| BorrowedCharacters | 145.4 μs | 117.71 μs | 6.45 μs |      - |      - |         - |
| EndToEndBytes      | 284.0 μs |   2.27 μs | 0.12 μs | 5.8594 | 0.4883 |   52673 B |
