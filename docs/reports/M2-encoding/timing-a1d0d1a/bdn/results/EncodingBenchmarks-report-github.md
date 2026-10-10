```

BenchmarkDotNet v0.15.8, macOS Sequoia 15.8.1 (24H32) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
.NET SDK 10.0.400
  [Host]   : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

```
| Method          | Label        | Length | Mean         | Error        | StdDev     | Ratio | RatioSD | Allocated | Alloc Ratio |
|---------------- |------------- |------- |-------------:|-------------:|-----------:|------:|--------:|----------:|------------:|
| **Straightforward** | **ISO-8859-1**   | **8**      |     **11.73 ns** |     **3.114 ns** |   **0.171 ns** |  **1.00** |    **0.02** |         **-** |          **NA** |
| BoundedDecoder  | ISO-8859-1   | 8      |     77.90 ns |     0.955 ns |   0.052 ns |  6.64 |    0.08 |         - |          NA |
|                 |              |        |              |              |            |       |         |           |             |
| **Straightforward** | **ISO-8859-1**   | **4096**   |  **6,175.25 ns** |   **767.471 ns** |  **42.068 ns** |  **1.00** |    **0.01** |         **-** |          **NA** |
| BoundedDecoder  | ISO-8859-1   | 4096   | 37,822.79 ns | 4,354.415 ns | 238.680 ns |  6.13 |    0.05 |         - |          NA |
|                 |              |        |              |              |            |       |         |           |             |
| **Straightforward** | **US-ASCII**     | **8**      |     **11.84 ns** |     **1.828 ns** |   **0.100 ns** |  **1.00** |    **0.01** |         **-** |          **NA** |
| BoundedDecoder  | US-ASCII     | 8      |     79.56 ns |     2.491 ns |   0.137 ns |  6.72 |    0.05 |         - |          NA |
|                 |              |        |              |              |            |       |         |           |             |
| **Straightforward** | **US-ASCII**     | **4096**   |  **6,222.54 ns** |   **768.490 ns** |  **42.124 ns** |  **1.00** |    **0.01** |         **-** |          **NA** |
| BoundedDecoder  | US-ASCII     | 4096   | 37,484.99 ns | 8,400.646 ns | 460.468 ns |  6.02 |    0.07 |         - |          NA |
|                 |              |        |              |              |            |       |         |           |             |
| **Straightforward** | **Windows-1252** | **8**      |     **11.78 ns** |     **0.782 ns** |   **0.043 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| BoundedDecoder  | Windows-1252 | 8      |     78.59 ns |    18.856 ns |   1.034 ns |  6.67 |    0.08 |         - |          NA |
|                 |              |        |              |              |            |       |         |           |             |
| **Straightforward** | **Windows-1252** | **4096**   |  **6,140.52 ns** | **1,086.799 ns** |  **59.571 ns** |  **1.00** |    **0.01** |         **-** |          **NA** |
| BoundedDecoder  | Windows-1252 | 4096   | 37,297.80 ns | 9,513.710 ns | 521.479 ns |  6.07 |    0.09 |         - |          NA |
