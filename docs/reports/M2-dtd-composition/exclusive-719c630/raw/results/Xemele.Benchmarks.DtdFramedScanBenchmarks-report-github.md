```

BenchmarkDotNet v0.15.8, macOS Sequoia 15.8.1 (24H32) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
.NET SDK 10.0.400
  [Host]   : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method              | CommentScalars | Mean        | Error        | StdDev    | Allocated |
|-------------------- |--------------- |------------:|-------------:|----------:|----------:|
| **BorrowedCommentScan** | **32**             |    **235.3 ns** |     **34.22 ns** |   **1.88 ns** |         **-** |
| **BorrowedCommentScan** | **4096**           | **20,697.7 ns** | **13,216.27 ns** | **724.43 ns** |         **-** |
