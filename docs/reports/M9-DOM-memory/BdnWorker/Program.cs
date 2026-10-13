using BenchmarkDotNet.Running;
using Xemele.Benchmarks;

BenchmarkSwitcher.FromTypes([typeof(DomStorageBenchmarks)]).Run(args);
