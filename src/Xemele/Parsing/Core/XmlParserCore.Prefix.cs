using System.Buffers;
using System.Text;
using Xemele.Internal.Operations;
using Xemele.Parsing.Dtd;
using Xemele.Parsing.Entities;

namespace Xemele.Parsing.Syntax;

internal sealed partial class XmlParserCore
{
    internal readonly record struct DoctypeScan(int End, XmlDocumentType Declaration, DtdModel Model, long Expanded, long Names);

    // Captures the DOCTYPE and stops the core immediately after its closing '>'.
    private sealed class DoctypeProbe : IXmlEventSink, IXmlDtdSink
    {
        internal XmlDocumentType? Doctype;
        internal DtdModel? Model;
        public bool StartDocument(XmlLocation location) => true;
        public bool Declaration(XmlDeclarationView declaration) => true;
        public bool StartElement(XmlNameView name, bool isEmptyElement, XmlLocation location) => true;
        public bool NamespaceDeclaration(XmlNamespaceView declaration) => true;
        public bool Attribute(XmlAttributeView attribute) => true;
        public bool StartElementContent() => true;
        public bool EndElement(XmlNameView name, XmlLocation location) => true;
        public bool StartText(XmlTextKind kind, ReadOnlySpan<char> target, XmlLocation location) => true;
        public bool TextFragment(ReadOnlySpan<char> text, bool tokenEnded, XmlLocation location) => true;
        public bool EndDocument(XmlLocation location) => true;
        public bool DocumentType(XmlDocumentType declaration, DtdModel model, XmlLocation location)
        {
            Doctype = declaration; Model = model;
            return false;
        }
    }

    // Runs the real parser over a DOCTYPE that starts at input[start] (line/column are its coordinates) and reports
    // where it ends together with the exact metadata the streaming parser would deliver. External subsets are never
    // loaded: the probe has no resolver, so any external identifier or parameter reference is refused here.
    // Any failure, limit hit or unusual condition returns false and the caller falls back to the streaming parser.
    internal static bool TryScanDoctype(ReadOnlySpan<byte> input, int start, int line, int column, bool charInput,
        XmlParseOptions options, CancellationToken cancellationToken, out DoctypeScan scan)
    {
        scan = default;
        const int MaxBytes = 1 << 20;
        XmlParserCore? core = null;
        try
        {
            core = new XmlParserCore(options with { Resolver = null }, trustedByteMappings: !charInput);
            core.position.Seed(line, column);
            var probe = new DoctypeProbe();
            char[] chars = new char[2048];
            long[] offsets = new long[2048];
            int at = start, capacity = 256;
            while (true)
            {
                if (at >= input.Length || at - start > MaxBytes) return false;
                int count = 0;
                while (at < input.Length && count + 2 <= capacity)
                {
                    if (Rune.DecodeFromUtf8(input[at..], out Rune rune, out int used) != OperationStatus.Done) return false;
                    if (rune.IsBmp) { chars[count] = (char)rune.Value; offsets[count++] = at; }
                    else { rune.EncodeToUtf16(chars.AsSpan(count)); offsets[count] = offsets[count + 1] = at; count += 2; }
                    at += used;
                }
                var result = core.Append(chars.AsSpan(0, count), charInput ? default : offsets.AsSpan(0, count), false, probe, cancellationToken);
                if (result.Step == ScanStep.NeedInput) { capacity = Math.Min(capacity * 2, chars.Length); continue; }
                if (result.Step != ScanStep.Paused || probe.Doctype is null || probe.Model is null || core.doctypeSystem is not null ||
                    core.subsetHasParameterReferences || core.incompleteDefaults is { Count: > 0 }) return false;
                int end = result.Consumed < count ? (int)offsets[result.Consumed] : at;
                if (end <= start || input[end - 1] != (byte)'>') return false;
                scan = new(end, probe.Doctype, probe.Model, core.budget.Count(Quota.ExpandedCharacters), core.budget.Count(Quota.InternedNames));
                return true;
            }
        }
        catch (ArgumentException) { return false; }
        finally { core?.Dispose(); }
    }
}
