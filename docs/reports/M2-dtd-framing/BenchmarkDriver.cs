using BenchmarkDotNet.Running;
BenchmarkSwitcher.FromAssembly(typeof(DtdFramingScanBenchmarks).Assembly).Run(args);
