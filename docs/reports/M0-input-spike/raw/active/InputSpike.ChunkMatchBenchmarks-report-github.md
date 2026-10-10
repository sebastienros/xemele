```

BenchmarkDotNet v0.15.8, macOS Sequoia 15.8.1 (24H32) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
.NET SDK 10.0.400
  [Host]         : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a
  M0-Provisional : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a

Job=M0-Provisional  IterationCount=3  IterationTime=100ms  
LaunchCount=1  WarmupCount=2  

```
| Method     | Token       | Mean      | Error      | StdDev    | Allocated |
|----------- |------------ |----------:|-----------:|----------:|----------:|
| **ByteLength** | **doctype**     |  **7.193 ns** |  **3.1565 ns** | **0.1730 ns** |         **-** |
| ByteHash   | doctype     | 14.674 ns |  7.1964 ns | 0.3945 ns |         - |
| CharLength | doctype     |  7.375 ns |  2.0249 ns | 0.1110 ns |         - |
| CharHash   | doctype     | 15.293 ns |  3.0060 ns | 0.1648 ns |         - |
| **ByteLength** | **DOCTYPE**     |  **6.902 ns** |  **1.7037 ns** | **0.0934 ns** |         **-** |
| ByteHash   | DOCTYPE     | 12.194 ns |  9.3538 ns | 0.5127 ns |         - |
| CharLength | DOCTYPE     |  6.897 ns |  1.9070 ns | 0.1045 ns |         - |
| CharHash   | DOCTYPE     | 13.348 ns |  4.6336 ns | 0.2540 ns |         - |
| **ByteLength** | **éml**         |  **4.184 ns** |  **0.0969 ns** | **0.0053 ns** |         **-** |
| ByteHash   | éml         |  4.478 ns |  0.6381 ns | 0.0350 ns |         - |
| CharLength | éml         |  4.093 ns |  0.0751 ns | 0.0041 ns |         - |
| CharHash   | éml         |  3.777 ns |  0.1281 ns | 0.0070 ns |         - |
| **ByteLength** | **standalone**  |  **8.140 ns** |  **2.4530 ns** | **0.1345 ns** |         **-** |
| ByteHash   | standalone  | 16.679 ns | 15.2360 ns | 0.8351 ns |         - |
| CharLength | standalone  |  9.042 ns |  1.4609 ns | 0.0801 ns |         - |
| CharHash   | standalone  | 15.818 ns |  4.2906 ns | 0.2352 ns |         - |
| **ByteLength** | **standaloneX** |  **2.956 ns** |  **0.3400 ns** | **0.0186 ns** |         **-** |
| ByteHash   | standaloneX |  2.873 ns |  0.2154 ns | 0.0118 ns |         - |
| CharLength | standaloneX |  2.886 ns |  0.3314 ns | 0.0182 ns |         - |
| CharHash   | standaloneX |  2.851 ns |  0.2448 ns | 0.0134 ns |         - |
| **ByteLength** | **xml**         |  **4.782 ns** |  **0.3583 ns** | **0.0196 ns** |         **-** |
| ByteHash   | xml         |  7.227 ns |  1.8102 ns | 0.0992 ns |         - |
| CharLength | xml         |  4.873 ns |  1.7690 ns | 0.0970 ns |         - |
| CharHash   | xml         |  7.310 ns |  0.7979 ns | 0.0437 ns |         - |
