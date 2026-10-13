using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml;
using Xemele.XPath;
using Candidate = Xemele.XmlDocument;
using Bcl = System.Xml.XmlDocument;

if (args.Length != 4 || args[0] is not ("xemele" or "bcl")) throw new ArgumentException("xemele|bcl fixture manifest output");
string engine = args[0]; byte[] input = File.ReadAllBytes(args[1]);
using var manifest = JsonDocument.Parse(File.ReadAllBytes(args[2]));
var fixture = manifest.RootElement.GetProperty("fixtures").EnumerateArray().Single(r => Path.GetFileName(r.GetProperty("path").GetString()) == Path.GetFileName(args[1]));
if (Convert.ToHexStringLower(SHA256.HashData(input)) != fixture.GetProperty("sha256").GetString()) throw new InvalidDataException("Fixture hash differs.");
string expected = fixture.GetProperty("semantic").GetProperty("sha256").GetString()!;
string expression = fixture.GetProperty("xpath").GetString()!;
double expectedXPath = fixture.GetProperty("xpathExpected").GetDouble();

// Warm parsing, serializer, query and digest implementation before any counter baseline.
Meter.Held = Parse(engine, input); Verify(engine, Meter.Held, expected, expression, expectedXPath); Meter.Held = null;
Collect();
long beforeControl = GC.GetTotalMemory(true);
Meter.Held = RetainedControl(); long controlLive = GC.GetTotalMemory(true) - beforeControl;
if (controlLive < 9 * 1024 * 1024) throw new InvalidDataException("Retained-memory meter missed the 9 MiB control.");
GC.KeepAlive(Meter.Held); Meter.Held = null; Collect();
var rows = new List<object>();
for (int cycle = 0; cycle < 3; cycle++)
{
    Collect(); long baseline = GC.GetTotalMemory(true), allocationStart = GC.GetAllocatedBytesForCurrentThread();
    Meter.Held = Parse(engine, input);
    long allocationBytes = GC.GetAllocatedBytesForCurrentThread() - allocationStart;
    long retainedBytes = GC.GetTotalMemory(true) - baseline;
    string digest = Verify(engine, Meter.Held, expected, expression, expectedXPath);
    if (retainedBytes <= 0 || allocationBytes < retainedBytes) throw new InvalidDataException("Invalid retention/allocation counters.");
    GC.KeepAlive(Meter.Held); Meter.Held = null; Collect();
    long releasedDelta = GC.GetTotalMemory(true) - baseline;
    rows.Add(new { cycle, retainedBytes, allocationBytes, releasedDelta, digest });
}
var output = new { engine, fixture = fixture.GetProperty("id").GetString(), inputBytes = input.Length,
    runtime = RuntimeInformation.FrameworkDescription, architecture = RuntimeInformation.ProcessArchitecture.ToString(),
    os = RuntimeInformation.OSDescription, productionSha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(typeof(Candidate).Assembly.Location))),
    workerSha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(typeof(Meter).Assembly.Location))),
    controlLive, rows, expectedXPath, expectedSemanticSha256 = expected, controlledTimingCredit = false };
File.WriteAllText(args[3], JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine(JsonSerializer.Serialize(new { engine, fixture = fixture.GetProperty("id").GetString(), controlLive, rows }));

[MethodImpl(MethodImplOptions.NoInlining)]
static object RetainedControl() { byte[] value = new byte[9 * 1024 * 1024]; value[0] = 42; value[^1] = 43; return value; }
static void Collect() { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); }
[MethodImpl(MethodImplOptions.NoInlining)]
static object Parse(string engine, byte[] input)
{
    if (engine == "xemele") return Candidate.Parse(input, new() { DtdProcessing = Xemele.XmlDtdProcessing.Parse });
    var document = new Bcl { PreserveWhitespace = true, XmlResolver = null };
    using var stream = new MemoryStream(input, writable: false);
    using var reader = System.Xml.XmlReader.Create(stream, new XmlReaderSettings
    { DtdProcessing = DtdProcessing.Parse, XmlResolver = null, IgnoreWhitespace = false, MaxCharactersInDocument = 16777216, MaxCharactersFromEntities = 16777216 });
    document.Load(reader); return document;
}
static string Verify(string engine, object value, string expected, string expression, double expectedXPath)
{
    string digest;
    if (engine == "xemele")
    {
        var document = (Candidate)value; digest = Semantic.Candidate(document.RootElement);
        var context = new XPathContext { Limits = new() { MaxInputItems = 1000000, MaxNodeSetItems = 1000000, MaxOperations = 100000000, MaxInputCharacters = 16777216, MaxStringCharacters = 16777216 } };
        if (XPathExpression.Compile(expression).Evaluate(document.DocumentNode, context).AsNumber() != expectedXPath) throw new InvalidDataException("Candidate XPath oracle differs.");
        using var bytes = new MemoryStream(); document.Save(bytes);
        if (Semantic.Candidate(Candidate.Parse(bytes.ToArray(), new() { DtdProcessing = Xemele.XmlDtdProcessing.Parse })) != expected)
            throw new InvalidDataException("Candidate serialization roundtrip differs.");
    }
    else
    {
        var document = (Bcl)value; digest = Semantic.Bcl(document.DocumentElement!);
        if ((double)document.CreateNavigator()!.Evaluate(expression) != expectedXPath) throw new InvalidDataException("BCL XPath oracle differs.");
        using var bytes = new MemoryStream();
        using (var writer = System.Xml.XmlWriter.Create(bytes, new XmlWriterSettings { Encoding = new UTF8Encoding(false), CloseOutput = false, Indent = false })) document.Save(writer);
        var loaded = (Bcl)Parse("bcl", bytes.ToArray());
        if (Semantic.Bcl(loaded.DocumentElement!) != expected) throw new InvalidDataException("BCL serialization roundtrip differs.");
    }
    if (digest != expected) throw new InvalidDataException($"{engine} semantic digest differs: {digest} != {expected}");
    return digest;
}

static class Meter { internal static object? Held; }
static class Semantic
{
    internal static string Candidate(Candidate document) => Candidate(document.RootElement);
    internal static string Candidate(Xemele.XmlNode node)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256); Visit(node, hash); return Convert.ToHexStringLower(hash.GetHashAndReset());
    }
    internal static string Bcl(System.Xml.XmlNode node)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256); Visit(node, hash); return Convert.ToHexStringLower(hash.GetHashAndReset());
    }
    private static void Frame(IncrementalHash hash, char kind, params string[] values)
    {
        hash.AppendData([(byte)kind]);
        Span<byte> length = stackalloc byte[4];
        foreach (string value in values)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(length, bytes.Length); hash.AppendData(length); hash.AppendData(bytes);
        }
    }
    private static void Visit(Xemele.XmlNode node, IncrementalHash hash)
    {
        if (node.Kind == Xemele.XmlNodeKind.Comment) { Frame(hash, 'C', node.Value); return; }
        if (node.Kind == Xemele.XmlNodeKind.ProcessingInstruction) { Frame(hash, 'P', node.LocalName, node.Value); return; }
        Frame(hash, 'S', node.NamespaceUri, node.LocalName);
        var attributes = new List<(string Uri, string Local, string Value)>();
        foreach (var a in node.Attributes) attributes.Add((a.NamespaceUri, a.LocalName, a.Value));
        foreach (var a in attributes.OrderBy(a => a.Uri, StringComparer.Ordinal).ThenBy(a => a.Local, StringComparer.Ordinal)) Frame(hash, 'A', a.Uri, a.Local, a.Value);
        var text = new StringBuilder();
        foreach (var child in node.Children)
        {
            if (child.Kind is Xemele.XmlNodeKind.Text or Xemele.XmlNodeKind.CData) text.Append(child.Value);
            else { Flush(); Visit(child, hash); }
        }
        Flush(); Frame(hash, 'E');
        void Flush() { if (text.Length != 0) { Frame(hash, 'T', text.ToString()); text.Clear(); } }
    }
    private static void Visit(System.Xml.XmlNode node, IncrementalHash hash)
    {
        if (node.NodeType == XmlNodeType.Comment) { Frame(hash, 'C', node.Value!); return; }
        if (node.NodeType == XmlNodeType.ProcessingInstruction) { Frame(hash, 'P', node.LocalName, node.Value!); return; }
        Frame(hash, 'S', node.NamespaceURI, node.LocalName);
        if (node.Attributes is { } attributes)
            foreach (XmlAttribute a in attributes.Cast<XmlAttribute>().Where(a => a.Name != "xmlns" && a.Prefix != "xmlns").OrderBy(a => a.NamespaceURI, StringComparer.Ordinal).ThenBy(a => a.LocalName, StringComparer.Ordinal)) Frame(hash, 'A', a.NamespaceURI, a.LocalName, a.Value);
        var text = new StringBuilder();
        foreach (System.Xml.XmlNode child in node.ChildNodes)
        {
            if (child.NodeType is XmlNodeType.Text or XmlNodeType.CDATA or XmlNodeType.Whitespace or XmlNodeType.SignificantWhitespace) text.Append(child.Value);
            else { Flush(); Visit(child, hash); }
        }
        Flush(); Frame(hash, 'E');
        void Flush() { if (text.Length != 0) { Frame(hash, 'T', text.ToString()); text.Clear(); } }
    }
}
