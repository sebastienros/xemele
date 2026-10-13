using BenchmarkDotNet.Running;
using Xemele.Benchmarks;
BenchmarkSwitcher.FromTypes([typeof(XPathOrderBenchmarks), typeof(XPathOverlapBenchmarks)]).Run(args);
