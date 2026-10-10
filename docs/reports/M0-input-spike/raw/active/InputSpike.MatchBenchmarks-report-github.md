```

BenchmarkDotNet v0.15.8, macOS Sequoia 15.8.1 (24H32) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
.NET SDK 10.0.400
  [Host]         : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a
  M0-Provisional : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a

Job=M0-Provisional  IterationCount=3  IterationTime=100ms  
LaunchCount=1  WarmupCount=2  

```
| Method     | Token       | IgnoreAsciiCase | Mean       | Error      | StdDev    | Median     | Allocated |
|----------- |------------ |---------------- |-----------:|-----------:|----------:|-----------:|----------:|
| **ByteLength** | **[ML**         | **False**           |  **0.2910 ns** |  **0.2436 ns** | **0.0133 ns** |  **0.2961 ns** |         **-** |
| ByteHash   | [ML         | False           |  4.5277 ns |  3.4187 ns | 0.1874 ns |  4.4522 ns |         - |
| CharLength | [ML         | False           |  0.4487 ns |  0.0713 ns | 0.0039 ns |  0.4464 ns |         - |
| CharHash   | [ML         | False           |  4.3674 ns |  0.7991 ns | 0.0438 ns |  4.3595 ns |         - |
| **ByteLength** | **[ML**         | **True**            |  **0.4049 ns** |  **0.3908 ns** | **0.0214 ns** |  **0.4060 ns** |         **-** |
| ByteHash   | [ML         | True            |  5.1572 ns |  0.9734 ns | 0.0534 ns |  5.1601 ns |         - |
| CharLength | [ML         | True            |  0.7467 ns |  1.1145 ns | 0.0611 ns |  0.7251 ns |         - |
| CharHash   | [ML         | True            |  4.8380 ns |  1.5997 ns | 0.0877 ns |  4.8340 ns |         - |
| **ByteLength** | **doctype**     | **False**           |  **1.2079 ns** |  **0.2819 ns** | **0.0155 ns** |  **1.2086 ns** |         **-** |
| ByteHash   | doctype     | False           |  8.1992 ns |  1.0692 ns | 0.0586 ns |  8.2184 ns |         - |
| CharLength | doctype     | False           |  1.8336 ns |  0.1702 ns | 0.0093 ns |  1.8290 ns |         - |
| CharHash   | doctype     | False           |  7.6577 ns |  1.2783 ns | 0.0701 ns |  7.6177 ns |         - |
| **ByteLength** | **doctype**     | **True**            |  **5.7121 ns** |  **2.4529 ns** | **0.1345 ns** |  **5.6811 ns** |         **-** |
| ByteHash   | doctype     | True            | 10.4926 ns |  9.5173 ns | 0.5217 ns | 10.4347 ns |         - |
| CharLength | doctype     | True            |  4.3205 ns |  4.3119 ns | 0.2363 ns |  4.1868 ns |         - |
| CharHash   | doctype     | True            | 10.1907 ns |  1.6737 ns | 0.0917 ns | 10.1509 ns |         - |
| **ByteLength** | **DOCTYPE**     | **False**           |  **0.6018 ns** |  **5.5643 ns** | **0.3050 ns** |  **0.5533 ns** |         **-** |
| ByteHash   | DOCTYPE     | False           |  6.7663 ns |  0.9852 ns | 0.0540 ns |  6.7698 ns |         - |
| CharLength | DOCTYPE     | False           |  0.4487 ns |  0.0557 ns | 0.0031 ns |  0.4474 ns |         - |
| CharHash   | DOCTYPE     | False           |  5.3184 ns |  1.3759 ns | 0.0754 ns |  5.2775 ns |         - |
| **ByteLength** | **DOCTYPE**     | **True**            |  **3.7579 ns** |  **6.7735 ns** | **0.3713 ns** |  **3.5894 ns** |         **-** |
| ByteHash   | DOCTYPE     | True            |  8.8095 ns | 13.5382 ns | 0.7421 ns |  8.9605 ns |         - |
| CharLength | DOCTYPE     | True            |  4.5317 ns |  1.8921 ns | 0.1037 ns |  4.5236 ns |         - |
| CharHash   | DOCTYPE     | True            |  8.0622 ns | 16.1721 ns | 0.8864 ns |  7.5677 ns |         - |
| **ByteLength** | **éml**         | **False**           |  **0.0000 ns** |  **0.0000 ns** | **0.0000 ns** |  **0.0000 ns** |         **-** |
| ByteHash   | éml         | False           |  0.2598 ns |  0.0729 ns | 0.0040 ns |  0.2596 ns |         - |
| CharLength | éml         | False           |  0.4700 ns |  0.2497 ns | 0.0137 ns |  0.4729 ns |         - |
| CharHash   | éml         | False           |  0.2792 ns |  0.2268 ns | 0.0124 ns |  0.2729 ns |         - |
| **ByteLength** | **éml**         | **True**            |  **0.0000 ns** |  **0.0000 ns** | **0.0000 ns** |  **0.0000 ns** |         **-** |
| ByteHash   | éml         | True            |  0.2808 ns |  0.1054 ns | 0.0058 ns |  0.2782 ns |         - |
| CharLength | éml         | True            |  0.4614 ns |  0.0738 ns | 0.0040 ns |  0.4627 ns |         - |
| CharHash   | éml         | True            |  0.2717 ns |  0.1020 ns | 0.0056 ns |  0.2690 ns |         - |
| **ByteLength** | **stand**       | **False**           |  **0.6689 ns** |  **0.0607 ns** | **0.0033 ns** |  **0.6698 ns** |         **-** |
| ByteHash   | stand       | False           |  4.6010 ns |  1.7514 ns | 0.0960 ns |  4.6184 ns |         - |
| CharLength | stand       | False           |  1.2002 ns |  0.1362 ns | 0.0075 ns |  1.1994 ns |         - |
| CharHash   | stand       | False           |  5.3919 ns |  1.7905 ns | 0.0981 ns |  5.4274 ns |         - |
| **ByteLength** | **stand**       | **True**            |  **1.2528 ns** |  **0.0559 ns** | **0.0031 ns** |  **1.2513 ns** |         **-** |
| ByteHash   | stand       | True            |  4.5587 ns |  1.3356 ns | 0.0732 ns |  4.5705 ns |         - |
| CharLength | stand       | True            |  1.7185 ns |  0.1159 ns | 0.0064 ns |  1.7181 ns |         - |
| CharHash   | stand       | True            |  5.4549 ns |  5.6548 ns | 0.3100 ns |  5.4114 ns |         - |
| **ByteLength** | **standalone**  | **False**           |  **0.4515 ns** |  **0.0335 ns** | **0.0018 ns** |  **0.4514 ns** |         **-** |
| ByteHash   | standalone  | False           |  6.3144 ns |  2.4067 ns | 0.1319 ns |  6.2812 ns |         - |
| CharLength | standalone  | False           |  0.4677 ns |  0.1465 ns | 0.0080 ns |  0.4669 ns |         - |
| CharHash   | standalone  | False           |  6.1368 ns |  1.1619 ns | 0.0637 ns |  6.1721 ns |         - |
| **ByteLength** | **standalone**  | **True**            |  **5.7824 ns** | **16.2164 ns** | **0.8889 ns** |  **5.2746 ns** |         **-** |
| ByteHash   | standalone  | True            | 11.3977 ns | 17.9990 ns | 0.9866 ns | 10.9929 ns |         - |
| CharLength | standalone  | True            |  5.8739 ns | 17.5120 ns | 0.9599 ns |  5.3675 ns |         - |
| CharHash   | standalone  | True            | 11.8452 ns | 25.6607 ns | 1.4065 ns | 11.1127 ns |         - |
| **ByteLength** | **standaloneX** | **False**           |  **0.0000 ns** |  **0.0000 ns** | **0.0000 ns** |  **0.0000 ns** |         **-** |
| ByteHash   | standaloneX | False           |  7.1110 ns |  0.6454 ns | 0.0354 ns |  7.1228 ns |         - |
| CharLength | standaloneX | False           |  0.4663 ns |  0.0506 ns | 0.0028 ns |  0.4650 ns |         - |
| CharHash   | standaloneX | False           |  7.2360 ns |  0.6164 ns | 0.0338 ns |  7.2368 ns |         - |
| **ByteLength** | **standaloneX** | **True**            |  **0.0013 ns** |  **0.0411 ns** | **0.0023 ns** |  **0.0000 ns** |         **-** |
| ByteHash   | standaloneX | True            |  7.5302 ns |  6.5042 ns | 0.3565 ns |  7.4335 ns |         - |
| CharLength | standaloneX | True            |  0.4649 ns |  0.0187 ns | 0.0010 ns |  0.4645 ns |         - |
| CharHash   | standaloneX | True            |  8.2077 ns |  0.8890 ns | 0.0487 ns |  8.1945 ns |         - |
| **ByteLength** | **standalonf**  | **False**           |  **0.4543 ns** |  **0.1883 ns** | **0.0103 ns** |  **0.4525 ns** |         **-** |
| ByteHash   | standalonf  | False           |  6.2509 ns |  0.6356 ns | 0.0348 ns |  6.2391 ns |         - |
| CharLength | standalonf  | False           |  0.7316 ns |  0.0961 ns | 0.0053 ns |  0.7319 ns |         - |
| CharHash   | standalonf  | False           |  6.1285 ns |  2.7121 ns | 0.1487 ns |  6.0892 ns |         - |
| **ByteLength** | **standalonf**  | **True**            |  **5.1872 ns** | **21.8395 ns** | **1.1971 ns** |  **4.6154 ns** |         **-** |
| ByteHash   | standalonf  | True            |  7.0046 ns | 16.0229 ns | 0.8783 ns |  6.6730 ns |         - |
| CharLength | standalonf  | True            |  4.9340 ns |  0.7886 ns | 0.0432 ns |  4.9372 ns |         - |
| CharHash   | standalonf  | True            |  6.5388 ns |  1.5082 ns | 0.0827 ns |  6.5205 ns |         - |
| **ByteLength** | **utf-8**       | **False**           |  **0.7026 ns** |  **0.1488 ns** | **0.0082 ns** |  **0.6989 ns** |         **-** |
| ByteHash   | utf-8       | False           |  7.3701 ns |  1.4023 ns | 0.0769 ns |  7.3937 ns |         - |
| CharLength | utf-8       | False           |  1.2743 ns |  0.8245 ns | 0.0452 ns |  1.2596 ns |         - |
| CharHash   | utf-8       | False           |  7.5262 ns |  1.2726 ns | 0.0698 ns |  7.5076 ns |         - |
| **ByteLength** | **utf-8**       | **True**            |  **3.4097 ns** |  **2.0905 ns** | **0.1146 ns** |  **3.4241 ns** |         **-** |
| ByteHash   | utf-8       | True            |  5.3045 ns |  5.7496 ns | 0.3152 ns |  5.3903 ns |         - |
| CharLength | utf-8       | True            |  4.4143 ns |  1.7622 ns | 0.0966 ns |  4.4294 ns |         - |
| CharHash   | utf-8       | True            |  6.6814 ns |  3.3602 ns | 0.1842 ns |  6.7352 ns |         - |
| **ByteLength** | **UTF-8**       | **False**           |  **0.3444 ns** |  **0.0740 ns** | **0.0041 ns** |  **0.3436 ns** |         **-** |
| ByteHash   | UTF-8       | False           |  2.4312 ns |  2.8762 ns | 0.1577 ns |  2.4934 ns |         - |
| CharLength | UTF-8       | False           |  0.4673 ns |  0.2975 ns | 0.0163 ns |  0.4580 ns |         - |
| CharHash   | UTF-8       | False           |  2.9797 ns |  0.6495 ns | 0.0356 ns |  2.9681 ns |         - |
| **ByteLength** | **UTF-8**       | **True**            |  **3.9562 ns** |  **3.0132 ns** | **0.1652 ns** |  **3.9020 ns** |         **-** |
| ByteHash   | UTF-8       | True            |  6.8203 ns |  1.8351 ns | 0.1006 ns |  6.8287 ns |         - |
| CharLength | UTF-8       | True            |  4.1150 ns |  0.9900 ns | 0.0543 ns |  4.0942 ns |         - |
| CharHash   | UTF-8       | True            |  7.4371 ns |  2.7652 ns | 0.1516 ns |  7.4757 ns |         - |
| **ByteLength** | **xml**         | **False**           |  **0.2973 ns** |  **0.4447 ns** | **0.0244 ns** |  **0.2884 ns** |         **-** |
| ByteHash   | xml         | False           |  1.4786 ns |  0.2051 ns | 0.0112 ns |  1.4773 ns |         - |
| CharLength | xml         | False           |  0.4846 ns |  0.5238 ns | 0.0287 ns |  0.4705 ns |         - |
| CharHash   | xml         | False           |  1.6001 ns |  3.0355 ns | 0.1664 ns |  1.5144 ns |         - |
| **ByteLength** | **xml**         | **True**            |  **1.3341 ns** |  **0.6423 ns** | **0.0352 ns** |  **1.3155 ns** |         **-** |
| ByteHash   | xml         | True            |  3.6619 ns |  8.4610 ns | 0.4638 ns |  3.8532 ns |         - |
| CharLength | xml         | True            |  1.7616 ns |  0.6453 ns | 0.0354 ns |  1.7619 ns |         - |
| CharHash   | xml         | True            |  3.6245 ns |  4.8991 ns | 0.2685 ns |  3.6545 ns |         - |
| **ByteLength** | **xnm**         | **False**           |  **0.3211 ns** |  **0.5616 ns** | **0.0308 ns** |  **0.3197 ns** |         **-** |
| ByteHash   | xnm         | False           |  4.4221 ns |  0.2414 ns | 0.0132 ns |  4.4261 ns |         - |
| CharLength | xnm         | False           |  0.4807 ns |  0.2849 ns | 0.0156 ns |  0.4894 ns |         - |
| CharHash   | xnm         | False           |  4.3116 ns |  1.3736 ns | 0.0753 ns |  4.3494 ns |         - |
| **ByteLength** | **xnm**         | **True**            |  **0.7656 ns** |  **0.0626 ns** | **0.0034 ns** |  **0.7646 ns** |         **-** |
| ByteHash   | xnm         | True            |  6.1403 ns |  5.6545 ns | 0.3099 ns |  6.2895 ns |         - |
| CharLength | xnm         | True            |  1.3131 ns |  0.5578 ns | 0.0306 ns |  1.2965 ns |         - |
| CharHash   | xnm         | True            |  6.8258 ns |  1.6862 ns | 0.0924 ns |  6.8312 ns |         - |
