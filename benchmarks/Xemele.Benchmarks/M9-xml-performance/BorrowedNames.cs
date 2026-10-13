using System.Runtime.InteropServices;
using System.IO.Hashing;
using BenchmarkDotNet.Attributes;

namespace Xemele.XmlPerformance;

[MemoryDiagnoser, ShortRunJob]
public class BorrowedNames
{
    private static readonly string[] Html = ["html", "head", "body", "div", "span", "p", "a", "img", "ul", "li", "table", "tr", "td", "script", "style", "class", "id", "href", "src", "title", "type", "name", "value"];
    private readonly Dictionary<string, string> names = new(StringComparer.Ordinal);
    private readonly string?[] current = new string?[64], packed = new string?[64], fixedCache = new string?[64], hashed = new string?[64], hybridPrimary = new string?[64], hybridSecondary = new string?[64];
    private string[] inputs = [];
    private string last = "";
    [Params("html", "xml", "prefix", "unicode", "long")] public string Shape { get; set; } = "html";
    [GlobalSetup] public void Setup()
    {
        inputs = new string[128]; names.Clear(); Array.Clear(current); Array.Clear(packed); Array.Clear(fixedCache); Array.Clear(hashed); Array.Clear(hybridPrimary); Array.Clear(hybridSecondary);
        for (int i = 0; i < inputs.Length; i++)
        {
            string name = Shape switch
            {
                "html" => Html[i % Html.Length],
                "xml" => "element" + i % 32,
                "prefix" => "item" + (i % 32).ToString("D8", System.Globalization.CultureInfo.InvariantCulture),
                "unicode" => "名前" + i % 32 + "α",
                _ => new string('x', 80) + i % 32
            };
            inputs[i] = name;
            names.TryAdd(name, name);
        }
        if (CurrentCache() != PackedCache() || CurrentCache() != KnownHtmlCache() || CurrentCache() != MaterializeEach() || CurrentCache() != XxHashCache() || CurrentCache() != HybridCache()) throw new InvalidOperationException("Name oracle differs.");
        var allInputs = inputs;
        foreach (string expected in allInputs)
        {
            inputs = [new string(expected.AsSpan())];
            foreach (Func<int> method in new Func<int>[] { CurrentCache, PackedCache, KnownHtmlCache, XxHashCache, HybridCache, MaterializeEach })
            { method(); if (!last.AsSpan().SequenceEqual(expected)) throw new InvalidOperationException("Exact name differs."); }
        }
        inputs = allInputs;
        foreach (string known in Html) if (!ReferenceEquals(Known(known), known)) throw new InvalidOperationException("Known names must return literals.");
        if (Known("DIV") is not null || Known("divx") is not null || Known("dív") is not null) throw new InvalidOperationException("Unknown name accepted.");
    }
    [Benchmark(Baseline = true, OperationsPerInvoke = 128)]
    public int CurrentCache()
    {
        int total = 0;
        foreach (string name in inputs)
        {
            var span = name.AsSpan(); int slot = span.Length <= 64 ? (span.Length * 17 + span[0]) & 63 : -1;
            string owned;
            if (slot >= 0 && current[slot] is string cached && span.SequenceEqual(cached)) owned = cached;
            else { owned = names.GetAlternateLookup<ReadOnlySpan<char>>()[span]; if (slot >= 0) current[slot] = owned; }
            last = owned; total += owned.Length;
        }
        return total;
    }
    [Benchmark(OperationsPerInvoke = 128)] public int PackedCache() => RunPacked(false);
    [Benchmark(OperationsPerInvoke = 128)] public int KnownHtmlCache() => RunPacked(true);
    [Benchmark(OperationsPerInvoke = 128)] public int XxHashCache()
    {
        int total = 0;
        foreach (string name in inputs)
        {
            var span = name.AsSpan();
            int slot = span.Length <= 4 ? Slot(span) : span.Length <= 64 ? (int)(XxHash3.HashToUInt64(MemoryMarshal.AsBytes(span)) & 63) : -1;
            string owned;
            if (slot >= 0 && hashed[slot] is string cached && span.SequenceEqual(cached)) owned = cached;
            else { owned = names.GetAlternateLookup<ReadOnlySpan<char>>()[span]; if (slot >= 0) hashed[slot] = owned; }
            last = owned; total += owned.Length;
        }
        return total;
    }
    [Benchmark(OperationsPerInvoke = 128)] public int HybridCache()
    {
        int total = 0;
        foreach (string name in inputs)
        {
            var span = name.AsSpan(); int slot = span.Length <= 64 ? (span.Length * 17 + span[0]) & 63 : -1;
            string owned; int second = -1;
            if (slot >= 0 && hybridPrimary[slot] is string cached && span.SequenceEqual(cached)) owned = cached;
            else
            {
                if (slot >= 0 && hybridPrimary[slot] is not null)
                {
                    second = string.GetHashCode(span) & 63;
                    if (hybridSecondary[second] is string secondary && span.SequenceEqual(secondary))
                    { hybridPrimary[slot] = secondary; last = secondary; total += secondary.Length; continue; }
                }
                owned = names.GetAlternateLookup<ReadOnlySpan<char>>()[span];
                if (slot >= 0) hybridPrimary[slot] = owned;
                if (second >= 0) hybridSecondary[second] = owned;
            }
            last = owned; total += owned.Length;
        }
        return total;
    }
    [Benchmark(OperationsPerInvoke = 128)] public int MaterializeEach()
    {
        int total = 0;
        foreach (string name in inputs) { last = new string(name.AsSpan()); total += last.Length; }
        return total;
    }
    private int RunPacked(bool known)
    {
        int total = 0; var cache = known ? fixedCache : packed;
        foreach (string name in inputs)
        {
            var span = name.AsSpan(); string? owned = known ? Known(span) : null;
            if (owned is null)
            {
                int slot = span.Length <= 64 ? Slot(span) : -1;
                if (slot >= 0 && cache[slot] is string cached && span.SequenceEqual(cached)) owned = cached;
                else { owned = names.GetAlternateLookup<ReadOnlySpan<char>>()[span]; if (slot >= 0) cache[slot] = owned; }
            }
            last = owned; total += owned.Length;
        }
        return total;
    }
    internal static int Slot(ReadOnlySpan<char> name)
    {
        ulong prefix = 0;
        if (name.Length >= 4) prefix = MemoryMarshal.Read<ulong>(MemoryMarshal.AsBytes(name[..4]));
        else for (int i = 0; i < name.Length; i++) prefix |= (ulong)name[i] << (16 * i);
        uint hash = (uint)prefix ^ (uint)(prefix >> 32); hash ^= hash >> 16;
        hash = unchecked(hash * 2654435761u);
        return (int)((hash >> 26) ^ (uint)name.Length) & 63;
    }
    internal static string? Known(ReadOnlySpan<char> name) => name switch
    {
        "html" => "html", "head" => "head", "body" => "body", "div" => "div", "span" => "span", "p" => "p", "a" => "a", "img" => "img", "ul" => "ul", "li" => "li", "table" => "table", "tr" => "tr", "td" => "td", "script" => "script", "style" => "style", "class" => "class", "id" => "id", "href" => "href", "src" => "src", "title" => "title", "type" => "type", "name" => "name", "value" => "value", _ => null
    };
}
