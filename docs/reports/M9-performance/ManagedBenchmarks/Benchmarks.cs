using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml;
using BenchmarkDotNet.Attributes;
using Xemele.Streaming;
using Xemele.XPath;
using Candidate = Xemele.XmlDocument;
using BclDocument = System.Xml.XmlDocument;
using BclReader = System.Xml.XmlReader;
using CandidateReader = Xemele.Streaming.XmlReader;

namespace Xemele.ReleaseBenchmarks;

internal static class ReleaseInputs
{
    internal static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "global.json"))) return directory.FullName;
        throw new InvalidOperationException("Release source root unavailable.");
    }
    private static JsonDocument Manifest()
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine(Root(), "benchmarks/baselines/release/fixtures.json"));
        if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != "d14e74167c1d552be721c121e6a6c6a267d30e2ad4a53d3ef71ce82031363fb0")
            throw new InvalidDataException("Frozen fixture manifest changed.");
        return JsonDocument.Parse(bytes);
    }
    internal static IEnumerable<string> FixtureIds()
    {
        using var manifest = Manifest();
        return manifest.RootElement.GetProperty("fixtures").EnumerateArray().Select(f => f.GetProperty("id").GetString()!).ToArray();
    }
    internal static (byte[] Bytes, string XPath, double Expected, int Elements) Load(string id)
    {
        using var manifest = Manifest();
        var fixture = manifest.RootElement.GetProperty("fixtures").EnumerateArray().Single(f => f.GetProperty("id").GetString() == id);
        byte[] bytes = File.ReadAllBytes(Path.Combine(Root(), "benchmarks/baselines/release", fixture.GetProperty("path").GetString()!));
        if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != fixture.GetProperty("sha256").GetString()) throw new InvalidDataException("Frozen bytes changed.");
        return (bytes, fixture.GetProperty("xpath").GetString()!, fixture.GetProperty("xpathExpected").GetDouble(), fixture.GetProperty("semantic").GetProperty("elements").GetInt32());
    }
    internal static XmlReaderSettings ReaderOptions() => new()
    { DtdProcessing = DtdProcessing.Parse, XmlResolver = null, IgnoreWhitespace = false, IgnoreComments = false, IgnoreProcessingInstructions = false, MaxCharactersInDocument = 16777216 };
    internal static XmlParseOptions CandidateOptions() => new() { DtdProcessing = XmlDtdProcessing.Parse };
    internal static object Parse(string engine, byte[] bytes)
    {
        if (engine == "xemele") return Candidate.Parse(bytes, CandidateOptions());
        using var stream = new MemoryStream(bytes, writable: false);
        using var reader = BclReader.Create(stream, ReaderOptions());
        var document = new BclDocument { PreserveWhitespace = true, XmlResolver = null }; document.Load(reader); return document;
    }
}

[MemoryDiagnoser, ShortRunJob]
public class ManagedDomBenchmarks
{
    public IEnumerable<string> Fixtures => ReleaseInputs.FixtureIds();
    [ParamsSource(nameof(Fixtures))] public string Fixture { get; set; } = "feed-small";
    [Params("xemele", "bcl")] public string Engine { get; set; } = "xemele";
    private byte[] input = [];
    private object document = null!;
    private Xemele.XPath.XPathExpression? candidateExpression;
    private System.Xml.XPath.XPathExpression? bclExpression;
    private System.Xml.XPath.XPathNavigator? bclNavigator;
    private readonly XPathContext context = new()
    { Limits = new() { MaxInputItems = 1000000, MaxNodeSetItems = 1000000, MaxOperations = 100000000, MaxInputCharacters = 33554432, MaxStringCharacters = 16777216 } };
    public double ExpectedXPath { get; private set; }
    [GlobalSetup]
    public void Setup()
    {
        var fixture = ReleaseInputs.Load(Fixture); input = fixture.Bytes; ExpectedXPath = fixture.Expected;
        document = ReleaseInputs.Parse(Engine, input);
        if (Engine == "xemele") candidateExpression = Xemele.XPath.XPathExpression.Compile(fixture.XPath);
        else { bclNavigator = ((BclDocument)document).CreateNavigator()!; bclExpression = bclNavigator.Compile(fixture.XPath); }
        if (EvaluateCompiledXPath() != ExpectedXPath) throw new InvalidDataException("Precompiled query oracle differs.");
    }
    [Benchmark] public object ParseOwnedDom() => ReleaseInputs.Parse(Engine, input);
    // Explicit full collection is a separately named lifecycle diagnostic. Its
    // cost is reported separately from steady parsing and batch GC diagnostics.
    [Benchmark] public int ParseWithFullCollection()
    {
        int consumed = CreateAndDrop(); GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); return consumed;
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private int CreateAndDrop()
    {
        object value = ReleaseInputs.Parse(Engine, input);
        int consumed = value is Candidate candidate ? candidate.RootElement.LocalName.Length : ((BclDocument)value).DocumentElement!.LocalName.Length;
        GC.KeepAlive(value); return consumed;
    }
    [Benchmark] public long SerializeUtf8()
    {
        using var output = new MemoryStream();
        if (document is Candidate candidate) candidate.Save(output);
        else
        {
            using var writer = System.Xml.XmlWriter.Create(output, new XmlWriterSettings { Encoding = new UTF8Encoding(false), CloseOutput = false, Indent = false });
            ((BclDocument)document).Save(writer);
        }
        return output.Length;
    }
    [Benchmark] public double EvaluateCompiledXPath()
    {
        double value = document is Candidate candidate ? candidateExpression!.Evaluate(candidate.DocumentNode, context).AsNumber() : (double)bclNavigator!.Evaluate(bclExpression!);
        if (value != ExpectedXPath) throw new InvalidDataException("Query result differs.");
        return value;
    }
}

[MemoryDiagnoser, ShortRunJob]
public class ManagedForwardBenchmarks
{
    public IEnumerable<string> Fixtures => ReleaseInputs.FixtureIds();
    [ParamsSource(nameof(Fixtures))] public string Fixture { get; set; } = "feed-small";
    [Params("xemele-reader", "xemele-sax", "bcl-reader")] public string Engine { get; set; } = "xemele-reader";
    private byte[] input = [];
    private int expectedElements;
    public long ExpectedCharacters { get; private set; }
    [GlobalSetup]
    public void Setup()
    {
        var fixture = ReleaseInputs.Load(Fixture); input = fixture.Bytes; expectedElements = fixture.Elements;
        var document = (BclDocument)ReleaseInputs.Parse("bcl", input); ExpectedCharacters = document.DocumentElement!.InnerText.Length;
    }
    [Benchmark] public long ReadAll()
    {
        using var stream = new MemoryStream(input, writable: false);
        long characters = 0; int starts = 0, ends = 0, depth = 0;
        if (Engine == "xemele-sax")
        {
            var counter = new Counter(); XmlSaxParser.Parse(stream, counter, ReleaseInputs.CandidateOptions());
            characters = counter.Characters; starts = counter.Starts; ends = counter.Ends;
        }
        else if (Engine == "xemele-reader")
        {
            using var reader = CandidateReader.Create(stream, ReleaseInputs.CandidateOptions());
            while (reader.Read())
                switch (reader.TokenKind)
                {
                    case XmlTokenKind.StartElement: starts++; depth++; break;
                    case XmlTokenKind.EndElement: ends++; depth--; break;
                    case XmlTokenKind.Text: if (depth > 0) characters += reader.ValueSpan.Length; break;
                }
        }
        else
        {
            using var reader = BclReader.Create(stream, ReleaseInputs.ReaderOptions());
            while (reader.Read())
                switch (reader.NodeType)
                {
                    case XmlNodeType.Element: starts++; if (reader.IsEmptyElement) ends++; else depth++; break;
                    case XmlNodeType.EndElement: ends++; depth--; break;
                    case XmlNodeType.Text or XmlNodeType.CDATA or XmlNodeType.Whitespace or XmlNodeType.SignificantWhitespace:
                        if (depth > 0) characters += reader.Value.Length; break;
                }
        }
        if (starts != expectedElements || ends != starts || characters != ExpectedCharacters) throw new InvalidDataException("Forward element/text oracle differs.");
        return characters;
    }
    private sealed class Counter : XmlSaxHandler
    {
        internal int Starts, Ends, Depth;
        internal long Characters;
        public override void StartElement(XmlStartElement element) { Starts++; Depth++; }
        public override void EndElement(XmlEndElement element) { Ends++; Depth--; }
        public override void Text(ReadOnlySpan<char> value) { if (Depth > 0) Characters += value.Length; }
    }
}
