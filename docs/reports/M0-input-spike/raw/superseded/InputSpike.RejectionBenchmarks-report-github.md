```

BenchmarkDotNet v0.15.8, macOS Sequoia 15.8.1 (24H32) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
.NET SDK 10.0.400
  [Host]         : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a
  M0-Provisional : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a

Job=M0-Provisional  IterationCount=3  IterationTime=100ms  
LaunchCount=1  WarmupCount=2  

```
| Method           | Case      | Mean     | Error      | StdDev    | Gen0   | Allocated |
|----------------- |---------- |---------:|-----------:|----------:|-------:|----------:|
| **ByteCore**         | **Overlong**  | **2.894 μs** |  **0.9363 μs** | **0.0513 μs** | **0.1158** |   **1.13 KB** |
| CharacterScratch | Overlong  | 2.894 μs |  0.6407 μs | 0.0351 μs | 0.1158 |   1.13 KB |
| **ByteCore**         | **Surrogate** | **2.960 μs** |  **0.2162 μs** | **0.0119 μs** | **0.1183** |   **1.13 KB** |
| CharacterScratch | Surrogate | 2.978 μs |  0.2553 μs | 0.0140 μs | 0.1179 |   1.13 KB |
| **ByteCore**         | **Truncated** | **3.769 μs** | **15.3492 μs** | **0.8413 μs** | **0.1217** |   **1.16 KB** |
| CharacterScratch | Truncated | 2.988 μs |  0.3194 μs | 0.0175 μs | 0.1185 |   1.16 KB |
