```

BenchmarkDotNet v0.15.8, macOS Sequoia 15.8.1 (24H32) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
.NET SDK 10.0.400
  [Host]         : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a
  M0-Provisional : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a

Job=M0-Provisional  IterationCount=3  IterationTime=100ms  
LaunchCount=1  WarmupCount=2  

```
| Method                      | Case         | Chunk | Mean         | Error        | StdDev     | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|---------------------------- |------------- |------ |-------------:|-------------:|-----------:|------:|--------:|-------:|----------:|------------:|
| **StringToCharacterCore**       | **AsciiLong**    | **7**     |  **4,725.99 ns** | **2,271.052 ns** | **124.484 ns** |  **1.00** |    **0.03** |      **-** |         **-** |          **NA** |
| ByteToByteCore              | AsciiLong    | 7     |  9,272.82 ns | 1,976.977 ns | 108.365 ns |  1.96 |    0.05 |      - |         - |          NA |
| ByteToCharacterScratch      | AsciiLong    | 7     |  6,818.58 ns | 1,365.642 ns |  74.855 ns |  1.44 |    0.04 |      - |         - |          NA |
| StringToCharacterCorePaused | AsciiLong    | 7     |  4,485.91 ns | 5,844.070 ns | 320.333 ns |  0.95 |    0.06 |      - |         - |          NA |
| ByteToByteCorePaused        | AsciiLong    | 7     |  8,102.38 ns | 1,457.292 ns |  79.879 ns |  1.72 |    0.04 |      - |         - |          NA |
| ByteToOwnedString           | AsciiLong    | 7     |  5,245.30 ns |   962.018 ns |  52.731 ns |  1.11 |    0.03 | 0.8532 |    7448 B |          NA |
| StringViaOwnedUtf8          | AsciiLong    | 7     |  9,641.56 ns | 3,731.722 ns | 204.548 ns |  2.04 |    0.06 | 0.3754 |    3736 B |          NA |
|                             |              |       |              |              |            |       |         |        |           |             |
| **StringToCharacterCore**       | **AsciiLong**    | **256**   |  **3,770.35 ns** |   **341.909 ns** |  **18.741 ns** |  **1.00** |    **0.01** |      **-** |         **-** |          **NA** |
| ByteToByteCore              | AsciiLong    | 256   |  5,021.42 ns |   693.119 ns |  37.992 ns |  1.33 |    0.01 |      - |         - |          NA |
| ByteToCharacterScratch      | AsciiLong    | 256   |  3,974.84 ns |   358.328 ns |  19.641 ns |  1.05 |    0.01 |      - |         - |          NA |
| StringToCharacterCorePaused | AsciiLong    | 256   |  3,905.18 ns |   377.990 ns |  20.719 ns |  1.04 |    0.01 |      - |         - |          NA |
| ByteToByteCorePaused        | AsciiLong    | 256   |  4,634.87 ns |   374.479 ns |  20.526 ns |  1.23 |    0.01 |      - |         - |          NA |
| ByteToOwnedString           | AsciiLong    | 256   |  4,207.51 ns |   258.768 ns |  14.184 ns |  1.12 |    0.01 | 0.8618 |    7448 B |          NA |
| StringViaOwnedUtf8          | AsciiLong    | 256   |  5,194.22 ns |   416.115 ns |  22.809 ns |  1.38 |    0.01 | 0.4119 |    3736 B |          NA |
|                             |              |       |              |              |            |       |         |        |           |             |
| **StringToCharacterCore**       | **AsciiShort**   | **7**     |     **33.32 ns** |     **1.710 ns** |   **0.094 ns** |  **1.00** |    **0.00** |      **-** |         **-** |          **NA** |
| ByteToByteCore              | AsciiShort   | 7     |     77.97 ns |     7.934 ns |   0.435 ns |  2.34 |    0.01 |      - |         - |          NA |
| ByteToCharacterScratch      | AsciiShort   | 7     |     60.72 ns |     5.140 ns |   0.282 ns |  1.82 |    0.01 |      - |         - |          NA |
| StringToCharacterCorePaused | AsciiShort   | 7     |     30.53 ns |     4.654 ns |   0.255 ns |  0.92 |    0.01 |      - |         - |          NA |
| ByteToByteCorePaused        | AsciiShort   | 7     |     67.19 ns |     3.768 ns |   0.207 ns |  2.02 |    0.01 |      - |         - |          NA |
| ByteToOwnedString           | AsciiShort   | 7     |     48.02 ns |     4.398 ns |   0.241 ns |  1.44 |    0.01 | 0.0094 |      80 B |          NA |
| StringViaOwnedUtf8          | AsciiShort   | 7     |     86.28 ns |     4.616 ns |   0.253 ns |  2.59 |    0.01 | 0.0062 |      56 B |          NA |
|                             |              |       |              |              |            |       |         |        |           |             |
| **StringToCharacterCore**       | **AsciiShort**   | **256**   |     **19.84 ns** |     **2.654 ns** |   **0.146 ns** |  **1.00** |    **0.01** |      **-** |         **-** |          **NA** |
| ByteToByteCore              | AsciiShort   | 256   |     38.82 ns |     9.975 ns |   0.547 ns |  1.96 |    0.03 |      - |         - |          NA |
| ByteToCharacterScratch      | AsciiShort   | 256   |     32.64 ns |     7.874 ns |   0.432 ns |  1.64 |    0.02 |      - |         - |          NA |
| StringToCharacterCorePaused | AsciiShort   | 256   |     20.79 ns |     1.503 ns |   0.082 ns |  1.05 |    0.01 |      - |         - |          NA |
| ByteToByteCorePaused        | AsciiShort   | 256   |     39.10 ns |     7.651 ns |   0.419 ns |  1.97 |    0.02 |      - |         - |          NA |
| ByteToOwnedString           | AsciiShort   | 256   |     30.81 ns |    14.600 ns |   0.800 ns |  1.55 |    0.04 | 0.0095 |      80 B |          NA |
| StringViaOwnedUtf8          | AsciiShort   | 256   |     54.76 ns |     6.422 ns |   0.352 ns |  2.76 |    0.02 | 0.0062 |      56 B |          NA |
|                             |              |       |              |              |            |       |         |        |           |             |
| **StringToCharacterCore**       | **UnicodeLong**  | **7**     |  **4,768.23 ns** | **2,229.651 ns** | **122.215 ns** |  **1.00** |    **0.03** |      **-** |         **-** |          **NA** |
| ByteToByteCore              | UnicodeLong  | 7     | 22,580.98 ns | 3,575.197 ns | 195.969 ns |  4.74 |    0.11 |      - |         - |          NA |
| ByteToCharacterScratch      | UnicodeLong  | 7     | 17,738.71 ns | 3,554.109 ns | 194.813 ns |  3.72 |    0.09 |      - |         - |          NA |
| StringToCharacterCorePaused | UnicodeLong  | 7     |  4,584.00 ns | 2,390.524 ns | 131.033 ns |  0.96 |    0.03 |      - |         - |          NA |
| ByteToByteCorePaused        | UnicodeLong  | 7     | 22,702.25 ns | 6,561.138 ns | 359.638 ns |  4.76 |    0.12 |      - |         - |          NA |
| ByteToOwnedString           | UnicodeLong  | 7     |  8,454.17 ns |   424.283 ns |  23.256 ns |  1.77 |    0.04 | 0.8469 |    7704 B |          NA |
| StringViaOwnedUtf8          | UnicodeLong  | 7     | 24,749.85 ns | 2,527.915 ns | 138.564 ns |  5.19 |    0.12 | 0.4921 |    6168 B |          NA |
|                             |              |       |              |              |            |       |         |        |           |             |
| **StringToCharacterCore**       | **UnicodeLong**  | **256**   |  **3,996.73 ns** | **1,191.447 ns** |  **65.307 ns** |  **1.00** |    **0.02** |      **-** |         **-** |          **NA** |
| ByteToByteCore              | UnicodeLong  | 256   |  6,319.38 ns | 1,233.462 ns |  67.610 ns |  1.58 |    0.03 |      - |         - |          NA |
| ByteToCharacterScratch      | UnicodeLong  | 256   |  5,537.45 ns | 2,733.211 ns | 149.816 ns |  1.39 |    0.04 |      - |         - |          NA |
| StringToCharacterCorePaused | UnicodeLong  | 256   |  3,918.76 ns | 1,224.362 ns |  67.111 ns |  0.98 |    0.02 |      - |         - |          NA |
| ByteToByteCorePaused        | UnicodeLong  | 256   |  6,001.05 ns | 1,943.617 ns | 106.536 ns |  1.50 |    0.03 |      - |         - |          NA |
| ByteToOwnedString           | UnicodeLong  | 256   |  7,377.91 ns | 1,343.823 ns |  73.659 ns |  1.85 |    0.03 | 0.8731 |    7704 B |          NA |
| StringViaOwnedUtf8          | UnicodeLong  | 256   |  8,625.64 ns |   395.176 ns |  21.661 ns |  2.16 |    0.03 | 0.6897 |    6168 B |          NA |
|                             |              |       |              |              |            |       |         |        |           |             |
| **StringToCharacterCore**       | **UnicodeShort** | **7**     |     **36.37 ns** |    **27.739 ns** |   **1.520 ns** |  **1.00** |    **0.05** |      **-** |         **-** |          **NA** |
| ByteToByteCore              | UnicodeShort | 7     |    170.80 ns |    23.097 ns |   1.266 ns |  4.70 |    0.17 |      - |         - |          NA |
| ByteToCharacterScratch      | UnicodeShort | 7     |    133.83 ns |    60.000 ns |   3.289 ns |  3.68 |    0.15 |      - |         - |          NA |
| StringToCharacterCorePaused | UnicodeShort | 7     |     36.84 ns |     2.083 ns |   0.114 ns |  1.01 |    0.04 |      - |         - |          NA |
| ByteToByteCorePaused        | UnicodeShort | 7     |    182.74 ns |    18.186 ns |   0.997 ns |  5.03 |    0.18 |      - |         - |          NA |
| ByteToOwnedString           | UnicodeShort | 7     |     68.00 ns |    14.383 ns |   0.788 ns |  1.87 |    0.07 | 0.0100 |      88 B |          NA |
| StringViaOwnedUtf8          | UnicodeShort | 7     |    191.80 ns |    13.048 ns |   0.715 ns |  5.28 |    0.19 | 0.0076 |      72 B |          NA |
|                             |              |       |              |              |            |       |         |        |           |             |
| **StringToCharacterCore**       | **UnicodeShort** | **256**   |     **20.73 ns** |     **1.707 ns** |   **0.094 ns** |  **1.00** |    **0.01** |      **-** |         **-** |          **NA** |
| ByteToByteCore              | UnicodeShort | 256   |     45.69 ns |     5.233 ns |   0.287 ns |  2.20 |    0.01 |      - |         - |          NA |
| ByteToCharacterScratch      | UnicodeShort | 256   |     39.11 ns |     2.942 ns |   0.161 ns |  1.89 |    0.01 |      - |         - |          NA |
| StringToCharacterCorePaused | UnicodeShort | 256   |     22.42 ns |     3.710 ns |   0.203 ns |  1.08 |    0.01 |      - |         - |          NA |
| ByteToByteCorePaused        | UnicodeShort | 256   |     46.71 ns |    12.387 ns |   0.679 ns |  2.25 |    0.03 |      - |         - |          NA |
| ByteToOwnedString           | UnicodeShort | 256   |     51.82 ns |     6.661 ns |   0.365 ns |  2.50 |    0.02 | 0.0104 |      88 B |          NA |
| StringViaOwnedUtf8          | UnicodeShort | 256   |     73.12 ns |     4.131 ns |   0.226 ns |  3.53 |    0.02 | 0.0083 |      72 B |          NA |
