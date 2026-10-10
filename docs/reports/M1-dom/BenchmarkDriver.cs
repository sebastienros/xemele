using BenchmarkDotNet.Running;
using Xemele.DomMeasurements;
if (args.Length == 1 && args[0] == "--oracle")
{
    foreach (string shape in new[] { "Ascii", "Unicode", "LongText", "Deep" })
    {
        new DomBenchmarks { Shape = shape }.Setup();
        Console.WriteLine("Complete DOM transport oracle passed: " + shape);
    }
    return;
}
BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
