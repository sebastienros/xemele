using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Text.Unicode;
using Xemele.Internal.Operations;
using Xemele.Parsing.Dom;

namespace Xemele;

public sealed partial class XmlDocument
{
    // Optimistic whole-buffer UTF-8 DOM parse. It accepts only the common, well-formed subset
    // (ASCII names, LF line breaks, predefined and numeric references, no DTD) and builds exactly the
    // document, names, locations and counters that the streaming parser would. Anything else,
    // including every error, returns false before the caller-visible result exists, and the
    // streaming parser re-parses from scratch so diagnostics and semantics stay authoritative.
    internal static bool TryParseUtf8Optimistic(ReadOnlySpan<byte> input, XmlParseOptions options,
        CancellationToken cancellationToken, out XmlDocument? document)
    {
        document = null;
        var limits = options.Limits;
        if (options.Recover || input.IsEmpty) return false;
        long expanded = limits.ExpandedCharacters ?? long.MaxValue;
        if (input.Length > (limits.DocumentCharacters ?? long.MaxValue) || input.Length > expanded) return false;
        var candidate = new XmlDocument(limits);
        var parser = new OptimisticParser(candidate, input, limits, cancellationToken);
        if (!parser.Run()) return false;
        document = candidate;
        return true;
    }

    private sealed class OptimisticName
    {
        internal byte[] Key = [];
        internal uint Hash;
        internal string Prefix = "", Local = "";
        // 0 plain, 1 "xmlns", 2 "xmlns:prefix".
        internal int Kind;
        internal NameData? DeclName;
        internal NameData? Element, Attribute;
        // Scope id the cached resolution is valid for; -2 means every scope.
        internal int ElementScope = -1, AttributeScope = -1;
    }

    private struct OptimisticFrame
    {
        internal int Slot, First, Last, Count, NameStart, NameLength, SavedScope, BindingStart;
    }

    private struct OptimisticMember
    {
        internal OptimisticName Name;
        internal string Value;
        internal int Line, Column, Offset;
    }

    [InlineArray(32)]
    private struct OptimisticMembers { private OptimisticMember first; }

    private ref struct OptimisticParser
    {
        private const uint Seed = 2166136261, Prime = 16777619;
        private const int MaxMembers = 32;
        private const int ScopeAny = -2;
        private const string XmlNamespace = "http://www.w3.org/XML/1998/namespace";
        private const string XmlnsNamespace = "http://www.w3.org/2000/xmlns/";

        // 1 name character, 2 name start, 4 attribute-value terminator or special.
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

        private readonly XmlDocument doc;
        private readonly ReadOnlySpan<byte> s;
        private readonly int n;
        private readonly CancellationToken cancellation;
        private readonly int maxDepth, maxMembers, tokenLimit, nameLimit;
        private long referenceBudget;
        private int i, line, lineStart, cont;
        private int nodeCount, memberCount, depth, scope, scopeCounter, qnameCount;
        private bool rootSeen;
        private OptimisticFrame[] frames;
        private OptimisticName?[] table;
        private int tableCount;
        private string[] bindingPrefixes, bindingUris;
        private int bindingCount;
        private char[]? scratch;
        private DomSink.ShortValueCache values;
        private OptimisticMembers attributes, declarations;
        private int attributeCount, declarationCount;

        internal OptimisticParser(XmlDocument doc, ReadOnlySpan<byte> input, XmlResourceLimits limits, CancellationToken cancellation)
        {
            this.doc = doc; s = input; n = input.Length; this.cancellation = cancellation;
            maxDepth = limits.Depth ?? int.MaxValue;
            maxMembers = Math.Min(MaxMembers, limits.AttributesPerElement ?? int.MaxValue);
            tokenLimit = limits.TokenCharacters ?? int.MaxValue;
            nameLimit = limits.InternedNames ?? int.MaxValue;
            referenceBudget = (limits.ExpandedCharacters ?? long.MaxValue) - input.Length;
            line = 1;
            frames = new OptimisticFrame[16];
            frames[0] = new() { Slot = 0, First = -1, Last = -1 };
            table = new OptimisticName?[128];
            bindingPrefixes = new string[8]; bindingUris = new string[8];
        }

        internal bool Run()
        {
            try { return Parse(); }
            finally { if (scratch is not null) ArrayPool<char>.Shared.Return(scratch); }
        }

        private bool Parse()
        {
            PresizeArenas();
            doc.RecordSourceOrigin(new(null, 1, 1, 0));
            if (n >= 3 && s[0] == 0xEF && s[1] == 0xBB && s[2] == 0xBF) { i = 3; lineStart = 3; }
            var head = s[i..];
            if (head.Length >= 6 && head.StartsWith("<?xml"u8) && head[5] is (byte)' ' or (byte)'\t' or (byte)'\n' or (byte)'\r')
            { if (!Declaration()) return false; }
            if (!Prolog()) return false;
            if (!Content()) return false;
            if (!Epilog()) return false;
            ref var root = ref doc.Nodes[0];
            ref var top = ref frames[0];
            root.First = top.First; root.Last = top.Last; root.Count = top.Count;
            long total = nodeCount + memberCount;
            doc.MutationVersion = total;
            doc.ContentRevision = total;
            doc.budget.TryCharge(Quota.InternedNames, doc.names.Count);
            doc.MarkValidationSourceComplete();
            return true;
        }

        private void PresizeArenas()
        {
            int nodes = 1, attributeEstimate = 0; bool tag = false; byte quote = 0;
            for (int offset = 0; offset < n && nodes < 256 && attributeEstimate < 256;)
            {
                var block = s.Slice(offset, Math.Min(4096, n - offset));
                int at = block.IndexOfAny("<>='\""u8);
                if (at < 0) { offset += block.Length; continue; }
                offset += at;
                byte value = s[offset++];
                if (quote != 0) { if (value == quote) quote = 0; continue; }
                if (tag && (value == '\'' || value == '"')) { quote = value; continue; }
                if (value == '<' && !tag)
                {
                    if (offset < n && s[offset] == '!') break;
                    tag = true; if (offset == n || s[offset] != '/') nodes++;
                }
                else if (value == '>' && tag)
                {
                    tag = false; if (offset < n && s[offset] != '<') nodes++;
                }
                else if (value == '=' && tag) attributeEstimate++;
            }
            int reserveNodes = nodes >= 256 || attributeEstimate >= 256 ? 256 : nodes;
            int reserveAttributes = attributeEstimate >= 256 || nodes >= 256 ? 256 : attributeEstimate;
            doc.Nodes.EnsureCapacity(reserveNodes);
            doc.AttributeSlots.EnsureCapacity(reserveAttributes);
            doc.ReserveSourceLocationCapacity(reserveNodes, reserveAttributes);
        }

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

        // Appends a validated segment to the scratch buffer (decoding and validating in one pass).
        private bool AppendSegment(int start, int end, ref int length)
        {
            if (start == end) return true;
            var run = s[start..end];
            var buffer = Scratch(length + run.Length + 1, length);
            if (Utf8.ToUtf16(run, buffer.AsSpan(length), out _, out int written, replaceInvalidSequences: false) != OperationStatus.Done) return false;
            bool high = written != run.Length;
            if (high && HasNonCharacter(run)) return false;
            TrackLines(start, end, high);
            length += written;
            return true;
        }

        private bool TextValue(int start, int end, out string value)
        {
            var run = s[start..end];
            if (Ascii.IsValid(run))
            {
                TrackLines(start, end, false);
                value = values.OwnAscii(run);
                return true;
            }
            int length = 0;
            value = "";
            if (!AppendSegment(start, end, ref length)) return false;
            value = new string(scratch!, 0, length);
            return true;
        }

        private static string Own(ref DomSink.ShortValueCache cache, ReadOnlySpan<byte> run)
        {
            if (run.IsEmpty) return string.Empty;
            if (Ascii.IsValid(run)) return cache.OwnAscii(run);
            return Encoding.UTF8.GetString(run);
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
            i += 2;
            int digits = 0; while ((uint)i < (uint)n && s[i] is >= (byte)'0' and <= (byte)'9') { i++; digits++; }
            if (digits == 0 || (uint)i >= (uint)n || s[i++] != quote) return false;
            int mark = i; SkipWhitespace(); bool spaced = i > mark;
            if (Keyword("encoding"u8))
            {
                if (!spaced) return false;
                SkipWhitespace(); if (!Eat('=')) return false; SkipWhitespace();
                if ((uint)i >= (uint)n || s[i] is not ((byte)'"' or (byte)'\'')) return false;
                quote = s[i++];
                int close = s[i..].IndexOf(quote);
                if (close < 0 || !Ascii.EqualsIgnoreCase(s.Slice(i, close), "UTF-8"u8)) return false;
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
            doc.SetValidationDeclaration(standalone, new(null, 1, 1, declarationOffset));
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

        private bool Prolog()
        {
            while (true)
            {
                SkipWhitespace();
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
                SkipWhitespace();
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

        private void Link(ref OptimisticFrame parent, int slot)
        {
            ref var node = ref doc.Nodes[slot];
            node.Parent = parent.Slot; node.Previous = parent.Last;
            if (parent.Last >= 0) doc.Nodes[parent.Last].Next = slot; else parent.First = slot;
            parent.Last = slot; parent.Count++;
            nodeCount++;
        }

        private void AddLeaf(XmlNodeKind kind, NameData? name, string value, int atLine, int atColumn, int atOffset)
        {
            int slot = doc.Nodes.Count;
            doc.Nodes.Add(new(kind, name, value));
            Link(ref frames[depth], slot);
            doc.nodeLocations!.Add(new() { Line = (uint)atLine | LocationPresent, Position = (uint)atColumn, Offset = (uint)atOffset });
        }

        private bool Text()
        {
            int start = i, startLine = line, startColumn = Column(i);
            int searchFrom = i;
            while (true)
            {
                int at = s[searchFrom..].IndexOfAny(TextStops);
                if (at < 0) return false;
                int end = searchFrom + at;
                byte stop = s[end];
                if (stop == '<')
                {
                    if (end - start >= tokenLimit || !TextValue(start, end, out string value)) return false;
                    AddLeaf(XmlNodeKind.Text, null, value, startLine, startColumn, start);
                    i = end; return true;
                }
                if (stop == ']')
                {
                    if (end + 2 < n && s[end + 1] == ']' && s[end + 2] == '>') return false;
                    searchFrom = end + 1; continue;
                }
                if (stop != '&') return false;
                return TextWithReferences(start, startLine, startColumn);
            }
        }

        private char[] Scratch(int capacity, int used = 0)
        {
            if (scratch is null || scratch.Length < capacity)
            {
                var grown = ArrayPool<char>.Shared.Rent(Math.Max(capacity, 256));
                if (scratch is not null)
                {
                    scratch.AsSpan(0, used).CopyTo(grown);
                    ArrayPool<char>.Shared.Return(scratch);
                }
                scratch = grown;
            }
            return scratch;
        }

        private bool TextWithReferences(int start, int startLine, int startColumn)
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
                if (end - start >= tokenLimit || !AppendSegment(segment, end, ref length)) return false;
                if (stop == '<')
                {
                    AddLeaf(XmlNodeKind.Text, null, new string(scratch!, 0, length), startLine, startColumn, start);
                    i = end; return true;
                }
                if (!Reference(end, out int value, out int referenceLength)) return false;
                length = Append(Scratch(length + 3, length), length, value);
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
            if (code < 0x20 || code > 0x10FFFF || code is >= 0xD800 and <= 0xDFFF or 0xFFFE or 0xFFFF) return false;
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
            int startLine = line, startColumn = Column(open);
            int bodyStart = open + 4;
            int dash = s[bodyStart..].IndexOf("--"u8);
            if (dash < 0) return false;
            int bodyEnd = bodyStart + dash;
            if (bodyEnd + 2 >= n || s[bodyEnd + 2] != '>') return false;
            if (bodyEnd - open >= tokenLimit || !Scan(bodyStart, bodyEnd, true)) return false;
            AddLeaf(XmlNodeKind.Comment, null, Own(ref values, s[bodyStart..bodyEnd]), startLine, startColumn, open);
            i = bodyEnd + 3;
            return true;
        }

        private bool CData()
        {
            int open = i;
            if (!s[open..].StartsWith("<![CDATA["u8)) return false;
            int startLine = line, startColumn = Column(open);
            int bodyStart = open + 9;
            int close = s[bodyStart..].IndexOf("]]>"u8);
            if (close < 0) return false;
            int bodyEnd = bodyStart + close;
            if (bodyEnd - open >= tokenLimit || !Scan(bodyStart, bodyEnd, true)) return false;
            AddLeaf(XmlNodeKind.CData, null, Own(ref values, s[bodyStart..bodyEnd]), startLine, startColumn, open);
            i = bodyEnd + 3;
            return true;
        }

        private bool ProcessingInstruction()
        {
            int open = i, startLine = line, startColumn = Column(open);
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
                if (bodyEnd - open >= tokenLimit || !Scan(bodyStart, bodyEnd, true)) return false;
            }
            string name = TargetName(target);
            if (doc.names.Count + qnameCount + 8 > nameLimit) return false;
            var data = doc.GetName(name, "", "");
            AddLeaf(XmlNodeKind.ProcessingInstruction, data, Own(ref values, s[bodyStart..bodyEnd]), startLine, startColumn, open);
            i = p;
            return true;
        }

        private string TargetName(ReadOnlySpan<byte> target)
        {
            Span<char> chars = stackalloc char[target.Length];
            Ascii.ToUtf16(target, chars, out _);
            return doc.FindInternedName(chars) ?? new string(chars);
        }

        private bool CloseTag()
        {
            ref var frame = ref frames[depth];
            if (depth == 0) return false;
            int p = i + 2;
            var open = s.Slice(frame.NameStart, frame.NameLength);
            if (n - p < open.Length || !s.Slice(p, open.Length).SequenceEqual(open)) return false;
            p += open.Length;
            i = p; SkipWhitespace();
            if ((uint)i >= (uint)n || s[i] != '>') return false;
            i++;
            ref var element = ref doc.Nodes[frame.Slot];
            element.First = frame.First; element.Last = frame.Last; element.Count = frame.Count;
            scope = frame.SavedScope; bindingCount = frame.BindingStart;
            depth--;
            return true;
        }

        private OptimisticName? FindOrCreateName(int start, int length, uint hash)
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

        private OptimisticName? CreateName(ReadOnlySpan<byte> key, uint hash, uint slot)
        {
            if ((FlagTable[key[0]] & 2) == 0) return null;
            int colon = key.IndexOf((byte)':');
            if (colon >= 0 && key[(colon + 1)..].Contains((byte)':')) return null;
            if (colon == 0 || colon == key.Length - 1) return null;
            if (colon > 0 && (FlagTable[key[colon + 1]] & 2) == 0) return null;
            if (doc.names.Count + ++qnameCount + 8 > nameLimit) return null;
            var entry = new OptimisticName { Key = key.ToArray(), Hash = hash };
            string prefix = colon < 0 ? "" : Latin(key[..colon]);
            string local = colon < 0 ? Latin(key) : Latin(key[(colon + 1)..]);
            if (colon < 0 && key.SequenceEqual("xmlns"u8)) entry.Kind = 1;
            else if (prefix == "xmlns")
            {
                if (local is "xmlns" or "xml") return null;
                entry.Kind = 2;
            }
            if (entry.Kind == 2) { entry.Prefix = ""; entry.Local = ""; entry.DeclName = doc.GetName("", "", local); entry.Local = doc.InternOwned(local); }
            else if (entry.Kind == 0) { entry.Prefix = doc.InternOwned(prefix); entry.Local = doc.InternOwned(local); }
            table[slot] = entry;
            if (++tableCount * 2 > table.Length) GrowTable();
            return entry;
        }

        private static string Latin(ReadOnlySpan<byte> bytes) => Encoding.Latin1.GetString(bytes);

        private void GrowTable()
        {
            var old = table;
            var grown = new OptimisticName?[old.Length * 2];
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

        private NameData? ElementName(OptimisticName entry)
        {
            if (entry.ElementScope == scope || entry.ElementScope == ScopeAny) return entry.Element;
            string prefix = entry.Prefix;
            string? uri; int validFor = scope;
            if (prefix == "xml") { uri = XmlNamespace; validFor = ScopeAny; }
            else uri = Lookup(prefix);
            if (uri is null) return null;
            entry.Element = doc.GetName(entry.Local, uri, prefix);
            entry.ElementScope = validFor;
            return entry.Element;
        }

        private NameData? AttributeName(OptimisticName entry)
        {
            if (entry.AttributeScope == scope || entry.AttributeScope == ScopeAny) return entry.Attribute;
            string prefix = entry.Prefix;
            string? uri; int validFor = scope;
            if (prefix.Length == 0) { uri = ""; validFor = ScopeAny; }
            else if (prefix == "xml") { uri = XmlNamespace; validFor = ScopeAny; }
            else uri = Lookup(prefix);
            if (uri is null) return null;
            entry.Attribute = doc.GetName(entry.Local, uri, prefix);
            entry.AttributeScope = validFor;
            return entry.Attribute;
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
            i = p; attributeCount = 0; declarationCount = 0;
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
                string? value = AttributeValue(quote);
                if (value is null) return false;
                if (key.Kind == 0) attributes[attributeCount++] = new() { Name = key, Value = value, Line = attributeLine, Column = attributeColumn, Offset = attributeStart };
                else declarations[declarationCount++] = new() { Name = key, Value = value, Line = attributeLine, Column = attributeColumn, Offset = attributeStart };
            }
            if (i - tagStart >= tokenLimit) return false;

            int savedScope = scope, savedBindings = bindingCount;
            if (declarationCount > 0 && !Declare()) return false;

            var elementName = ElementName(name);
            if (elementName is null) return false;
            if (attributeCount > 1 && !AttributesAreUnique()) return false;

            int slot = doc.Nodes.Count;
            doc.Nodes.Add(new(XmlNodeKind.Element, elementName, ""));
            Link(ref frames[depth], slot);
            doc.nodeLocations!.Add(new() { Line = (uint)tagLine | LocationPresent, Position = (uint)tagColumn, Offset = (uint)tagStart });

            ref var element = ref doc.Nodes[slot];
            if (declarationCount > 0) WriteDeclarations(slot, ref element);
            if (attributeCount > 0 && !WriteAttributes(slot)) return false;
            if (empty)
            {
                scope = savedScope; bindingCount = savedBindings;
                rootSeen |= depth == 0;
                return true;
            }
            depth++;
            if (depth >= frames.Length) Array.Resize(ref frames, frames.Length * 2);
            frames[depth] = new() { Slot = slot, First = -1, Last = -1, NameStart = nameStart, NameLength = nameLength, SavedScope = savedScope, BindingStart = savedBindings };
            rootSeen = true;
            return true;
        }

        private bool AttributesAreUnique()
        {
            for (int a = 1; a < attributeCount; a++)
            {
                var first = AttributeName(attributes[a].Name);
                if (first is null) return false;
                for (int b = 0; b < a; b++)
                {
                    var second = AttributeName(attributes[b].Name);
                    if (second is null || (ReferenceEquals(first.Local, second.Local) && ReferenceEquals(first.Uri, second.Uri))) return false;
                }
            }
            return true;
        }

        private bool Declare()
        {
            if (doc.names.Count + qnameCount + 8 > nameLimit) return false;
            for (int d = 0; d < declarationCount; d++)
            {
                ref var declaration = ref declarations[d];
                for (int e = 0; e < d; e++) if (ReferenceEquals(declarations[e].Name, declaration.Name)) return false;
                string prefix = declaration.Name.Kind == 1 ? "" : declaration.Name.Local;
                string uri = declaration.Value;
                if (uri == XmlnsNamespace || uri == XmlNamespace || (prefix.Length != 0 && uri.Length == 0)) return false;
                uri = doc.InternOwned(uri);
                declaration.Value = uri;
                if (bindingCount == bindingPrefixes.Length)
                {
                    Array.Resize(ref bindingPrefixes, bindingCount * 2); Array.Resize(ref bindingUris, bindingCount * 2);
                }
                bindingPrefixes[bindingCount] = prefix; bindingUris[bindingCount++] = uri;
            }
            scope = ++scopeCounter;
            return true;
        }

        private void WriteDeclarations(int slot, ref NodeData element)
        {
            var slots = doc.NamespaceSlots;
            int first = slots.Count;
            var locations = doc.namespaceLocations ??= new();
            for (int d = 0; d < declarationCount; d++)
            {
                ref var declaration = ref declarations[d];
                int at = first + d;
                slots.Add(new MemberData(slot, declaration.Name.DeclName, declaration.Value) { Previous = d == 0 ? -1 : at - 1, Next = d == declarationCount - 1 ? -1 : at + 1 });
                locations.Add(new() { Line = (uint)declaration.Line | LocationPresent, Position = (uint)declaration.Column, Offset = (uint)declaration.Offset });
            }
            element.FirstNamespace = first; element.LastNamespace = first + declarationCount - 1; element.NamespaceCount = declarationCount;
            memberCount += declarationCount;
        }

        private bool WriteAttributes(int slot)
        {
            var slots = doc.AttributeSlots;
            int first = slots.Count;
            var locations = doc.attributeLocations ??= new();
            for (int a = 0; a < attributeCount; a++)
            {
                ref var attribute = ref attributes[a];
                var attributeName = AttributeName(attribute.Name);
                if (attributeName is null) return false;
                int at = first + a;
                slots.Add(new MemberData(slot, attributeName, attribute.Value) { Previous = a == 0 ? -1 : at - 1, Next = a == attributeCount - 1 ? -1 : at + 1 });
                locations.Add(new() { Line = (uint)attribute.Line | LocationPresent, Position = (uint)attribute.Column, Offset = (uint)attribute.Offset });
            }
            ref var element = ref doc.Nodes[slot];
            element.FirstAttribute = first; element.LastAttribute = first + attributeCount - 1; element.AttributeCount = attributeCount;
            memberCount += attributeCount;
            return true;
        }

        // Value of a quoted attribute; i is just past the opening quote and is left past the closing quote.
        private string? AttributeValue(byte quote)
        {
            int start = i, p = i;
            while (true)
            {
                if ((uint)p >= (uint)n) return null;
                byte b = s[p];
                if (b == quote)
                {
                    if (p - start >= tokenLimit) return null;
                    i = p + 1;
                    return p == start ? string.Empty : values.OwnAscii(s[start..p]);
                }
                if (b == '<' || b < 0x20 || b == '&' || b >= 0x80) break;
                p++;
            }
            return ComplexAttributeValue(quote, start, p);
        }

        private string? ComplexAttributeValue(byte quote, int start, int p)
        {
            var buffer = Scratch(256);
            int length = 0;
            if (p - start > buffer.Length) buffer = Scratch(p - start + 16);
            for (int k = start; k < p; k++) buffer[length++] = (char)s[k];
            while (true)
            {
                if ((uint)p >= (uint)n) return null;
                byte b = s[p];
                if (length + 3 > buffer.Length) buffer = GrowScratch(length);
                if (b == quote) break;
                if (b == '&')
                {
                    if (!Reference(p, out int value, out int referenceLength)) return null;
                    length = Append(buffer, length, value); p += referenceLength;
                }
                else if (b == '\t') { buffer[length++] = ' '; p++; }
                else if (b == '\n') { buffer[length++] = ' '; p++; line++; lineStart = p; cont = 0; }
                else if (b < 0x20 || b == '<') return null;
                else if (b < 0x80) { buffer[length++] = (char)b; p++; }
                else
                {
                    if (Rune.DecodeFromUtf8(s[p..], out Rune rune, out int consumed) != OperationStatus.Done || rune.Value is 0xFFFE or 0xFFFF) return null;
                    length += rune.EncodeToUtf16(buffer.AsSpan(length));
                    cont += consumed - 1; p += consumed;
                }
            }
            if (p - start >= tokenLimit) return null;
            i = p + 1;
            return values.Own(buffer.AsSpan(0, length));
        }

        private char[] GrowScratch(int used)
        {
            var old = scratch!;
            var grown = ArrayPool<char>.Shared.Rent(old.Length * 2);
            Array.Copy(old, grown, used);
            ArrayPool<char>.Shared.Return(old);
            scratch = grown;
            return grown;
        }
    }
}
