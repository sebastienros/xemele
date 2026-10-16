using BenchmarkDotNet.Running;
using XmlComparison;

if (args.Contains("--qualify"))
{
    var dom = new DomBenchmarks();
    dom.Setup();
    var streaming = new ReaderBenchmarks();
    streaming.Setup();
    Console.WriteLine("XML parse, serialization, XPath and reader workloads qualify.");
}
else
{
    BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
}
