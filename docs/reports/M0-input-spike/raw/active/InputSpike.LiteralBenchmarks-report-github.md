```

BenchmarkDotNet v0.15.8, macOS Sequoia 15.8.1 (24H32) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
.NET SDK 10.0.400
  [Host]         : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a
  M0-Provisional : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a

Job=M0-Provisional  IterationCount=3  IterationTime=100ms  
LaunchCount=1  WarmupCount=2  

```
| Method                   | Token | Mean      | Error     | StdDev    | Allocated |
|------------------------- |------ |----------:|----------:|----------:|----------:|
| **ByteLiteral**              | **&lt;?xml** | **0.0000 ns** | **0.0000 ns** | **0.0000 ns** |         **-** |
| ByteArray                | &lt;?xml | 0.0000 ns | 0.0000 ns | 0.0000 ns |         - |
| CharLiteral              | &lt;?xml | 0.0000 ns | 0.0000 ns | 0.0000 ns |         - |
| CharArray                | &lt;?xml | 0.1740 ns | 0.0219 ns | 0.0012 ns |         - |
| ConstantCollection       | &lt;?xml | 0.0000 ns | 0.0000 ns | 0.0000 ns |         - |
| ConstantIntCollection    | &lt;?xml | 0.0000 ns | 0.0000 ns | 0.0000 ns |         - |
| RuntimeCollectionControl | &lt;?xml | 0.2228 ns | 0.0845 ns | 0.0046 ns |         - |
| MutableCollectionControl | &lt;?xml | 0.2478 ns | 0.3846 ns | 0.0211 ns |         - |
| RuntimeLocalCollection   | &lt;?xml | 0.2006 ns | 0.6416 ns | 0.0352 ns |         - |
| MutableLocalCollection   | &lt;?xml | 0.1876 ns | 0.0996 ns | 0.0055 ns |         - |
| **ByteLiteral**              | **&lt;?xmX** | **0.0000 ns** | **0.0000 ns** | **0.0000 ns** |         **-** |
| ByteArray                | &lt;?xmX | 0.0000 ns | 0.0000 ns | 0.0000 ns |         - |
| CharLiteral              | &lt;?xmX | 0.2109 ns | 0.0571 ns | 0.0031 ns |         - |
| CharArray                | &lt;?xmX | 0.1914 ns | 0.1037 ns | 0.0057 ns |         - |
| ConstantCollection       | &lt;?xmX | 0.0000 ns | 0.0000 ns | 0.0000 ns |         - |
| ConstantIntCollection    | &lt;?xmX | 0.0000 ns | 0.0000 ns | 0.0000 ns |         - |
| RuntimeCollectionControl | &lt;?xmX | 0.2163 ns | 0.2352 ns | 0.0129 ns |         - |
| MutableCollectionControl | &lt;?xmX | 0.3997 ns | 1.3126 ns | 0.0719 ns |         - |
| RuntimeLocalCollection   | &lt;?xmX | 0.1633 ns | 0.0820 ns | 0.0045 ns |         - |
| MutableLocalCollection   | &lt;?xmX | 0.1906 ns | 0.1533 ns | 0.0084 ns |         - |
