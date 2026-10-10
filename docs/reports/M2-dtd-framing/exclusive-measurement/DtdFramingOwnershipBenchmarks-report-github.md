```

BenchmarkDotNet v0.15.8, macOS Sequoia 15.8.1 (24H32) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
.NET SDK 10.0.400
  [Host]   : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

```
| Method           | Corpus       | Mean         | Error        | StdDev     | Gen0   | Gen1   | Allocated |
|----------------- |------------- |-------------:|-------------:|-----------:|-------:|-------:|----------:|
| **FrameOwnedResult** | **absent**       |     **62.10 ns** |     **6.197 ns** |   **0.340 ns** | **0.0774** |      **-** |     **648 B** |
| **FrameOwnedResult** | **chunked**      |    **674.93 ns** |    **45.349 ns** |   **2.486 ns** | **0.0916** |      **-** |     **768 B** |
| **FrameOwnedResult** | **long-label**   | **21,061.39 ns** | **4,703.271 ns** | **257.802 ns** | **3.0212** | **0.0305** |   **25376 B** |
| **FrameOwnedResult** | **malformed**    |    **121.22 ns** |     **9.800 ns** |   **0.537 ns** | **0.0861** |      **-** |     **720 B** |
| **FrameOwnedResult** | **multilingual** |     **65.18 ns** |     **1.210 ns** |   **0.066 ns** | **0.0774** |      **-** |     **648 B** |
| **FrameOwnedResult** | **short**        |    **242.59 ns** |     **2.045 ns** |   **0.112 ns** | **0.0916** |      **-** |     **768 B** |
