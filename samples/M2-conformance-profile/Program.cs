using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Xemele;
using Xemele.Parsing.Dom;
using Xemele.Parsing.Input;
using Xemele.Parsing.Syntax;
using Xemele.ReferenceProtocol;

// Independently compiled test driver. Exact bytes; primary and external sources
// have explicit identities. No decoding through framework replacement fallbacks.
while (Console.ReadLine() is { } line)
{
    var request = JsonSerializer.Deserialize<Request>(line, Wire.Json)!;
    var source = new Uri(request.Options!["sourceUri"].GetString()!, UriKind.Absolute);
    var resolver = new MappedResolver(request.OfflineResources ?? []);
    var options = new XmlParseOptions { DtdProcessing = XmlDtdProcessing.Parse, Resolver = resolver };
    try
    {
        byte[] bytes = Convert.FromBase64String(request.InputBase64);
        using var decoder = new XmlInputDecoder(options, source);
        using var core = new XmlParserCore(options, source);
        var sink = new DomSink(options, decoder);
        char[] chars = new char[2]; long[] offsets = new long[2];
        var prefix = new List<char>(); var prefixOffsets = new List<long>();
        bool decided = false; XmlDiagnostic? diagnostic = null;
        int position = 0;
        while (diagnostic is null)
        {
            bool final = position == bytes.Length;
            var decoded = decoder.Decode(final ? [] : bytes.AsSpan(position, 1), chars, offsets, final, default);
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
                var result = core.Append([], [], true, sink, default);
                diagnostic = sink.Diagnostic ?? result.Diagnostic;
            }
            break;
        }
        Write(diagnostic is null ? "success" : "parse-rejected", diagnostic?.Code,
            diagnostic is null ? Model(sink.Document) : null);
        XmlDiagnostic? Feed(ReadOnlySpan<char> input, ReadOnlySpan<long> mappings)
        {
            var result = core.Append(input, mappings, false, sink, default);
            return sink.Diagnostic ?? result.Diagnostic;
        }
    }
    catch (Exception error) { Write("runner-failure", error.GetType().Name, null); }
    void Write(string outcome, string? diagnostic, Event[]? events) => Console.WriteLine(JsonSerializer.Serialize(new
    { caseId = request.CaseId, outcome, diagnostic, events, opens = resolver.Opens, identity = new { driverBuild = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion, productBuild = typeof(XmlDocument).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion, runtime = RuntimeInformation.FrameworkDescription, coreRuntimeBuild = typeof(object).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion } }, Wire.Json));
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
            foreach (var a in node.Attributes) attrs.Add(new(a.NamespaceUri, a.LocalName, a.Value));
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
sealed class MappedResolver(Dictionary<string,string> resources) : XmlResourceResolver
{
    internal int Opens;
    public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!resources.TryGetValue(request.Uri.AbsoluteUri, out var value)) throw new IOException("Unmapped resource: " + request.Uri);
        Opens++;
        return new(new MemoryStream(Convert.FromBase64String(value),false),request.Uri);
    }
}
