using BenchmarkDotNet.Attributes;

namespace Xemele.XmlPerformance;

// Resolved names are already document-local atoms in XmlParserCore.
[MemoryDiagnoser, ShortRunJob]
public class AttributeDuplicateFilters
{
    private string[] names = [], uris = [];
    [Params(4, 16, 64)] public int Count { get; set; }
    [Params("distinct", "collision", "duplicate", "other-uri", "common")] public string Shape { get; set; } = "distinct";
    [GlobalSetup] public void Setup()
    {
        names = new string[Count]; uris = new string[Count];
        for (int i = 0; i < Count; i++)
        {
            string start = "attr" + i.ToString("D4", System.Globalization.CultureInfo.InvariantCulture);
            int bucket = Shape == "collision" ? 0 : i & 63;
            char suffix = (char)(0x400 + ((bucket - ((start.Length + 1) * 17 + start[0])) & 63));
            names[i] = start + suffix;
            if (Shape == "common") names[i] = new[] { "id", "name", "value", "type", "status", "price", "quantity", "code" }[i & 7] + i.ToString(System.Globalization.CultureInfo.InvariantCulture);
            uris[i] = "urn:test";
            if (Shape == "other-uri") { names[i] = "same"; uris[i] = "urn:" + i.ToString(System.Globalization.CultureInfo.InvariantCulture); }
        }
        if (Shape == "duplicate") { names[^1] = names[0]; uris[^1] = uris[0]; }
        bool expected = Shape == "duplicate";
        if (Pairwise() != expected || Bitmask() != expected || FirstCharacter() != expected) throw new InvalidOperationException("Duplicate filter oracle differs.");
        if (Shape is "distinct" or "collision")
        {
            ulong bits = 0;
            foreach (string name in names) bits |= 1UL << Bucket(name);
            int expectedBits = Shape == "collision" ? 1 : Count;
            if (System.Numerics.BitOperations.PopCount(bits) != expectedBits) throw new InvalidOperationException("Prepared hash distribution differs.");
        }
    }
    private static int Bucket(string value) => unchecked((value.Length * 17 + value[0] + value[^1]) & 63);
    [Benchmark(Baseline = true)] public bool Pairwise()
    {
        for (int n = 0; n < names.Length; n++)
            for (int p = 0; p < n; p++)
                if (ReferenceEquals(names[p], names[n]) && ReferenceEquals(uris[p], uris[n])) return true;
        return false;
    }
    [Benchmark] public bool FirstCharacter()
    {
        ulong seen = 0;
        for (int n = 0; n < names.Length; n++)
        {
            ulong bit = 1UL << (names[n][0] & 63);
            if ((seen & bit) == 0) { seen |= bit; continue; }
            for (int p = 0; p < n; p++)
                if (ReferenceEquals(names[p], names[n]) && ReferenceEquals(uris[p], uris[n])) return true;
        }
        return false;
    }
    [Benchmark] public bool Bitmask()
    {
        ulong seen = 0;
        for (int n = 0; n < names.Length; n++)
        {
            ulong bit = 1UL << Bucket(names[n]);
            if ((seen & bit) == 0) { seen |= bit; continue; }
            for (int p = 0; p < n; p++)
                if (ReferenceEquals(names[p], names[n]) && ReferenceEquals(uris[p], uris[n])) return true;
        }
        return false;
    }
}
