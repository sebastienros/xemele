using BenchmarkDotNet.Attributes;
using System.Text;
using Xemele.Streaming;
namespace Xemele.Benchmarks.Reader;

[MemoryDiagnoser]
public class ReaderBenchmarks
{
    private readonly XmlReader.Cursor cursor = new();
    private string value = "";
    private byte[] document = [];
    [Params(8, 4096)]
    public int Length
    {
        get; set;
    }
    [GlobalSetup]
    public void Setup()
    {
        value = new string('Ω', Length - 2) + "😀";
        cursor.Text(value, default);
        cursor.Clear();
        document = Encoding.UTF8.GetBytes("<r a='v'>" + value + "</r>");
    }
    [Benchmark(Baseline = true)] public string OwnedCopyBaseline() => new(value.AsSpan());
    [Benchmark]
    public int BorrowedCurrentCopy()
    {
        cursor.Clear();
        cursor.Text(value, default);
        return cursor.Get(0).Length;
    }
    [Benchmark]
    public int EndToEndOwnedSetupAndRows()
    {
        using var input = new MemoryStream(document, false);
        using var reader = XmlReader.Create(input);
        var count = 0;
        while (reader.Read())
        {
            count += reader.ValueSpan.Length + reader.Attributes.Count;
        }

        return count;
    }
}
