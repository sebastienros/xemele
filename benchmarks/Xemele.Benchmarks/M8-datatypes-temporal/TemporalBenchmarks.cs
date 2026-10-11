using BenchmarkDotNet.Attributes;
using Xemele.Validation;

namespace Xemele.Benchmarks.M8DatatypesTemporal;

[MemoryDiagnoser, ShortRunJob]
public class TemporalBenchmarks
{
    [Params("shortEqual", "shortMiss", "longEqual", "longMiss")] public string Case { get; set; } = "shortEqual";
    private Fixture fixture = null!;
    private DatatypeAtomicValue left = null!, right = null!;
    private string leftFraction = "", rightFraction = "";
    [GlobalSetup] public void Setup()
    {
        fixture = new(); bool longInput = Case.StartsWith("long", StringComparison.Ordinal), equal = Case.EndsWith("Equal", StringComparison.Ordinal);
        string a = fixture.Inputs[longInput ? "timeLong" : "timeShort"];
        string b = fixture.Inputs[longInput ? (equal ? "timeLong" : "timeLongOther") : (equal ? "timeShort" : "timeShortOther")];
        left = fixture.Parse("time", a); right = fixture.Parse("time", b);
        // Fixtures deliberately have identical canonical HH:mm:ss and zone
        // absence. Fraction comparison is an independent reduced-case oracle.
        leftFraction = a[9..]; rightFraction = b[9..];
        if (ScalarFraction() != ProviderCompare()) throw new InvalidOperationException();
    }
    [Benchmark(Baseline = true)] public int ScalarFraction()
    {
        for (int i = 0; i < Math.Max(leftFraction.Length, rightFraction.Length); i++)
        {
            char a = i < leftFraction.Length ? leftFraction[i] : '0', b = i < rightFraction.Length ? rightFraction[i] : '0';
            if (a != b) return a < b ? -1 : 1;
        }
        return 0;
    }
    [Benchmark] public int ProviderCompare() => (int)DatatypeValues.Compare(left, right, fixture.Operation);
}
