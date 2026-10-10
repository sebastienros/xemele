```

BenchmarkDotNet v0.15.8, macOS Sequoia 15.8.1 (24H32) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
.NET SDK 10.0.400
  [Host]   : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

```
| Method   | Corpus       | Mean          | Error       | StdDev     | Median        | Ratio | RatioSD | Allocated | Alloc Ratio |
|--------- |------------- |--------------:|------------:|-----------:|--------------:|------:|--------:|----------:|------------:|
| **Loop**     | **dense**        |     **0.0057 ns** |   **0.1789 ns** |  **0.0098 ns** |     **0.0000 ns** |     **?** |       **?** |         **-** |           **?** |
| SmallSet | dense        |     0.5219 ns |   0.0540 ns |  0.0030 ns |     0.5219 ns |     ? |       ? |         - |           ? |
| Cached   | dense        |     0.9918 ns |   0.2251 ns |  0.0123 ns |     0.9901 ns |     ? |       ? |         - |           ? |
|          |              |               |             |            |               |       |         |           |             |
| **Loop**     | **long-hit**     | **1,665.0009 ns** | **399.8467 ns** | **21.9169 ns** | **1,662.3882 ns** |  **1.00** |    **0.02** |         **-** |          **NA** |
| SmallSet | long-hit     |   348.3692 ns |  71.6905 ns |  3.9296 ns |   347.5463 ns |  0.21 |    0.00 |         - |          NA |
| Cached   | long-hit     |   138.8726 ns |   3.9699 ns |  0.2176 ns |   138.9252 ns |  0.08 |    0.00 |         - |          NA |
|          |              |               |             |            |               |       |         |           |             |
| **Loop**     | **long-miss**    | **1,554.5419 ns** | **746.8637 ns** | **40.9381 ns** | **1,555.4146 ns** |  **1.00** |    **0.03** |         **-** |          **NA** |
| SmallSet | long-miss    |   334.1267 ns |   7.1242 ns |  0.3905 ns |   334.1418 ns |  0.22 |    0.00 |         - |          NA |
| Cached   | long-miss    |   139.2247 ns |  20.6940 ns |  1.1343 ns |   139.7296 ns |  0.09 |    0.00 |         - |          NA |
|          |              |               |             |            |               |       |         |           |             |
| **Loop**     | **multilingual** |     **5.8627 ns** |   **2.4809 ns** |  **0.1360 ns** |     **5.9369 ns** |  **1.00** |    **0.03** |         **-** |          **NA** |
| SmallSet | multilingual |     0.9987 ns |   0.2932 ns |  0.0161 ns |     0.9903 ns |  0.17 |    0.00 |         - |          NA |
| Cached   | multilingual |     1.2419 ns |   0.0185 ns |  0.0010 ns |     1.2424 ns |  0.21 |    0.00 |         - |          NA |
|          |              |               |             |            |               |       |         |           |             |
| **Loop**     | **short-hit**    |     **0.9546 ns** |   **1.3713 ns** |  **0.0752 ns** |     **0.9342 ns** |  **1.00** |    **0.10** |         **-** |          **NA** |
| SmallSet | short-hit    |     0.8336 ns |   0.9247 ns |  0.0507 ns |     0.8524 ns |  0.88 |    0.07 |         - |          NA |
| Cached   | short-hit    |     1.2768 ns |   0.2438 ns |  0.0134 ns |     1.2775 ns |  1.34 |    0.09 |         - |          NA |
|          |              |               |             |            |               |       |         |           |             |
| **Loop**     | **short-miss**   |     **2.1937 ns** |   **2.1863 ns** |  **0.1198 ns** |     **2.1596 ns** |  **1.00** |    **0.07** |         **-** |          **NA** |
| SmallSet | short-miss   |     2.8962 ns |   2.5236 ns |  0.1383 ns |     2.8994 ns |  1.32 |    0.08 |         - |          NA |
| Cached   | short-miss   |     2.4984 ns |   0.1695 ns |  0.0093 ns |     2.5012 ns |  1.14 |    0.05 |         - |          NA |
