namespace Xemele.Streaming;

/// <summary>A forward reader. Current views expire on the next read invocation or disposal.</summary>
public sealed partial class XmlReader : IDisposable
{
    private readonly Stream input;
    private readonly XmlStreamingPump pump;
    private readonly CancellationToken cancellationToken;
    private readonly byte[] inputBuffer = new byte[4096];
    private readonly Cursor current = new();
    private int gate;
    private bool disposed, faulted, completed;
    private XmlReader(Stream input, XmlParseOptions options, CancellationToken cancellationToken)
    { this.input = input; this.cancellationToken = cancellationToken; pump = new(options); }

    public static XmlReader Create(Stream input, XmlParseOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!input.CanRead) throw new ArgumentException("The stream must be readable.", nameof(input));
        options ??= new();
        ArgumentNullException.ThrowIfNull(options.Limits);
        options.Limits.Validate();
        if (options.Recover) throw new ArgumentException("Reader requires strict options.", nameof(options));
        return new(input, options with { Limits = options.Limits with { } }, cancellationToken);
    }
    public bool Read()
    {
        Enter();
        try
        {
            current.Clear();
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted) throw new InvalidOperationException("Reader is faulted.");
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (completed) return false;
                while (true)
                {
                    switch (pump.Advance(current, cancellationToken))
                    {
                        case StreamingStep.Paused:
                            if (current.Kind == XmlTokenKind.Diagnostic && pump.Diagnostic is { } terminal) throw new XmlParseException([terminal]);
                            return true;
                        case StreamingStep.Completed: completed = true; return false;
                        case StreamingStep.Rejected: throw new XmlParseException([pump.Diagnostic!]);
                        case StreamingStep.NeedInput:
                            cancellationToken.ThrowIfCancellationRequested();
                            int count = input.Read(inputBuffer);
                            pump.Supply(inputBuffer.AsMemory(0, count), count == 0);
                            break;
                        default: throw new InvalidOperationException("Unexpected resource suspension.");
                    }
                }
            }
            catch { faulted = true; current.Clear(); try { pump.Dispose(); } catch { } throw; }
        }
        finally { Exit(); }
    }
    public void Dispose()
    {
        Enter();
        try { if (disposed) return; disposed = true; current.Clear(); pump.Dispose(); current.Release(); }
        finally { Exit(); }
    }
    private void Enter() { if (Interlocked.CompareExchange(ref gate, 1, 0) != 0) throw new InvalidOperationException("Reader operation already active."); }
    private void Exit() => Volatile.Write(ref gate, 0);
    public XmlTokenKind TokenKind => current.Kind;
    public XmlLocation Location => current.Location;
    public ReadOnlySpan<char> ValueSpan => current.Get(0);
    public ReadOnlySpan<char> LocalName => current.Get(1);
    public ReadOnlySpan<char> Prefix => current.Get(2);
    public ReadOnlySpan<char> NamespaceUri => current.Get(3);
    public ReadOnlySpan<char> Target => current.Get(4);
    public XmlAttributeView Attributes => new(current.Attributes.AsSpan(0, current.AttributeCount));
    public XmlNamespaceView NamespaceDeclarations => new(current.Namespaces.AsSpan(0, current.NamespaceCount));
    public bool IsEmptyElement => current.Empty;
    public XmlDeclaration Declaration => TokenKind == XmlTokenKind.Declaration ? new(current.Get(5), current.Get(6), current.Flag, Location) : default;
    public XmlDocumentTypeView DocumentType => TokenKind == XmlTokenKind.StartDtd ? new(current.Get(5), current.Get(6), current.Get(7), current.HasPublic, current.HasSystem, Location) : default;
    public XmlDtdDeclaration DtdDeclaration => TokenKind == XmlTokenKind.DtdDeclaration ? new(current.DtdKind, current.Get(5), current.Get(6), current.Get(7), current.Get(8), current.DefaultKind, current.Get(9), current.HasValue, current.Get(10), current.HasSystem, current.Get(11), current.HasPublic, current.Get(12), current.Parameter, current.Flag, Location) : default;
    public XmlSkippedEntity SkippedEntity => TokenKind == XmlTokenKind.SkippedEntity ? new(current.Get(5), current.Get(6), current.Parameter, Location) : default;
    public XmlIncompleteAttributeDefault? IncompleteAttributeDefault => current.Incomplete;
    public XmlDiagnostic? Diagnostic => current.Error;

    // Only the accepted current token is copied. No callback ref view escapes.
    internal sealed class Cursor : XmlStreamingSink
    {
        internal XmlTokenKind Kind;
        internal XmlLocation Location;
        private char[] chars = new char[256];
        private readonly int[] starts = new int[13], lengths = new int[13];
        private int used;
        internal AttributeRow[] Attributes = [];
        internal NamespaceRow[] Namespaces = [];
        internal int AttributeCount, NamespaceCount;
        internal bool Empty, HasPublic, HasSystem, HasValue, Parameter;
        internal bool? Flag;
        internal XmlDtdDeclarationKind DtdKind;
        internal XmlDtdDefaultKind DefaultKind;
        internal XmlIncompleteAttributeDefault? Incomplete;
        internal XmlDiagnostic? Error;
        internal ReadOnlySpan<char> Get(int slot) => chars.AsSpan(starts[slot], lengths[slot]);
        private void Copy(int slot, ReadOnlySpan<char> value)
        {
            int required = checked(used + value.Length);
            if (required > chars.Length) Array.Resize(ref chars, Math.Max(required, checked(chars.Length * 2)));
            starts[slot] = used; lengths[slot] = value.Length;
            value.CopyTo(chars.AsSpan(used)); used = required;
        }
        internal void Clear()
        {
            Kind = XmlTokenKind.None; Location = default; used = 0;
            Array.Clear(starts); Array.Clear(lengths);
            Array.Clear(Attributes, 0, AttributeCount); Array.Clear(Namespaces, 0, NamespaceCount);
            AttributeCount = NamespaceCount = 0;
            Empty = HasPublic = HasSystem = HasValue = Parameter = false; Flag = null;
            DtdKind = default; DefaultKind = default; Incomplete = null; Error = null;
        }
        internal void Release() { chars = []; Attributes = []; Namespaces = []; }
        private bool Set(XmlTokenKind kind, XmlLocation location) { Kind = kind; Location = location; return false; }
        public override bool StartDocument(XmlLocation location) => Set(XmlTokenKind.StartDocument, location);
        public override bool EndDocument(XmlLocation location) => Set(XmlTokenKind.EndDocument, location);
        public override bool StartCData(XmlLocation location) => Set(XmlTokenKind.StartCData, location);
        public override bool EndCData(XmlLocation location) => Set(XmlTokenKind.EndCData, location);
        public override bool EndDtd(XmlLocation location) => Set(XmlTokenKind.EndDtd, location);
        public override bool Text(ReadOnlySpan<char> text, XmlLocation location) { Copy(0, text); return Set(XmlTokenKind.Text, location); }
        public override bool Comment(ReadOnlySpan<char> text, XmlLocation location) { Copy(0, text); return Set(XmlTokenKind.Comment, location); }
        public override bool ProcessingInstruction(ReadOnlySpan<char> target, ReadOnlySpan<char> data, XmlLocation location) { Copy(4, target); Copy(0, data); return Set(XmlTokenKind.ProcessingInstruction, location); }
        public override bool StartElement(XmlStartElement element)
        {
            Copy(1, element.LocalName); Copy(2, element.Prefix); Copy(3, element.NamespaceUri); Empty = element.IsEmptyElement;
            int attributeCount = element.Attributes.Count, namespaceCount = element.NamespaceDeclarations.Count;
            if (attributeCount > Attributes.Length) Array.Resize(ref Attributes, attributeCount);
            if (namespaceCount > Namespaces.Length) Array.Resize(ref Namespaces, namespaceCount);
            AttributeCount = attributeCount; NamespaceCount = namespaceCount;
            for (int i = 0; i < AttributeCount; i++)
            {
                var a = element.Attributes[i];
                Attributes[i] = new(a.LocalName.ToString(), a.Prefix.ToString(), a.NamespaceUri.ToString(), a.Value.ToString(), a.IsDefaulted, a.IsValueComplete, a.ValueParts.IsEmpty ? ReadOnlyMemory<XmlAttributeValuePart>.Empty : a.ValueParts.ToArray(), a.Location);
            }
            for (int i = 0; i < NamespaceCount; i++) { var n = element.NamespaceDeclarations[i]; Namespaces[i] = new(n.Prefix.ToString(), n.NamespaceUri.ToString(), n.Location); }
            return Set(XmlTokenKind.StartElement, element.Location);
        }
        public override bool EndElement(XmlEndElement element) { Copy(1, element.LocalName); Copy(2, element.Prefix); Copy(3, element.NamespaceUri); return Set(XmlTokenKind.EndElement, element.Location); }
        public override bool Declaration(XmlDeclaration declaration) { Copy(5, declaration.Version); Copy(6, declaration.EncodingName); Flag = declaration.Standalone; return Set(XmlTokenKind.Declaration, declaration.Location); }
        public override bool StartDtd(XmlDocumentTypeView d) { Copy(5, d.Name); Copy(6, d.PublicId); Copy(7, d.SystemId); HasPublic = d.HasPublicId; HasSystem = d.HasSystemId; return Set(XmlTokenKind.StartDtd, d.Location); }
        public override bool DtdDeclaration(XmlDtdDeclaration d)
        {
            Copy(5, d.Name); Copy(6, d.ElementName); Copy(7, d.ContentModel); Copy(8, d.AttributeType); Copy(9, d.Value); Copy(10, d.SystemId); Copy(11, d.PublicId); Copy(12, d.NotationName);
            DtdKind = d.Kind; DefaultKind = d.DefaultKind; HasValue = d.HasValue; HasSystem = d.HasSystemId; HasPublic = d.HasPublicId; Parameter = d.IsParameter; Flag = d.IsExternal;
            return Set(XmlTokenKind.DtdDeclaration, d.Location);
        }
        public override bool SkippedEntity(XmlSkippedEntity entity) { Copy(5, entity.Name); Copy(6, entity.LexicalReference); Parameter = entity.IsParameter; return Set(XmlTokenKind.SkippedEntity, entity.Location); }
        public override bool IncompleteAttributeDefault(XmlIncompleteAttributeDefault declaration) { Incomplete = declaration; return Set(XmlTokenKind.IncompleteAttributeDefault, declaration.DeclarationLocation); }
        // Read distinguishes the pump terminal diagnostic before publishing a token.
        public override bool Diagnostic(XmlDiagnostic diagnostic) { Error = diagnostic; return Set(XmlTokenKind.Diagnostic, diagnostic.Location); }
    }
}
