using BenchmarkDotNet.Running;
using Xemele.ReleaseBenchmarks;

if (args is ["--qualify"])
{
    int checks = 0;
    foreach (string fixture in ReleaseInputs.FixtureIds())
    {
        foreach (string engine in new[] { "xemele", "bcl" })
        {
            var benchmark = new ManagedDomBenchmarks { Fixture = fixture, Engine = engine };
            benchmark.Setup();
            if (benchmark.ParseOwnedDom() is null || benchmark.SerializeUtf8() <= 0 || benchmark.EvaluateCompiledXPath() != benchmark.ExpectedXPath)
                throw new InvalidDataException("Managed benchmark operation oracle differs.");
            checks += 3;
        }
        foreach (string engine in new[] { "xemele-reader", "xemele-sax", "bcl-reader" })
        {
            var benchmark = new ManagedForwardBenchmarks { Fixture = fixture, Engine = engine };
            benchmark.Setup();
            if (benchmark.ReadAll() != benchmark.ExpectedCharacters) throw new InvalidDataException("Forward benchmark text oracle differs.");
            checks++;
        }
        Console.WriteLine("PASS managed benchmark operations " + fixture);
    }
    Console.WriteLine("PASS " + checks + " preloaded managed benchmark operation oracles; no timings executed");
    return;
}
BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
