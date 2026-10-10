```

BenchmarkDotNet v0.15.8, macOS Sequoia 15.8.1 (24H32) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
.NET SDK 10.0.400
  [Host]   : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method            | Corpus       | Mean         | Error        | StdDev      | Gen0    | Gen1    | Gen2    | Allocated |
|------------------ |------------- |-------------:|-------------:|------------:|--------:|--------:|--------:|----------:|
| **OwnedString**       | **long**         | **111,930.3 ns** | **34,721.54 ns** | **1,903.20 ns** | **41.6260** | **41.6260** | **41.6260** | **271.16 KB** |
| IncrementalStream | long         | 105,945.9 ns | 52,928.72 ns | 2,901.20 ns |  0.1221 |       - |       - |   1.36 KB |
| **OwnedString**       | **multilingual** |  **83,679.7 ns** |  **6,214.39 ns** |   **340.63 ns** | **43.4570** | **43.4570** | **43.4570** | **279.55 KB** |
| IncrementalStream | multilingual | 129,143.4 ns | 15,869.09 ns |   869.84 ns |       - |       - |       - |   1.36 KB |
| **OwnedString**       | **namespaces**   |  **12,243.5 ns** |  **1,905.26 ns** |   **104.43 ns** |  **5.2948** |  **0.5035** |       **-** |  **43.38 KB** |
| IncrementalStream | namespaces   |  15,872.9 ns |  1,952.63 ns |   107.03 ns |  3.7537 |  0.2136 |       - |   30.8 KB |
| **OwnedString**       | **short**        |     **213.8 ns** |     **24.52 ns** |     **1.34 ns** |  **0.1817** |  **0.0007** |       **-** |   **1.48 KB** |
| IncrementalStream | short        |     270.1 ns |      6.55 ns |     0.36 ns |  0.1664 |  0.0005 |       - |   1.36 KB |
