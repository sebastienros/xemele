# Serialization contract — M1/M3

Decision owners: `M1-contracts`, then `M3-contracts`. Depends on tree/common contracts only; canonicalization is a separate M5 service.

```csharp
namespace Xemele;
public sealed record XmlSaveOptions
{
    public bool Indent { get; init; }
    public bool OmitXmlDeclaration { get; init; }
    public Encoding Encoding { get; init; }
}
public static class XmlSerializer
{
    public static string Serialize(Document document, XmlSaveOptions? options = null);
    public static void Serialize(Document document, Stream output, XmlSaveOptions? options = null);
}
// M3 addition; ownership of an output stream is explicit in options/constructor.
public sealed partial class XmlWriter : IDisposable
{
    public XmlWriter(Stream output, XmlSaveOptions? options = null, bool leaveOpen = true);
    public void WriteStartElement(string localName, string? namespaceUri = null, string? prefix = null);
    public void WriteAttribute(string localName, string value, string? namespaceUri = null, string? prefix = null);
    public void WriteString(ReadOnlySpan<char> text);
    public void WriteEndElement();
    public void Flush();
    public Task FlushAsync(CancellationToken cancellationToken = default);
}
```

M1 serializes complete documents to string/stream and provides Document wrappers. M3 adds a forward writer with document/comment/CDATA/PI methods, full namespace/output options, async output transport, and async document save. Its contract task supplies all exact overloads, including non-span async arguments; no spans survive an await. Forward writing works without a DOM.

The writer lane implements the synchronous members and `IDisposable`. The later async transport lane adds `FlushAsync`, async write members, and `IAsyncDisposable` in separate partial files. The synchronous lane must compile and function on its own without declarations of unfinished async members.

Default string output omits a misleading byte-encoding declaration; byte output uses its actual encoding and documented BOM policy. Escape text/attributes correctly, validate XML characters, preserve significant mixed-content whitespace, and reject illegal output sequences. Formatting cannot insert whitespace inside mixed content unless explicitly requested with documented semantic consequences.

Namespace handling validates or repairs according to an explicit option whose default is settled in M1. Test prefix collisions and inherited declarations. Attribute order is deterministic according to the chosen output policy but is not canonicalization. Never silently call M5 to provide ordinary output.

Writer state is forward-only: attributes occur before content, end tags must balance, and use after disposal fails. Disposal releases resources; define whether it completes open elements or reports incomplete output (proposal: do not silently invent closing structure). Leave caller streams open by default. Cancellation and I/O errors propagate; async transport never uses `Task.Run`.

Tests cover known expected bytes without the Xemele parser, semantic round trips, stream ownership, mixed content, invalid output, encoding declarations, incremental flush and partial I/O. Serializer and writer lanes own separate folders and only share immutable contracts/utilities from completed prerequisites.
