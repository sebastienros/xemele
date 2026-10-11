```

BenchmarkDotNet v0.15.8, macOS Sequoia 15.8.1 (24H32) [Darwin 24.6.0]
Unknown processor
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a

Job=InProcess  Toolchain=InProcessEmitToolchain  InvocationCount=256  
IterationCount=3  UnrollFactor=1  WarmupCount=1  

```
| Method      | ChunkSize | Mean       | Error      | StdDev    | Median     | Allocated |
|------------ |---------- |-----------:|-----------:|----------:|-----------:|----------:|
| **AppendChunk** | **4096**      |   **285.5 μs** | **3,486.7 μs** | **191.12 μs** |   **209.0 μs** |         **-** |
| **AppendChunk** | **65536**     | **3,073.5 μs** | **1,718.1 μs** |  **94.17 μs** | **3,043.4 μs** |       **7 B** |
