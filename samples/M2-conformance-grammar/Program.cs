using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Xemele;
using Xemele.Parsing.Dom;
using Xemele.Parsing.Input;
using Xemele.Parsing.Syntax;
using Xemele.Parsing.Dtd;
using Xemele.Parsing.Entities;
using Xemele.ReferenceProtocol;

// Independently compiled test driver. Exact bytes; primary and external sources
// have explicit identities. No decoding through framework replacement fallbacks.
while (Console.ReadLine() is { } line)
{
    var request = JsonSerializer.Deserialize<Request>(line, Wire.Json)!;
    var source = new Uri(request.Options!["sourceUri"].GetString()!, UriKind.Absolute);
    var resolver = new MappedResolver(request.OfflineResources ?? []);
    int chunk = request.Options.TryGetValue("chunk", out var chunkValue) ? chunkValue.GetInt32() : 1;
    bool pause = request.Options.TryGetValue("pause", out var pauseValue) && pauseValue.GetBoolean();
    object? details = null;
    var options = new XmlParseOptions { DtdProcessing = XmlDtdProcessing.Parse, Resolver = resolver };
    try
    {
        byte[] bytes = Convert.FromBase64String(request.InputBase64);
        using var decoder = new XmlInputDecoder(options, source);
        using var core = new XmlParserCore(options, source);
        var sink = new DomSink(options, decoder);
        var recording = new RecordingSink(sink, pause);
        char[] chars = new char[4096]; long[] offsets = new long[4096];
        var prefix = new List<char>(); var prefixOffsets = new List<long>();
        bool decided = false; XmlDiagnostic? diagnostic = null;
        int position = 0;
        while (diagnostic is null)
        {
            bool final = position == bytes.Length;
            int take = Math.Min(bytes.Length - position, sink.DeclarationValidated ? chunk : 1);
            int capacity = sink.DeclarationValidated ? 4096 : 2;
            var decoded = decoder.Decode(final ? [] : bytes.AsSpan(position, take), chars.AsSpan(0, capacity), offsets.AsSpan(0, capacity), final, default);
            position += decoded.ConsumedBytes;
            for (int i = 0; i < decoded.WrittenCharacters && diagnostic is null; i++)
            {
                if (!decided)
                {
                    prefix.Add(chars[i]); prefixOffsets.Add(offsets[i]);
                    int n = prefix.Count;
                    bool absent = n <= 5 ? chars[i] != "<?xml"[n-1] : chars[i] is not (' ' or '\t' or '\r' or '\n');
                    if (!absent && n < 6) continue;
                    decided = true;
                    if (absent) sink.ValidateAbsentDeclaration(new(source, 1, 1, prefixOffsets[0]));
                    diagnostic = sink.Diagnostic;
                    if (diagnostic is null) diagnostic = Feed(prefix.ToArray(), prefixOffsets.ToArray());
                }
                else diagnostic = Feed(chars.AsSpan(i, 1), offsets.AsSpan(i, 1));
            }
            diagnostic ??= decoded.Diagnostic;
            if (diagnostic is not null) break;
            if (decoded.Step != DecodeStep.Completed) continue;
            if (!decided)
            {
                sink.ValidateAbsentDeclaration(new(source, 1, 1, prefix.Count == 0 ? bytes.Length : prefixOffsets[0]));
                diagnostic = sink.Diagnostic;
                if (diagnostic is null && prefix.Count != 0) diagnostic = Feed(prefix.ToArray(), prefixOffsets.ToArray());
            }
            if (diagnostic is null)
            {
                core.CompleteByteSource(bytes.Length);
                ScanResult result;
                do { result = core.Append([], [], true, recording, default); } while (result.Step == ScanStep.Paused && sink.Diagnostic is null);
                diagnostic = sink.Diagnostic ?? result.Diagnostic;
                if (diagnostic is null && result.Step != ScanStep.Completed) throw new InvalidOperationException("Finalization did not complete.");
            }
            break;
        }
        if (diagnostic is null) details = new { recording.Version, recording.Encoding, recording.Standalone, doctype = sink.Document.DocumentType?.Name, unresolved = sink.Document.UnresolvedReferences.Select(r => new { r.Name, r.LexicalReference, parentNamespace = r.Parent.NamespaceUri, parentName = r.Parent.LocalName, r.ChildBoundary, r.Sequence, sourceUri = r.Location.SourceUri?.AbsoluteUri, anchorValid = Anchor(r) }).ToArray(), incompleteDefaults = sink.Document.IncompleteAttributeDefaults.Select(IncompleteDefault).ToArray(), incompleteValues = sink.Document.IncompleteAttributeValues.Select(v => new { namespaceUri = v.Attribute.NamespaceUri, localName = v.Attribute.LocalName, v.LexicalValue, v.IsTokenized, parts = Parts(v.Parts), defaultSequence = v.Default?.Sequence }).ToArray(), resourceUris = resolver.Uris, resolver.Disposals, callbacks = recording.Callbacks };
        Write(diagnostic is null ? "success" : "parse-rejected", diagnostic?.Code,
            diagnostic is null ? Model(sink.Document) : null);
        XmlDiagnostic? Feed(ReadOnlySpan<char> input, ReadOnlySpan<long> mappings)
        {
            int consumed = 0;
            ScanResult result;
            do
            {
                result = core.Append(input[consumed..], mappings[consumed..], false, recording, default);
                if ((uint)result.Consumed > (uint)(input.Length - consumed)) throw new InvalidOperationException("Invalid consumption.");
                consumed += result.Consumed;
                if (result.Diagnostic is not null || sink.Diagnostic is not null) return sink.Diagnostic ?? result.Diagnostic;
            } while (result.Step == ScanStep.Paused);
            if (consumed != input.Length || result.Step != ScanStep.NeedInput) throw new InvalidOperationException("Incomplete nonfinal consumption.");
            return null;
        }
    }
    catch (Exception error) { Write("runner-failure", error.GetType().Name, null); }
    void Write(string outcome, string? diagnostic, Event[]? events) => Console.WriteLine(JsonSerializer.Serialize(new
    { caseId = request.CaseId, outcome, diagnostic, events, details, opens = resolver.Opens, identity = new { driverBuild = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion, productBuild = typeof(XmlDocument).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion, runtime = RuntimeInformation.FrameworkDescription, coreRuntimeBuild = typeof(object).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion } }, Wire.Json));
}

static Event[] Model(XmlDocument document)
{
    var events = new List<Event>();
    var stack = new Stack<(XmlNode Node, bool End)>();
    stack.Push((document.RootElement, false));
    while (stack.TryPop(out var item))
    {
        var node = item.Node;
        if (node.Kind == XmlNodeKind.Element)
        {
            if (item.End) { events.Add(new("end", node.NamespaceUri, node.LocalName)); continue; }
            var attrs = new List<AttributeValue>();
            foreach (var a in node.Attributes)
            {
                if (document.IncompleteAttributeValues.Any(v => v.Attribute == a)) continue; // symbolic sidecar is projected in details
                attrs.Add(new(a.NamespaceUri, a.LocalName, a.Value));
            }
            events.Add(new("start", node.NamespaceUri, node.LocalName, Attributes: attrs.OrderBy(a => a.NamespaceUri, StringComparer.Ordinal).ThenBy(a => a.LocalName, StringComparer.Ordinal).ToArray()));
            stack.Push((node,true));
            for (var child = node.LastChild; !child.IsDefault; child = child.PreviousSibling) stack.Push((child,false));
        }
        else if (node.Kind is XmlNodeKind.Text or XmlNodeKind.CData)
        {
            if (events.Count>0 && events[^1].Kind == "text") events[^1] = events[^1] with { Value = events[^1].Value + node.Value };
            else events.Add(new("text",Value:node.Value));
        }
    }
    return events.ToArray();
}
static bool Anchor(XmlUnresolvedReference reference)
{
    int count = 0; XmlNode previous = default;
    foreach (var child in reference.Parent.Children)
    {
        if (count == reference.ChildBoundary) break;
        previous = child; count++;
    }
    return count == reference.ChildBoundary && previous == reference.PreviousSibling;
}
static object[] Parts(IReadOnlyList<XmlAttributeValuePart> parts) => parts.Select(p => p switch
{
    XmlKnownAttributeText k => (object)new { kind = "known", text = k.Text },
    XmlUnknownAttributeReference u => new { kind = "unknown", name = u.Name, lexical = u.LexicalReference },
    _ => throw new InvalidOperationException("Unknown symbolic part")
}).ToArray();
static object IncompleteDefault(XmlIncompleteAttributeDefault d) => new { d.ElementName, d.AttributeName, d.LexicalValue, d.IsTokenized, d.IsExternal, d.Sequence, parts = Parts(d.Parts) };
sealed class MappedResolver(Dictionary<string,string> resources) : XmlResourceResolver
{
    internal int Opens, Disposals;
    internal readonly List<string> Uris = [];
    public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!resources.TryGetValue(request.Uri.AbsoluteUri, out var value)) throw new IOException("Unmapped resource: " + request.Uri);
        Opens++; Uris.Add(request.Uri.AbsoluteUri);
        return new(new Tracked(Convert.FromBase64String(value), this),request.Uri);
    }
    private sealed class Tracked(byte[] bytes, MappedResolver owner) : MemoryStream(bytes, false)
    {
        private bool disposed;
        protected override void Dispose(bool disposing) { if (disposing && !disposed) { disposed = true; owner.Disposals++; } base.Dispose(disposing); }
    }
}

sealed class RecordingSink(DomSink dom, bool pause) : IXmlEventSink, IXmlDtdSink, IXmlUnresolvedReferenceSink, IXmlIncompleteAttributeSink
{
    internal string? Version, Encoding;
    internal bool? Standalone;
    internal int Callbacks;
    private bool Delivered(bool accepted) { Callbacks++; return accepted && !pause; }
    public bool StartDocument(XmlLocation location) => Delivered(dom.StartDocument(location));
    public bool Declaration(XmlDeclarationView d)
    { Version = d.Version.ToString(); Encoding = d.EncodingName.IsEmpty ? null : d.EncodingName.ToString(); Standalone = d.Standalone; return Delivered(dom.Declaration(d)); }
    public bool StartElement(XmlNameView name, bool empty, XmlLocation location) => Delivered(dom.StartElement(name, empty, location));
    public bool NamespaceDeclaration(XmlNamespaceView d) => Delivered(dom.NamespaceDeclaration(d));
    public bool Attribute(XmlAttributeView a) => Delivered(dom.Attribute(a));
    public bool StartElementContent() => Delivered(dom.StartElementContent());
    public bool EndElement(XmlNameView name, XmlLocation location) => Delivered(dom.EndElement(name, location));
    public bool StartText(XmlTextKind kind, ReadOnlySpan<char> target, XmlLocation location) => Delivered(dom.StartText(kind, target, location));
    public bool TextFragment(ReadOnlySpan<char> text, bool ended, XmlLocation location) => Delivered(dom.TextFragment(text, ended, location));
    public bool EndDocument(XmlLocation location) => Delivered(dom.EndDocument(location));
    public bool DocumentType(XmlDocumentType d, DtdModel model, XmlLocation location) => Delivered(((IXmlDtdSink)dom).DocumentType(d, model, location));
    public bool UnresolvedReference(XmlUnresolvedReferenceView r) => Delivered(((IXmlUnresolvedReferenceSink)dom).UnresolvedReference(r));
    public bool IncompleteAttributeDefault(XmlIncompleteAttributeDefaultView d) => Delivered(((IXmlIncompleteAttributeSink)dom).IncompleteAttributeDefault(d));
    public bool IncompleteAttribute(XmlIncompleteAttributeView a) => Delivered(((IXmlIncompleteAttributeSink)dom).IncompleteAttribute(a));
}
