```

BenchmarkDotNet v0.15.8, macOS Sequoia 15.8.1 (24H32) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
.NET SDK 10.0.400
  [Host]         : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a
  M0-Provisional : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a

Job=M0-Provisional  IterationCount=3  IterationTime=100ms  
LaunchCount=1  WarmupCount=2  

```
| Method             | Case        | Mean        | Error         | StdDev     | Allocated |
|------------------- |------------ |------------:|--------------:|-----------:|----------:|
| **ByteCached**         | **Dense**       |   **0.2759 ns** |     **0.3181 ns** |  **0.0174 ns** |         **-** |
| ByteSimple         | Dense       |   1.1384 ns |     1.3403 ns |  0.0735 ns |         - |
| CharCached         | Dense       |   0.9527 ns |     0.0308 ns |  0.0017 ns |         - |
| CharSimple         | Dense       |   1.0292 ns |     2.6209 ns |  0.1437 ns |         - |
| ByteSmallSet       | Dense       |   0.2139 ns |     1.1452 ns |  0.0628 ns |         - |
| CharSmallSet       | Dense       |   0.4682 ns |     0.3229 ns |  0.0177 ns |         - |
| ByteIndexOfControl | Dense       |  16.8975 ns |     1.7879 ns |  0.0980 ns |         - |
| CharIndexOfControl | Dense       |  37.9769 ns |     2.8234 ns |  0.1548 ns |         - |
| **ByteCached**         | **LongEarly**   |   **0.2067 ns** |     **0.1227 ns** |  **0.0067 ns** |         **-** |
| ByteSimple         | LongEarly   |   0.0000 ns |     0.0000 ns |  0.0000 ns |         - |
| CharCached         | LongEarly   |   0.9551 ns |     0.0483 ns |  0.0026 ns |         - |
| CharSimple         | LongEarly   |   0.0000 ns |     0.0000 ns |  0.0000 ns |         - |
| ByteSmallSet       | LongEarly   |   0.2374 ns |     0.6508 ns |  0.0357 ns |         - |
| CharSmallSet       | LongEarly   |   0.4657 ns |     0.1508 ns |  0.0083 ns |         - |
| ByteIndexOfControl | LongEarly   |   0.3270 ns |     0.1081 ns |  0.0059 ns |         - |
| CharIndexOfControl | LongEarly   |   0.4673 ns |     0.0990 ns |  0.0054 ns |         - |
| **ByteCached**         | **LongLate**    |  **22.8583 ns** |     **1.6326 ns** |  **0.0895 ns** |         **-** |
| ByteSimple         | LongLate    | 410.4871 ns |   396.6512 ns | 21.7418 ns |         - |
| CharCached         | LongLate    |  30.7652 ns |     3.4532 ns |  0.1893 ns |         - |
| CharSimple         | LongLate    | 397.4638 ns |    22.1752 ns |  1.2155 ns |         - |
| ByteSmallSet       | LongLate    |  43.5821 ns |    27.8545 ns |  1.5268 ns |         - |
| CharSmallSet       | LongLate    |  96.0324 ns |    11.6860 ns |  0.6405 ns |         - |
| ByteIndexOfControl | LongLate    |  16.9230 ns |     6.0377 ns |  0.3309 ns |         - |
| CharIndexOfControl | LongLate    |  38.4008 ns |     2.2746 ns |  0.1247 ns |         - |
| **ByteCached**         | **LongMiddle**  |  **11.1331 ns** |     **0.1126 ns** |  **0.0062 ns** |         **-** |
| ByteSimple         | LongMiddle  | 246.6381 ns |   216.3718 ns | 11.8601 ns |         - |
| CharCached         | LongMiddle  |  15.4038 ns |     5.0489 ns |  0.2767 ns |         - |
| CharSimple         | LongMiddle  | 216.3098 ns |   368.3438 ns | 20.1902 ns |         - |
| ByteSmallSet       | LongMiddle  |  21.6483 ns |     1.2798 ns |  0.0702 ns |         - |
| CharSmallSet       | LongMiddle  |  43.2201 ns |     1.9444 ns |  0.1066 ns |         - |
| ByteIndexOfControl | LongMiddle  |  17.0179 ns |     6.6422 ns |  0.3641 ns |         - |
| CharIndexOfControl | LongMiddle  |  38.3311 ns |     4.0834 ns |  0.2238 ns |         - |
| **ByteCached**         | **LongMiss**    |  **22.3855 ns** |     **1.8515 ns** |  **0.1015 ns** |         **-** |
| ByteSimple         | LongMiss    | 423.7498 ns |   804.0563 ns | 44.0730 ns |         - |
| CharCached         | LongMiss    |  30.1796 ns |     2.2636 ns |  0.1241 ns |         - |
| CharSimple         | LongMiss    | 399.4943 ns |    30.2192 ns |  1.6564 ns |         - |
| ByteSmallSet       | LongMiss    |  42.2458 ns |     9.3892 ns |  0.5147 ns |         - |
| CharSmallSet       | LongMiss    |  85.4855 ns |     7.9013 ns |  0.4331 ns |         - |
| ByteIndexOfControl | LongMiss    |  16.6250 ns |     3.1168 ns |  0.1708 ns |         - |
| CharIndexOfControl | LongMiss    |  38.3844 ns |     3.6881 ns |  0.2022 ns |         - |
| **ByteCached**         | **ShortEarly**  |   **3.5205 ns** |     **0.7819 ns** |  **0.0429 ns** |         **-** |
| ByteSimple         | ShortEarly  |   0.0000 ns |     0.0000 ns |  0.0000 ns |         - |
| CharCached         | ShortEarly  |   1.2058 ns |     0.1412 ns |  0.0077 ns |         - |
| CharSimple         | ShortEarly  |   0.0000 ns |     0.0000 ns |  0.0000 ns |         - |
| ByteSmallSet       | ShortEarly  |   0.0000 ns |     0.0000 ns |  0.0000 ns |         - |
| CharSmallSet       | ShortEarly  |   0.4644 ns |     0.0978 ns |  0.0054 ns |         - |
| ByteIndexOfControl | ShortEarly  |   0.0000 ns |     0.0000 ns |  0.0000 ns |         - |
| CharIndexOfControl | ShortEarly  |   0.4661 ns |     0.0863 ns |  0.0047 ns |         - |
| **ByteCached**         | **ShortLate**   |   **3.4824 ns** |     **0.7214 ns** |  **0.0395 ns** |         **-** |
| ByteSimple         | ShortLate   |   4.0940 ns |     7.1991 ns |  0.3946 ns |         - |
| CharCached         | ShortLate   |   1.7019 ns |     0.0963 ns |  0.0053 ns |         - |
| CharSimple         | ShortLate   |   4.2416 ns |    10.4103 ns |  0.5706 ns |         - |
| ByteSmallSet       | ShortLate   |   4.1266 ns |     2.4647 ns |  0.1351 ns |         - |
| CharSmallSet       | ShortLate   |   0.7010 ns |     0.0801 ns |  0.0044 ns |         - |
| ByteIndexOfControl | ShortLate   |   1.2274 ns |     0.1611 ns |  0.0088 ns |         - |
| CharIndexOfControl | ShortLate   |   0.4815 ns |     0.2749 ns |  0.0151 ns |         - |
| **ByteCached**         | **ShortMiss**   |   **1.6660 ns** |     **0.2182 ns** |  **0.0120 ns** |         **-** |
| ByteSimple         | ShortMiss   |   4.4203 ns |    12.4249 ns |  0.6811 ns |         - |
| CharCached         | ShortMiss   |   0.9392 ns |     0.1621 ns |  0.0089 ns |         - |
| CharSimple         | ShortMiss   |   4.3786 ns |    11.5861 ns |  0.6351 ns |         - |
| ByteSmallSet       | ShortMiss   |   4.3354 ns |     1.7518 ns |  0.0960 ns |         - |
| CharSmallSet       | ShortMiss   |   0.5156 ns |     0.2281 ns |  0.0125 ns |         - |
| ByteIndexOfControl | ShortMiss   |   1.2378 ns |     0.4771 ns |  0.0262 ns |         - |
| CharIndexOfControl | ShortMiss   |   0.4753 ns |     0.1824 ns |  0.0100 ns |         - |
| **ByteCached**         | **UnicodeLate** |  **34.8143 ns** |     **2.7534 ns** |  **0.1509 ns** |         **-** |
| ByteSimple         | UnicodeLate | 642.5904 ns | 1,471.4811 ns | 80.6568 ns |         - |
| CharCached         | UnicodeLate |  18.8950 ns |     1.6197 ns |  0.0888 ns |         - |
| CharSimple         | UnicodeLate | 252.9905 ns |    25.1413 ns |  1.3781 ns |         - |
| ByteSmallSet       | UnicodeLate |  67.5543 ns |    10.3580 ns |  0.5678 ns |         - |
| CharSmallSet       | UnicodeLate |  55.7941 ns |     5.7897 ns |  0.3174 ns |         - |
| ByteIndexOfControl | UnicodeLate |  32.8353 ns |     1.1497 ns |  0.0630 ns |         - |
| CharIndexOfControl | UnicodeLate |  26.6332 ns |    10.0576 ns |  0.5513 ns |         - |
| **ByteCached**         | **UnicodeMiss** |  **34.4180 ns** |     **3.6266 ns** |  **0.1988 ns** |         **-** |
| ByteSimple         | UnicodeMiss | 659.9068 ns | 1,393.8923 ns | 76.4039 ns |         - |
| CharCached         | UnicodeMiss |  18.2296 ns |     1.3142 ns |  0.0720 ns |         - |
| CharSimple         | UnicodeMiss | 249.2819 ns |    41.9223 ns |  2.2979 ns |         - |
| ByteSmallSet       | UnicodeMiss |  63.9453 ns |     8.7621 ns |  0.4803 ns |         - |
| CharSmallSet       | UnicodeMiss |  52.6497 ns |     9.0180 ns |  0.4943 ns |         - |
| ByteIndexOfControl | UnicodeMiss |  32.9274 ns |    11.5098 ns |  0.6309 ns |         - |
| CharIndexOfControl | UnicodeMiss |  25.5785 ns |     6.1216 ns |  0.3355 ns |         - |
