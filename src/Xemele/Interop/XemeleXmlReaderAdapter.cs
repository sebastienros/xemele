using System.Xml;
using Xemele.Streaming;

namespace Xemele.Interop;

/// <summary>Owns copies of the current forward event. The primary input stream remains caller-owned.</summary>
public sealed class XemeleXmlReaderAdapter : System.Xml.XmlReader
{
    private const string XmlUri = "http://www.w3.org/XML/1998/namespace";
    private const string XmlnsUri = "http://www.w3.org/2000/xmlns/";
    private sealed record Attribute(string Local, string Prefix, string Uri, string Name, string Value);
    private sealed record Frame(Attribute[] Declarations, bool Empty, bool PreserveSpace, string BaseUri, string SourceUri);
    private readonly Xemele.Streaming.XmlReader reader;
    private readonly bool leaveOpen;
    private readonly NameTable names = new();
    private readonly List<Frame> frames = [];
    private Attribute[] attributes = [];
    private int attribute = -1, depth, gate;
    private bool attributeValue, empty, pop, cdata, cdataText, dtd;
    private string local = "", prefix = "", uri = "", name = "", value = "", baseUri = "";
    private XmlNodeType kind;
    private ReadState state;

    public XemeleXmlReaderAdapter(Xemele.Streaming.XmlReader reader, bool leaveOpen = true)
    { ArgumentNullException.ThrowIfNull(reader); this.reader = reader; this.leaveOpen = leaveOpen; }
    private Attribute? CurrentAttribute => attribute < 0 ? null : attributes[attribute];
    public override string LocalName => attributeValue ? "" : CurrentAttribute?.Local ?? local;
    public override string NamespaceURI => attributeValue ? "" : CurrentAttribute?.Uri ?? uri;
    public override string Prefix => attributeValue ? "" : CurrentAttribute?.Prefix ?? prefix;
    public override string Name => attributeValue ? "" : CurrentAttribute?.Name ?? name;
    public override string Value => CurrentAttribute?.Value ?? value;
    public override string BaseURI => baseUri;
    public override int Depth => depth + (attribute < 0 ? 0 : attributeValue ? 2 : 1);
    public override int AttributeCount => attributes.Length;
    public override bool EOF => state == ReadState.EndOfFile;
    public override bool IsEmptyElement => attribute < 0 && empty;
    public override XmlNodeType NodeType => attributeValue ? XmlNodeType.Text : attribute >= 0 ? XmlNodeType.Attribute : kind;
    public override ReadState ReadState => state;
    public override XmlNameTable NameTable => names;
    public override bool HasValue => NodeType is XmlNodeType.Attribute or XmlNodeType.Text or XmlNodeType.CDATA or XmlNodeType.Comment or XmlNodeType.ProcessingInstruction or XmlNodeType.Whitespace or XmlNodeType.SignificantWhitespace or XmlNodeType.XmlDeclaration or XmlNodeType.DocumentType;
    public override bool CanResolveEntity => false;
    public override string? GetAttribute(string name)
    { ArgumentNullException.ThrowIfNull(name); foreach (var a in attributes) if (a.Name == name) return a.Value; return null; }
    public override string? GetAttribute(string localName, string? namespaceURI)
    { ArgumentNullException.ThrowIfNull(localName); foreach (var a in attributes) if (a.Local == localName && a.Uri == (namespaceURI ?? "")) return a.Value; return null; }
    private void CheckIndex(int i) { if ((uint)i >= (uint)attributes.Length) throw new ArgumentOutOfRangeException(nameof(i)); }
    public override string GetAttribute(int i) { CheckIndex(i); return attributes[i].Value; }
    public override void MoveToAttribute(int i) { CheckIndex(i); attribute = i; attributeValue = false; }
    public override bool MoveToAttribute(string name)
    { ArgumentNullException.ThrowIfNull(name); for (int i = 0; i < attributes.Length; i++) if (attributes[i].Name == name) { MoveToAttribute(i); return true; } return false; }
    public override bool MoveToAttribute(string localName, string? ns)
    { ArgumentNullException.ThrowIfNull(localName); for (int i = 0; i < attributes.Length; i++) if (attributes[i].Local == localName && attributes[i].Uri == (ns ?? "")) { MoveToAttribute(i); return true; } return false; }
    public override bool MoveToFirstAttribute() { if (attributes.Length == 0) return false; MoveToAttribute(0); return true; }
    public override bool MoveToNextAttribute() { if (attribute + 1 >= attributes.Length) return false; MoveToAttribute(attribute + 1); return true; }
    public override bool MoveToElement() { if (attribute < 0) return false; attribute = -1; attributeValue = false; return true; }
    public override bool ReadAttributeValue() { if (attribute < 0 || attributeValue) return false; attributeValue = true; return true; }
    public override string? LookupNamespace(string prefix)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        if (state != ReadState.Interactive) return null;
        if (prefix == "xml") return XmlUri;
        if (prefix == "xmlns") return XmlnsUri;
        for (int i = frames.Count - 1; i >= 0; i--)
            foreach (var a in frames[i].Declarations) if ((a.Prefix.Length == 0 ? "" : a.Local) == prefix) return a.Value;
        return prefix.Length == 0 ? "" : null;
    }
    public override void ResolveEntity() => throw new InvalidOperationException("Entity references are already expanded; entity resolution is unsupported.");
    private void Enter() { if (Interlocked.CompareExchange(ref gate, 1, 0) != 0) throw new InvalidOperationException("Reader operation already active."); }
    private void Exit() => Volatile.Write(ref gate, 0);
    private void ClearEvent()
    { attribute = -1; attributeValue = empty = false; attributes = []; depth = 0; kind = XmlNodeType.None; local = prefix = uri = name = value = baseUri = ""; }
    private bool Begin()
    {
        if (state is ReadState.Closed or ReadState.EndOfFile) return false;
        if (state == ReadState.Error) throw new InvalidOperationException("Reader is faulted.");
        ClearEvent();
        if (pop) { frames.RemoveAt(frames.Count - 1); pop = false; }
        return true;
    }
    public override bool Read()
    {
        Enter();
        try { if (!Begin()) return false; try { while (reader.Read()) if (Capture()) return true; return Finish(); } catch { Fault(); throw; } }
        finally { Exit(); }
    }
    public override async Task<bool> ReadAsync()
    {
        Enter();
        try { if (!Begin()) return false; try { while (await reader.ReadAsync().ConfigureAwait(false)) if (Capture()) return true; return Finish(); } catch { Fault(); throw; } }
        finally { Exit(); }
    }
    private bool Finish() { ClearEvent(); frames.Clear(); state = ReadState.EndOfFile; return false; }
    private void Fault() { ClearEvent(); frames.Clear(); state = ReadState.Error; }
    private string Intern(ReadOnlySpan<char> text) => text.IsEmpty ? "" : names.Add(text.ToString());
    private Attribute Copy(ReadOnlySpan<char> local, ReadOnlySpan<char> prefix, ReadOnlySpan<char> uri, ReadOnlySpan<char> value)
    { string l = Intern(local), p = Intern(prefix); return new(l, p, Intern(uri), names.Add(p.Length == 0 ? l : string.Concat(p, ":", l)), value.ToString()); }
    private void SetName(ReadOnlySpan<char> l, ReadOnlySpan<char> p, ReadOnlySpan<char> u)
    { local = Intern(l); prefix = Intern(p); uri = Intern(u); name = names.Add(prefix.Length == 0 ? local : string.Concat(prefix, ":", local)); }
    private bool Capture()
    {
        var token = reader.TokenKind;
        if (dtd && token != XmlTokenKind.EndDtd) return false;
        depth = frames.Count;
        string sourceUri = reader.Location.SourceUri?.AbsoluteUri ?? "";
        baseUri = frames.Count != 0 && frames[^1].SourceUri == sourceUri ? frames[^1].BaseUri : sourceUri;
        switch (token)
        {
            case XmlTokenKind.StartElement:
                SetName(reader.LocalName, reader.Prefix, reader.NamespaceUri); kind = XmlNodeType.Element; empty = reader.IsEmptyElement;
                var a = reader.Attributes; var n = reader.NamespaceDeclarations;
                attributes = new Attribute[a.Count + n.Count]; var declarations = new Attribute[n.Count];
                bool preserve = frames.Count != 0 && frames[^1].PreserveSpace;
                for (int i = 0; i < a.Count; i++)
                {
                    var row = a[i]; attributes[i] = Copy(row.LocalName, row.Prefix, row.NamespaceUri, row.Value);
                    if (row.NamespaceUri.SequenceEqual(XmlUri))
                    {
                        if (row.LocalName.SequenceEqual("space")) preserve = row.Value.SequenceEqual("preserve");
                        if (row.LocalName.SequenceEqual("base")) baseUri = AdapterBaseUri.Resolve(baseUri, row.Value.ToString());
                    }
                }
                for (int i = 0; i < n.Count; i++)
                {
                    var row = n[i]; declarations[i] = Copy(row.Prefix.IsEmpty ? "xmlns" : row.Prefix, row.Prefix.IsEmpty ? "" : "xmlns", XmlnsUri, row.NamespaceUri);
                    attributes[a.Count + i] = declarations[i];
                }
                frames.Add(new(declarations, empty, preserve, baseUri, sourceUri)); break;
            case XmlTokenKind.EndElement:
                if (frames[^1].Empty) { frames.RemoveAt(frames.Count - 1); return false; }
                depth = frames.Count - 1; baseUri = frames[^1].BaseUri; SetName(reader.LocalName, reader.Prefix, reader.NamespaceUri); kind = XmlNodeType.EndElement; pop = true; break;
            case XmlTokenKind.Text:
                value = reader.ValueSpan.ToString(); kind = cdata ? XmlNodeType.CDATA : IsWhitespace(value) ? frames.Count != 0 && frames[^1].PreserveSpace ? XmlNodeType.SignificantWhitespace : XmlNodeType.Whitespace : XmlNodeType.Text;
                cdataText |= cdata; break;
            case XmlTokenKind.StartCData: cdata = true; cdataText = false; return false;
            case XmlTokenKind.EndCData: cdata = false; if (cdataText) return false; kind = XmlNodeType.CDATA; break;
            case XmlTokenKind.Comment: kind = XmlNodeType.Comment; value = reader.ValueSpan.ToString(); break;
            case XmlTokenKind.ProcessingInstruction: kind = XmlNodeType.ProcessingInstruction; SetName(reader.Target, "", ""); value = reader.ValueSpan.ToString(); break;
            case XmlTokenKind.Declaration:
                kind = XmlNodeType.XmlDeclaration; SetName("xml", "", ""); var declaration = reader.Declaration;
                value = "version=\"" + declaration.Version.ToString() + "\"";
                var declarationAttributes = new List<Attribute> { Copy("version", "", "", declaration.Version) };
                if (!declaration.EncodingName.IsEmpty) { value += " encoding=\"" + declaration.EncodingName.ToString() + "\""; declarationAttributes.Add(Copy("encoding", "", "", declaration.EncodingName)); }
                if (declaration.Standalone is bool standalone) { string s = standalone ? "yes" : "no"; value += " standalone=\"" + s + "\""; declarationAttributes.Add(Copy("standalone", "", "", s)); }
                attributes = declarationAttributes.ToArray(); break;
            case XmlTokenKind.StartDtd:
                kind = XmlNodeType.DocumentType; SetName(reader.DocumentType.Name, "", ""); dtd = true;
                var dt = reader.DocumentType; var ids = new List<Attribute>();
                if (dt.HasPublicId) ids.Add(Copy("PUBLIC", "", "", dt.PublicId));
                if (dt.HasSystemId) ids.Add(Copy("SYSTEM", "", "", dt.SystemId));
                attributes = ids.ToArray(); break;
            case XmlTokenKind.EndDtd: dtd = false; return false;
            default: return false;
        }
        state = ReadState.Interactive; return true;
    }
    private static bool IsWhitespace(ReadOnlySpan<char> text) { foreach (char c in text) if (c is not (' ' or '\t' or '\r' or '\n')) return false; return true; }
    public override void Close()
    {
        Enter();
        try
        {
            if (state == ReadState.Closed) return;
            ClearEvent(); frames.Clear(); state = ReadState.Closed;
            if (!leaveOpen) reader.Dispose();
        }
        finally { Exit(); }
    }
}

internal static class AdapterBaseUri
{
    internal static string Resolve(string inherited, string reference)
    {
        if (Uri.TryCreate(reference, UriKind.Absolute, out var absolute)) return absolute.AbsoluteUri;
        return Uri.TryCreate(inherited, UriKind.Absolute, out var parent) && Uri.TryCreate(parent, reference, out var resolved) ? resolved.AbsoluteUri : reference;
    }
}
