using System.Buffers;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Text;
using System.Text.Unicode;
using Xemele.Internal.Operations;

namespace Xemele.Streaming;

// One distinct raw name (prefix:local) seen by the optimistic SAX parser.
internal sealed class SaxName
{
    internal byte[] Key = [];
    internal uint Hash;
    internal string Prefix = "", Local = "";
    // 0 plain, 1 "xmlns", 2 "xmlns:prefix".
    internal int Kind;
    internal string? ElementUri, AttributeUri;
    // Scope id the cached resolution is valid for; -2 means every scope.
    internal int ElementScope = -1, AttributeScope = -1;
    // Position in the parser's name list (the token tape refers to names by it) and the ids of the cached namespace URIs.
    internal int Id, ElementUriId, AttributeUriId;
}

// An attribute (or namespace declaration) of the tag being delivered; its value lives in the shared character buffer.
internal struct FastAttribute
{
    internal SaxName Name;
    internal string NamespaceUri;
    internal int ValueStart, ValueLength, UriId;
    internal XmlLocation Location;
}

// Limits of the SAX token tape and, for tests, a record of what the parser did with it. The tape is a pooled int array
// that the validating pass fills so the delivery pass can replay it without scanning again; when a cap is reached the
// parser gives up recording and the delivery pass re-scans the input instead.
internal sealed class SaxTapeProbe
{
    internal const int DefaultMaxTapeInts = 1 << 23, DefaultMaxArenaChars = 1 << 23;
    internal int MaxTapeInts = DefaultMaxTapeInts, MaxArenaChars = DefaultMaxArenaChars;
    // Starting tape size; 0 picks a size from the input length. Tests use small values to exercise growth.
    internal int InitialTapeInts;
    // Outcome: the delivery pass replayed the tape, the final tape and attribute-character sizes, and pooled array balance.
    internal bool Replayed;
    internal int TapeInts, ArenaChars, Rented, Returned;
}

internal static class OptimisticSax
{
    // Whole-buffer SAX parse for the common well-formed subset (the same subset as the optimistic DOM parser).
    // A first pass validates the entire document without delivering anything; only then does a second pass
    // deliver events straight from the input. A document the first pass declines is left untouched for the
    // streaming parser, so diagnostics, recovery and every unusual construct keep their authoritative behavior.
    internal static bool TryParse(ReadOnlySpan<byte> input, IXmlSaxHandler handler, XmlParseOptions options, CancellationToken cancellationToken, SaxTapeProbe? probe = null)
    {
        var limits = options.Limits;
        if (options.Recover || input.IsEmpty) return false;
        if (input.Length > (limits.DocumentCharacters ?? long.MaxValue) || input.Length > (limits.ExpandedCharacters ?? long.MaxValue)) return false;
        return input.Contains((byte)'\r')
            ? RunNormalized(input, handler, options, cancellationToken, charInput: false, probe)
            : new SaxParser(input, handler, options, cancellationToken, charInput: false, probe: probe).Run();
    }

    // CR and CRLF input is parsed from an LF-normalized copy; line and column numbers are unchanged and byte offsets are mapped back.
    private static bool RunNormalized(ReadOnlySpan<byte> input, IXmlSaxHandler handler, XmlParseOptions options, CancellationToken cancellationToken, bool charInput, SaxTapeProbe? probe)
    {
        byte[] buffer = FastLineBreaks.Rent(input.Length, out bool pooled);
        int[]? dropped = null;
        try
        {
            int length = FastLineBreaks.Normalize(input, buffer, !charInput, ref dropped, out int droppedCount);
            var parser = new SaxParser(buffer.AsSpan(0, length), handler, options, cancellationToken, charInput, input.Length, dropped, droppedCount, normalized: true, probe: probe);
            return parser.Run();
        }
        finally { FastLineBreaks.Release(buffer, pooled, dropped); }
    }

    internal static bool TryParse(ReadOnlySpan<char> input, IXmlSaxHandler handler, XmlParseOptions options, CancellationToken cancellationToken, SaxTapeProbe? probe = null)
    {
        var limits = options.Limits;
        if (options.Recover || input.IsEmpty || input[0] == '\uFEFF') return false;
        if (input.Length > (limits.DocumentCharacters ?? long.MaxValue) || input.Length > (limits.ExpandedCharacters ?? long.MaxValue)) return false;
        int byteCount = Encoding.UTF8.GetByteCount(input);
        bool pooled = byteCount <= 1 << 20;
        byte[] rented = pooled ? ArrayPool<byte>.Shared.Rent(byteCount) : GC.AllocateUninitializedArray<byte>(byteCount);
        try
        {
            if (Utf8.FromUtf16(input, rented, out _, out int written, replaceInvalidSequences: false) != OperationStatus.Done || written != byteCount) return false;
            if (byteCount > (limits.DocumentCharacters ?? long.MaxValue) || byteCount > (limits.ExpandedCharacters ?? long.MaxValue)) return false;
            var bytes = rented.AsSpan(0, byteCount);
            return bytes.Contains((byte)'\r')
                ? RunNormalized(bytes, handler, options, cancellationToken, charInput: true, probe)
                : new SaxParser(bytes, handler, options, cancellationToken, charInput: true, probe: probe).Run();
        }
        finally { if (pooled) ArrayPool<byte>.Shared.Return(rented); }
    }

    private struct Frame
    {
        internal SaxName Name;
        internal string Uri;
        internal int NameStart, NameLength, SavedScope, BindingStart, UriId;
    }

    // Token tape: pass 1 appends one record per delivered token and the replay walks them. All values are ints; offsets
    // index the (LF-normalized) input, names and URIs are ids into the parser's lists, locations are line, column and
    // the already mapped byte offset. Records:
    //   Declaration  version start/end, encoding start/end, standalone (0 absent, 1 yes, 2 no), byte offset
    //   Misc, Text, Comment, CData  start, end (an input span; whitespace outside the root, plain text, comment/CDATA body)
    //   Pi           target start/end, body start/end
    //   Doctype      (none; the doctype read by pass 1 delivers itself)
    //   RichText     parts up to PartEnd: PartSpan start/end, PartChar code point, PartEntity entity id
    //   Start        head (kind, empty flag, attribute count, declaration count), name, uri, line, column, offset, then 7 ints per
    //                attribute (name, uri, value start, value length, line, column, offset), then 5 per namespace declaration
    //                (name, uri, line, column, offset); value starts index the attribute character arena
    //   End          name, uri, line, column, offset
    private const int KDeclaration = 1, KMisc = 2, KText = 3, KComment = 4, KCData = 5, KPi = 6, KDoctype = 7, KRichText = 8, KStart = 9, KEnd = 10;
    private const int PartEnd = 0, PartSpan = 1, PartChar = 2, PartEntity = 3;
    private const int StartHeader = 6, AttributeRecord = 7, DeclarationRecord = 5, EndRecord = 6;

    private ref struct SaxParser
    {
        private const uint Seed = 2166136261, Prime = 16777619;
        private const int MaxMembers = 32;
        // Bounds linear probing so crafted colliding names cannot degrade the name table; the streaming parser then decides.
        private const int MaxProbes = 256;
        private const int ScopeAny = -2;
        private const int TextChunk = 8192;
        private const string XmlNamespace = "http://www.w3.org/XML/1998/namespace";
        private const string XmlnsNamespace = "http://www.w3.org/2000/xmlns/";

        // 1 name character, 2 name start.
        private static readonly byte[] FlagTable = BuildFlags();
        private static readonly SearchValues<byte> TextStops = SearchValues.Create(
            [(byte)'<', (byte)'&', (byte)']', 0, 1, 2, 3, 4, 5, 6, 7, 8, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31]);
        private static readonly SearchValues<byte> Controls = SearchValues.Create(
            [0, 1, 2, 3, 4, 5, 6, 7, 8, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31]);

        private static byte[] BuildFlags()
        {
            var t = new byte[256];
            for (int c = 'a'; c <= 'z'; c++) t[c] = 3;
            for (int c = 'A'; c <= 'Z'; c++) t[c] = 3;
            t['_'] = 3;
            for (int c = '0'; c <= '9'; c++) t[c] = 1;
            t['.'] = 1; t['-'] = 1; t[':'] = 1;
            return t;
        }

        private readonly ReadOnlySpan<byte> s;
        private readonly int n;
        private readonly bool charInput;
        private readonly IXmlSaxHandler handler;
        private readonly CancellationToken cancellation;
        private readonly int maxDepth, maxMembers, tokenLimit, nameLimit;
        private readonly long referenceLimit;
        private long referenceBudget;
        private bool emit;
        private int i, line, lineStart, cont;
        private int depth, scope, scopeCounter, internCount;
        private bool rootSeen;
        private readonly XmlParseOptions options;
        private readonly bool normalized, innerReferencesAllowed;
        private bool doctypeSeen;
        private long nameBase;
        // Read by the first pass and reused by the delivery pass.
        private OptimisticDoctype? doctype;
        private Frame[] frames;
        private SaxName?[] table;
        private int tableCount;
        private string[] bindingPrefixes, bindingUris;
        private int bindingCount;
        private char[]? scratch;
        private char[]? attributeChars;
        private int attributeLength;
        private FastAttribute[] attributes, declarations;
        private NamespaceRow[] namespaces;
        private int attributeCount, declarationCount;
        private Dictionary<string, int>? uris;
        private List<int>? declared;
        private int declaredIndex;
        private readonly SaxTapeProbe? probe;
        private readonly int maxTapeInts, maxArenaChars;
        // True while pass 1 is filling the tape; cleared (and the tape returned) when a cap is hit.
        private bool record;
        private int[]? tape;
        private int tapeLength;
        private SaxName[] nameById;
        private int nameCount;
        private string[] uriList;
        private int uriCount;
        private int[] bindingUriIds;
        private OptimisticEntity?[] entityList = [];
        private int entityCount;

        internal SaxParser(ReadOnlySpan<byte> input, IXmlSaxHandler handler, XmlParseOptions options, CancellationToken cancellation, bool charInput, int rawLength = -1, int[]? dropped = null, int droppedCount = 0, bool normalized = false, SaxTapeProbe? probe = null)
        {
            var limits = options.Limits;
            this.options = options; this.normalized = normalized; innerReferencesAllowed = (limits.EntityDepth ?? int.MaxValue) >= 2;
            this.handler = handler; this.charInput = charInput; s = input; n = input.Length; this.cancellation = cancellation;
            maxDepth = limits.Depth ?? int.MaxValue;
            maxMembers = Math.Min(MaxMembers, limits.AttributesPerElement ?? int.MaxValue);
            tokenLimit = limits.TokenCharacters ?? int.MaxValue;
            nameLimit = limits.InternedNames ?? int.MaxValue;
            referenceLimit = (limits.ExpandedCharacters ?? long.MaxValue) - (rawLength < 0 ? input.Length : rawLength);
            this.dropped = dropped; this.droppedCount = droppedCount;
            this.probe = probe;
            maxTapeInts = probe?.MaxTapeInts ?? SaxTapeProbe.DefaultMaxTapeInts;
            maxArenaChars = probe?.MaxArenaChars ?? SaxTapeProbe.DefaultMaxArenaChars;
            frames = new Frame[16];
            table = new SaxName?[128];
            nameById = new SaxName[32];
            uriList = new string[8]; uriList[0] = ""; uriList[1] = XmlNamespace; uriCount = 2;
            bindingPrefixes = new string[8]; bindingUris = new string[8]; bindingUriIds = new int[8];
            attributes = new FastAttribute[8]; declarations = new FastAttribute[4]; namespaces = new NamespaceRow[4];
        }

        private T[] Rent<T>(int size)
        {
            if (probe is not null) probe.Rented++;
            return ArrayPool<T>.Shared.Rent(size);
        }

        private void Return<T>(T[] array)
        {
            if (probe is not null) probe.Returned++;
            ArrayPool<T>.Shared.Return(array);
        }

        // Pass 1 validates the whole document (delivering nothing) while recording the tape; when the tape stays within its caps
        // the delivery pass replays it, otherwise it re-scans the input. Pooled buffers go back on every path.
        internal bool Run()
        {
            try
            {
                int initial = probe is { InitialTapeInts: > 0 } ? probe.InitialTapeInts : Math.Clamp(n / 4, 256, 1 << 20);
                tape = Rent<int>(Math.Min(initial, maxTapeInts));
                record = true;
                if (!Pass(false)) return false;
                if (record)
                {
                    if (probe is not null) { probe.Replayed = true; probe.TapeInts = tapeLength; probe.ArenaChars = attributeLength; }
                    Replay();
                }
                else if (!Pass(true)) throw new InvalidOperationException("The optimistic SAX parser rejected a document that it had already validated.");
                return true;
            }
            finally
            {
                if (tape is not null) Return(tape);
                if (scratch is not null) Return(scratch);
                if (attributeChars is not null) Return(attributeChars);
            }
        }

        // Delivers the recorded tape: same events, locations and cancellation checks as the re-scanning delivery pass.
        private void Replay()
        {
            handler.StartDocument();
            cancellation.ThrowIfCancellationRequested();
            ReadOnlySpan<int> t = tape.AsSpan(0, tapeLength);
            for (int p = 0; p < t.Length; )
            {
                int head = t[p];
                switch (head & 0xFF)
                {
                    case KStart:
                    {
                        bool empty = (head & 0x100) != 0;
                        int attributeTotal = (head >> 16) & 0xFF, declarationTotal = (head >>> 24) & 0xFF;
                        var name = nameById[t[p + 1]];
                        string elementUri = uriList[t[p + 2]];
                        var location = new XmlLocation(null, t[p + 3], t[p + 4], charInput ? null : t[p + 5]);
                        p += StartHeader;
                        if (attributeTotal > attributes.Length) Array.Resize(ref attributes, Math.Max(attributeTotal, attributes.Length * 2));
                        for (int a = 0; a < attributeTotal; a++, p += AttributeRecord)
                        {
                            ref var attribute = ref attributes[a];
                            attribute.Name = nameById[t[p]]; attribute.NamespaceUri = uriList[t[p + 1]];
                            attribute.ValueStart = t[p + 2]; attribute.ValueLength = t[p + 3];
                            attribute.Location = new XmlLocation(null, t[p + 4], t[p + 5], charInput ? null : t[p + 6]);
                        }
                        if (declarationTotal > namespaces.Length) namespaces = new NamespaceRow[Math.Max(declarationTotal, namespaces.Length * 2)];
                        for (int d = 0; d < declarationTotal; d++, p += DeclarationRecord)
                        {
                            var declared = nameById[t[p]];
                            namespaces[d] = new(declared.Kind == 1 ? "" : declared.Local, uriList[t[p + 1]],
                                new XmlLocation(null, t[p + 2], t[p + 3], charInput ? null : t[p + 4]));
                        }
                        handler.StartElement(new XmlStartElement(name.Local, name.Prefix, elementUri,
                            new XmlAttributeView(attributes.AsSpan(0, attributeTotal), attributeChars), new XmlNamespaceView(namespaces.AsSpan(0, declarationTotal)), empty, location));
                        cancellation.ThrowIfCancellationRequested();
                        if (empty) { handler.EndElement(new XmlEndElement(name.Local, name.Prefix, elementUri, location)); cancellation.ThrowIfCancellationRequested(); }
                        break;
                    }
                    case KEnd:
                    {
                        var name = nameById[t[p + 1]];
                        handler.EndElement(new XmlEndElement(name.Local, name.Prefix, uriList[t[p + 2]], new XmlLocation(null, t[p + 3], t[p + 4], charInput ? null : t[p + 5])));
                        cancellation.ThrowIfCancellationRequested();
                        p += EndRecord;
                        break;
                    }
                    case KText: EmitText(t[p + 1], t[p + 2]); p += 3; break;
                    case KRichText: p = ReplayRichText(t, p + 1); break;
                    case KMisc:
                    {
                        var buffer = Scratch(TextChunk);
                        for (int at = t[p + 1], end = t[p + 2]; at < end; )
                        {
                            int count = Math.Min(TextChunk, end - at);
                            Ascii.ToUtf16(s.Slice(at, count), buffer, out _);
                            handler.Text(buffer.AsSpan(0, count));
                            cancellation.ThrowIfCancellationRequested();
                            at += count;
                        }
                        p += 3;
                        break;
                    }
                    case KComment:
                    {
                        int length = Decode(t[p + 1], t[p + 2], out _);
                        handler.Comment(scratch.AsSpan(0, length));
                        cancellation.ThrowIfCancellationRequested();
                        p += 3;
                        break;
                    }
                    case KCData:
                        handler.StartCData();
                        cancellation.ThrowIfCancellationRequested();
                        EmitText(t[p + 1], t[p + 2]);
                        handler.EndCData();
                        cancellation.ThrowIfCancellationRequested();
                        p += 3;
                        break;
                    case KPi:
                    {
                        var target = s[t[p + 1]..t[p + 2]];
                        var body = s[t[p + 3]..t[p + 4]];
                        var buffer = Scratch(target.Length + body.Length + 1);
                        Ascii.ToUtf16(target, buffer, out _);
                        Utf8.ToUtf16(body, buffer.AsSpan(target.Length), out _, out int written, replaceInvalidSequences: false);
                        handler.ProcessingInstruction(buffer.AsSpan(0, target.Length), buffer.AsSpan(target.Length, written));
                        cancellation.ThrowIfCancellationRequested();
                        p += 5;
                        break;
                    }
                    case KDeclaration:
                    {
                        int versionStart = t[p + 1], versionLength = t[p + 2] - versionStart, encodingStart = t[p + 3], encodingLength = t[p + 4] - encodingStart;
                        var buffer = Scratch(versionLength + encodingLength + 2);
                        Ascii.ToUtf16(s.Slice(versionStart, versionLength), buffer, out _);
                        Ascii.ToUtf16(s.Slice(encodingStart, encodingLength), buffer.AsSpan(versionLength), out _);
                        bool? standalone = t[p + 5] == 0 ? null : t[p + 5] == 1;
                        handler.Declaration(new XmlDeclaration(buffer.AsSpan(0, versionLength), buffer.AsSpan(versionLength, encodingLength), standalone,
                            new XmlLocation(null, 1, 1, charInput ? null : t[p + 6])));
                        cancellation.ThrowIfCancellationRequested();
                        p += 7;
                        break;
                    }
                    case KDoctype:
                        doctype!.Deliver(handler, cancellation);
                        p += 1;
                        break;
                    default: throw new InvalidOperationException("Corrupt SAX token tape.");
                }
            }
            handler.EndDocument();
        }

        // Replays the parts of one text run with references; returns the index after its end marker.
        private int ReplayRichText(ReadOnlySpan<int> t, int p)
        {
            int length = 0;
            Scratch(TextChunk + 8);
            while (true)
            {
                switch (t[p])
                {
                    case PartSpan: AppendText(t[p + 1], t[p + 2], ref length); p += 3; break;
                    case PartEntity: AppendEntity(entityList[t[p + 1]]!.Content, ref length); p += 2; break;
                    case PartChar:
                        if (length + 2 > TextChunk) { handler.Text(scratch.AsSpan(0, length)); cancellation.ThrowIfCancellationRequested(); length = 0; }
                        length = Append(scratch!, length, t[p + 1]);
                        p += 2;
                        break;
                    default:
                        if (length > 0) { handler.Text(scratch.AsSpan(0, length)); cancellation.ThrowIfCancellationRequested(); }
                        return p + 1;
                }
            }
        }

        // Starts a tape record of count ints and returns its index, or -1 once recording has stopped.
        private int Take(int count)
        {
            if (!record) return -1;
            int at = tapeLength;
            long need = (long)at + count;
            if (need > maxTapeInts) { StopRecording(); return -1; }
            var current = tape!;
            if (need > current.Length)
            {
                var grown = Rent<int>((int)Math.Min(maxTapeInts, Math.Max(need, (long)current.Length * 2)));
                current.AsSpan(0, at).CopyTo(grown);
                Return(current);
                tape = grown;
            }
            tapeLength = (int)need;
            return at;
        }

        private void StopRecording()
        {
            record = false;
            if (tape is not null) { Return(tape); tape = null; }
            tapeLength = 0;
        }

        private bool Pass(bool deliver)
        {
            emit = deliver;
            if (deliver) record = false;
            i = 0; line = 1; lineStart = 0; cont = 0; depth = 0; scope = 0; bindingCount = 0; rootSeen = false;
            referenceBudget = referenceLimit; declaredIndex = 0; doctypeSeen = false; nameBase = 0;
            frames[0] = default;
            if (deliver) { handler.StartDocument(); cancellation.ThrowIfCancellationRequested(); }
            if (n >= 3 && s[0] == 0xEF && s[1] == 0xBB && s[2] == 0xBF) { i = 3; lineStart = 3; }
            var head = s[i..];
            if (head.Length >= 6 && head.StartsWith("<?xml"u8) && head[5] is (byte)' ' or (byte)'\t' or (byte)'\n' or (byte)'\r')
            { if (!Declaration()) return false; }
            if (!Prolog()) return false;
            if (!Content()) return false;
            if (!Epilog()) return false;
            if (deliver) handler.EndDocument();
            return true;
        }

        private readonly int[]? dropped;
        private readonly int droppedCount;
        private int droppedCursor;
        private int RawOffset(int atOffset) => dropped is null ? atOffset : FastLineBreaks.Raw(dropped, droppedCount, ref droppedCursor, atOffset);
        private XmlLocation Loc(int atLine, int atColumn, int atOffset) => new(null, atLine, atColumn, charInput ? null : RawOffset(atOffset));

        private int Column(int position) => position - lineStart - cont + 1;

        private void SkipWhitespace()
        {
            while ((uint)i < (uint)n)
            {
                byte c = s[i];
                if (c == ' ' || c == '\t') i++;
                else if (c == '\n') { i++; line++; lineStart = i; cont = 0; }
                else break;
            }
        }

        private static int ContinuationBytes(ReadOnlySpan<byte> bytes)
        {
            int count = 0, at = 0;
            if (Vector128.IsHardwareAccelerated && bytes.Length >= Vector128<byte>.Count)
            {
                ref byte origin = ref MemoryMarshal.GetReference(bytes);
                var threshold = Vector128.Create((sbyte)-64);
                for (; at <= bytes.Length - Vector128<byte>.Count; at += Vector128<byte>.Count)
                {
                    var block = Vector128.LoadUnsafe(ref origin, (nuint)at).AsSByte();
                    count += BitOperations.PopCount(Vector128.LessThan(block, threshold).ExtractMostSignificantBits());
                }
            }
            for (; at < bytes.Length; at++) if ((bytes[at] & 0xC0) == 0x80) count++;
            return count;
        }

        private static bool HasNonCharacter(ReadOnlySpan<byte> bytes)
        {
            while (true)
            {
                int at = bytes.IndexOf((byte)0xEF);
                if (at < 0 || at + 2 >= bytes.Length) return false;
                if (bytes[at + 1] == 0xBF && bytes[at + 2] is 0xBE or 0xBF) return true;
                bytes = bytes[(at + 1)..];
            }
        }

        // Advances line/column state past a validated run; high means it contains non-ASCII bytes.
        private void TrackLines(int start, int end, bool high)
        {
            var run = s[start..end];
            int newline = run.LastIndexOf((byte)'\n');
            if (newline >= 0)
            {
                line += run.Count((byte)'\n');
                lineStart = start + newline + 1;
                cont = high ? ContinuationBytes(run[(newline + 1)..]) : 0;
            }
            else if (high) cont += ContinuationBytes(run);
        }

        // Validates a content run and advances the line state past it. Controls (including CR) reject.
        private bool Scan(int start, int end, bool checkControls)
        {
            if (start == end) return true;
            var run = s[start..end];
            if (checkControls && run.IndexOfAny(Controls) >= 0) return false;
            bool high = !Ascii.IsValid(run);
            if (high && (!Utf8.IsValid(run) || HasNonCharacter(run))) return false;
            TrackLines(start, end, high);
            return true;
        }

        private char[] Scratch(int capacity)
        {
            if (scratch is null || scratch.Length < capacity)
            {
                var grown = Rent<char>(Math.Max(capacity, TextChunk + 8));
                if (scratch is not null) Return(scratch);
                scratch = grown;
            }
            return scratch;
        }

        // Decodes a validated run into the scratch buffer from the given start and returns the character count.
        private int Decode(int start, int end, out bool high)
        {
            var run = s[start..end];
            var buffer = Scratch(run.Length + 1);
            Utf8.ToUtf16(run, buffer, out _, out int written, replaceInvalidSequences: false);
            high = written != run.Length;
            return written;
        }

        private bool Declaration()
        {
            int declarationOffset = i;
            i += 5;
            SkipWhitespace();
            if (!Keyword("version"u8)) return false;
            SkipWhitespace(); if (!Eat('=')) return false; SkipWhitespace();
            if ((uint)i >= (uint)n || s[i] is not ((byte)'"' or (byte)'\'')) return false;
            byte quote = s[i++];
            if (!(i + 1 < n && s[i] == '1' && s[i + 1] == '.')) return false;
            int versionStart = i;
            i += 2;
            int digits = 0; while ((uint)i < (uint)n && s[i] is >= (byte)'0' and <= (byte)'9') { i++; digits++; }
            int versionEnd = i;
            if (digits == 0 || (uint)i >= (uint)n || s[i++] != quote) return false;
            int mark = i; SkipWhitespace(); bool spaced = i > mark;
            int encodingStart = 0, encodingEnd = 0;
            if (Keyword("encoding"u8))
            {
                if (!spaced) return false;
                SkipWhitespace(); if (!Eat('=')) return false; SkipWhitespace();
                if ((uint)i >= (uint)n || s[i] is not ((byte)'"' or (byte)'\'')) return false;
                quote = s[i++];
                int close = s[i..].IndexOf(quote);
                if (close < 0 || !Ascii.EqualsIgnoreCase(s.Slice(i, close), "UTF-8"u8)) return false;
                encodingStart = i; encodingEnd = i + close;
                i += close + 1;
                mark = i; SkipWhitespace(); spaced = i > mark;
            }
            bool? standalone = null;
            if (Keyword("standalone"u8))
            {
                if (!spaced) return false;
                SkipWhitespace(); if (!Eat('=')) return false; SkipWhitespace();
                if ((uint)i >= (uint)n || s[i] is not ((byte)'"' or (byte)'\'')) return false;
                quote = s[i++];
                if (n - i >= 4 && s[i] == 'y' && s[i + 1] == 'e' && s[i + 2] == 's' && s[i + 3] == quote) { standalone = true; i += 4; }
                else if (n - i >= 3 && s[i] == 'n' && s[i + 1] == 'o' && s[i + 2] == quote) { standalone = false; i += 3; }
                else return false;
                SkipWhitespace();
            }
            if (!(i + 1 < n && s[i] == '?' && s[i + 1] == '>')) return false;
            i += 2;
            if (i - declarationOffset + 8 >= tokenLimit) return false;
            if (emit)
            {
                var buffer = Scratch(versionEnd - versionStart + encodingEnd - encodingStart + 2);
                int versionLength = versionEnd - versionStart, encodingLength = encodingEnd - encodingStart;
                Ascii.ToUtf16(s[versionStart..versionEnd], buffer, out _);
                Ascii.ToUtf16(s[encodingStart..encodingEnd], buffer.AsSpan(versionLength), out _);
                handler.Declaration(new XmlDeclaration(buffer.AsSpan(0, versionLength), buffer.AsSpan(versionLength, encodingLength), standalone,
                    new XmlLocation(null, 1, 1, charInput ? null : declarationOffset)));
                cancellation.ThrowIfCancellationRequested();
            }
            else if (Take(7) is var w and >= 0)
            {
                var t = tape!;
                t[w] = KDeclaration; t[w + 1] = versionStart; t[w + 2] = versionEnd; t[w + 3] = encodingStart; t[w + 4] = encodingEnd;
                t[w + 5] = standalone is null ? 0 : standalone.Value ? 1 : 2; t[w + 6] = declarationOffset;
            }
            return true;
        }

        private bool Keyword(ReadOnlySpan<byte> word)
        {
            if (!s[i..].StartsWith(word)) return false;
            i += word.Length; return true;
        }

        private bool Eat(char c)
        {
            if ((uint)i < (uint)n && s[i] == c) { i++; return true; }
            return false;
        }

        // Whitespace between prolog and epilog markup is delivered as text, like any other character data.
        private bool Misc()
        {
            int start = i;
            SkipWhitespace();
            if (!emit)
            {
                if (i - start >= tokenLimit) return false;
                if (i != start && Take(3) is var w and >= 0) { var t = tape!; t[w] = KMisc; t[w + 1] = start; t[w + 2] = i; }
                return true;
            }
            if (i == start) return true;
            var buffer = Scratch(TextChunk);
            for (int at = start; at < i; )
            {
                int count = Math.Min(TextChunk, i - at);
                Ascii.ToUtf16(s.Slice(at, count), buffer, out _);
                handler.Text(buffer.AsSpan(0, count));
                cancellation.ThrowIfCancellationRequested();
                at += count;
            }
            return true;
        }

        private bool Prolog()
        {
            while (true)
            {
                if (!Misc()) return false;
                if ((uint)i >= (uint)n || s[i] != '<' || i + 1 >= n) return false;
                byte next = s[i + 1];
                if (next == '!') { if (!(i + 2 < n && s[i + 2] == 'D' ? Doctype() : Comment())) return false; }
                else if (next == '?') { if (!ProcessingInstruction()) return false; }
                else return true;
            }
        }

        private bool Doctype()
        {
            if (doctypeSeen || normalized) return false;
            doctypeSeen = true;
            if (!emit)
            {
                if (!OptimisticDoctype.TryRead(s, i, line, Column(i), charInput, options, cancellation, out doctype)) return false;
            }
            var read = doctype!;
            referenceBudget -= read.Expanded;
            if (referenceBudget < 0) return false;
            if (emit) read.Deliver(handler, cancellation);
            else if (Take(1) is var w and >= 0) tape![w] = KDoctype;
            TrackLines(i, read.End, !Ascii.IsValid(s[i..read.End]));
            i = read.End;
            nameBase = read.Names;
            return true;
        }

        // 1: a declared entity (charged against the expansion budget), 0: not an entity reference (predefined, numeric or no DTD,
        // left to Reference), -1: unknown, undeclared or unsupported, so the streaming parser decides.
        private int EntityAt(int at, out OptimisticEntity? entity, out int referenceLength)
        {
            entity = null; referenceLength = 0;
            if (doctype is null || !doctypeSeen || at + 2 >= n || (FlagTable[s[at + 1]] & 2) == 0) return 0;
            int p = at + 2, limit = Math.Min(n, at + 130);
            while (p < limit && (FlagTable[s[p]] & 1) != 0 && s[p] != ':') p++;
            if (p >= n || s[p] != ';') return 0;
            var name = s[(at + 1)..p];
            if (name.SequenceEqual("lt"u8) || name.SequenceEqual("gt"u8) || name.SequenceEqual("amp"u8) || name.SequenceEqual("apos"u8) || name.SequenceEqual("quot"u8)) return 0;
            if (!doctype.TryFind(name, out entity) || entity is null) return -1;
            if (entity.InnerRefs != 0 && !innerReferencesAllowed) return -1;
            referenceBudget -= entity.Length + entity.InnerRefs;
            if (referenceBudget < 0) return -1;
            if (entity.TapeId < 0)
            {
                if (entityCount == entityList.Length) Array.Resize(ref entityList, Math.Max(4, entityCount * 2));
                entity.TapeId = entityCount;
                entityList[entityCount++] = entity;
            }
            referenceLength = p + 1 - at;
            return 1;
        }

        // Appends replacement text to the scratch buffer, delivering a chunk whenever it fills (never inside a surrogate pair).
        private void AppendEntity(string text, ref int length)
        {
            var buffer = Scratch(TextChunk + 8);
            int taken = 0;
            while (taken < text.Length)
            {
                int room = TextChunk - length;
                if (room < 2) { handler.Text(buffer.AsSpan(0, length)); cancellation.ThrowIfCancellationRequested(); length = 0; room = TextChunk; }
                int count = Math.Min(room, text.Length - taken);
                if (taken + count < text.Length && char.IsHighSurrogate(text[taken + count - 1])) count--;
                text.CopyTo(taken, buffer, length, count);
                length += count; taken += count;
            }
        }

        private bool Epilog()
        {
            while (true)
            {
                if (!Misc()) return false;
                if (i >= n) return true;
                if (s[i] != '<' || i + 1 >= n) return false;
                byte next = s[i + 1];
                if (next == '!') { if (!Comment()) return false; }
                else if (next == '?') { if (!ProcessingInstruction()) return false; }
                else return false;
            }
        }

        private bool Content()
        {
            // The root start tag is next (Prolog stops at the first element-like markup).
            if (s[i + 1] == '/' || s[i + 1] == '!' || s[i + 1] == '?') return false;
            int guard = 0;
            do
            {
                if ((++guard & 0x3FF) == 0) cancellation.ThrowIfCancellationRequested();
                if (i >= n) return false;
                if (s[i] != '<') { if (!Text()) return false; continue; }
                if (i + 1 >= n) return false;
                switch (s[i + 1])
                {
                    case (byte)'/': if (!CloseTag()) return false; break;
                    case (byte)'!': if (!(i + 2 < n && s[i + 2] == '[' ? CData() : Comment())) return false; break;
                    case (byte)'?': if (!ProcessingInstruction()) return false; break;
                    default: if (!OpenTag()) return false; break;
                }
            }
            while (depth > 0);
            return true;
        }

        // Delivers a validated run (no references) as text, a bounded chunk at a time; true when it contains non-ASCII characters.
        private bool EmitText(int start, int end)
        {
            var run = s[start..end];
            var buffer = Scratch(TextChunk).AsSpan(0, TextChunk);
            bool high = false;
            while (!run.IsEmpty)
            {
                Utf8.ToUtf16(run, buffer, out int read, out int written, replaceInvalidSequences: false);
                if (written != read) high = true;
                if (written > 0) { handler.Text(buffer[..written]); cancellation.ThrowIfCancellationRequested(); }
                run = run[read..];
            }
            return high;
        }

        private void EmitRun(int start, int end) => TrackLines(start, end, EmitText(start, end));

        private bool Text()
        {
            int start = i;
            int searchFrom = i;
            while (true)
            {
                int at = s[searchFrom..].IndexOfAny(TextStops);
                if (at < 0) return false;
                int end = searchFrom + at;
                byte stop = s[end];
                if (stop == '<')
                {
                    if (!emit && (end - start >= tokenLimit || !Scan(start, end, false))) return false;
                    if (emit) EmitRun(start, end);
                    else if (Take(3) is var w and >= 0) { var t = tape!; t[w] = KText; t[w + 1] = start; t[w + 2] = end; }
                    i = end; return true;
                }
                if (stop == ']')
                {
                    if (end + 2 < n && s[end + 1] == ']' && s[end + 2] == '>') return false;
                    searchFrom = end + 1; continue;
                }
                if (stop != '&') return false;
                return TextWithReferences(start);
            }
        }

        // Appends a validated segment to the scratch buffer, delivering a chunk whenever it fills.
        private void AppendSegment(int start, int end, ref int length) => TrackLines(start, end, AppendText(start, end, ref length));

        private bool AppendText(int start, int end, ref int length)
        {
            var run = s[start..end];
            var buffer = Scratch(TextChunk + 8);
            bool high = false;
            while (!run.IsEmpty)
            {
                var status = Utf8.ToUtf16(run, buffer.AsSpan(length, TextChunk - length), out int read, out int written, replaceInvalidSequences: false);
                if (written != read) high = true;
                length += written;
                run = run[read..];
                if (status == OperationStatus.DestinationTooSmall) { handler.Text(buffer.AsSpan(0, length)); cancellation.ThrowIfCancellationRequested(); length = 0; }
            }
            return high;
        }

        // Appends one rich-text part (or the record's kind) to the tape; the sizes are fixed per kind.
        private void Put(int kind, int a, int b)
        {
            int count = kind == KRichText || kind == PartEnd ? 1 : kind == PartSpan ? 3 : 2;
            int w = Take(count);
            if (w < 0) return;
            var t = tape!;
            t[w] = kind;
            if (count > 1) t[w + 1] = a;
            if (count > 2) t[w + 2] = b;
        }

        private bool TextWithReferences(int start)
        {
            int length = 0, segment = start, at = start;
            long extra = 0;
            if (!emit) Put(KRichText, 0, 0);
            while (true)
            {
                int found = s[at..].IndexOfAny(TextStops);
                if (found < 0) return false;
                int end = at + found;
                byte stop = s[end];
                if (stop == ']')
                {
                    if (end + 2 < n && s[end + 1] == ']' && s[end + 2] == '>') return false;
                    at = end + 1; continue;
                }
                if (stop != '<' && stop != '&') return false;
                if (!emit && (end - start + extra >= tokenLimit || !Scan(segment, end, false))) return false;
                if (emit) AppendSegment(segment, end, ref length);
                else if (end > segment) Put(PartSpan, segment, end);
                if (stop == '<')
                {
                    if (emit && length > 0) { handler.Text(scratch.AsSpan(0, length)); cancellation.ThrowIfCancellationRequested(); }
                    else if (!emit) Put(PartEnd, 0, 0);
                    i = end; return true;
                }
                int kind = EntityAt(end, out var entity, out int referenceLength);
                if (kind < 0) return false;
                if (kind > 0)
                {
                    extra += entity!.Length;
                    if (emit) AppendEntity(entity.Content, ref length);
                    else Put(PartEntity, entity.TapeId, 0);
                }
                else
                {
                    if (!Reference(end, out int value, out referenceLength)) return false;
                    if (emit)
                    {
                        if (length + 2 > TextChunk) { handler.Text(scratch.AsSpan(0, length)); cancellation.ThrowIfCancellationRequested(); length = 0; }
                        length = Append(scratch!, length, value);
                    }
                    else Put(PartChar, value, 0);
                }
                i = segment = at = end + referenceLength;
            }
        }

        // Decodes a predefined or numeric reference at '&'; control characters and anything unusual bail.
        private bool Reference(int at, out int value, out int length)
        {
            value = 0; length = 0;
            if (--referenceBudget < 0 || at + 3 >= n) return false;
            switch (s[at + 1])
            {
                case (byte)'#': return NumericReference(at, out value, out length);
                case (byte)'l': if (s[at + 2] == 't' && s[at + 3] == ';') { value = '<'; length = 4; return true; } return false;
                case (byte)'g': if (s[at + 2] == 't' && s[at + 3] == ';') { value = '>'; length = 4; return true; } return false;
                case (byte)'a':
                    if (s[at + 2] == 'm' && s[at + 3] == 'p' && at + 4 < n && s[at + 4] == ';') { value = '&'; length = 5; return true; }
                    if (s[at + 2] == 'p' && s[at + 3] == 'o' && at + 5 < n && s[at + 4] == 's' && s[at + 5] == ';') { value = '\''; length = 6; return true; }
                    return false;
                case (byte)'q':
                    if (s[at + 2] == 'u' && s[at + 3] == 'o' && at + 5 < n && s[at + 4] == 't' && s[at + 5] == ';') { value = '"'; length = 6; return true; }
                    return false;
            }
            return false;
        }

        private bool NumericReference(int at, out int value, out int length)
        {
            value = 0; length = 0;
            int p = at + 2, digits = 0;
            long code = 0;
            if (s[p] == 'x')
            {
                p++;
                for (; p < n && digits < 8; p++, digits++)
                {
                    byte b = s[p]; int d;
                    if (b >= '0' && b <= '9') d = b - '0';
                    else if (b >= 'a' && b <= 'f') d = b - 'a' + 10;
                    else if (b >= 'A' && b <= 'F') d = b - 'A' + 10;
                    else break;
                    code = code * 16 + d;
                }
            }
            else
            {
                for (; p < n && digits < 8; p++, digits++)
                {
                    byte b = s[p];
                    if (b < '0' || b > '9') break;
                    code = code * 10 + (b - '0');
                }
            }
            if (digits == 0 || p >= n || s[p] != ';') return false;
            if ((code < 0x20 && code is not (9 or 10)) || code > 0x10FFFF || code is >= 0xD800 and <= 0xDFFF or 0xFFFE or 0xFFFF) return false;
            value = (int)code; length = p + 1 - at;
            return true;
        }

        private static int Append(char[] buffer, int length, int codePoint)
        {
            if (codePoint <= 0xFFFF) { buffer[length] = (char)codePoint; return length + 1; }
            codePoint -= 0x10000;
            buffer[length] = (char)(0xD800 + (codePoint >> 10));
            buffer[length + 1] = (char)(0xDC00 + (codePoint & 0x3FF));
            return length + 2;
        }

        private bool Comment()
        {
            // At "<!"; only "<!--" is accepted here (DOCTYPE and others fall back).
            int open = i;
            if (!(open + 3 < n && s[open + 2] == '-' && s[open + 3] == '-')) return false;
            int bodyStart = open + 4;
            int dash = s[bodyStart..].IndexOf("--"u8);
            if (dash < 0) return false;
            int bodyEnd = bodyStart + dash;
            if (bodyEnd + 2 >= n || s[bodyEnd + 2] != '>') return false;
            if (emit)
            {
                int length = Decode(bodyStart, bodyEnd, out bool high);
                TrackLines(bodyStart, bodyEnd, high);
                handler.Comment(scratch.AsSpan(0, length));
                cancellation.ThrowIfCancellationRequested();
            }
            else if (bodyEnd + 3 - open >= tokenLimit || !Scan(bodyStart, bodyEnd, true)) return false;
            else if (Take(3) is var w and >= 0) { var t = tape!; t[w] = KComment; t[w + 1] = bodyStart; t[w + 2] = bodyEnd; }
            i = bodyEnd + 3;
            return true;
        }

        private bool CData()
        {
            int open = i;
            if (!s[open..].StartsWith("<![CDATA["u8)) return false;
            int bodyStart = open + 9;
            int close = s[bodyStart..].IndexOf("]]>"u8);
            if (close < 0) return false;
            int bodyEnd = bodyStart + close;
            if (emit)
            {
                handler.StartCData();
                cancellation.ThrowIfCancellationRequested();
                EmitRun(bodyStart, bodyEnd);
                handler.EndCData();
                cancellation.ThrowIfCancellationRequested();
            }
            else if (bodyEnd + 3 - open >= tokenLimit || !Scan(bodyStart, bodyEnd, true)) return false;
            else if (Take(3) is var w and >= 0) { var t = tape!; t[w] = KCData; t[w + 1] = bodyStart; t[w + 2] = bodyEnd; }
            i = bodyEnd + 3;
            return true;
        }

        private bool ProcessingInstruction()
        {
            int open = i;
            int p = open + 2, targetStart = p;
            if (p >= n || (FlagTable[s[p]] & 2) == 0) return false;
            while (p < n && (FlagTable[s[p]] & 1) != 0 && s[p] != ':') p++;
            if (p >= n || s[p] == ':') return false;
            var target = s[targetStart..p];
            if (target.Length == 3 && Ascii.EqualsIgnoreCase(target, "xml"u8)) return false;
            int bodyStart, bodyEnd;
            if (s[p] == '?')
            {
                if (p + 1 >= n || s[p + 1] != '>') return false;
                bodyStart = bodyEnd = p; p += 2;
            }
            else
            {
                if (s[p] is not ((byte)' ' or (byte)'\t' or (byte)'\n')) return false;
                i = p; SkipWhitespace();
                bodyStart = i;
                int close = s[bodyStart..].IndexOf("?>"u8);
                if (close < 0) return false;
                bodyEnd = bodyStart + close; p = bodyEnd + 2;
                if (!emit && (bodyEnd + 2 - open >= tokenLimit || !Scan(bodyStart, bodyEnd, true))) return false;
            }
            if (!emit && p - open >= tokenLimit) return false;
            if (emit)
            {
                int targetLength = target.Length;
                var buffer = Scratch(targetLength + (bodyEnd - bodyStart) + 1);
                Ascii.ToUtf16(target, buffer, out _);
                Utf8.ToUtf16(s[bodyStart..bodyEnd], buffer.AsSpan(targetLength), out _, out int written, replaceInvalidSequences: false);
                TrackLines(bodyStart, bodyEnd, written != bodyEnd - bodyStart);
                handler.ProcessingInstruction(buffer.AsSpan(0, targetLength), buffer.AsSpan(targetLength, written));
                cancellation.ThrowIfCancellationRequested();
            }
            else
            {
                if (++internCount + nameBase + 8 > nameLimit) return false;
                if (Take(5) is var w and >= 0) { var t = tape!; t[w] = KPi; t[w + 1] = targetStart; t[w + 2] = targetStart + target.Length; t[w + 3] = bodyStart; t[w + 4] = bodyEnd; }
            }
            i = p;
            return true;
        }

        private bool CloseTag()
        {
            if (depth == 0) return false;
            ref var frame = ref frames[depth];
            int tagStart = i, closeLine = emit || record ? line : 0, closeColumn = emit || record ? Column(i) : 0;
            int p = i + 2;
            var open = s.Slice(frame.NameStart, frame.NameLength);
            if (n - p < open.Length || !s.Slice(p, open.Length).SequenceEqual(open)) return false;
            p += open.Length;
            i = p; SkipWhitespace();
            if ((uint)i >= (uint)n || s[i] != '>') return false;
            i++;
            if (!emit && i - tagStart >= tokenLimit) return false;
            scope = frame.SavedScope; bindingCount = frame.BindingStart;
            if (emit)
            {
                handler.EndElement(new XmlEndElement(frame.Name.Local, frame.Name.Prefix, frame.Uri, Loc(closeLine, closeColumn, tagStart)));
                cancellation.ThrowIfCancellationRequested();
            }
            else if (Take(EndRecord) is var w and >= 0)
            {
                var t = tape!;
                t[w] = KEnd; t[w + 1] = frame.Name.Id; t[w + 2] = frame.UriId; t[w + 3] = closeLine; t[w + 4] = closeColumn; t[w + 5] = charInput ? 0 : RawOffset(tagStart);
            }
            depth--;
            return true;
        }

        private SaxName? FindOrCreateName(int start, int length, uint hash)
        {
            var key = s.Slice(start, length);
            uint mask = (uint)table.Length - 1;
            uint slot = (hash ^ (hash >> 15)) & mask;
            int probes = 0;
            while (table[slot] is { } entry)
            {
                if (entry.Hash == hash && entry.Key.AsSpan().SequenceEqual(key)) return entry;
                if (++probes > MaxProbes) return null;
                slot = (slot + 1) & mask;
            }
            return CreateName(key, hash, slot);
        }

        private SaxName? CreateName(ReadOnlySpan<byte> key, uint hash, uint slot)
        {
            if ((FlagTable[key[0]] & 2) == 0) return null;
            int colon = key.IndexOf((byte)':');
            if (colon >= 0 && key[(colon + 1)..].Contains((byte)':')) return null;
            if (colon == 0 || colon == key.Length - 1) return null;
            if (colon > 0 && (FlagTable[key[colon + 1]] & 2) == 0) return null;
            internCount += 2;
            if (internCount + nameBase + 8 > nameLimit) return null;
            var entry = new SaxName { Key = key.ToArray(), Hash = hash };
            string prefix = colon < 0 ? "" : Latin(key[..colon]);
            string local = colon < 0 ? Latin(key) : Latin(key[(colon + 1)..]);
            if (colon < 0 && key.SequenceEqual("xmlns"u8)) entry.Kind = 1;
            else if (prefix == "xmlns")
            {
                if (local is "xmlns" or "xml") return null;
                entry.Kind = 2;
            }
            if (entry.Kind == 2) entry.Local = local;
            else if (entry.Kind == 0) { entry.Prefix = prefix; entry.Local = local; }
            entry.Id = nameCount;
            if (nameCount == nameById.Length) Array.Resize(ref nameById, nameCount * 2);
            nameById[nameCount++] = entry;
            table[slot] = entry;
            if (++tableCount * 2 > table.Length) GrowTable();
            return entry;
        }

        private static string Latin(ReadOnlySpan<byte> bytes) => Encoding.Latin1.GetString(bytes);

        private void GrowTable()
        {
            var old = table;
            var grown = new SaxName?[old.Length * 2];
            uint mask = (uint)grown.Length - 1;
            foreach (var entry in old)
            {
                if (entry is null) continue;
                uint slot = (entry.Hash ^ (entry.Hash >> 15)) & mask;
                while (grown[slot] is not null) slot = (slot + 1) & mask;
                grown[slot] = entry;
            }
            table = grown;
        }

        // Binding lookup for the current element/attribute scope; null means unbound.
        private string? Lookup(string prefix, out int id)
        {
            for (int b = bindingCount - 1; b >= 0; b--) if (bindingPrefixes[b] == prefix) { id = bindingUriIds[b]; return bindingUris[b]; }
            id = 0;
            return prefix.Length == 0 ? "" : null;
        }

        private string? ElementUri(SaxName entry)
        {
            if (entry.ElementScope == scope || entry.ElementScope == ScopeAny) return entry.ElementUri;
            string prefix = entry.Prefix;
            string? uri; int validFor = scope, id;
            if (prefix == "xml") { uri = XmlNamespace; id = 1; validFor = ScopeAny; }
            else uri = Lookup(prefix, out id);
            if (uri is null) return null;
            entry.ElementUri = uri; entry.ElementUriId = id;
            entry.ElementScope = validFor;
            return uri;
        }

        private string? AttributeUri(SaxName entry)
        {
            if (entry.AttributeScope == scope || entry.AttributeScope == ScopeAny) return entry.AttributeUri;
            string prefix = entry.Prefix;
            string? uri; int validFor = scope, id = 0;
            if (prefix.Length == 0) { uri = ""; validFor = ScopeAny; }
            else if (prefix == "xml") { uri = XmlNamespace; id = 1; validFor = ScopeAny; }
            else uri = Lookup(prefix, out id);
            if (uri is null) return null;
            entry.AttributeUri = uri; entry.AttributeUriId = id;
            entry.AttributeScope = validFor;
            return uri;
        }

        private bool OpenTag()
        {
            bool locate = emit || record;
            int tagStart = i, tagLine = locate ? line : 0, tagColumn = locate ? Column(i) : 0;
            if (depth >= maxDepth - 1 || (depth == 0 && rootSeen)) return false;
            uint hash = Seed; int p = i + 1, nameStart = p;
            while ((uint)p < (uint)n) { byte b = s[p]; if ((FlagTable[b] & 1) == 0) break; hash = (hash ^ b) * Prime; p++; }
            if (p == nameStart) return false;
            var name = FindOrCreateName(nameStart, p - nameStart, hash);
            if (name is null || name.Kind != 0) return false;
            int nameLength = p - nameStart;
            i = p; attributeCount = 0; declarationCount = 0;
            if (!record) attributeLength = 0;
            bool empty = false;
            while (true)
            {
                int mark = i; SkipWhitespace();
                if ((uint)i >= (uint)n) return false;
                byte c = s[i];
                if (c == '>') { i++; break; }
                if (c == '/')
                {
                    if (i + 1 < n && s[i + 1] == '>') { i += 2; empty = true; break; }
                    return false;
                }
                if (i == mark || attributeCount + declarationCount >= maxMembers) return false;
                int attributeStart = i, attributeLine = locate ? line : 0, attributeColumn = locate ? Column(i) : 0;
                uint h = Seed; p = i;
                while ((uint)p < (uint)n) { byte b = s[p]; if ((FlagTable[b] & 1) == 0) break; h = (h ^ b) * Prime; p++; }
                if (p == i) return false;
                var key = FindOrCreateName(attributeStart, p - attributeStart, h);
                if (key is null) return false;
                i = p; SkipWhitespace();
                if (!Eat('=')) return false;
                SkipWhitespace();
                if ((uint)i >= (uint)n) return false;
                byte quote = s[i];
                if (quote != '"' && quote != '\'') return false;
                i++;
                if (!AttributeValue(quote, emit || record || key.Kind != 0, out int valueStart, out int valueLength)) return false;
                if (key.Kind == 0)
                {
                    if (attributeCount == attributes.Length) Array.Resize(ref attributes, attributes.Length * 2);
                    ref var attribute = ref attributes[attributeCount++];
                    attribute.Name = key; attribute.ValueStart = valueStart; attribute.ValueLength = valueLength;
                    if (locate) attribute.Location = Loc(attributeLine, attributeColumn, attributeStart);
                }
                else
                {
                    if (declarationCount == declarations.Length) Array.Resize(ref declarations, declarations.Length * 2);
                    ref var declaration = ref declarations[declarationCount++];
                    declaration.Name = key; declaration.ValueStart = valueStart; declaration.ValueLength = valueLength;
                    if (locate) declaration.Location = Loc(attributeLine, attributeColumn, attributeStart);
                }
            }
            if (i - tagStart >= tokenLimit) return false;

            int savedScope = scope, savedBindings = bindingCount;
            if (declarationCount > 0 && !Declare()) return false;

            string? elementUri = ElementUri(name);
            if (elementUri is null) return false;
            for (int a = 0; a < attributeCount; a++)
            {
                ref var attribute = ref attributes[a];
                string? uri = AttributeUri(attribute.Name);
                if (uri is null) return false;
                attribute.NamespaceUri = uri; attribute.UriId = attribute.Name.AttributeUriId;
            }
            if (!emit && attributeCount > 1 && !AttributesAreUnique()) return false;

            if (emit)
            {
                if (namespaces.Length < declarationCount) namespaces = new NamespaceRow[declarations.Length];
                for (int d = 0; d < declarationCount; d++)
                {
                    ref var declaration = ref declarations[d];
                    namespaces[d] = new(declaration.Name.Kind == 1 ? "" : declaration.Name.Local, declaration.NamespaceUri, declaration.Location);
                }
                var location = Loc(tagLine, tagColumn, tagStart);
                handler.StartElement(new XmlStartElement(name.Local, name.Prefix, elementUri,
                    new XmlAttributeView(attributes.AsSpan(0, attributeCount), attributeChars), new XmlNamespaceView(namespaces.AsSpan(0, declarationCount)), empty, location));
                cancellation.ThrowIfCancellationRequested();
                if (empty) { handler.EndElement(new XmlEndElement(name.Local, name.Prefix, elementUri, location)); cancellation.ThrowIfCancellationRequested(); }
            }
            else if (record) RecordStart(name, tagLine, tagColumn, tagStart, empty);
            if (empty)
            {
                scope = savedScope; bindingCount = savedBindings;
                rootSeen |= depth == 0;
                return true;
            }
            depth++;
            if (depth >= frames.Length) Array.Resize(ref frames, frames.Length * 2);
            frames[depth] = new() { Name = name, Uri = elementUri, UriId = name.ElementUriId, NameStart = nameStart, NameLength = nameLength, SavedScope = savedScope, BindingStart = savedBindings };
            rootSeen = true;
            return true;
        }

        private void RecordStart(SaxName name, int tagLine, int tagColumn, int tagStart, bool empty)
        {
            int w = Take(StartHeader + AttributeRecord * attributeCount + DeclarationRecord * declarationCount);
            if (w < 0) return;
            var t = tape!;
            t[w] = KStart | (empty ? 0x100 : 0) | (attributeCount << 16) | (declarationCount << 24);
            t[w + 1] = name.Id; t[w + 2] = name.ElementUriId; t[w + 3] = tagLine; t[w + 4] = tagColumn; t[w + 5] = charInput ? 0 : RawOffset(tagStart);
            w += StartHeader;
            for (int a = 0; a < attributeCount; a++, w += AttributeRecord)
            {
                ref var attribute = ref attributes[a];
                t[w] = attribute.Name.Id; t[w + 1] = attribute.UriId; t[w + 2] = attribute.ValueStart; t[w + 3] = attribute.ValueLength;
                t[w + 4] = attribute.Location.LineNumber; t[w + 5] = attribute.Location.LinePosition; t[w + 6] = (int)(attribute.Location.ByteOffset ?? 0);
            }
            for (int d = 0; d < declarationCount; d++, w += DeclarationRecord)
            {
                ref var declaration = ref declarations[d];
                t[w] = declaration.Name.Id; t[w + 1] = declaration.UriId;
                t[w + 2] = declaration.Location.LineNumber; t[w + 3] = declaration.Location.LinePosition; t[w + 4] = (int)(declaration.Location.ByteOffset ?? 0);
            }
        }

        private bool AttributesAreUnique()
        {
            for (int a = 1; a < attributeCount; a++)
            {
                ref var first = ref attributes[a];
                for (int b = 0; b < a; b++)
                {
                    ref var second = ref attributes[b];
                    if (ReferenceEquals(first.Name, second.Name) || (first.Name.Local == second.Name.Local && first.NamespaceUri == second.NamespaceUri)) return false;
                }
            }
            return true;
        }

        private bool Declare()
        {
            for (int d = 0; d < declarationCount; d++)
            {
                ref var declaration = ref declarations[d];
                if (!emit) for (int e = 0; e < d; e++) if (ReferenceEquals(declarations[e].Name, declaration.Name)) return false;
                string prefix = declaration.Name.Kind == 1 ? "" : declaration.Name.Local;
                string uri; int uriId;
                if (emit) { uriId = declared![declaredIndex++]; uri = uriList[uriId]; }
                else
                {
                    var value = attributeChars.AsSpan(declaration.ValueStart, declaration.ValueLength);
                    if (value.SequenceEqual(XmlnsNamespace) || value.SequenceEqual(XmlNamespace) || (prefix.Length != 0 && value.IsEmpty)) return false;
                    if (++internCount + nameBase + 8 > nameLimit) return false;
                    uri = InternUri(value, out uriId);
                    (declared ??= []).Add(uriId);
                }
                declaration.NamespaceUri = uri; declaration.UriId = uriId;
                if (bindingCount == bindingPrefixes.Length)
                {
                    Array.Resize(ref bindingPrefixes, bindingCount * 2); Array.Resize(ref bindingUris, bindingCount * 2); Array.Resize(ref bindingUriIds, bindingCount * 2);
                }
                bindingPrefixes[bindingCount] = prefix; bindingUriIds[bindingCount] = uriId; bindingUris[bindingCount++] = uri;
            }
            scope = ++scopeCounter;
            return true;
        }

        private string InternUri(ReadOnlySpan<char> value, out int id)
        {
            id = 0;
            if (value.IsEmpty) return "";
            uris ??= new(StringComparer.Ordinal);
            var lookup = uris.GetAlternateLookup<ReadOnlySpan<char>>();
            if (lookup.TryGetValue(value, out id)) return uriList[id];
            string owned = new(value);
            if (uriCount == uriList.Length) Array.Resize(ref uriList, uriCount * 2);
            id = uriCount;
            uriList[uriCount++] = owned;
            uris.Add(owned, id);
            return owned;
        }

        private void EnsureAttributeChars(int needed)
        {
            // The arena only accumulates across tags while recording; past its cap the tape is dropped and the delivery pass re-scans.
            if (record && needed > maxArenaChars) StopRecording();
            if (attributeChars is not null && attributeChars.Length >= needed) return;
            int size = Math.Max(512, (attributeChars?.Length ?? 0) * 2);
            if (record) size = Math.Min(size, maxArenaChars);
            var grown = Rent<char>(Math.Max(size, needed));
            if (attributeChars is not null)
            {
                attributeChars.AsSpan(0, attributeLength).CopyTo(grown);
                Return(attributeChars);
            }
            attributeChars = grown;
        }

        // Value of a quoted attribute; i is just past the opening quote and is left past the closing quote.
        // When decode is false a simple value is only validated.
        private bool AttributeValue(byte quote, bool decode, out int valueStart, out int valueLength)
        {
            int start = i, p = i;
            valueStart = attributeLength; valueLength = 0;
            while (true)
            {
                if ((uint)p >= (uint)n) return false;
                byte b = s[p];
                if (b == quote)
                {
                    if (p - start >= tokenLimit) return false;
                    i = p + 1;
                    int length = p - start;
                    if (decode && length > 0)
                    {
                        EnsureAttributeChars(attributeLength + length);
                        Ascii.ToUtf16(s.Slice(start, length), attributeChars.AsSpan(attributeLength), out _);
                        attributeLength += length; valueLength = length;
                    }
                    return true;
                }
                if (b == '<' || b < 0x20 || b == '&' || b >= 0x80) break;
                p++;
            }
            return ComplexAttributeValue(quote, start, p, out valueStart, out valueLength);
        }

        private bool ComplexAttributeValue(byte quote, int start, int p, out int valueStart, out int valueLength)
        {
            valueStart = attributeLength; valueLength = 0;
            long extra = 0;
            EnsureAttributeChars(attributeLength + (p - start) + 16);
            for (int k = start; k < p; k++) attributeChars![attributeLength++] = (char)s[k];
            while (true)
            {
                if ((uint)p >= (uint)n) return false;
                byte b = s[p];
                if (attributeLength + 3 > attributeChars!.Length) EnsureAttributeChars(attributeLength + 3);
                var buffer = attributeChars;
                if (b == quote) break;
                if (b == '&')
                {
                    int kind = EntityAt(p, out var entity, out int referenceLength);
                    if (kind < 0) return false;
                    if (kind > 0)
                    {
                        string text = entity!.Attribute;
                        EnsureAttributeChars(attributeLength + text.Length + 3);
                        text.CopyTo(0, attributeChars!, attributeLength, text.Length);
                        attributeLength += text.Length; extra += entity.Length; p += referenceLength;
                    }
                    else
                    {
                        if (!Reference(p, out int value, out referenceLength)) return false;
                        attributeLength = Append(buffer, attributeLength, value); p += referenceLength;
                    }
                }
                else if (b == '\t') { buffer[attributeLength++] = ' '; p++; }
                else if (b == '\n') { buffer[attributeLength++] = ' '; p++; line++; lineStart = p; cont = 0; }
                else if (b < 0x20 || b == '<') return false;
                else if (b < 0x80) { buffer[attributeLength++] = (char)b; p++; }
                else
                {
                    if (Rune.DecodeFromUtf8(s[p..], out Rune rune, out int consumed) != OperationStatus.Done || rune.Value is 0xFFFE or 0xFFFF) return false;
                    attributeLength += rune.EncodeToUtf16(buffer.AsSpan(attributeLength));
                    cont += consumed - 1; p += consumed;
                }
            }
            if (p - start + extra >= tokenLimit) return false;
            i = p + 1;
            valueLength = attributeLength - valueStart;
            return true;
        }
    }
}
