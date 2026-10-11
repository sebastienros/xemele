```

BenchmarkDotNet v0.15.8, macOS Sequoia 15.8.1 (24H32) [Darwin 24.6.0]
Unknown processor
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a

Job=InProcess  Toolchain=InProcessEmitToolchain  IterationCount=3  
IterationTime=100ms  WarmupCount=1  

```
| Method  | Corpus       | Mean          | Error         | StdDev     | Ratio | RatioSD | Allocated | Alloc Ratio |
|-------- |------------- |--------------:|--------------:|-----------:|------:|--------:|----------:|------------:|
| **Scalar**  | **dense**        |     **0.0000 ns** |     **0.0000 ns** |  **0.0000 ns** |     **?** |       **?** |         **-** |           **?** |
| Cached  | dense        |     0.2591 ns |     0.2358 ns |  0.0129 ns |     ? |       ? |         - |           ? |
| Runtime | dense        |     0.2110 ns |     0.0815 ns |  0.0045 ns |     ? |       ? |         - |           ? |
|         |              |               |               |            |       |         |           |             |
| **Scalar**  | **long-hit**     | **5,849.3225 ns** |   **514.2784 ns** | **28.1893 ns** |  **1.00** |    **0.01** |         **-** |          **NA** |
| Cached  | long-hit     |   315.7425 ns |    14.1534 ns |  0.7758 ns |  0.05 |    0.00 |         - |          NA |
| Runtime | long-hit     |   690.9610 ns |    77.8383 ns |  4.2666 ns |  0.12 |    0.00 |         - |          NA |
|         |              |               |               |            |       |         |           |             |
| **Scalar**  | **long-miss**    | **5,906.2068 ns** | **1,131.0228 ns** | **61.9952 ns** |  **1.00** |    **0.01** |         **-** |          **NA** |
| Cached  | long-miss    |   317.3988 ns |    49.8475 ns |  2.7323 ns |  0.05 |    0.00 |         - |          NA |
| Runtime | long-miss    |   684.6754 ns |    34.1277 ns |  1.8707 ns |  0.12 |    0.00 |         - |          NA |
|         |              |               |               |            |       |         |           |             |
| **Scalar**  | **multilingual** | **5,970.6479 ns** |   **873.1167 ns** | **47.8585 ns** |  **1.00** |    **0.01** |         **-** |          **NA** |
| Cached  | multilingual |   318.6395 ns |    48.5584 ns |  2.6617 ns |  0.05 |    0.00 |         - |          NA |
| Runtime | multilingual |   692.8532 ns |    18.0916 ns |  0.9917 ns |  0.12 |    0.00 |         - |          NA |
|         |              |               |               |            |       |         |           |             |
| **Scalar**  | **short-hit**    |     **0.9835 ns** |     **0.0683 ns** |  **0.0037 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| Cached  | short-hit    |     1.6433 ns |     3.9972 ns |  0.2191 ns |  1.67 |    0.19 |         - |          NA |
| Runtime | short-hit    |     1.2054 ns |     0.2233 ns |  0.0122 ns |  1.23 |    0.01 |         - |          NA |
|         |              |               |               |            |       |         |           |             |
| **Scalar**  | **short-miss**   |     **3.8321 ns** |     **1.3107 ns** |  **0.0718 ns** |  **1.00** |    **0.02** |         **-** |          **NA** |
| Cached  | short-miss   |     3.0452 ns |     0.7435 ns |  0.0408 ns |  0.79 |    0.02 |         - |          NA |
| Runtime | short-miss   |     2.0549 ns |     1.6127 ns |  0.0884 ns |  0.54 |    0.02 |         - |          NA |
