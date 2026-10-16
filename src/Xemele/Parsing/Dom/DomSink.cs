using System.Text;
using System.Buffers;
using System.Runtime.CompilerServices;
using Xemele.Internal.Operations;
using Xemele.Parsing.Input;
using Xemele.Parsing.Syntax;

namespace Xemele.Parsing.Dom;

// Callback views never escape. Text scratch owns one logical token until its
// final fragment; the final string and names then belong to the document arena.
internal sealed partial class DomSink : IXmlDomBatchSink, IXmlOwnedNameSink, IXmlValidationProvenanceSink
{
    private readonly OperationBudget budget;
    private readonly XmlInputDecoder? decoder;
    private readonly XmlDocument.DomBuilder builder;
    private readonly CancellationToken cancellation;
    private XmlNode parent;
    private XmlTextKind textKind;
    private XmlLocation textLocation;
    private string target = "";
    private readonly StringBuilder text = new();
    private bool prepared;
    private ShortValueCache shortValues;
    private static readonly SearchValues<char> CapacityMarkers = SearchValues.Create("<>='\"");
    private static readonly SearchValues<byte> CapacityByteMarkers = SearchValues.Create("<>='\""u8);
    internal XmlDocument Document
    {
        get;
    }
    internal XmlDiagnostic? Diagnostic
    {
        get; private set;
    }
    internal bool DeclarationValidated
    {
        get; private set;
    }

    internal DomSink(XmlParseOptions options, XmlInputDecoder? decoder = null, CancellationToken cancellationToken = default)
    {
        budget = new(options, cancellationToken);
        cancellation = cancellationToken;
        Document = new(options.Limits);
        builder = Document.BeginDomConstruction();
        parent = Document.DocumentNode;
        this.decoder = decoder;
    }
    public void PrepareInput(ReadOnlySpan<char> input)
    {
        if (prepared || input.IsEmpty)
        {
            return;
        }

        prepared = true;
        int nodes = 1, attributes = 0;
        var tag = false;
        var quote = '\0';
        for (var offset = 0; offset < input.Length && nodes < 256 && attributes < 256;)
        {
            cancellation.ThrowIfCancellationRequested();
            var block = input.Slice(offset, Math.Min(4096, input.Length - offset));
            var at = block.IndexOfAny(CapacityMarkers);
            if (at < 0)
            {
                offset += block.Length;
                continue;
            }
            offset += at;
            var value = input[offset++];
            if (quote != 0)
            {
                if (value == quote)
                {
                    quote = '\0';
                }

                continue;
            }
            if (tag && (value == '\'' || value == '"'))
            {
                quote = (char)value;
                continue;
            }
            if (value == '<' && !tag)
            {
                // Complex declarations/CDATA/comments are not a sizing oracle.
                if (offset < input.Length && input[offset] == '!')
                {
                    break;
                }

                tag = true;
                if (offset == input.Length || input[offset] != '/')
                {
                    nodes++;
                }
            }
            else if (value == '>' && tag)
            {
                tag = false;
                if (offset < input.Length && input[offset] != '<')
                {
                    nodes++;
                }
            }
            else if (value == '=' && tag)
            {
                attributes++;
            }
        }
        ReserveInitialPages(nodes >= 256 || attributes >= 256 ? 256 : nodes, attributes >= 256 || nodes >= 256 ? 256 : attributes);
    }
    internal void PrepareInput(ReadOnlySpan<byte> input)
    {
        if (prepared || input.IsEmpty)
        {
            return;
        }

        prepared = true;
        int nodes = 1, attributes = 0;
        var tag = false;
        var quote = '\0';
        for (var offset = 0; offset < input.Length && nodes < 256 && attributes < 256;)
        {
            cancellation.ThrowIfCancellationRequested();
            var block = input.Slice(offset, Math.Min(4096, input.Length - offset));
            var at = block.IndexOfAny(CapacityByteMarkers);
            if (at < 0)
            {
                offset += block.Length;
                continue;
            }
            offset += at;
            var value = input[offset++];
            if (quote != 0)
            {
                if (value == quote)
                {
                    quote = '\0';
                }

                continue;
            }
            if (tag && (value == '\'' || value == '"'))
            {
                quote = (char)value;
                continue;
            }
            if (value == '<' && !tag)
            {
                // Complex declarations/CDATA/comments are not a sizing oracle.
                if (offset < input.Length && input[offset] == '!')
                {
                    break;
                }

                tag = true;
                if (offset == input.Length || input[offset] != '/')
                {
                    nodes++;
                }
            }
            else if (value == '>' && tag)
            {
                tag = false;
                if (offset < input.Length && input[offset] != '<')
                {
                    nodes++;
                }
            }
            else if (value == '=' && tag)
            {
                attributes++;
            }
        }
        ReserveInitialPages(nodes >= 256 || attributes >= 256 ? 256 : nodes, attributes >= 256 || nodes >= 256 ? 256 : attributes);
    }
    private void ReserveInitialPages(int nodes, int attributes)
    {
        // This bounded lexical estimate is a capacity hint, never a grammar
        // verdict. Quotes/CDATA may overestimate; no more than one page per
        // arena is reserved, and source/caller buffers are never retained.
        Document.Nodes.EnsureCapacity(nodes);
        Document.AttributeSlots.EnsureCapacity(attributes);
        Document.ReserveSourceLocationCapacity(nodes, attributes);
    }
    internal void ValidateAbsentDeclaration(XmlLocation location)
    {
        DeclarationValidated = true;
        Diagnostic = decoder!.ValidateDeclaration([], location);
    }
    public bool StartDocument(XmlLocation location)
    {
        cancellation.ThrowIfCancellationRequested();
        Document.RecordSourceOrigin(location);
        return true;
    }
    public bool Declaration(XmlDeclarationView declaration)
    {
        Document.SetValidationDeclaration(declaration.Standalone, declaration.Location);
        if (decoder is null)
        {
            return true;
        }

        DeclarationValidated = true;
        Diagnostic = decoder.ValidateDeclaration(declaration.EncodingName, declaration.Location);
        return Diagnostic is null;
    }
    private bool Names(ReadOnlySpan<char> a, ReadOnlySpan<char> b, ReadOnlySpan<char> c, XmlLocation location)
    {
        var missing = 0;
        if (!a.IsEmpty && Document.FindInternedName(a) is null)
        {
            missing++;
        }

        if (!b.IsEmpty && !b.SequenceEqual(a) && Document.FindInternedName(b) is null)
        {
            missing++;
        }

        if (!c.IsEmpty && !c.SequenceEqual(a) && !c.SequenceEqual(b) && Document.FindInternedName(c) is null)
        {
            missing++;
        }

        if (budget.TryCharge(Quota.InternedNames, missing))
        {
            return true;
        }

        Diagnostic = new("XML.ResourceLimit", XmlDiagnosticSeverity.Error, "DOM interned-name limit exceeded.", location);
        return false;
    }
    private string OwnName(ReadOnlySpan<char> value) => Document.FindInternedName(value) ?? value.ToString();
    public bool StartElement(XmlNameView name, bool isEmptyElement, XmlLocation location)
    {
        cancellation.ThrowIfCancellationRequested();
        if (!Names(name.LocalName, name.NamespaceUri, name.Prefix, location))
        {
            return false;
        }

        var element = builder.AppendParsedNode(parent, XmlNodeKind.Element, OwnName(name.LocalName), OwnName(name.NamespaceUri), OwnName(name.Prefix), "", cancellation);
        Document.RecordSourceLocation(element, location);
        parent = element;
        return true;
    }
    public bool CompleteTag(XmlParserCore.Name name, XmlParserCore.DomTagView attributes, bool empty, XmlLocation location)
    {
        if (!StartOwnedElement(name.Local, name.Uri, name.Prefix, empty, location))
        {
            return false;
        }

        for (var i = 0; i < attributes.Count; i++)
        {
            var attribute = attributes[i];
            if (!attribute.Namespace)
            {
                continue;
            }

            if (!Names(attribute.Prefix, attribute.Uri, [], attribute.Location))
            {
                return false;
            }

            var stored = builder.AppendSyntaxValidatedNamespaceDeclaration(parent, attribute.Prefix, attribute.Uri, cancellation);
            Document.RecordSourceLocation(stored, attribute.Location);
        }
        for (var i = 0; i < attributes.Count; i++)
        {
            var attribute = attributes[i];
            if (attribute.Namespace)
            {
                continue;
            }

            var value = attribute.Value;
            if (!OwnedAttribute(attribute.Name.Local, attribute.Name.Uri, attribute.Name.Prefix, value,
                attribute.OwnedValue, attribute.Location, attribute.IsDefaulted))
            {
                return false;
            }
        }
        return true;
    }
    public bool StartOwnedElement(string local, string uri, string prefix, bool empty, XmlLocation location)
    {
        cancellation.ThrowIfCancellationRequested();
        if (!Document.HasNameTriple(local, uri, prefix) && !Names(local, uri, prefix, location))
        {
            return false;
        }

        var element = builder.AppendParsedNode(parent, XmlNodeKind.Element, local, uri, prefix, "", cancellation);
        Document.RecordSourceLocation(element, location);
        parent = element;
        return true;
    }
    public bool OwnedAttribute(string local, string uri, string prefix, ReadOnlySpan<char> value, string? ownedValue, XmlLocation location, bool defaulted)
    {
        cancellation.ThrowIfCancellationRequested();
        if (!Document.HasNameTriple(local, uri, prefix) && !Names(local, uri, prefix, location))
        {
            return false;
        }

        var stored = builder.AppendSyntaxValidatedAttribute(parent, local, shortValues.Own(value, ownedValue), uri, prefix, cancellation);
        Document.RecordSourceLocation(stored, location);
        return true;
    }
    public bool NamespaceDeclaration(XmlNamespaceView declaration)
    {
        cancellation.ThrowIfCancellationRequested();
        if (!Names(declaration.Prefix, declaration.NamespaceUri, [], declaration.Location))
        {
            return false;
        }

        var stored = builder.AppendSyntaxValidatedNamespaceDeclaration(parent, OwnName(declaration.Prefix), OwnName(declaration.NamespaceUri), cancellation);
        Document.RecordSourceLocation(stored, declaration.Location);
        return true;
    }
    public bool Attribute(XmlAttributeView attribute)
    {
        cancellation.ThrowIfCancellationRequested();
        var name = attribute.Name;
        if (!Names(name.LocalName, name.NamespaceUri, name.Prefix, attribute.Location))
        {
            return false;
        }

        var stored = builder.AppendSyntaxValidatedAttribute(parent, OwnName(name.LocalName), shortValues.Own(attribute.Value), OwnName(name.NamespaceUri), OwnName(name.Prefix), cancellation);
        Document.RecordSourceLocation(stored, attribute.Location);
        return true;
    }
    public bool StartElementContent() => true;
    public bool EndElement(XmlNameView name, XmlLocation location)
    {
        parent.Require();
        parent = Document.Handle(Document.Nodes[parent.Slot].Parent);
        return true;
    }
    public bool StartText(XmlTextKind kind, ReadOnlySpan<char> piTarget, XmlLocation location)
    {
        cancellation.ThrowIfCancellationRequested();
        textKind = kind;
        textLocation = location;
        text.Clear();
        if (!Names(piTarget, [], [], location))
        {
            return false;
        }

        target = OwnName(piTarget);
        return true;
    }
    public bool NormalizedTextRun(ReadOnlySpan<char> raw, bool ended, XmlLocation location)
    {
        cancellation.ThrowIfCancellationRequested();
        // Syntax already proved this bounded run contains only ordinary XML
        // scalars and the five complete predefined references. No source escapes.
        if (raw.Length > Array.MaxLength - text.Length)
        {
            // Conservative source-length check falls back to the exact decoded
            // length, since reference spellings shrink during normalization.
            var length = 0;
            for (var i = 0; i < raw.Length;)
            {
                if (raw[i] != '&')
                {
                    i++;
                    length++;
                    continue;
                }
                i += raw[i + 1] is 'l' or 'g' ? 4 : raw[i + 1] == 'a' && raw[i + 2] == 'm' ? 5 : 6;
                length++;
            }
            if (length > Array.MaxLength - text.Length)
            {
                Diagnostic = new("XML.ResourceLimit", XmlDiagnosticSeverity.Error, "DOM text capacity exceeded.", location);
                return false;
            }
        }
        while (!raw.IsEmpty)
        {
            var reference = raw.IndexOf('&');
            if (reference < 0)
            {
                text.Append(raw);
                break;
            }
            text.Append(raw[..reference]);
            raw = raw[reference..];
            char value;
            int length;
            switch (raw[1])
            {
                case 'l':
                    value = '<';
                    length = 4;
                    break;
                case 'g':
                    value = '>';
                    length = 4;
                    break;
                case 'q':
                    value = '"';
                    length = 6;
                    break;
                default:
                    var amp = raw[2] == 'm';
                    value = amp ? '&' : '\'';
                    length = amp ? 5 : 6;
                    break;
            }
            text.Append(value);
            raw = raw[length..];
        }
        return !ended || TextFragment([], true, location);
    }
    public bool TextFragment(ReadOnlySpan<char> fragment, bool tokenEnded, XmlLocation location)
    {
        cancellation.ThrowIfCancellationRequested();
        // Outer whitespace is delivered by syntax but cannot be a document child.
        if (textKind == XmlTextKind.Text)
        {
            parent.Require();
            if (parent.Slot == 0)
            {
                return true;
            }
        }
        if (fragment.Length > Array.MaxLength - text.Length)
        {
            Diagnostic = new("XML.ResourceLimit", XmlDiagnosticSeverity.Error, "DOM text capacity exceeded.", location);
            return false;
        }
        if (!tokenEnded)
        {
            text.Append(fragment);
            return true;
        }
        // A complete single borrowed fragment needs just its final owned
        // string. Fragmented/normalized tokens retain accumulated scratch.
        string value;
        if (text.Length == 0)
        {
            value = shortValues.Own(fragment);
        }
        else
        {
            text.Append(fragment);
            value = text.ToString();
        }
        var kind = textKind switch
        {
            XmlTextKind.Text => XmlNodeKind.Text,
            XmlTextKind.CData => XmlNodeKind.CData,
            XmlTextKind.Comment => XmlNodeKind.Comment,
            _ => XmlNodeKind.ProcessingInstruction
        };
        var node = builder.AppendParsedNode(parent, kind, kind == XmlNodeKind.ProcessingInstruction ? target : "", "", "", value, cancellation);
        Document.RecordSourceLocation(node, textLocation);
        text.Clear();
        return true;
    }
    // Only immutable owned strings survive this operation-local, fixed-size
    // accelerator. Sampling chooses a slot; full ordinal equality proves hits.
    internal struct ShortValueCache
    {
        [InlineArray(64)]
        private struct Entries
        {
            private string? first;
        }
        private Entries entries;
        internal string Own(ReadOnlySpan<char> value, string? owned = null)
        {
            if (value.IsEmpty)
            {
                return string.Empty;
            }

            if (value.Length > 32)
            {
                return owned ?? value.ToString();
            }

            var key = unchecked((uint)(value.Length * 397 ^ value[0] * 31 ^ value[value.Length / 2] * 17 ^ value[^1]));
            key ^= key >> 6;
            ref var entry = ref entries[(int)(key & 63)];
            if (entry is not null && value.SequenceEqual(entry))
            {
                return entry;
            }

            return entry = owned ?? value.ToString();
        }
        internal string OwnAscii(ReadOnlySpan<byte> value)
        {
            if (value.IsEmpty)
            {
                return string.Empty;
            }

            if (value.Length > 32)
            {
                return Encoding.Latin1.GetString(value);
            }

            var key = unchecked((uint)(value.Length * 397 ^ value[0] * 31 ^ value[value.Length / 2] * 17 ^ value[^1]));
            key ^= key >> 6;
            ref var entry = ref entries[(int)(key & 63)];
            if (entry is not null && System.Text.Ascii.Equals(value, entry))
            {
                return entry;
            }

            return entry = Encoding.Latin1.GetString(value);
        }
    }
    public void ValidationProvenance(XmlDiagnostic diagnostic, Xemele.Parsing.Dtd.DtdElementDeclaration declaration)
    {
        cancellation.ThrowIfCancellationRequested();
        Document.SetValidationInstanceProvenance(diagnostic, declaration);
    }
    public bool EndDocument(XmlLocation location)
    {
        cancellation.ThrowIfCancellationRequested();
        if (!budget.Options.Recover)
        {
            Document.MarkValidationSourceComplete();
        }

        return true;
    }
}
