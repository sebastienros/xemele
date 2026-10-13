using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;

namespace Xemele.XmlPerformance;

[MemoryDiagnoser, ShortRunJob]
public class NameCaches
{
    private sealed record Name(string Local, string Uri, string Prefix);
    private Name[] inputs = [];
    private readonly Name?[] linear = new Name?[8], direct = new Name?[64], composite = new Name?[64];
    private readonly Dictionary<(string, string, string), Name> names = new();
    private int cursor;
    [Params(8, 32, 128)] public int Vocabulary { get; set; }
    [Params("identity", "copies", "namespaces")] public string Shape { get; set; } = "identity";
    [GlobalSetup] public void Setup()
    {
        names.Clear(); Array.Clear(linear); Array.Clear(direct); Array.Clear(composite); cursor = 0;
        inputs = new Name[Vocabulary];
        string commonUri = "urn:" + new string('u', 128), commonLocal = "item";
        for (int i = 0; i < inputs.Length; i++)
        {
            string local = Shape == "namespaces" ? commonLocal : "element" + i;
            string uri = Shape == "namespaces" ? commonUri + i : commonUri;
            var name = new Name(local, uri, "p"); names.Add((local, uri, "p"), name);
            inputs[i] = Shape == "copies" ? new(new string(local.AsSpan()), new string(uri.AsSpan()), new string("p".AsSpan())) : name;
        }
        if (Linear() != SingleProbe() || Linear() != CompositeProbe()) throw new InvalidOperationException("Cache oracle differs.");
    }
    [Benchmark(Baseline = true)] public int Linear()
    {
        int total = 0;
        foreach (var input in inputs)
        {
            Name? found = null;
            foreach (var cached in linear)
                if (cached is not null && ReferenceEquals(cached.Local, input.Local) && ReferenceEquals(cached.Uri, input.Uri) && ReferenceEquals(cached.Prefix, input.Prefix)) { found = cached; break; }
            if (found is null) { found = names[(input.Local, input.Uri, input.Prefix)]; linear[cursor] = found; cursor = (cursor + 1) & 7; }
            total += found.Local.Length;
        }
        return total;
    }
    [Benchmark] public int SingleProbe()
    {
        int total = 0;
        foreach (var input in inputs)
        {
            int bucket = RuntimeHelpers.GetHashCode(input.Local) & 63;
            var found = direct[bucket];
            if (found is null || !ReferenceEquals(found.Local, input.Local) || !ReferenceEquals(found.Uri, input.Uri) || !ReferenceEquals(found.Prefix, input.Prefix))
            { found = names[(input.Local, input.Uri, input.Prefix)]; direct[bucket] = found; }
            total += found.Local.Length;
        }
        return total;
    }    [Benchmark] public int CompositeProbe()
    {
        int total = 0;
        foreach (var input in inputs)
        {
            int bucket = (RuntimeHelpers.GetHashCode(input.Local) ^ RuntimeHelpers.GetHashCode(input.Uri)) & 63;
            var found = composite[bucket];
            if (found is null || !ReferenceEquals(found.Local, input.Local) || !ReferenceEquals(found.Uri, input.Uri) || !ReferenceEquals(found.Prefix, input.Prefix))
            { found = names[(input.Local, input.Uri, input.Prefix)]; composite[bucket] = found; }
            total += found.Local.Length;
        }
        return total;
    }
}
