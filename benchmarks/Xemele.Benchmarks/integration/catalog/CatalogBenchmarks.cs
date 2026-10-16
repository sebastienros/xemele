using BenchmarkDotNet.Attributes;
namespace Xemele.Benchmarks.M6Catalog;

[MemoryDiagnoser, ShortRunJob]
public class CatalogBenchmarks
{
    [Params(8, 128)]
    public int Count
    {
        get; set;
    }
    private XmlCatalog catalog = null!;
    private string source = null!;
    private XmlResourceRequest miss = null!;
    private XmlResourceRequest request = null!;
    private KeyValuePair<string, Uri>[] baseline = null!;
    public static string Fixture(int count)
    {
        var xml = new System.Text.StringBuilder("<catalog xmlns='urn:oasis:names:tc:entity:xmlns:xml:catalog'>");
        for (var i = 0; i < count; i++)
        {
            xml.Append($"<system systemId='key-{i}' uri='https://mapped/{i}'/>");
        }

        xml.Append("<public publicId='A B' uri='https://mapped/public'/><uri name='https://request/' uri='https://mapped/uri'/><rewriteSystem systemIdStartString='rewrite:' rewritePrefix='https://mapped/rewrite/'/></catalog>");
        return xml.ToString();
    }
    [GlobalSetup]
    public void Setup()
    {
        source = Fixture(Count);
        catalog = XmlCatalog.Parse(source);
        request = new(new("https://request/"), null, XmlResourceKind.ExternalEntity)
        {
            SystemId = "key-" + (Count - 1)
        };
        miss = request with
        {
            SystemId = "absent"
        };
        baseline = new KeyValuePair<string, Uri>[Count];
        for (var i = 0; i < Count; i++)
        {
            baseline[i] = new("key-" + i, new("https://mapped/" + i));
        }
    }
    [Benchmark(Baseline = true)]
    public Uri? StraightforwardExact()
    {
        foreach (var entry in baseline)
        {
            if (entry.Key == request.SystemId)
            {
                return entry.Value;
            }
        }

        return null;
    }
    [Benchmark] public Uri? CatalogExact() => catalog.Resolve(request);
    [Benchmark] public Uri? CatalogMiss() => catalog.Resolve(miss);
    [Benchmark] public XmlCatalog OwnedParse() => XmlCatalog.Parse(source);
}
