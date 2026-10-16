using System.Text;
using BenchmarkDotNet.Attributes;
namespace Xemele.XmlPerformance;

[MemoryDiagnoser, ShortRunJob]
public class TextOwnership
{
    private readonly StringBuilder builder = new();
    private string value = "";
    [Params(1, 8, 128, 4096)]
    public int Length
    {
        get; set;
    }
    [Params(false, true)]
    public bool Multilingual
    {
        get; set;
    }
    [GlobalSetup]
    public void Setup()
    {
        value = Multilingual ? string.Concat(Enumerable.Repeat("α日本語😀", Length / 6 + 1))[..Length] : new string('x', Length);
        builder.EnsureCapacity(Length);
        if (ScratchCopy() != DirectOwned())
        {
            throw new InvalidOperationException("Owned text differs.");
        }
    }
    [Benchmark(Baseline = true)]
    public string ScratchCopy()
    {
        builder.Clear();
        builder.Append(value.AsSpan());
        return builder.ToString();
    }
    [Benchmark] public string DirectOwned() => value.AsSpan().ToString();
}
