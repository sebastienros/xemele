using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xemele;
using Xemele.Parsing.Dom;
using Xemele.Parsing.Dtd;
using Xemele.Parsing.Entities;
using Xemele.Parsing.Input;
using Xemele.Parsing.Syntax;

string root = Path.GetFullPath(args[0]);
string cache = Environment.GetEnvironmentVariable("XEMELE_CORPUS_RESOURCES") ?? "/private/tmp/xemele-corpus-cache/w3c-xml-20130923/resources";
const string prefix = "xmlconf/eduni/errata-2e/";
byte[] primary = Read("E38.xml", "b6be7039a5dfeaad8eaad16eb3976c754888985bdd598b518a81a0f9547e3cf6");
byte[] entity = Read("E38.ent", "028229ed9dda8481d7996ef03a1e17300255abef2bf082011312f09b28623eef");
using var expected = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root, "tests/fixtures/M2-conformance/ProfileVersionRepair/expected.json")));
var model = expected.RootElement;
int runs = 0;
// Primary one-byte submissions preserve exact decoder mappings; every external
// read size exercises text-declaration/body boundaries without changing bytes.
for (int size = 1; size <= entity.Length; size++)
{
    var resolver = new Grant(entity, size);
    Snapshot snapshot = Parse(primary, resolver, out var failure) ?? throw new InvalidOperationException("original rejection: " + failure?.Code);
    // Assert copied callback snapshots/owned DOM/DTD after decoder/core disposal.
    Equal(model.GetProperty("events").EnumerateArray().Select(x => x.GetString()!), snapshot.Events, "full events");
    Equal(model.GetProperty("dom").EnumerateArray().Select(x => x.GetString()!), Tree(snapshot.Document.DocumentNode, 0), "full DOM");
    Require(snapshot.Document.RootElement.GetTextContent() == "\n\n", "root text");
    var doctype = snapshot.Document.DocumentType!;
    Require(doctype.Name == "foo" && doctype.PublicId is null && doctype.SystemId is null && doctype.InternalSubset == model.GetProperty("internalSubset").GetString(), "owned DOCTYPE");
    var dtd = snapshot.Model;
    Require(ReferenceEquals(dtd, snapshot.Document.Dtd), "same owned DTD");
    Require(dtd.Elements.Count == 1 && dtd.GeneralEntities.Count == 1 && dtd.Attributes.Count == 0 && dtd.ParameterEntities.Count == 0 && dtd.Notations.Count == 0, "complete DTD tables");
    var element = dtd.Elements[0];
    var declaration = dtd.GeneralEntities[0];
    Require(element.Name == "foo" && !element.IsExternal && element.Content.Kind == DtdParticleKind.Any && element.Content.Name is null && element.Content.Occurrence == DtdOccurrence.Once && element.Content.Children.Count == 0 && Sink.Location(element.Location) == model.GetProperty("elementLocation").GetString(), "ELEMENT ANY");
    Require(declaration.Name == "e" && !declaration.IsParameter && !declaration.IsExternal && declaration.ReplacementText is null && declaration.SystemId == "E38.ent" && declaration.PublicId is null && declaration.NotationName is null && Sink.Location(declaration.Location) == model.GetProperty("entityLocation").GetString(), "ENTITY declaration");
    resolver.Check();
    runs++;
}
// Paired controls change only supplied external bytes; original input is intact.
foreach (string control in new[] { "<?xml version='1.' encoding='utf-8'?><foo/>", "<?xml version='1.x' encoding='utf-8'?><foo/>", "<?xml version='2.0' encoding='utf-8'?><foo/>", "<?xml version='1.1' encoding='utf-8'?><foo>&#x1;</foo>", "<?xml version='1.1' encoding='utf-8'?><foo>\u0001</foo>", "<?xml version='1.1' encoding='utf-8'?><foo>" })
{
    var resolver = new Grant(Encoding.UTF8.GetBytes(control), 1);
    var document = Parse(primary, resolver, out var diagnostic);
    Require(document is null && diagnostic is not null, "negative control rejects");
    Require(diagnostic!.Code == "XML.Syntax", "grammar rejection identity: " + diagnostic.Code);
    Require(diagnostic.Location.SourceUri == Sink.Entity, "external diagnostic identity");
    if (control.EndsWith("<foo>", StringComparison.Ordinal))
        Require(diagnostic.Location.ByteOffset == Encoding.UTF8.GetByteCount(control), "actual external EOF boundary");
    resolver.Check();
    runs++;
}
// Required grant denial cannot silently drop the nested element.
Require(!XmlDocument.TryParse(primary, out _, out var denied, new() { DtdProcessing = XmlDtdProcessing.Parse }, default) && denied.Count == 1 && denied[0].Code == "XML.ResourceDenied", "no implicit external access");
Console.WriteLine(JsonSerializer.Serialize(new
{
    acceptedModels = 46,
    rejectionControls = 6,
    denialControls = 1,
    runs,
    sourceRevision = typeof(XmlDocument).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion,
    runtime = RuntimeInformation.FrameworkDescription,
    runtimeBuild = typeof(object).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion,
    productSha256 = Hash(File.ReadAllBytes(typeof(XmlDocument).Assembly.Location)),
    runtimeSha256 = Hash(File.ReadAllBytes(typeof(object).Assembly.Location))
}));

static Snapshot? Parse(byte[] primary, Grant resolver, out XmlDiagnostic? diagnostic)
{
    var options = new XmlParseOptions { DtdProcessing = XmlDtdProcessing.Parse, Resolver = resolver };
    using var decoder = new XmlInputDecoder(options, Sink.Primary);
    using var core = new XmlParserCore(options, Sink.Primary);
    var sink = new Sink(new DomSink(options, decoder));
    char[] chars = new char[2];
    long[] offsets = new long[2];
    int position = 0;
    bool validated = false;
    diagnostic = null;
    while (true)
    {
        var decoded = decoder.Decode(position == primary.Length ? [] : primary.AsSpan(position, 1), chars, offsets, position == primary.Length, default);
        position += decoded.ConsumedBytes;
        Require(decoded.Diagnostic is null, "primary decode");
        if (decoded.WrittenCharacters > 0)
        {
            if (!validated)
            {
                sink.Dom.ValidateAbsentDeclaration(new(Sink.Primary, 1, 1, 0));
                validated = true;
            }
            var result = core.Append(chars.AsSpan(0, decoded.WrittenCharacters), offsets.AsSpan(0, decoded.WrittenCharacters), false, sink, default);
            diagnostic = result.Diagnostic ?? sink.Dom.Diagnostic;
            if (diagnostic is not null)
                return null;
            Require(result.Step == ScanStep.NeedInput, "primary append");
        }
        if (decoded.Step != DecodeStep.Completed)
            continue;
        core.CompleteByteSource(primary.Length);
        var final = core.Append([], [], true, sink, default);
        diagnostic = final.Diagnostic ?? sink.Dom.Diagnostic;
        if (diagnostic is not null)
            return null;
        Require(final.Step == ScanStep.Completed, "completion");
        return new(sink.Events.ToArray(), sink.Dom.Document, sink.Model!);
    }
}

byte[] Read(string file, string hash) { byte[] data = File.ReadAllBytes(Path.Combine(cache, prefix, file)); Require(Hash(data) == hash, "original hash " + file); return data; }
static string Hash(byte[] data) => Convert.ToHexStringLower(SHA256.HashData(data));
static void Require(bool condition, string label) { if (!condition) throw new InvalidOperationException(label); }
static void Equal(IEnumerable<string> expected, IEnumerable<string> actual, string label)
{
    if (!expected.SequenceEqual(actual))
        throw new InvalidOperationException(label + " differ");
}
static IEnumerable<string> Tree(XmlNode node, int depth)
{
    Require(node.NamespaceUri == "" && node.Prefix == "", "DOM namespace/prefix");
    yield return $"{depth}:{node.Kind}:{node.LocalName}:{node.Value}:{node.NamespaceUri}:{node.Attributes.Count}:{node.NamespaceDeclarations.Count}";
    XmlNode previous = default;
    foreach (var child in node.Children)
    {
        Require(child.Parent == node && child.PreviousSibling == previous, "DOM links");
        if (!previous.IsDefault)
            Require(previous.NextSibling == child, "DOM next link");
        foreach (var value in Tree(child, depth + 1))
            yield return value;
        previous = child;
    }
    Require(node.LastChild == previous && (previous.IsDefault || previous.NextSibling.IsDefault), "DOM last link");
}
sealed record Snapshot(string[] Events, XmlDocument Document, DtdModel Model);
sealed class Grant(byte[] bytes, int size) : XmlResourceResolver
{
    private int opens;
    private Tracked? stream;
    public override XmlResolvedResource Resolve(XmlResourceRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.Uri != Sink.Entity || request.SystemId != "E38.ent" || request.PublicId is not null || request.Kind != XmlResourceKind.ExternalEntity || opens++ != 0)
            throw new IOException("Unmapped resource");
        stream = new(bytes, size);
        return new(stream, Sink.Entity);
    }
    internal void Check()
    {
        if (opens != 1 || stream?.Disposals != 1)
            throw new InvalidOperationException("one grant/open/disposal");
    }
}
sealed class Tracked(byte[] bytes, int size) : MemoryStream(bytes, false)
{
    internal int Disposals;
    public override int Read(Span<byte> buffer) => base.Read(buffer[..Math.Min(size, buffer.Length)]);
    protected override void Dispose(bool disposing)
    {
        if (disposing)
            Disposals++;
        base.Dispose(disposing);
    }
}
sealed class Sink(DomSink dom) : IXmlEventSink, IXmlDtdSink
{
    internal static readonly Uri Primary = new("https://xemele.invalid/corpus/xmlconf/eduni/errata-2e/E38.xml");
    internal static readonly Uri Entity = new("https://xemele.invalid/corpus/xmlconf/eduni/errata-2e/E38.ent");
    internal DomSink Dom = dom;
    internal readonly List<string> Events = [];
    internal DtdModel? Model;
    private readonly StringBuilder text = new();
    private string header = "";
    internal static string Location(XmlLocation p) => $"{(p.SourceUri == Primary ? "P" : p.SourceUri == Entity ? "E" : throw new InvalidOperationException("unexpected source"))}:{p.LineNumber}:{p.LinePosition}:{p.ByteOffset}";
    public bool StartDocument(XmlLocation p)
    {
        Events.Add("document:" + Location(p));
        return Dom.StartDocument(p);
    }
    public bool Declaration(XmlDeclarationView d) => throw new InvalidOperationException("unexpected primary declaration");
    public bool DocumentType(XmlDocumentType d, DtdModel m, XmlLocation p)
    {
        Model = m;
        Events.Add($"doctype:{d.Name}:{Location(p)}");
        return Dom.DocumentType(d, m, p);
    }
    public bool StartElement(XmlNameView n, bool empty, XmlLocation p)
    {
        Events.Add($"start:{n.LocalName}:{n.NamespaceUri}:{n.Prefix}:{empty}:{Location(p)}");
        return Dom.StartElement(n, empty, p);
    }
    public bool NamespaceDeclaration(XmlNamespaceView d) => throw new InvalidOperationException("unexpected namespace");
    public bool Attribute(XmlAttributeView a) => throw new InvalidOperationException("unexpected attribute");
    public bool StartElementContent()
    {
        Events.Add("content");
        return Dom.StartElementContent();
    }
    public bool EndElement(XmlNameView n, XmlLocation p)
    {
        Events.Add($"end:{n.LocalName}:{n.NamespaceUri}:{n.Prefix}:{Location(p)}");
        return Dom.EndElement(n, p);
    }
    public bool StartText(XmlTextKind kind, ReadOnlySpan<char> target, XmlLocation p)
    {
        text.Clear();
        header = $"text:{kind}:{target}:{Location(p)}:";
        return Dom.StartText(kind, target, p);
    }
    public bool TextFragment(ReadOnlySpan<char> value, bool ended, XmlLocation p)
    {
        text.Append(value);
        if (ended)
            Events.Add(header + text);
        return Dom.TextFragment(value, ended, p);
    }
    public bool EndDocument(XmlLocation p)
    {
        Events.Add("complete:" + Location(p));
        return Dom.EndDocument(p);
    }
}
