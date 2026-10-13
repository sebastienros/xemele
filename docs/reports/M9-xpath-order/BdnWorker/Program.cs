using BenchmarkDotNet.Running;
using Xemele.Benchmarks;
BenchmarkSwitcher.FromTypes([typeof(XPathOrderBenchmarks)]).Run(args);
