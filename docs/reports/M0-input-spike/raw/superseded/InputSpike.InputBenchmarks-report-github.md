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
| **StringToCharacterCore**       | **AsciiLong**    | **7**     |  **4,751.78 ns** | **2,303.075 ns** | **126.239 ns** |  **1.00** |    **0.03** |      **-** |         **-** |          **NA** |
| ByteToByteCore              | AsciiLong    | 7     |  9,584.40 ns | 3,597.249 ns | 197.177 ns |  2.02 |    0.06 |      - |         - |          NA |
| ByteToCharacterScratch      | AsciiLong    | 7     |  7,023.66 ns | 1,258.213 ns |  68.967 ns |  1.48 |    0.04 |      - |         - |          NA |
| StringToCharacterCorePaused | AsciiLong    | 7     |  4,425.32 ns |   113.892 ns |   6.243 ns |  0.93 |    0.02 |      - |         - |          NA |
| ByteToByteCorePaused        | AsciiLong    | 7     |  8,224.33 ns |   250.639 ns |  13.738 ns |  1.73 |    0.04 |      - |         - |          NA |
| ByteToOwnedString           | AsciiLong    | 7     |  5,404.23 ns | 1,330.385 ns |  72.923 ns |  1.14 |    0.03 | 0.8576 |    7448 B |          NA |
| StringViaOwnedUtf8          | AsciiLong    | 7     |  9,981.98 ns | 1,736.713 ns |  95.195 ns |  2.10 |    0.05 | 0.3840 |    3736 B |          NA |
|                             |              |       |              |              |            |       |         |        |           |             |
| **StringToCharacterCore**       | **AsciiLong**    | **256**   |  **3,972.04 ns** |   **816.945 ns** |  **44.779 ns** |  **1.00** |    **0.01** |      **-** |         **-** |          **NA** |
| ByteToByteCore              | AsciiLong    | 256   |  4,995.42 ns |   643.300 ns |  35.261 ns |  1.26 |    0.01 |      - |         - |          NA |
| ByteToCharacterScratch      | AsciiLong    | 256   |  4,071.04 ns |   792.786 ns |  43.455 ns |  1.03 |    0.01 |      - |         - |          NA |
| StringToCharacterCorePaused | AsciiLong    | 256   |  3,851.16 ns |    88.638 ns |   4.859 ns |  0.97 |    0.01 |      - |         - |          NA |
| ByteToByteCorePaused        | AsciiLong    | 256   |  4,626.09 ns |   231.969 ns |  12.715 ns |  1.16 |    0.01 |      - |         - |          NA |
| ByteToOwnedString           | AsciiLong    | 256   |  4,213.98 ns |   490.374 ns |  26.879 ns |  1.06 |    0.01 | 0.8862 |    7448 B |          NA |
| StringViaOwnedUtf8          | AsciiLong    | 256   |  5,267.74 ns |   259.697 ns |  14.235 ns |  1.33 |    0.01 | 0.4274 |    3736 B |          NA |
|                             |              |       |              |              |            |       |         |        |           |             |
| **StringToCharacterCore**       | **AsciiShort**   | **7**     |     **32.36 ns** |     **4.231 ns** |   **0.232 ns** |  **1.00** |    **0.01** |      **-** |         **-** |          **NA** |
| ByteToByteCore              | AsciiShort   | 7     |     79.32 ns |    24.189 ns |   1.326 ns |  2.45 |    0.04 |      - |         - |          NA |
| ByteToCharacterScratch      | AsciiShort   | 7     |     61.30 ns |     9.154 ns |   0.502 ns |  1.89 |    0.02 |      - |         - |          NA |
| StringToCharacterCorePaused | AsciiShort   | 7     |     32.66 ns |     0.578 ns |   0.032 ns |  1.01 |    0.01 |      - |         - |          NA |
| ByteToByteCorePaused        | AsciiShort   | 7     |     67.25 ns |     3.915 ns |   0.215 ns |  2.08 |    0.01 |      - |         - |          NA |
| ByteToOwnedString           | AsciiShort   | 7     |     45.95 ns |     9.079 ns |   0.498 ns |  1.42 |    0.02 | 0.0094 |      80 B |          NA |
| StringViaOwnedUtf8          | AsciiShort   | 7     |     87.56 ns |    16.940 ns |   0.929 ns |  2.71 |    0.03 | 0.0061 |      56 B |          NA |
|                             |              |       |              |              |            |       |         |        |           |             |
| **StringToCharacterCore**       | **AsciiShort**   | **256**   |     **19.26 ns** |     **6.043 ns** |   **0.331 ns** |  **1.00** |    **0.02** |      **-** |         **-** |          **NA** |
| ByteToByteCore              | AsciiShort   | 256   |     37.95 ns |     0.589 ns |   0.032 ns |  1.97 |    0.03 |      - |         - |          NA |
| ByteToCharacterScratch      | AsciiShort   | 256   |     34.20 ns |     9.612 ns |   0.527 ns |  1.78 |    0.04 |      - |         - |          NA |
| StringToCharacterCorePaused | AsciiShort   | 256   |     21.76 ns |     3.413 ns |   0.187 ns |  1.13 |    0.02 |      - |         - |          NA |
| ByteToByteCorePaused        | AsciiShort   | 256   |     40.26 ns |     4.641 ns |   0.254 ns |  2.09 |    0.03 |      - |         - |          NA |
| ByteToOwnedString           | AsciiShort   | 256   |     32.54 ns |     5.886 ns |   0.323 ns |  1.69 |    0.03 | 0.0094 |      80 B |          NA |
| StringViaOwnedUtf8          | AsciiShort   | 256   |     55.57 ns |    20.972 ns |   1.150 ns |  2.89 |    0.07 | 0.0063 |      56 B |          NA |
|                             |              |       |              |              |            |       |         |        |           |             |
| **StringToCharacterCore**       | **UnicodeLong**  | **7**     |  **4,881.10 ns** |   **232.609 ns** |  **12.750 ns** |  **1.00** |    **0.00** |      **-** |         **-** |          **NA** |
| ByteToByteCore              | UnicodeLong  | 7     | 23,271.78 ns | 3,913.305 ns | 214.501 ns |  4.77 |    0.04 |      - |         - |          NA |
| ByteToCharacterScratch      | UnicodeLong  | 7     | 18,609.46 ns | 2,821.304 ns | 154.645 ns |  3.81 |    0.03 |      - |         - |          NA |
| StringToCharacterCorePaused | UnicodeLong  | 7     |  4,575.25 ns | 1,808.083 ns |  99.107 ns |  0.94 |    0.02 |      - |         - |          NA |
| ByteToByteCorePaused        | UnicodeLong  | 7     | 22,651.47 ns | 6,082.104 ns | 333.381 ns |  4.64 |    0.06 |      - |         - |          NA |
| ByteToOwnedString           | UnicodeLong  | 7     |  8,675.09 ns |   999.230 ns |  54.771 ns |  1.78 |    0.01 | 0.8657 |    7704 B |          NA |
| StringViaOwnedUtf8          | UnicodeLong  | 7     | 26,241.77 ns | 8,730.699 ns | 478.559 ns |  5.38 |    0.09 | 0.4960 |    6168 B |          NA |
|                             |              |       |              |              |            |       |         |        |           |             |
| **StringToCharacterCore**       | **UnicodeLong**  | **256**   |  **4,134.63 ns** |   **236.798 ns** |  **12.980 ns** |  **1.00** |    **0.00** |      **-** |         **-** |          **NA** |
| ByteToByteCore              | UnicodeLong  | 256   |  6,197.25 ns |   464.890 ns |  25.482 ns |  1.50 |    0.01 |      - |         - |          NA |
| ByteToCharacterScratch      | UnicodeLong  | 256   |  5,573.89 ns |   549.465 ns |  30.118 ns |  1.35 |    0.01 |      - |         - |          NA |
| StringToCharacterCorePaused | UnicodeLong  | 256   |  4,224.14 ns |   225.400 ns |  12.355 ns |  1.02 |    0.00 |      - |         - |          NA |
| ByteToByteCorePaused        | UnicodeLong  | 256   |  5,900.87 ns | 2,116.104 ns | 115.991 ns |  1.43 |    0.02 |      - |         - |          NA |
| ByteToOwnedString           | UnicodeLong  | 256   |  7,513.58 ns |   991.930 ns |  54.371 ns |  1.82 |    0.01 | 0.8939 |    7704 B |          NA |
| StringViaOwnedUtf8          | UnicodeLong  | 256   |  8,776.53 ns | 1,146.563 ns |  62.847 ns |  2.12 |    0.01 | 0.7123 |    6168 B |          NA |
|                             |              |       |              |              |            |       |         |        |           |             |
| **StringToCharacterCore**       | **UnicodeShort** | **7**     |     **34.31 ns** |     **2.028 ns** |   **0.111 ns** |  **1.00** |    **0.00** |      **-** |         **-** |          **NA** |
| ByteToByteCore              | UnicodeShort | 7     |    171.18 ns |    39.213 ns |   2.149 ns |  4.99 |    0.06 |      - |         - |          NA |
| ByteToCharacterScratch      | UnicodeShort | 7     |    129.26 ns |    60.372 ns |   3.309 ns |  3.77 |    0.08 |      - |         - |          NA |
| StringToCharacterCorePaused | UnicodeShort | 7     |     35.38 ns |     3.510 ns |   0.192 ns |  1.03 |    0.01 |      - |         - |          NA |
| ByteToByteCorePaused        | UnicodeShort | 7     |    185.81 ns |    18.525 ns |   1.015 ns |  5.42 |    0.03 |      - |         - |          NA |
| ByteToOwnedString           | UnicodeShort | 7     |     74.27 ns |    19.909 ns |   1.091 ns |  2.16 |    0.03 | 0.0105 |      88 B |          NA |
| StringViaOwnedUtf8          | UnicodeShort | 7     |    208.94 ns |    24.295 ns |   1.332 ns |  6.09 |    0.04 | 0.0068 |      72 B |          NA |
|                             |              |       |              |              |            |       |         |        |           |             |
| **StringToCharacterCore**       | **UnicodeShort** | **256**   |     **21.33 ns** |     **3.886 ns** |   **0.213 ns** |  **1.00** |    **0.01** |      **-** |         **-** |          **NA** |
| ByteToByteCore              | UnicodeShort | 256   |     46.70 ns |     4.098 ns |   0.225 ns |  2.19 |    0.02 |      - |         - |          NA |
| ByteToCharacterScratch      | UnicodeShort | 256   |     40.79 ns |    13.122 ns |   0.719 ns |  1.91 |    0.03 |      - |         - |          NA |
| StringToCharacterCorePaused | UnicodeShort | 256   |     22.98 ns |     3.363 ns |   0.184 ns |  1.08 |    0.01 |      - |         - |          NA |
| ByteToByteCorePaused        | UnicodeShort | 256   |     47.78 ns |     8.391 ns |   0.460 ns |  2.24 |    0.03 |      - |         - |          NA |
| ByteToOwnedString           | UnicodeShort | 256   |     52.23 ns |    16.197 ns |   0.888 ns |  2.45 |    0.04 | 0.0105 |      88 B |          NA |
| StringViaOwnedUtf8          | UnicodeShort | 256   |     78.94 ns |   105.293 ns |   5.771 ns |  3.70 |    0.24 | 0.0084 |      72 B |          NA |
