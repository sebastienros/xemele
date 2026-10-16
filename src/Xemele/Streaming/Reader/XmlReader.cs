using System.Buffers;
using System.Text;
using System.Text.Unicode;
namespace Xemele.Streaming;

/// <summary>A forward reader. Current views expire on the next read invocation or disposal.</summary>
public sealed partial class XmlReader : IDisposable
{
    private const int InputBufferSize = 4096;
    private readonly Stream input;
    private readonly XmlParseOptions options;
    private XmlStreamingPump? pump;
    private OptimisticPullReader? optimistic;
    private readonly CancellationToken cancellationToken;
    private byte[]? inputBuffer;
    private readonly Cursor current = new();
    private int gate;
    private bool disposed, faulted, completed;
    private XmlReader(Stream input, XmlParseOptions options, CancellationToken cancellationToken)
    {
        this.input = input;
        this.options = options;
        this.cancellationToken = cancellationToken;
        if (OptimisticPullReader.CanUse(options))
        {
            optimistic = new(options);
        }
        else
        {
            pump = new(options, allowBulkText: true);
            try
            {
                inputBuffer = ArrayPool<byte>.Shared.Rent(InputBufferSize);
            }
            catch
            {
                pump.Dispose();
                throw;
            }
        }
    }

    /// <summary>Creates a strict forward reader without taking ownership of the input stream.</summary>
    /// <param name="input">A readable stream that remains open after reader disposal.</param>
    /// <param name="options">Parsing options, or null for defaults. Recovery must be disabled.</param>
    /// <param name="cancellationToken">A token observed by subsequent reads.</param>
    /// <returns>A reader positioned before its first event.</returns>
    /// <exception cref="ArgumentNullException">The input stream or resource limits are null.</exception>
    /// <exception cref="ArgumentException">The stream is unreadable or recovery is enabled.</exception>
    public static XmlReader Create(Stream input, XmlParseOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!input.CanRead)
        {
            throw new ArgumentException("The stream must be readable.", nameof(input));
        }

        options ??= new();
        ArgumentNullException.ThrowIfNull(options.Limits);
        options.Limits.Validate();
        if (options.Recover)
        {
            throw new ArgumentException("Reader requires strict options.", nameof(options));
        }

        return new(input, options with
        {
            Limits = options.Limits with
            {
            }
        }, cancellationToken);
    }
    /// <summary>Advances to the next event, invalidating all previously borrowed views.</summary>
    /// <returns>True if an event is current; false after the final event.</returns>
    /// <exception cref="XmlParseException">The XML is rejected.</exception>
    /// <exception cref="InvalidOperationException">The reader is faulted or another operation is active.</exception>
    /// <exception cref="ObjectDisposedException">The reader has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The reader's cancellation token was canceled.</exception>
    /// <remarks>A failed read faults the reader; further reads are not permitted. The input stream remains open.</remarks>
    public bool Read()
    {
        Enter();
        try
        {
            current.Clear();
            ObjectDisposedException.ThrowIf(disposed, this);
            if (faulted)
            {
                throw new InvalidOperationException("Reader is faulted.");
            }

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (completed)
                {
                    return false;
                }

                if (optimistic is not null)
                {
                    while (true)
                    {
                        switch (optimistic.Advance(current))
                        {
                            case OptimisticPullReader.Step.Token:
                                return true;
                            case OptimisticPullReader.Step.Completed:
                                completed = true;
                                return false;
                            case OptimisticPullReader.Step.NeedInput:
                                cancellationToken.ThrowIfCancellationRequested();
                                optimistic.ReadMoreFrom(input);
                                break;
                            case OptimisticPullReader.Step.Declined:
                                FallbackToPump(cancellationToken);
                                goto PumpLoop;
                        }
                    }
                }

            PumpLoop:
                inputBuffer ??= ArrayPool<byte>.Shared.Rent(InputBufferSize);
                pump!.SetAsyncResources(false);
                while (true)
                {
                    switch (pump.Advance(current, cancellationToken))
                    {
                        case StreamingStep.Paused:
                            if (current.Kind == XmlTokenKind.Diagnostic && pump.Diagnostic is { } terminal)
                            {
                                throw new XmlParseException([terminal]);
                            }

                            return true;
                        case StreamingStep.Completed:
                            completed = true;
                            return false;
                        case StreamingStep.Rejected:
                            throw new XmlParseException([pump.Diagnostic!]);
                        case StreamingStep.NeedInput:
                            cancellationToken.ThrowIfCancellationRequested();
                            var count = input.Read(inputBuffer.AsSpan(0, InputBufferSize));
                            pump.Supply(inputBuffer.AsMemory(0, count), count == 0);
                            break;
                        default:
                            throw new InvalidOperationException("Unexpected resource suspension.");
                    }
                }
            }
            catch
            {
                faulted = true;
                current.Clear();
                ReleaseBuffer();
                optimistic?.Dispose();
                optimistic = null;
                try
                {
                    pump?.Dispose();
                }
                catch { }
                throw;
            }
        }
        finally { Exit(); }
    }

    private void FallbackToPump(CancellationToken token)
    {
        var opt = optimistic!;
        optimistic = null;
        var emitted = opt.EmittedTokens;
        try
        {
            pump = new(options, allowBulkText: true);
            pump.SetAsyncResources(false);
            if (!opt.RawInput.IsEmpty || opt.EofSeen)
            {
                pump.Supply(opt.RawInput, opt.EofSeen);
            }
            for (var skipped = 0; skipped < emitted; skipped++)
            {
                current.Clear();
                switch (pump.Advance(current, token))
                {
                    case StreamingStep.Paused:
                        if (current.Kind == XmlTokenKind.Diagnostic && pump.Diagnostic is { } terminal)
                        {
                            throw new XmlParseException([terminal]);
                        }
                        break;
                    case StreamingStep.Rejected:
                        throw new XmlParseException([pump.Diagnostic!]);
                    default:
                        throw new InvalidOperationException("Unexpected fallback replay state.");
                }
            }
            current.Clear();
        }
        finally
        {
            opt.Dispose();
        }
    }

    /// <summary>Releases parser state and invalidates current views without closing the input stream.</summary>
    public void Dispose()
    {
        Enter();
        try
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            current.Clear();
            ReleaseBuffer();
            optimistic?.Dispose();
            optimistic = null;
            try
            {
                pump?.Dispose();
            }
            finally { current.Release(); }
        }
        finally { Exit(); }
    }
    private void ReleaseBuffer()
    {
        if (inputBuffer is { Length: > 0 })
        {
            ArrayPool<byte>.Shared.Return(inputBuffer);
            inputBuffer = null;
        }
    }
    private void Enter()
    {
        if (Interlocked.CompareExchange(ref gate, 1, 0) != 0)
        {
            throw new InvalidOperationException("Reader operation already active.");
        }
    }
    private void Exit() => Volatile.Write(ref gate, 0);
    /// <summary>Gets the current event kind, or <see cref="XmlTokenKind.None"/> when no event is current.</summary>
    public XmlTokenKind TokenKind => current.Kind;
    /// <summary>Gets the current event's source location.</summary>
    public XmlLocation Location => current.Location;
    /// <summary>Gets borrowed text, comment content, or processing-instruction data for the current event.</summary>
    public ReadOnlySpan<char> ValueSpan => current.Get(0);
    /// <summary>Gets the borrowed local name for the current element event.</summary>
    public ReadOnlySpan<char> LocalName => current.OwnedLocalName is { } s ? s : current.Get(1);
    /// <summary>Gets the borrowed prefix for the current element event.</summary>
    public ReadOnlySpan<char> Prefix => current.OwnedPrefix is { } s ? s : current.Get(2);
    /// <summary>Gets the borrowed namespace URI for the current element event.</summary>
    public ReadOnlySpan<char> NamespaceUri => current.OwnedNamespaceUri is { } s ? s : current.Get(3);
    /// <summary>Gets the borrowed target of the current processing instruction.</summary>
    public ReadOnlySpan<char> Target => current.Get(4);
    /// <summary>Gets borrowed attributes for the current start element, or an empty view otherwise.</summary>
    public XmlAttributeView Attributes => new(current.Attributes.AsSpan(0, current.AttributeCount), current.Characters);
    /// <summary>Gets borrowed namespace declarations for the current start element, or an empty view otherwise.</summary>
    public XmlNamespaceView NamespaceDeclarations => new(current.Namespaces.AsSpan(0, current.NamespaceCount));
    /// <summary>Gets whether the current start element used empty-element syntax.</summary>
    public bool IsEmptyElement => current.Empty;
    /// <summary>Gets the borrowed XML declaration, or a default view for other event kinds.</summary>
    public XmlDeclaration Declaration => TokenKind == XmlTokenKind.Declaration ? new(current.Get(5), current.Get(6), current.Flag, Location) : default;
    /// <summary>Gets the borrowed document type header, or a default view for other event kinds.</summary>
    public XmlDocumentTypeView DocumentType => TokenKind == XmlTokenKind.StartDtd ? new(current.Get(5), current.Get(6), current.Get(7), current.HasPublic, current.HasSystem, Location) : default;
    /// <summary>Gets the borrowed DTD declaration, or a default view for other event kinds.</summary>
    public XmlDtdDeclaration DtdDeclaration => TokenKind == XmlTokenKind.DtdDeclaration ? new(current.DtdKind, current.Get(5), current.Get(6), current.Get(7), current.Get(8), current.DefaultKind, current.Get(9), current.HasValue, current.Get(10), current.HasSystem, current.Get(11), current.HasPublic, current.Get(12), current.Parameter, current.Flag, Location) : default;
    /// <summary>Gets the borrowed skipped-entity information, or a default view for other event kinds.</summary>
    public XmlSkippedEntity SkippedEntity => TokenKind == XmlTokenKind.SkippedEntity ? new(current.Get(5), current.Get(6), current.Parameter, Location) : default;
    /// <summary>Gets the current incomplete attribute-default declaration, or null for other event kinds.</summary>
    public XmlIncompleteAttributeDefault? IncompleteAttributeDefault => current.Incomplete;
    /// <summary>Gets the current diagnostic, or null for other event kinds.</summary>
    public XmlDiagnostic? Diagnostic => current.Error;

    // Only the accepted current token is copied. No callback ref view escapes.
    internal sealed class Cursor : XmlStreamingSink
    {
        internal XmlTokenKind Kind;
        internal XmlLocation Location;
        private char[] chars = ArrayPool<char>.Shared.Rent(256);
        private int[] starts = new int[13], lengths = new int[13];
        private int used;
        internal AttributeRow[] Attributes = [];
        internal int AttributeCapacity;
        internal NamespaceRow[] Namespaces = [];
        internal int NamespaceCapacity;
        internal int AttributeCount, NamespaceCount;
        internal string? OwnedLocalName, OwnedPrefix, OwnedNamespaceUri;
        internal bool Empty, HasPublic, HasSystem, HasValue, Parameter;
        private int dirtySlots;
        internal bool? Flag;
        internal XmlDtdDeclarationKind DtdKind;
        internal XmlDtdDefaultKind DefaultKind;
        internal XmlIncompleteAttributeDefault? Incomplete;
        internal XmlDiagnostic? Error;
        internal ReadOnlySpan<char> Characters => chars.AsSpan(0, used);
        internal ReadOnlySpan<char> Get(int slot) => chars.Length == 0 ? ReadOnlySpan<char>.Empty : chars.AsSpan(starts[slot], lengths[slot]);
        internal void SetAsciiText(ReadOnlySpan<byte> asciiBytes, XmlLocation location)
        {
            EnsureChars(asciiBytes.Length);
            Ascii.ToUtf16(asciiBytes, chars.AsSpan(0, asciiBytes.Length), out _);
            used = asciiBytes.Length;
            starts[0] = 0;
            lengths[0] = asciiBytes.Length;
            dirtySlots |= 1;
            Kind = XmlTokenKind.Text;
            Location = location;
        }
        internal void SetUtf8Token(XmlTokenKind kind, ReadOnlySpan<byte> utf8Bytes, bool hasHighBytes, XmlLocation location)
        {
            EnsureChars(utf8Bytes.Length);
            if (!hasHighBytes)
            {
                Ascii.ToUtf16(utf8Bytes, chars.AsSpan(0, utf8Bytes.Length), out _);
                used = utf8Bytes.Length;
            }
            else
            {
                used = Encoding.UTF8.GetChars(utf8Bytes, chars);
            }
            starts[0] = 0;
            lengths[0] = used;
            dirtySlots |= 1;
            Kind = kind;
            Location = location;
        }
        internal void BeginTextBuild()
        {
            used = 0;
            starts[0] = 0;
        }
        internal void AppendUtf8Text(ReadOnlySpan<byte> utf8Bytes, bool hasHighBytes)
        {
            var required = checked(used + utf8Bytes.Length);
            EnsureChars(required);
            if (!hasHighBytes)
            {
                Ascii.ToUtf16(utf8Bytes, chars.AsSpan(used, utf8Bytes.Length), out _);
                used = required;
            }
            else
            {
                used += Encoding.UTF8.GetChars(utf8Bytes, chars.AsSpan(used));
            }
        }
        internal void AppendScalarText(int codePoint)
        {
            EnsureChars(used + 2);
            if (codePoint <= 0xFFFF)
            {
                chars[used++] = (char)codePoint;
            }
            else
            {
                codePoint -= 0x10000;
                chars[used++] = (char)(0xD800 + (codePoint >> 10));
                chars[used++] = (char)(0xDC00 + (codePoint & 0x3FF));
            }
        }
        internal void FinishTextBuild(XmlLocation location)
        {
            lengths[0] = used;
            dirtySlots |= 1;
            Kind = XmlTokenKind.Text;
            Location = location;
        }
        internal void SetAsciiDeclaration(ReadOnlySpan<byte> version, ReadOnlySpan<byte> encoding, bool? standalone, XmlLocation location)
        {
            EnsureChars(version.Length + encoding.Length);
            Ascii.ToUtf16(version, chars.AsSpan(0, version.Length), out _);
            starts[5] = 0;
            lengths[5] = version.Length;
            if (!encoding.IsEmpty)
            {
                Ascii.ToUtf16(encoding, chars.AsSpan(version.Length, encoding.Length), out _);
            }
            starts[6] = version.Length;
            lengths[6] = encoding.Length;
            used = version.Length + encoding.Length;
            dirtySlots |= (1 << 5) | (1 << 6);
            Flag = standalone;
            Kind = XmlTokenKind.Declaration;
            Location = location;
        }
        internal void SetProcessingInstruction(ReadOnlySpan<byte> target, ReadOnlySpan<byte> data, XmlLocation location)
        {
            EnsureChars(target.Length + data.Length);
            Ascii.ToUtf16(target, chars.AsSpan(0, target.Length), out _);
            starts[4] = 0;
            lengths[4] = target.Length;
            var dataChars = 0;
            if (!data.IsEmpty)
            {
                if (Ascii.IsValid(data))
                {
                    Ascii.ToUtf16(data, chars.AsSpan(target.Length, data.Length), out _);
                    dataChars = data.Length;
                }
                else
                {
                    dataChars = Encoding.UTF8.GetChars(data, chars.AsSpan(target.Length));
                }
            }
            starts[0] = target.Length;
            lengths[0] = dataChars;
            used = target.Length + dataChars;
            dirtySlots |= (1 << 4) | 1;
            Kind = XmlTokenKind.ProcessingInstruction;
            Location = location;
        }
        internal void SetPullStartElement(
            string localName,
            string prefix,
            string namespaceUri,
            bool isEmpty,
            XmlLocation location,
            ReadOnlySpan<OptimisticPullReader.NameEntry> names,
            ReadOnlySpan<OptimisticPullReader.AttrSlot> fastAttributes,
            ReadOnlySpan<OptimisticPullReader.AttrSlot> fastDeclarations,
            ReadOnlySpan<char> attrChars)
        {
            OwnedLocalName = localName;
            OwnedPrefix = prefix;
            OwnedNamespaceUri = namespaceUri;
            Empty = isEmpty;
            int attributeCount = fastAttributes.Length, namespaceCount = fastDeclarations.Length;
            EnsureAttributes(attributeCount);
            EnsureNamespaces(namespaceCount);
            AttributeCount = attributeCount;
            NamespaceCount = namespaceCount;
            if (!attrChars.IsEmpty)
            {
                EnsureChars(attrChars.Length);
                attrChars.CopyTo(chars);
                used = attrChars.Length;
            }
            for (var i = 0; i < attributeCount; i++)
            {
                ref readonly var a = ref fastAttributes[i];
                ref readonly var name = ref names[a.NameId];
                Attributes[i] = new(name.Local, name.Prefix, a.NamespaceUri, null, a.ValueStart, a.ValueLength, false, true, ReadOnlyMemory<XmlAttributeValuePart>.Empty, a.Location);
            }
            for (var i = 0; i < namespaceCount; i++)
            {
                ref readonly var d = ref fastDeclarations[i];
                ref readonly var name = ref names[d.NameId];
                var nsPrefix = name.Kind == 1 ? "" : name.Local;
                Namespaces[i] = new(nsPrefix, d.NamespaceUri, d.Location);
            }
            Kind = XmlTokenKind.StartElement;
            Location = location;
        }
        internal void SetOwnedEndElement(string localName, string prefix, string namespaceUri, XmlLocation location)
        {
            OwnedLocalName = localName;
            OwnedPrefix = prefix;
            OwnedNamespaceUri = namespaceUri;
            Kind = XmlTokenKind.EndElement;
            Location = location;
        }
        private void EnsureChars(int required)
        {
            if (required > chars.Length)
            {
                var next = ArrayPool<char>.Shared.Rent(Math.Max(required, checked(chars.Length * 2)));
                chars.AsSpan(0, used).CopyTo(next);
                ArrayPool<char>.Shared.Return(chars);
                chars = next;
            }
        }
        private void EnsureAttributes(int required)
        {
            if (required > AttributeCapacity)
            {
                var next = ArrayPool<AttributeRow>.Shared.Rent(Math.Max(required, 4));
                if (AttributeCapacity != 0)
                {
                    Attributes.AsSpan(0, AttributeCount).CopyTo(next);
                    Attributes.AsSpan(0, AttributeCount).Clear();
                    ArrayPool<AttributeRow>.Shared.Return(Attributes);
                }
                Attributes = next;
                AttributeCapacity = next.Length;
            }
        }
        private void EnsureNamespaces(int required)
        {
            if (required > NamespaceCapacity)
            {
                var next = ArrayPool<NamespaceRow>.Shared.Rent(Math.Max(required, 4));
                if (NamespaceCapacity != 0)
                {
                    Namespaces.AsSpan(0, NamespaceCount).CopyTo(next);
                    Namespaces.AsSpan(0, NamespaceCount).Clear();
                    ArrayPool<NamespaceRow>.Shared.Return(Namespaces);
                }
                Namespaces = next;
                NamespaceCapacity = next.Length;
            }
        }
        private int AppendChars(ReadOnlySpan<char> value)
        {
            var start = used;
            var required = checked(start + value.Length);
            EnsureChars(required);
            value.CopyTo(chars.AsSpan(start));
            used = required;
            return start;
        }
        private void Copy(int slot, ReadOnlySpan<char> value)
        {
            starts[slot] = AppendChars(value);
            lengths[slot] = value.Length;
            dirtySlots |= 1 << slot;
        }
        internal void Clear()
        {
            Kind = XmlTokenKind.None;
            Location = default;
            used = 0;
            OwnedLocalName = OwnedPrefix = OwnedNamespaceUri = null;
            if (dirtySlots != 0)
            {
                if ((dirtySlots & ~0b11111) == 0)
                {
                    starts[0] = starts[1] = starts[2] = starts[3] = starts[4] = 0;
                    lengths[0] = lengths[1] = lengths[2] = lengths[3] = lengths[4] = 0;
                }
                else
                {
                    starts.AsSpan(0, 13).Clear();
                    lengths.AsSpan(0, 13).Clear();
                }
                dirtySlots = 0;
            }
            if (AttributeCount != 0)
            {
                Array.Clear(Attributes, 0, AttributeCount);
                AttributeCount = 0;
            }
            if (NamespaceCount != 0)
            {
                Array.Clear(Namespaces, 0, NamespaceCount);
                NamespaceCount = 0;
            }
            Empty = HasPublic = HasSystem = HasValue = Parameter = false;
            Flag = null;
            DtdKind = default;
            DefaultKind = default;
            Incomplete = null;
            Error = null;
        }
        internal void Release()
        {
            if (chars.Length != 0)
            {
                ArrayPool<char>.Shared.Return(chars);
                chars = [];
            }
            if (AttributeCapacity != 0)
            {
                ArrayPool<AttributeRow>.Shared.Return(Attributes);
                Attributes = [];
                AttributeCapacity = 0;
            }
            if (NamespaceCapacity != 0)
            {
                ArrayPool<NamespaceRow>.Shared.Return(Namespaces);
                Namespaces = [];
                NamespaceCapacity = 0;
            }
        }
        private bool Set(XmlTokenKind kind, XmlLocation location)
        {
            Kind = kind;
            Location = location;
            return false;
        }
        public override bool StartDocument(XmlLocation location) => Set(XmlTokenKind.StartDocument, location);
        public override bool EndDocument(XmlLocation location) => Set(XmlTokenKind.EndDocument, location);
        public override bool StartCData(XmlLocation location) => Set(XmlTokenKind.StartCData, location);
        public override bool EndCData(XmlLocation location) => Set(XmlTokenKind.EndCData, location);
        public override bool EndDtd(XmlLocation location) => Set(XmlTokenKind.EndDtd, location);
        public override bool Text(ReadOnlySpan<char> text, XmlLocation location)
        {
            Copy(0, text);
            return Set(XmlTokenKind.Text, location);
        }
        public override bool Comment(ReadOnlySpan<char> text, XmlLocation location)
        {
            Copy(0, text);
            return Set(XmlTokenKind.Comment, location);
        }
        public override bool ProcessingInstruction(ReadOnlySpan<char> target, ReadOnlySpan<char> data, XmlLocation location)
        {
            Copy(4, target);
            Copy(0, data);
            return Set(XmlTokenKind.ProcessingInstruction, location);
        }
        public override bool StartElement(XmlStartElement element)
        {
            if (element.OwnedLocalName is not null)
            {
                OwnedLocalName = element.OwnedLocalName;
                OwnedPrefix = element.OwnedPrefix;
                OwnedNamespaceUri = element.OwnedNamespaceUri;
            }
            else
            {
                Copy(1, element.LocalName);
                Copy(2, element.Prefix);
                Copy(3, element.NamespaceUri);
            }
            Empty = element.IsEmptyElement;
            int attributeCount = element.Attributes.Count, namespaceCount = element.NamespaceDeclarations.Count;
            EnsureAttributes(attributeCount);
            EnsureNamespaces(namespaceCount);
            AttributeCount = attributeCount;
            NamespaceCount = namespaceCount;
            var rows = element.Attributes.Rows;
            if (!rows.IsEmpty)
            {
                var sourceChars = element.Attributes.Characters;
                for (var i = 0; i < AttributeCount; i++)
                {
                    ref readonly var row = ref rows[i];
                    int start = 0, length = 0;
                    if (row.IsValueComplete && row.Value is null)
                    {
                        var val = sourceChars.Slice(row.ValueStart, row.ValueLength);
                        start = AppendChars(val);
                        length = val.Length;
                    }
                    Attributes[i] = new(row.LocalName, row.Prefix, row.NamespaceUri, row.Value, start, length, row.IsDefaulted, row.IsValueComplete, row.ValueParts, row.Location);
                }
            }
            else if (AttributeCount != 0)
            {
                for (var i = 0; i < AttributeCount; i++)
                {
                    var a = element.Attributes[i];
                    var start = a.IsValueComplete ? AppendChars(a.Value) : 0;
                    var length = a.IsValueComplete ? a.Value.Length : 0;
                    Attributes[i] = new(a.LocalName.ToString(), a.Prefix.ToString(), a.NamespaceUri.ToString(), null, start, length, a.IsDefaulted, a.IsValueComplete, a.ValueParts.IsEmpty ? ReadOnlyMemory<XmlAttributeValuePart>.Empty : a.ValueParts.ToArray(), a.Location);
                }
            }
            var nsRows = element.NamespaceDeclarations.Rows;
            if (!nsRows.IsEmpty)
            {
                nsRows.CopyTo(Namespaces.AsSpan(0, NamespaceCount));
            }
            else if (NamespaceCount != 0)
            {
                for (var i = 0; i < NamespaceCount; i++)
                {
                    var n = element.NamespaceDeclarations[i];
                    Namespaces[i] = new(n.Prefix.ToString(), n.NamespaceUri.ToString(), n.Location);
                }
            }
            return Set(XmlTokenKind.StartElement, element.Location);
        }
        public override bool EndElement(XmlEndElement element)
        {
            if (element.OwnedLocalName is not null)
            {
                OwnedLocalName = element.OwnedLocalName;
                OwnedPrefix = element.OwnedPrefix;
                OwnedNamespaceUri = element.OwnedNamespaceUri;
            }
            else
            {
                Copy(1, element.LocalName);
                Copy(2, element.Prefix);
                Copy(3, element.NamespaceUri);
            }
            return Set(XmlTokenKind.EndElement, element.Location);
        }
        public override bool Declaration(XmlDeclaration declaration)
        {
            Copy(5, declaration.Version);
            Copy(6, declaration.EncodingName);
            Flag = declaration.Standalone;
            return Set(XmlTokenKind.Declaration, declaration.Location);
        }
        public override bool StartDtd(XmlDocumentTypeView d)
        {
            Copy(5, d.Name);
            Copy(6, d.PublicId);
            Copy(7, d.SystemId);
            HasPublic = d.HasPublicId;
            HasSystem = d.HasSystemId;
            return Set(XmlTokenKind.StartDtd, d.Location);
        }
        public override bool DtdDeclaration(XmlDtdDeclaration d)
        {
            Copy(5, d.Name);
            Copy(6, d.ElementName);
            Copy(7, d.ContentModel);
            Copy(8, d.AttributeType);
            Copy(9, d.Value);
            Copy(10, d.SystemId);
            Copy(11, d.PublicId);
            Copy(12, d.NotationName);
            DtdKind = d.Kind;
            DefaultKind = d.DefaultKind;
            HasValue = d.HasValue;
            HasSystem = d.HasSystemId;
            HasPublic = d.HasPublicId;
            Parameter = d.IsParameter;
            Flag = d.IsExternal;
            return Set(XmlTokenKind.DtdDeclaration, d.Location);
        }
        public override bool SkippedEntity(XmlSkippedEntity entity)
        {
            Copy(5, entity.Name);
            Copy(6, entity.LexicalReference);
            Parameter = entity.IsParameter;
            return Set(XmlTokenKind.SkippedEntity, entity.Location);
        }
        public override bool IncompleteAttributeDefault(XmlIncompleteAttributeDefault declaration)
        {
            Incomplete = declaration;
            return Set(XmlTokenKind.IncompleteAttributeDefault, declaration.DeclarationLocation);
        }
        // Read distinguishes the pump terminal diagnostic before publishing a token.
        public override bool Diagnostic(XmlDiagnostic diagnostic)
        {
            Error = diagnostic;
            return Set(XmlTokenKind.Diagnostic, diagnostic.Location);
        }
    }
}
