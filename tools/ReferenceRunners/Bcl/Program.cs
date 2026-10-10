using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Xml;
using Xemele.ReferenceProtocol;

var engine = new Engine("System.Xml", typeof(XmlReader).Assembly.GetName().Version!.ToString(),
    RuntimeInformation.FrameworkDescription, Assembly.GetExecutingAssembly()
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion);
while (true)
{
    var line = new StringBuilder();
    bool oversized = false;
    int ch;
    while ((ch = Console.In.Read()) >= 0 && ch != '\n')
    {
        if (line.Length < Wire.MaximumLineCharacters) line.Append((char)ch);
        else oversized = true;
    }
    if (ch < 0 && line.Length == 0 && !oversized) break;
    Request? request = null;
    bool deniedResource = false;
    Response response;
    try
    {
        if (oversized) throw new ArgumentException("Request exceeds line limit.");
        request = JsonSerializer.Deserialize<Request>(line.ToString(), Wire.Json)
            ?? throw new ArgumentException("Null request.");
        if (request.ProtocolVersion != 1 || string.IsNullOrWhiteSpace(request.CaseId)
            || request.Operation != "xml.events" || request.Profile != "xml-1.0"
            || request.Options?.Count > 0)
            throw new ArgumentException("Unsupported version, identity, operation, profile or options.");
        if (request.EncodingHint is not null && request.EncodingHint != "utf-8")
            throw new ArgumentException("Only absent or utf-8 encoding hint is supported.");
        byte[] input = Convert.FromBase64String(request.InputBase64);
        var resources = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var pair in request.OfflineResources ?? [])
        {
            if (!Uri.TryCreate(pair.Key, UriKind.Absolute, out var uri))
                throw new ArgumentException("Offline keys must be absolute URIs.");
            resources.Add(uri.AbsoluteUri, Convert.FromBase64String(pair.Value));
        }
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Parse,
            XmlResolver = new OfflineResolver(resources, () => deniedResource = true), MaxCharactersInDocument = 1_048_576,
            MaxCharactersFromEntities = 262_144, IgnoreComments = true, IgnoreProcessingInstructions = true };
        using var stream = new MemoryStream(input);
        using var reader = request.EncodingHint is null
            ? XmlReader.Create(stream, settings, "urn:xemele:input")
            : XmlReader.Create(new StreamReader(stream, new UTF8Encoding(false, true), false), settings, "urn:xemele:input");
        var events = new List<Event>();
        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element)
            {
                string ns = reader.NamespaceURI, name = reader.LocalName;
                bool empty = reader.IsEmptyElement;
                var attrs = new List<AttributeValue>();
                if (reader.MoveToFirstAttribute())
                {
                    do { if (reader.NamespaceURI != "http://www.w3.org/2000/xmlns/")
                        attrs.Add(new(reader.NamespaceURI, reader.LocalName, reader.Value)); }
                    while (reader.MoveToNextAttribute());
                    reader.MoveToElement();
                }
                events.Add(new("start", ns, name, Attributes: attrs.OrderBy(a => a.NamespaceUri, StringComparer.Ordinal)
                    .ThenBy(a => a.LocalName, StringComparer.Ordinal).ToArray()));
                if (empty) events.Add(new("end", ns, name));
            }
            else if (reader.NodeType == XmlNodeType.EndElement)
                events.Add(new("end", reader.NamespaceURI, reader.LocalName));
            else if (reader.NodeType is XmlNodeType.Text or XmlNodeType.CDATA or XmlNodeType.Whitespace or XmlNodeType.SignificantWhitespace)
            {
                if (events.Count > 0 && events[^1].Kind == "text")
                    events[^1] = events[^1] with { Value = events[^1].Value + reader.Value };
                else events.Add(new("text", Value: reader.Value));
            }
        }
        response = Reply("success", [], events.ToArray());
    }
    catch (XmlException ex) { response = deniedResource
        ? Reply("runner-failure", [new("resource-denied", ex.Message)])
        : IsQuota(ex) ? Reply("runner-failure", [new("limit", ex.Message)])
        : Reply("parse-rejected", [new("xml", ex.Message)]); }
    catch (DecoderFallbackException ex) { response = Reply("parse-rejected", [new("xml", ex.Message)]); }
    catch (Exception ex) { response = Reply("runner-failure", [new(ex is ArgumentException or JsonException or FormatException ? "protocol" : "infrastructure", ex.Message)]); }
    string output = JsonSerializer.Serialize(response, Wire.Json);
    if (output.Length > Wire.MaximumLineCharacters)
        output = JsonSerializer.Serialize(Reply("runner-failure", [new("limit", "Response exceeds line limit.")]), Wire.Json);
    Console.Out.WriteLine(output);
    Response Reply(string outcome, Diagnostic[] diagnostics, Event[]? events = null) =>
        new(1, request?.CaseId, request?.Operation, engine, outcome, diagnostics, events);
}

// .NET 10 XmlReader quota diagnostics include the invariant setting name even
// when the surrounding framework message is localized. Do not match prose or
// classify arbitrary XmlException failures as quota failures.
static bool IsQuota(XmlException error) =>
    error.Message.Contains(nameof(XmlReaderSettings.MaxCharactersFromEntities), StringComparison.Ordinal)
    || error.Message.Contains(nameof(XmlReaderSettings.MaxCharactersInDocument), StringComparison.Ordinal);

sealed class OfflineResolver(Dictionary<string, byte[]> resources, Action denied) : XmlResolver
{
    public override ICredentials? Credentials { set { } }
    public override object GetEntity(Uri absoluteUri, string? role, Type? ofObjectToReturn)
    {
        if (!resources.TryGetValue(absoluteUri.AbsoluteUri, out var bytes))
        {
            denied();
            throw new IOException("External resource denied: " + absoluteUri.AbsoluteUri);
        }
        return new MemoryStream(bytes, false);
    }
}
