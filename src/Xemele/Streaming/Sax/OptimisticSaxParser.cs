using System.Buffers;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Text;
using System.Text.Unicode;

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
}

// An attribute (or namespace declaration) of the tag being delivered; its value lives in the shared character buffer.
internal struct FastAttribute
{
    internal SaxName Name;
    internal string LocalName, Prefix, NamespaceUri;
    internal int ValueStart, ValueLength;
    internal XmlLocation Location;
}

internal static class OptimisticSax
{
    // Whole-buffer SAX parse for the common well-formed subset (the same subset as the optimistic DOM parser).
    // A first pass validates the entire document without delivering anything; only then does a second pass
    // deliver events straight from the input. A document the first pass declines is left untouched for the
    // streaming parser, so diagnostics, recovery and every unusual construct keep their authoritative behavior.
    internal static bool TryParse(ReadOnlySpan<byte> input, IXmlSaxHandler handler, XmlParseOptions options, CancellationToken cancellationToken)
    {
        var limits = options.Limits;
        if (options.Recover || input.IsEmpty) return false;
        if (input.Length > (limits.DocumentCharacters ?? long.MaxValue) || input.Length > (limits.ExpandedCharacters ?? long.MaxValue)) return false;
        var parser = new SaxParser(input, handler, limits, cancellationToken, charInput: false);
        return parser.Run();
    }

    internal static bool TryParse(ReadOnlySpan<char> input, IXmlSaxHandler handler, XmlParseOptions options, CancellationToken cancellationToken)
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
            var parser = new SaxParser(rented.AsSpan(0, byteCount), handler, limits, cancellationToken, charInput: true);
            return parser.Run();
        }
        finally { if (pooled) ArrayPool<byte>.Shared.Return(rented); }
    }

    private struct Frame
    {
        internal SaxName Name;
        internal string Uri;
        internal int NameStart, NameLength, SavedScope, BindingStart;
    }

    private ref struct SaxParser
    {
        private const uint Seed = 2166136261, Prime = 16777619;
        private const int MaxMembers = 32;
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
        private Dictionary<string, string>? uris;
        private List<string>? declared;
        private int declaredIndex;

        internal SaxParser(ReadOnlySpan<byte> input, IXmlSaxHandler handler, XmlResourceLimits limits, CancellationToken cancellation, bool charInput)
        {
            this.handler = handler; this.charInput = charInput; s = input; n = input.Length; this.cancellation = cancellation;
            maxDepth = limits.Depth ?? int.MaxValue;
            maxMembers = Math.Min(MaxMembers, limits.AttributesPerElement ?? int.MaxValue);
            tokenLimit = limits.TokenCharacters ?? int.MaxValue;
            nameLimit = limits.InternedNames ?? int.MaxValue;
            referenceLimit = (limits.ExpandedCharacters ?? long.MaxValue) - input.Length;
            frames = new Frame[16];
            table = new SaxName?[128];
            bindingPrefixes = new string[8]; bindingUris = new string[8];
            attributes = new FastAttribute[8]; declarations = new FastAttribute[4]; namespaces = new NamespaceRow[4];
        }

        internal bool Run()
        {
            try
            {
                if (!Pass(false)) return false;
                if (!Pass(true)) throw new InvalidOperationException("The optimistic SAX parser rejected a document that it had already validated.");
                return true;
            }
            finally
            {
                if (scratch is not null) ArrayPool<char>.Shared.Return(scratch);
                if (attributeChars is not null) ArrayPool<char>.Shared.Return(attributeChars);
            }
        }

        private bool Pass(bool deliver)
        {
            emit = deliver;
            i = 0; line = 1; lineStart = 0; cont = 0; depth = 0; scope = 0; bindingCount = 0; rootSeen = false;
            referenceBudget = referenceLimit; declaredIndex = 0;
            frames[0] = default;
            if (deliver) handler.StartDocument();
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

        private XmlLocation Loc(int atLine, int atColumn, int atOffset) => new(null, atLine, atColumn, charInput ? null : atOffset);

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
                var grown = ArrayPool<char>.Shared.Rent(Math.Max(capacity, TextChunk + 8));
                if (scratch is not null) ArrayPool<char>.Shared.Return(scratch);
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
        private void Misc()
        {
            int start = i;
            SkipWhitespace();
            if (!emit || i == start) return;
            var buffer = Scratch(TextChunk);
            for (int at = start; at < i; )
            {
                int count = Math.Min(TextChunk, i - at);
                Ascii.ToUtf16(s.Slice(at, count), buffer, out _);
                handler.Text(buffer.AsSpan(0, count));
                at += count;
            }
        }

        private bool Prolog()
        {
            while (true)
            {
                Misc();
                if ((uint)i >= (uint)n || s[i] != '<' || i + 1 >= n) return false;
                byte next = s[i + 1];
                if (next == '!') { if (!Comment()) return false; }
                else if (next == '?') { if (!ProcessingInstruction()) return false; }
                else return true;
            }
        }

        private bool Epilog()
        {
            while (true)
            {
                Misc();
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

        // Delivers a validated run (no references) as text, a bounded chunk at a time.
        private void EmitRun(int start, int end)
        {
            var run = s[start..end];
            var buffer = Scratch(TextChunk).AsSpan(0, TextChunk);
            bool high = false;
            while (!run.IsEmpty)
            {
                Utf8.ToUtf16(run, buffer, out int read, out int written, replaceInvalidSequences: false);
                if (written != read) high = true;
                if (written > 0) handler.Text(buffer[..written]);
                run = run[read..];
            }
            TrackLines(start, end, high);
        }

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
        private void AppendSegment(int start, int end, ref int length)
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
                if (status == OperationStatus.DestinationTooSmall) { handler.Text(buffer.AsSpan(0, length)); length = 0; }
            }
            TrackLines(start, end, high);
        }

        private bool TextWithReferences(int start)
        {
            int length = 0, segment = start, at = start;
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
                if (!emit && (end - start >= tokenLimit || !Scan(segment, end, false))) return false;
                if (emit) AppendSegment(segment, end, ref length);
                if (stop == '<')
                {
                    if (emit && length > 0) handler.Text(scratch.AsSpan(0, length));
                    i = end; return true;
                }
                if (!Reference(end, out int value, out int referenceLength)) return false;
                if (emit)
                {
                    if (length + 2 > TextChunk) { handler.Text(scratch.AsSpan(0, length)); length = 0; }
                    length = Append(scratch!, length, value);
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
            }
            else if (bodyEnd - open >= tokenLimit || !Scan(bodyStart, bodyEnd, true)) return false;
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
                EmitRun(bodyStart, bodyEnd);
                handler.EndCData();
            }
            else if (bodyEnd - open >= tokenLimit || !Scan(bodyStart, bodyEnd, true)) return false;
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
                if (!emit && (bodyEnd - open >= tokenLimit || !Scan(bodyStart, bodyEnd, true))) return false;
            }
            if (emit)
            {
                int targetLength = target.Length;
                var buffer = Scratch(targetLength + (bodyEnd - bodyStart) + 1);
                Ascii.ToUtf16(target, buffer, out _);
                Utf8.ToUtf16(s[bodyStart..bodyEnd], buffer.AsSpan(targetLength), out _, out int written, replaceInvalidSequences: false);
                TrackLines(bodyStart, bodyEnd, written != bodyEnd - bodyStart);
                handler.ProcessingInstruction(buffer.AsSpan(0, targetLength), buffer.AsSpan(targetLength, written));
            }
            else if (++internCount + 8 > nameLimit) return false;
            i = p;
            return true;
        }

        private bool CloseTag()
        {
            if (depth == 0) return false;
            ref var frame = ref frames[depth];
            int tagStart = i, closeLine = line, closeColumn = Column(i);
            int p = i + 2;
            var open = s.Slice(frame.NameStart, frame.NameLength);
            if (n - p < open.Length || !s.Slice(p, open.Length).SequenceEqual(open)) return false;
            p += open.Length;
            i = p; SkipWhitespace();
            if ((uint)i >= (uint)n || s[i] != '>') return false;
            i++;
            scope = frame.SavedScope; bindingCount = frame.BindingStart;
            if (emit) handler.EndElement(new XmlEndElement(frame.Name.Local, frame.Name.Prefix, frame.Uri, Loc(closeLine, closeColumn, tagStart)));
            depth--;
            return true;
        }

        private SaxName? FindOrCreateName(int start, int length, uint hash)
        {
            var key = s.Slice(start, length);
            uint mask = (uint)table.Length - 1;
            uint slot = (hash ^ (hash >> 15)) & mask;
            while (table[slot] is { } entry)
            {
                if (entry.Hash == hash && entry.Key.AsSpan().SequenceEqual(key)) return entry;
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
            if (internCount + 8 > nameLimit) return null;
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
        private string? Lookup(string prefix)
        {
            for (int b = bindingCount - 1; b >= 0; b--) if (bindingPrefixes[b] == prefix) return bindingUris[b];
            return prefix.Length == 0 ? "" : null;
        }

        private string? ElementUri(SaxName entry)
        {
            if (entry.ElementScope == scope || entry.ElementScope == ScopeAny) return entry.ElementUri;
            string prefix = entry.Prefix;
            string? uri; int validFor = scope;
            if (prefix == "xml") { uri = XmlNamespace; validFor = ScopeAny; }
            else uri = Lookup(prefix);
            if (uri is null) return null;
            entry.ElementUri = uri;
            entry.ElementScope = validFor;
            return uri;
        }

        private string? AttributeUri(SaxName entry)
        {
            if (entry.AttributeScope == scope || entry.AttributeScope == ScopeAny) return entry.AttributeUri;
            string prefix = entry.Prefix;
            string? uri; int validFor = scope;
            if (prefix.Length == 0) { uri = ""; validFor = ScopeAny; }
            else if (prefix == "xml") { uri = XmlNamespace; validFor = ScopeAny; }
            else uri = Lookup(prefix);
            if (uri is null) return null;
            entry.AttributeUri = uri;
            entry.AttributeScope = validFor;
            return uri;
        }

        private bool OpenTag()
        {
            int tagStart = i, tagLine = line, tagColumn = Column(i);
            if (depth >= maxDepth - 1 || (depth == 0 && rootSeen)) return false;
            uint hash = Seed; int p = i + 1, nameStart = p;
            while ((uint)p < (uint)n) { byte b = s[p]; if ((FlagTable[b] & 1) == 0) break; hash = (hash ^ b) * Prime; p++; }
            if (p == nameStart) return false;
            var name = FindOrCreateName(nameStart, p - nameStart, hash);
            if (name is null || name.Kind != 0) return false;
            int nameLength = p - nameStart;
            i = p; attributeCount = 0; declarationCount = 0; attributeLength = 0;
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
                int attributeStart = i, attributeLine = line, attributeColumn = Column(i);
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
                if (!AttributeValue(quote, emit || key.Kind != 0, out int valueStart, out int valueLength)) return false;
                if (key.Kind == 0)
                {
                    if (attributeCount == attributes.Length) Array.Resize(ref attributes, attributes.Length * 2);
                    ref var attribute = ref attributes[attributeCount++];
                    attribute.Name = key; attribute.ValueStart = valueStart; attribute.ValueLength = valueLength;
                    attribute.Location = Loc(attributeLine, attributeColumn, attributeStart);
                }
                else
                {
                    if (declarationCount == declarations.Length) Array.Resize(ref declarations, declarations.Length * 2);
                    ref var declaration = ref declarations[declarationCount++];
                    declaration.Name = key; declaration.ValueStart = valueStart; declaration.ValueLength = valueLength;
                    declaration.Location = Loc(attributeLine, attributeColumn, attributeStart);
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
                attribute.NamespaceUri = uri; attribute.LocalName = attribute.Name.Local; attribute.Prefix = attribute.Name.Prefix;
            }
            if (attributeCount > 1 && !AttributesAreUnique()) return false;

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
                if (empty) handler.EndElement(new XmlEndElement(name.Local, name.Prefix, elementUri, location));
            }
            if (empty)
            {
                scope = savedScope; bindingCount = savedBindings;
                rootSeen |= depth == 0;
                return true;
            }
            depth++;
            if (depth >= frames.Length) Array.Resize(ref frames, frames.Length * 2);
            frames[depth] = new() { Name = name, Uri = elementUri, NameStart = nameStart, NameLength = nameLength, SavedScope = savedScope, BindingStart = savedBindings };
            rootSeen = true;
            return true;
        }

        private bool AttributesAreUnique()
        {
            for (int a = 1; a < attributeCount; a++)
            {
                ref var first = ref attributes[a];
                for (int b = 0; b < a; b++)
                {
                    ref var second = ref attributes[b];
                    if (ReferenceEquals(first.Name, second.Name) || (first.LocalName == second.LocalName && first.NamespaceUri == second.NamespaceUri)) return false;
                }
            }
            return true;
        }

        private bool Declare()
        {
            for (int d = 0; d < declarationCount; d++)
            {
                ref var declaration = ref declarations[d];
                for (int e = 0; e < d; e++) if (ReferenceEquals(declarations[e].Name, declaration.Name)) return false;
                string prefix = declaration.Name.Kind == 1 ? "" : declaration.Name.Local;
                string uri;
                if (emit) uri = declared![declaredIndex++];
                else
                {
                    var value = attributeChars.AsSpan(declaration.ValueStart, declaration.ValueLength);
                    if (value.SequenceEqual(XmlnsNamespace) || value.SequenceEqual(XmlNamespace) || (prefix.Length != 0 && value.IsEmpty)) return false;
                    if (++internCount + 8 > nameLimit) return false;
                    uri = InternUri(value);
                    (declared ??= []).Add(uri);
                }
                declaration.NamespaceUri = uri;
                if (bindingCount == bindingPrefixes.Length)
                {
                    Array.Resize(ref bindingPrefixes, bindingCount * 2); Array.Resize(ref bindingUris, bindingCount * 2);
                }
                bindingPrefixes[bindingCount] = prefix; bindingUris[bindingCount++] = uri;
            }
            scope = ++scopeCounter;
            return true;
        }

        private string InternUri(ReadOnlySpan<char> value)
        {
            if (value.IsEmpty) return "";
            uris ??= new(StringComparer.Ordinal);
            var lookup = uris.GetAlternateLookup<ReadOnlySpan<char>>();
            if (lookup.TryGetValue(value, out string? existing)) return existing;
            string owned = new(value);
            uris.Add(owned, owned);
            return owned;
        }

        private void EnsureAttributeChars(int needed)
        {
            if (attributeChars is not null && attributeChars.Length >= needed) return;
            var grown = ArrayPool<char>.Shared.Rent(Math.Max(needed, Math.Max(512, (attributeChars?.Length ?? 0) * 2)));
            if (attributeChars is not null)
            {
                attributeChars.AsSpan(0, attributeLength).CopyTo(grown);
                ArrayPool<char>.Shared.Return(attributeChars);
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
                    if (!Reference(p, out int value, out int referenceLength)) return false;
                    attributeLength = Append(buffer, attributeLength, value); p += referenceLength;
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
            if (p - start >= tokenLimit) return false;
            i = p + 1;
            valueLength = attributeLength - valueStart;
            return true;
        }
    }
}
