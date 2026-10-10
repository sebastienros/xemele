```

BenchmarkDotNet v0.15.8, macOS Sequoia 15.8.1 (24H32) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
.NET SDK 10.0.400
  [Host]         : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a
  M0-Provisional : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a

Job=M0-Provisional  IterationCount=3  IterationTime=100ms  
LaunchCount=1  WarmupCount=2  

```
| Method           | Case      | Mean     | Error     | StdDev    | Gen0   | Allocated |
|----------------- |---------- |---------:|----------:|----------:|-------:|----------:|
| **ByteCore**         | **Overlong**  | **2.874 μs** | **0.4566 μs** | **0.0250 μs** | **0.1157** |   **1.13 KB** |
| CharacterScratch | Overlong  | 2.873 μs | 0.2984 μs | 0.0164 μs | 0.1143 |   1.13 KB |
| **ByteCore**         | **Surrogate** | **3.001 μs** | **0.2195 μs** | **0.0120 μs** | **0.1249** |   **1.13 KB** |
| CharacterScratch | Surrogate | 2.992 μs | 0.3848 μs | 0.0211 μs | 0.1184 |   1.13 KB |
| **ByteCore**         | **Truncated** | **3.031 μs** | **0.2971 μs** | **0.0163 μs** | **0.1217** |   **1.16 KB** |
| CharacterScratch | Truncated | 3.009 μs | 0.3988 μs | 0.0219 μs | 0.1192 |   1.16 KB |
