using System.Buffers;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Text;
using System.Text.Unicode;
using Xemele.Internal.Operations;

namespace Xemele.Streaming;

// Incremental pull scanner for the common well-formed UTF-8/ASCII XML subset
// (DTD prohibited/absent, no recovery). When it encounters any construct or
// error it does not handle, it returns Declined so XmlReader can replay the
// raw stream through the authoritative XmlStreamingPump from the start and
// fast-forward to the exact token index already emitted.
internal sealed class OptimisticPullReader : IDisposable
{
    internal enum Step
    {
        Token,
        Completed,
        NeedInput,
        Declined
    }

    private const uint Seed = 2166136261, Prime = 16777619;
    private const int MaxMembers = 32;
    private const int MaxProbes = 256;
    private const int ScopeAny = -2;
    private const int TextChunk = 4096;
    private const string XmlNamespace = "http://www.w3.org/XML/1998/namespace";
    private const string XmlnsNamespace = "http://www.w3.org/2000/xmlns/";

    private static readonly byte[] FlagTable = BuildFlags();
    private static readonly SearchValues<byte> TextStops = SearchValues.Create("<&]\r\0\x01\x02\x03\x04\x05\x06\x07\x08\x0B\x0C\x0E\x0F\x10\x11\x12\x13\x14\x15\x16\x17\x18\x19\x1A\x1B\x1C\x1D\x1E\x1F"u8);
    private static readonly SearchValues<byte> Controls = SearchValues.Create("\r\0\x01\x02\x03\x04\x05\x06\x07\x08\x0B\x0C\x0E\x0F\x10\x11\x12\x13\x14\x15\x16\x17\x18\x19\x1A\x1B\x1C\x1D\x1E\x1F"u8);

    internal readonly struct NameEntry(int keyStart, int keyLength, uint hash, string local, string prefix, byte kind)
    {
        internal readonly int KeyStart = keyStart;
        internal readonly int KeyLength = keyLength;
        internal readonly uint Hash = hash;
        internal readonly string Local = local;
        internal readonly string Prefix = prefix;
        internal readonly byte Kind = kind;
    }

    private struct Frame
    {
        internal int NameId;
        internal string Uri;
        internal int SavedScope, BindingStart;
    }

    internal struct AttrSlot
    {
        internal int NameId;
        internal int ValueStart;
        internal int ValueLength;
        internal string NamespaceUri;
        internal XmlLocation Location;
    }

    private enum Phase
    {
        BeforeDocument,
        BeforeProlog,
        Prolog,
        Content,
        CDataBody,
        CDataEnd,
        EmptyClose,
        Epilog,
        EndDocument,
        Done
    }

    private byte[] rawBytes;
    private int rawLength;
    private bool eof;

    private Phase phase = Phase.BeforeDocument;
    private int i, line = 1, lineStart, cont;
    private int depth, scope, scopeCounter, internCount;
    private bool rootSeen;
    private readonly int maxDepth, maxMembers, tokenLimit, nameLimit;
    private readonly long docLimit, expandedLimit;
    private long referenceBudget;

    private Frame[] frames;
    private int[] table;
    private NameEntry[] names;
    private int[] nameElemScope;
    private string?[] nameElemUri;
    private int[] nameAttrScope;
    private string?[] nameAttrUri;
    private int nameCount;
    private string[]? bindingPrefixes, bindingUris;
    private int bindingCount;
    private Dictionary<string, string>? uris;

    private AttrSlot[] attributes, declarations;
    private int attributeCount, declarationCount;
    private char[] attributeChars;
    private int attributeLength;

    private int pendingCloseNameId = -1;
    private string pendingCloseUri = "";
    private XmlLocation pendingCloseLocation;
    private XmlLocation cdataEndLocation;

    internal int EmittedTokens { get; private set; }
    internal ReadOnlyMemory<byte> RawInput => rawBytes.AsMemory(0, rawLength);
    internal bool EofSeen => eof;

    internal OptimisticPullReader(XmlParseOptions options)
    {
        var limits = options.Limits;
        maxDepth = limits.Depth ?? int.MaxValue;
        maxMembers = Math.Min(MaxMembers, limits.AttributesPerElement ?? int.MaxValue);
        tokenLimit = limits.TokenCharacters ?? int.MaxValue;
        nameLimit = limits.InternedNames ?? int.MaxValue;
        docLimit = limits.DocumentCharacters ?? long.MaxValue;
        expandedLimit = limits.ExpandedCharacters ?? long.MaxValue;
        referenceBudget = expandedLimit;
        rawBytes = ArrayPool<byte>.Shared.Rent(8192);
        attributeChars = ArrayPool<char>.Shared.Rent(512);
        frames = ArrayPool<Frame>.Shared.Rent(16);
        table = ArrayPool<int>.Shared.Rent(64);
        table.AsSpan(0, 64).Fill(-1);
        tableSize = 64;
        names = ArrayPool<NameEntry>.Shared.Rent(32);
        nameElemScope = ArrayPool<int>.Shared.Rent(32);
        nameElemScope.AsSpan(0, 32).Fill(-1);
        nameElemUri = ArrayPool<string?>.Shared.Rent(32);
        nameAttrScope = ArrayPool<int>.Shared.Rent(32);
        nameAttrScope.AsSpan(0, 32).Fill(-1);
        nameAttrUri = ArrayPool<string?>.Shared.Rent(32);
        attributes = ArrayPool<AttrSlot>.Shared.Rent(8);
        declarations = ArrayPool<AttrSlot>.Shared.Rent(4);
    }

    internal static bool CanUse(XmlParseOptions options) =>
        !options.Recover && options.DtdProcessing != XmlDtdProcessing.Parse &&
        (options.Limits.DocumentCharacters ?? long.MaxValue) >= 64 &&
        (options.Limits.TokenCharacters ?? int.MaxValue) >= 64 &&
        (options.Limits.ExpandedCharacters ?? long.MaxValue) >= 64 &&
        (options.Limits.InternedNames ?? int.MaxValue) >= 16 &&
        (options.Limits.Depth ?? int.MaxValue) >= 4 &&
        (options.Limits.AttributesPerElement ?? int.MaxValue) >= 4;

    internal void AppendInput(ReadOnlySpan<byte> chunk, bool isFinal)
    {
        if (!chunk.IsEmpty)
        {
            var needed = checked(rawLength + chunk.Length);
            if (needed > rawBytes.Length)
            {
                var next = ArrayPool<byte>.Shared.Rent(Math.Max(needed, rawBytes.Length * 2));
                rawBytes.AsSpan(0, rawLength).CopyTo(next);
                ArrayPool<byte>.Shared.Return(rawBytes);
                rawBytes = next;
            }
            chunk.CopyTo(rawBytes.AsSpan(rawLength));
            rawLength = needed;
        }
        eof |= isFinal;
    }

    private int tableSize = 128;

    public void Dispose()
    {
        if (rawBytes.Length != 0)
        {
            ArrayPool<byte>.Shared.Return(rawBytes);
            rawBytes = [];
        }
        if (attributeChars.Length != 0)
        {
            ArrayPool<char>.Shared.Return(attributeChars);
            attributeChars = [];
        }
        if (frames.Length != 0)
        {
            ArrayPool<Frame>.Shared.Return(frames, clearArray: true);
            frames = [];
        }
        if (table.Length != 0)
        {
            ArrayPool<int>.Shared.Return(table);
            table = [];
        }
        if (names.Length != 0)
        {
            names.AsSpan(0, nameCount).Clear();
            ArrayPool<NameEntry>.Shared.Return(names);
            names = [];
        }
        if (nameElemScope.Length != 0)
        {
            ArrayPool<int>.Shared.Return(nameElemScope);
            nameElemScope = [];
        }
        if (nameElemUri.Length != 0)
        {
            nameElemUri.AsSpan(0, nameCount).Clear();
            ArrayPool<string?>.Shared.Return(nameElemUri);
            nameElemUri = [];
        }
        if (nameAttrScope.Length != 0)
        {
            ArrayPool<int>.Shared.Return(nameAttrScope);
            nameAttrScope = [];
        }
        if (nameAttrUri.Length != 0)
        {
            nameAttrUri.AsSpan(0, nameCount).Clear();
            ArrayPool<string?>.Shared.Return(nameAttrUri);
            nameAttrUri = [];
        }
        if (attributes.Length != 0)
        {
            ArrayPool<AttrSlot>.Shared.Return(attributes, clearArray: true);
            attributes = [];
        }
        if (declarations.Length != 0)
        {
            ArrayPool<AttrSlot>.Shared.Return(declarations, clearArray: true);
            declarations = [];
        }
    }

    private int Column(int position) => position - lineStart - cont + 1;
    private XmlLocation Loc(int atLine, int atColumn, int atOffset) => new(null, atLine, atColumn, atOffset);

    internal int ReadMoreFrom(Stream input)
    {
        var writable = rawBytes.Length - rawLength;
        if (writable < 4096)
        {
            var next = ArrayPool<byte>.Shared.Rent(rawBytes.Length * 2);
            rawBytes.AsSpan(0, rawLength).CopyTo(next);
            ArrayPool<byte>.Shared.Return(rawBytes);
            rawBytes = next;
        }
        var count = input.Read(rawBytes.AsSpan(rawLength, 4096));
        rawLength += count;
        eof |= count == 0;
        return count;
    }

    internal async ValueTask<int> ReadMoreFromAsync(Stream input, CancellationToken token)
    {
        var writable = rawBytes.Length - rawLength;
        if (writable < 4096)
        {
            var next = ArrayPool<byte>.Shared.Rent(rawBytes.Length * 2);
            rawBytes.AsSpan(0, rawLength).CopyTo(next);
            ArrayPool<byte>.Shared.Return(rawBytes);
            rawBytes = next;
        }
        var count = await input.ReadAsync(rawBytes.AsMemory(rawLength, 4096), token).ConfigureAwait(false);
        rawLength += count;
        eof |= count == 0;
        return count;
    }

    internal Step Advance(XmlReader.Cursor cursor)
    {
        if (rawLength > docLimit || rawLength > expandedLimit)
        {
            return Step.Declined;
        }

        while (true)
        {
            switch (phase)
            {
                case Phase.BeforeDocument:
                    phase = Phase.BeforeProlog;
                    EmittedTokens++;
                    cursor.StartDocument(new(null, 1, 1, 0));
                    return Step.Token;

                case Phase.BeforeProlog:
                    if (rawLength - i < 6 && !eof)
                    {
                        return Step.NeedInput;
                    }
                    if (rawLength >= 3 && rawBytes[0] == 0xEF && rawBytes[1] == 0xBB && rawBytes[2] == 0xBF)
                    {
                        i = 3;
                        lineStart = 3;
                    }
                    else if (rawLength >= 2 && (rawBytes[0] == 0xFE && rawBytes[1] == 0xFF || rawBytes[0] == 0xFF && rawBytes[1] == 0xFE || rawBytes[0] == 0 || rawBytes[1] == 0))
                    {
                        return Step.Declined;
                    }
                    if (rawLength - i < 6 && !eof)
                    {
                        return Step.NeedInput;
                    }
                    {
                        var head = rawBytes.AsSpan(i, rawLength - i);
                        if (head.Length >= 6 && head.StartsWith("<?xml"u8) && head[5] is (byte)' ' or (byte)'\t' or (byte)'\n' or (byte)'\r')
                        {
                            var declStep = TryReadDeclaration(cursor);
                            if (declStep != Step.Token)
                            {
                                return declStep;
                            }
                            phase = Phase.Prolog;
                            EmittedTokens++;
                            return Step.Token;
                        }
                    }
                    phase = Phase.Prolog;
                    break;

                case Phase.Prolog:
                {
                    var miscStep = TryReadMisc(cursor);
                    if (miscStep == Step.Token)
                    {
                        EmittedTokens++;
                        return Step.Token;
                    }
                    if (miscStep != Step.Completed)
                    {
                        return miscStep;
                    }
                    if (i >= rawLength)
                    {
                        return eof ? Step.Declined : Step.NeedInput;
                    }
                    if (rawBytes[i] != '<')
                    {
                        return Step.Declined;
                    }
                    if (i + 3 >= rawLength && !eof)
                    {
                        return Step.NeedInput;
                    }
                    if (i + 1 >= rawLength)
                    {
                        return Step.Declined;
                    }
                    var next = rawBytes[i + 1];
                    if (next == '!')
                    {
                        if (i + 2 < rawLength && rawBytes[i + 2] == 'D')
                        {
                            return Step.Declined;
                        }
                        var commentStep = TryReadComment(cursor);
                        if (commentStep == Step.Token)
                        {
                            EmittedTokens++;
                        }
                        return commentStep;
                    }
                    if (next == '?')
                    {
                        var piStep = TryReadProcessingInstruction(cursor);
                        if (piStep == Step.Token)
                        {
                            EmittedTokens++;
                        }
                        return piStep;
                    }
                    phase = Phase.Content;
                    break;
                }

                case Phase.EmptyClose:
                {
                    phase = depth == 0 ? Phase.Epilog : Phase.Content;
                    EmittedTokens++;
                    ref readonly var closeName = ref names[pendingCloseNameId];
                    cursor.SetOwnedEndElement(closeName.Local, closeName.Prefix, pendingCloseUri, pendingCloseLocation);
                    pendingCloseNameId = -1;
                    return Step.Token;
                }

                case Phase.CDataBody:
                {
                    var cdataBodyStep = TryReadCDataBody(cursor);
                    if (cdataBodyStep == Step.Token)
                    {
                        EmittedTokens++;
                        return Step.Token;
                    }
                    if (cdataBodyStep == Step.Completed)
                    {
                        continue;
                    }
                    return cdataBodyStep;
                }

                case Phase.CDataEnd:
                {
                    phase = Phase.Content;
                    EmittedTokens++;
                    cursor.EndCData(cdataEndLocation);
                    return Step.Token;
                }

                case Phase.Content:
                {
                    if (i >= rawLength)
                    {
                        return eof ? Step.Declined : Step.NeedInput;
                    }
                    if (rawBytes[i] != '<')
                    {
                        var textStep = TryReadText(cursor);
                        if (textStep == Step.Token)
                        {
                            EmittedTokens++;
                        }
                        return textStep;
                    }
                    if (i + 3 >= rawLength && !eof)
                    {
                        return Step.NeedInput;
                    }
                    if (i + 1 >= rawLength)
                    {
                        return Step.Declined;
                    }
                    switch (rawBytes[i + 1])
                    {
                        case (byte)'/':
                        {
                            var closeStep = TryReadCloseTag(cursor);
                            if (closeStep == Step.Token)
                            {
                                EmittedTokens++;
                                if (depth == 0)
                                {
                                    phase = Phase.Epilog;
                                }
                            }
                            return closeStep;
                        }
                        case (byte)'!':
                        {
                            if (i + 2 < rawLength && rawBytes[i + 2] == '[')
                            {
                                var cdataStep = TryReadStartCData(cursor);
                                if (cdataStep == Step.Token)
                                {
                                    EmittedTokens++;
                                }
                                return cdataStep;
                            }
                            var commentStep = TryReadComment(cursor);
                            if (commentStep == Step.Token)
                            {
                                EmittedTokens++;
                            }
                            return commentStep;
                        }
                        case (byte)'?':
                        {
                            var piStep = TryReadProcessingInstruction(cursor);
                            if (piStep == Step.Token)
                            {
                                EmittedTokens++;
                            }
                            return piStep;
                        }
                        default:
                        {
                            var openStep = TryReadOpenTag(cursor);
                            if (openStep == Step.Token)
                            {
                                EmittedTokens++;
                            }
                            return openStep;
                        }
                    }
                }

                case Phase.Epilog:
                {
                    var miscStep = TryReadMisc(cursor);
                    if (miscStep == Step.Token)
                    {
                        EmittedTokens++;
                        return Step.Token;
                    }
                    if (miscStep != Step.Completed)
                    {
                        return miscStep;
                    }
                    if (i >= rawLength)
                    {
                        if (!eof)
                        {
                            return Step.NeedInput;
                        }
                        phase = Phase.EndDocument;
                        break;
                    }
                    if (rawBytes[i] != '<')
                    {
                        return Step.Declined;
                    }
                    if (i + 3 >= rawLength && !eof)
                    {
                        return Step.NeedInput;
                    }
                    if (i + 1 >= rawLength)
                    {
                        return Step.Declined;
                    }
                    var next = rawBytes[i + 1];
                    if (next == '!')
                    {
                        var commentStep = TryReadComment(cursor);
                        if (commentStep == Step.Token)
                        {
                            EmittedTokens++;
                        }
                        return commentStep;
                    }
                    if (next == '?')
                    {
                        var piStep = TryReadProcessingInstruction(cursor);
                        if (piStep == Step.Token)
                        {
                            EmittedTokens++;
                        }
                        return piStep;
                    }
                    return Step.Declined;
                }

                case Phase.EndDocument:
                    phase = Phase.Done;
                    EmittedTokens++;
                    cursor.EndDocument(Loc(line, Column(i), i));
                    return Step.Token;

                case Phase.Done:
                    return Step.Completed;
            }
        }
    }

    private Step TryReadMisc(XmlReader.Cursor cursor)
    {
        if (i >= rawLength)
        {
            return eof ? Step.Completed : Step.NeedInput;
        }
        var c = rawBytes[i];
        if (c is not ((byte)' ' or (byte)'\t' or (byte)'\n'))
        {
            return c == '\r' ? Step.Declined : Step.Completed;
        }
        int start = i, startLine = line, startCol = Column(i);
        var p = i;
        int curLine = line, curLineStart = lineStart, curCont = cont;
        while (p < rawLength && p - start < TextChunk)
        {
            var b = rawBytes[p];
            if (b is (byte)' ' or (byte)'\t')
            {
                p++;
            }
            else if (b == '\n')
            {
                p++;
                curLine++;
                curLineStart = p;
                curCont = 0;
            }
            else if (b == '\r')
            {
                return Step.Declined;
            }
            else
            {
                break;
            }
        }
        if (p == rawLength && !eof && p - start < TextChunk)
        {
            return Step.NeedInput;
        }
        if (p - start >= tokenLimit)
        {
            return Step.Declined;
        }
        cursor.SetAsciiText(rawBytes.AsSpan(start, p - start), Loc(startLine, startCol, start));
        i = p;
        line = curLine;
        lineStart = curLineStart;
        cont = curCont;
        return Step.Token;
    }

    private Step TryReadDeclaration(XmlReader.Cursor cursor)
    {
        var s = rawBytes.AsSpan(0, rawLength);
        var close = s[i..].IndexOf("?>"u8);
        if (close < 0)
        {
            return eof || rawLength - i >= tokenLimit ? Step.Declined : Step.NeedInput;
        }
        if (close + 2 >= tokenLimit || s.Slice(i, close + 2).Contains((byte)'\r'))
        {
            return Step.Declined;
        }

        int savedI = i, savedLine = line, savedLineStart = lineStart, savedCont = cont;
        var declarationOffset = i;
        i += 5;
        SkipWhitespace(s);
        if (!Keyword(s, "version"u8))
        {
            return RestoreAndDecline(savedI, savedLine, savedLineStart, savedCont);
        }
        SkipWhitespace(s);
        if (!Eat(s, '='))
        {
            return RestoreAndDecline(savedI, savedLine, savedLineStart, savedCont);
        }
        SkipWhitespace(s);
        if ((uint)i >= (uint)s.Length || s[i] is not ((byte)'"' or (byte)'\''))
        {
            return RestoreAndDecline(savedI, savedLine, savedLineStart, savedCont);
        }
        var quote = s[i++];
        if (!(i + 1 < s.Length && s[i] == '1' && s[i + 1] == '.'))
        {
            return RestoreAndDecline(savedI, savedLine, savedLineStart, savedCont);
        }
        var versionStart = i;
        i += 2;
        var digits = 0;
        while ((uint)i < (uint)s.Length && s[i] is >= (byte)'0' and <= (byte)'9')
        {
            i++;
            digits++;
        }
        var versionEnd = i;
        if (digits == 0 || (uint)i >= (uint)s.Length || s[i++] != quote)
        {
            return RestoreAndDecline(savedI, savedLine, savedLineStart, savedCont);
        }

        var mark = i;
        SkipWhitespace(s);
        var spaced = i > mark;
        int encodingStart = 0, encodingEnd = 0;
        if (Keyword(s, "encoding"u8))
        {
            if (!spaced)
            {
                return RestoreAndDecline(savedI, savedLine, savedLineStart, savedCont);
            }
            SkipWhitespace(s);
            if (!Eat(s, '='))
            {
                return RestoreAndDecline(savedI, savedLine, savedLineStart, savedCont);
            }
            SkipWhitespace(s);
            if ((uint)i >= (uint)s.Length || s[i] is not ((byte)'"' or (byte)'\''))
            {
                return RestoreAndDecline(savedI, savedLine, savedLineStart, savedCont);
            }
            quote = s[i++];
            var encClose = s[i..].IndexOf(quote);
            if (encClose < 0 || !Ascii.EqualsIgnoreCase(s.Slice(i, encClose), "UTF-8"u8))
            {
                return RestoreAndDecline(savedI, savedLine, savedLineStart, savedCont);
            }
            encodingStart = i;
            encodingEnd = i + encClose;
            i += encClose + 1;
            mark = i;
            SkipWhitespace(s);
            spaced = i > mark;
        }
        bool? standalone = null;
        if (Keyword(s, "standalone"u8))
        {
            if (!spaced)
            {
                return RestoreAndDecline(savedI, savedLine, savedLineStart, savedCont);
            }
            SkipWhitespace(s);
            if (!Eat(s, '='))
            {
                return RestoreAndDecline(savedI, savedLine, savedLineStart, savedCont);
            }
            SkipWhitespace(s);
            if ((uint)i >= (uint)s.Length || s[i] is not ((byte)'"' or (byte)'\''))
            {
                return RestoreAndDecline(savedI, savedLine, savedLineStart, savedCont);
            }
            quote = s[i++];
            if (Keyword(s, "yes"u8))
            {
                standalone = true;
            }
            else if (Keyword(s, "no"u8))
            {
                standalone = false;
            }
            else
            {
                return RestoreAndDecline(savedI, savedLine, savedLineStart, savedCont);
            }
            if ((uint)i >= (uint)s.Length || s[i++] != quote)
            {
                return RestoreAndDecline(savedI, savedLine, savedLineStart, savedCont);
            }
            SkipWhitespace(s);
        }
        if (!(i + 1 < s.Length && s[i] == '?' && s[i + 1] == '>'))
        {
            return RestoreAndDecline(savedI, savedLine, savedLineStart, savedCont);
        }
        i += 2;
        cursor.SetAsciiDeclaration(s[versionStart..versionEnd], s[encodingStart..encodingEnd], standalone, new(null, 1, 1, declarationOffset));
        return Step.Token;
    }

    private Step RestoreAndDecline(int savedI, int savedLine, int savedLineStart, int savedCont)
    {
        i = savedI;
        line = savedLine;
        lineStart = savedLineStart;
        cont = savedCont;
        return Step.Declined;
    }

    private Step TryReadComment(XmlReader.Cursor cursor)
    {
        var s = rawBytes.AsSpan(0, rawLength);
        var open = i;
        if (open + 4 > s.Length)
        {
            return eof ? Step.Declined : Step.NeedInput;
        }
        if (!(s[open + 2] == '-' && s[open + 3] == '-'))
        {
            return Step.Declined;
        }
        var bodyStart = open + 4;
        var dash = s[bodyStart..].IndexOf("--"u8);
        if (dash < 0)
        {
            return eof || s.Length - open >= tokenLimit ? Step.Declined : Step.NeedInput;
        }
        var bodyEnd = bodyStart + dash;
        if (bodyEnd + 2 >= s.Length)
        {
            return eof ? Step.Declined : Step.NeedInput;
        }
        if (s[bodyEnd + 2] != '>' || bodyEnd + 3 - open >= tokenLimit)
        {
            return Step.Declined;
        }
        var run = s[bodyStart..bodyEnd];
        if (run.IndexOfAny(Controls) >= 0)
        {
            return Step.Declined;
        }
        var high = !Ascii.IsValid(run);
        if (high && (!Utf8.IsValid(run) || HasNonCharacter(run)))
        {
            return Step.Declined;
        }
        var loc = Loc(line, Column(open), open);
        cursor.SetUtf8Token(XmlTokenKind.Comment, run, high, loc);
        TrackLines(s, bodyStart, bodyEnd, high);
        i = bodyEnd + 3;
        return Step.Token;
    }

    private Step TryReadStartCData(XmlReader.Cursor cursor)
    {
        var s = rawBytes.AsSpan(0, rawLength);
        if (s.Length - i < 9)
        {
            return eof ? Step.Declined : Step.NeedInput;
        }
        if (!s[i..].StartsWith("<![CDATA["u8))
        {
            return Step.Declined;
        }
        var close = s[(i + 9)..].IndexOf("]]>"u8);
        if (close < 0)
        {
            return eof || s.Length - i >= tokenLimit ? Step.Declined : Step.NeedInput;
        }
        var bodyStart = i + 9;
        var bodyEnd = bodyStart + close;
        if (bodyEnd + 3 - i >= tokenLimit)
        {
            return Step.Declined;
        }
        var run = s[bodyStart..bodyEnd];
        if (run.IndexOfAny(Controls) >= 0)
        {
            return Step.Declined;
        }
        var high = !Ascii.IsValid(run);
        if (high && (!Utf8.IsValid(run) || HasNonCharacter(run)))
        {
            return Step.Declined;
        }
        var openLoc = Loc(line, Column(i), i);
        i = bodyStart;
        phase = Phase.CDataBody;
        cursor.StartCData(openLoc);
        return Step.Token;
    }

    private Step TryReadCDataBody(XmlReader.Cursor cursor)
    {
        var s = rawBytes.AsSpan(0, rawLength);
        var close = s[i..].IndexOf("]]>"u8);
        if (close < 0)
        {
            return Step.Declined;
        }
        var bodyEnd = i + close;
        if (bodyEnd > i)
        {
            var run = s[i..bodyEnd];
            var high = !Ascii.IsValid(run);
            var textLoc = Loc(line, Column(i), i);
            cursor.SetUtf8Token(XmlTokenKind.Text, run, high, textLoc);
            TrackLines(s, i, bodyEnd, high);
            cdataEndLocation = Loc(line, Column(bodyEnd), bodyEnd);
            i = bodyEnd + 3;
            phase = Phase.CDataEnd;
            return Step.Token;
        }
        cdataEndLocation = Loc(line, Column(bodyEnd), bodyEnd);
        i = bodyEnd + 3;
        phase = Phase.CDataEnd;
        return Step.Completed;
    }

    private Step TryReadProcessingInstruction(XmlReader.Cursor cursor)
    {
        var s = rawBytes.AsSpan(0, rawLength);
        var open = i;
        var close = s[(open + 2)..].IndexOf("?>"u8);
        if (close < 0)
        {
            return eof || s.Length - open >= tokenLimit ? Step.Declined : Step.NeedInput;
        }
        var end = open + 2 + close + 2;
        if (end - open >= tokenLimit)
        {
            return Step.Declined;
        }
        int p = open + 2, targetStart = p;
        if (p >= s.Length || (FlagTable[s[p]] & 2) == 0)
        {
            return Step.Declined;
        }
        while (p < s.Length && (FlagTable[s[p]] & 1) != 0 && s[p] != ':')
        {
            p++;
        }
        if (p >= s.Length || s[p] == ':')
        {
            return Step.Declined;
        }
        var target = s[targetStart..p];
        if (target.Length == 3 && Ascii.EqualsIgnoreCase(target, "xml"u8))
        {
            return Step.Declined;
        }
        if (internCount + 10 > nameLimit)
        {
            return Step.Declined;
        }
        int savedI = i, savedLine = line, savedLineStart = lineStart, savedCont = cont;
        int bodyStart, bodyEnd;
        if (s[p] == '?')
        {
            if (p + 1 >= s.Length || s[p + 1] != '>')
            {
                return Step.Declined;
            }
            bodyStart = bodyEnd = p;
            p += 2;
        }
        else
        {
            if (s[p] is not ((byte)' ' or (byte)'\t' or (byte)'\n'))
            {
                return Step.Declined;
            }
            i = p;
            SkipWhitespace(s);
            bodyStart = i;
            bodyEnd = end - 2;
            p = end;
            var bodyRun = s[bodyStart..bodyEnd];
            if (bodyRun.IndexOfAny(Controls) >= 0)
            {
                return RestoreAndDecline(savedI, savedLine, savedLineStart, savedCont);
            }
            var high = !Ascii.IsValid(bodyRun);
            if (high && (!Utf8.IsValid(bodyRun) || HasNonCharacter(bodyRun)))
            {
                return RestoreAndDecline(savedI, savedLine, savedLineStart, savedCont);
            }
            TrackLines(s, bodyStart, bodyEnd, high);
        }
        internCount++;
        i = p;
        cursor.SetProcessingInstruction(target, s[bodyStart..bodyEnd], Loc(savedLine, ColumnAt(open, savedLineStart, savedCont), open));
        return Step.Token;
    }

    private static int ColumnAt(int position, int lineStart, int cont) => position - lineStart - cont + 1;

    private Step TryReadText(XmlReader.Cursor cursor)
    {
        var s = rawBytes.AsSpan(0, rawLength);
        var start = i;
        var found = s[start..].IndexOfAny(TextStops);
        if (found < 0)
        {
            if (!eof)
            {
                if (s.Length - start < TextChunk)
                {
                    return Step.NeedInput;
                }
                return Step.Declined;
            }
            return Step.Declined;
        }
        var end = start + found;
        var stop = s[end];
        if (stop == '<')
        {
            if (end - start >= tokenLimit)
            {
                return Step.Declined;
            }
            var run = s[start..end];
            var high = !Ascii.IsValid(run);
            if (high && (!Utf8.IsValid(run) || HasNonCharacter(run)))
            {
                return Step.Declined;
            }
            var loc = Loc(line, Column(start), start);
            cursor.SetUtf8Token(XmlTokenKind.Text, run, high, loc);
            TrackLines(s, start, end, high);
            i = end;
            return Step.Token;
        }
        if (stop == '&')
        {
            var lt = s[end..].IndexOf((byte)'<');
            if (lt < 0)
            {
                return eof || s.Length - start >= tokenLimit ? Step.Declined : Step.NeedInput;
            }
            var textEnd = end + lt;
            if (textEnd - start >= tokenLimit)
            {
                return Step.Declined;
            }
            return TryReadTextWithReferences(s, start, textEnd, cursor);
        }
        return Step.Declined;
    }

    private Step TryReadTextWithReferences(ReadOnlySpan<byte> s, int start, int textEnd, XmlReader.Cursor cursor)
    {
        int savedLine = line, savedLineStart = lineStart, savedCont = cont;
        long savedRefBudget = referenceBudget;
        var startLoc = Loc(line, Column(start), start);
        cursor.BeginTextBuild();
        int segment = start, at = start;
        while (at < textEnd)
        {
            var found = s[at..textEnd].IndexOfAny(TextStops);
            var end = found < 0 ? textEnd : at + found;
            if (end > segment)
            {
                var run = s[segment..end];
                var high = !Ascii.IsValid(run);
                if (high && (!Utf8.IsValid(run) || HasNonCharacter(run)))
                {
                    line = savedLine; lineStart = savedLineStart; cont = savedCont; referenceBudget = savedRefBudget;
                    cursor.Clear();
                    return Step.Declined;
                }
                cursor.AppendUtf8Text(run, high);
                TrackLines(s, segment, end, high);
            }
            if (end == textEnd)
            {
                break;
            }
            if (s[end] != '&' || !Reference(s, end, out var value, out var refLen))
            {
                line = savedLine; lineStart = savedLineStart; cont = savedCont; referenceBudget = savedRefBudget;
                cursor.Clear();
                return Step.Declined;
            }
            cursor.AppendScalarText(value);
            at = segment = end + refLen;
        }
        i = textEnd;
        cursor.FinishTextBuild(startLoc);
        return Step.Token;
    }

    private Step TryReadOpenTag(XmlReader.Cursor cursor)
    {
        var s = rawBytes.AsSpan(0, rawLength);
        var gt = FindTagEnd(s, i + 1);
        if (gt < 0)
        {
            return eof || s.Length - i >= tokenLimit ? Step.Declined : Step.NeedInput;
        }
        if (gt + 1 - i >= tokenLimit)
        {
            return Step.Declined;
        }
        if (depth >= maxDepth - 1 || (depth == 0 && rootSeen))
        {
            return Step.Declined;
        }

        int savedI = i, savedLine = line, savedLineStart = lineStart, savedCont = cont;
        long savedRefBudget = referenceBudget;
        int tagStart = i, tagLine = line, tagColumn = Column(i);

        var hash = Seed;
        int p = i + 1, nameStart = p;
        while ((uint)p < (uint)s.Length)
        {
            var b = s[p];
            if ((FlagTable[b] & 1) == 0)
            {
                break;
            }
            hash = (hash ^ b) * Prime;
            p++;
        }
        if (p == nameStart)
        {
            return Step.Declined;
        }
        var nameId = FindOrCreateName(s, nameStart, p - nameStart, hash);
        if (nameId < 0 || names[nameId].Kind != 0)
        {
            return Step.Declined;
        }
        i = p;
        attributeCount = 0;
        declarationCount = 0;
        attributeLength = 0;
        var empty = false;

        while (true)
        {
            var mark = i;
            if (!SkipTagWhitespace(s))
            {
                return RestoreAndDecline(savedI, savedLine, savedLineStart, savedCont, savedRefBudget);
            }
            if ((uint)i >= (uint)s.Length)
            {
                return RestoreAndDecline(savedI, savedLine, savedLineStart, savedCont, savedRefBudget);
            }
            var c = s[i];
            if (c == '>')
            {
                i++;
                break;
            }
            if (c == '/')
            {
                if (i + 1 < s.Length && s[i + 1] == '>')
                {
                    i += 2;
                    empty = true;
                    break;
                }
                return RestoreAndDecline(savedI, savedLine, savedLineStart, savedCont, savedRefBudget);
            }
            if (i == mark || attributeCount + declarationCount >= maxMembers)
            {
                return RestoreAndDecline(savedI, savedLine, savedLineStart, savedCont, savedRefBudget);
            }

            int attrOffset = i, attrLine = line, attrColumn = Column(i);
            var attrHash = Seed;
            int ap = i, attrNameStart = ap;
            while ((uint)ap < (uint)s.Length)
            {
                var b = s[ap];
                if ((FlagTable[b] & 1) == 0)
                {
                    break;
                }
                attrHash = (attrHash ^ b) * Prime;
                ap++;
            }
            if (ap == attrNameStart)
            {
                return RestoreAndDecline(savedI, savedLine, savedLineStart, savedCont, savedRefBudget);
            }
            var attrNameId = FindOrCreateName(s, attrNameStart, ap - attrNameStart, attrHash);
            if (attrNameId < 0)
            {
                return RestoreAndDecline(savedI, savedLine, savedLineStart, savedCont, savedRefBudget);
            }
            i = ap;
            if (!SkipTagWhitespace(s) || !Eat(s, '=') || !SkipTagWhitespace(s))
            {
                return RestoreAndDecline(savedI, savedLine, savedLineStart, savedCont, savedRefBudget);
            }
            if ((uint)i >= (uint)s.Length || s[i] is not ((byte)'"' or (byte)'\''))
            {
                return RestoreAndDecline(savedI, savedLine, savedLineStart, savedCont, savedRefBudget);
            }
            var quote = s[i++];
            if (!AttributeValue(s, quote, out var valueStart, out var valueLength))
            {
                return RestoreAndDecline(savedI, savedLine, savedLineStart, savedCont, savedRefBudget);
            }
            var attrLoc = Loc(attrLine, attrColumn, attrOffset);
            if (names[attrNameId].Kind != 0)
            {
                if (declarationCount == declarations.Length)
                {
                    var grown = ArrayPool<AttrSlot>.Shared.Rent(declarations.Length * 2);
                    declarations.AsSpan(0, declarationCount).CopyTo(grown);
                    ArrayPool<AttrSlot>.Shared.Return(declarations, clearArray: true);
                    declarations = grown;
                }
                declarations[declarationCount++] = new()
                {
                    NameId = attrNameId,
                    ValueStart = valueStart,
                    ValueLength = valueLength,
                    Location = attrLoc
                };
            }
            else
            {
                if (attributeCount == attributes.Length)
                {
                    var grown = ArrayPool<AttrSlot>.Shared.Rent(attributes.Length * 2);
                    attributes.AsSpan(0, attributeCount).CopyTo(grown);
                    ArrayPool<AttrSlot>.Shared.Return(attributes, clearArray: true);
                    attributes = grown;
                }
                attributes[attributeCount++] = new()
                {
                    NameId = attrNameId,
                    ValueStart = valueStart,
                    ValueLength = valueLength,
                    Location = attrLoc
                };
            }
        }

        int savedScope = scope, savedBindings = bindingCount;
        if (declarationCount > 0 && !Declare())
        {
            scope = savedScope;
            bindingCount = savedBindings;
            return RestoreAndDecline(savedI, savedLine, savedLineStart, savedCont, savedRefBudget);
        }

        var elementUri = ElementUri(nameId);
        if (elementUri is null)
        {
            scope = savedScope;
            bindingCount = savedBindings;
            return RestoreAndDecline(savedI, savedLine, savedLineStart, savedCont, savedRefBudget);
        }

        for (var a = 0; a < attributeCount; a++)
        {
            ref var attribute = ref attributes[a];
            var uri = AttributeUri(attribute.NameId);
            if (uri is null)
            {
                scope = savedScope;
                bindingCount = savedBindings;
                return RestoreAndDecline(savedI, savedLine, savedLineStart, savedCont, savedRefBudget);
            }
            attribute.NamespaceUri = uri;
        }
        if (attributeCount > 1 && !AttributesAreUnique())
        {
            scope = savedScope;
            bindingCount = savedBindings;
            return RestoreAndDecline(savedI, savedLine, savedLineStart, savedCont, savedRefBudget);
        }

        var location = Loc(tagLine, tagColumn, tagStart);
        ref readonly var elemName = ref names[nameId];
        cursor.SetPullStartElement(
            elemName.Local,
            elemName.Prefix,
            elementUri,
            empty,
            location,
            names,
            attributes.AsSpan(0, attributeCount),
            declarations.AsSpan(0, declarationCount),
            attributeChars.AsSpan(0, attributeLength));

        if (empty)
        {
            scope = savedScope;
            bindingCount = savedBindings;
            rootSeen |= depth == 0;
            pendingCloseNameId = nameId;
            pendingCloseUri = elementUri;
            pendingCloseLocation = location;
            phase = Phase.EmptyClose;
            return Step.Token;
        }

        depth++;
        if (depth >= frames.Length)
        {
            var grown = ArrayPool<Frame>.Shared.Rent(frames.Length * 2);
            frames.AsSpan(0, depth).CopyTo(grown);
            ArrayPool<Frame>.Shared.Return(frames, clearArray: true);
            frames = grown;
        }
        frames[depth] = new()
        {
            NameId = nameId,
            Uri = elementUri,
            SavedScope = savedScope,
            BindingStart = savedBindings
        };
        rootSeen = true;
        return Step.Token;
    }

    private Step RestoreAndDecline(int savedI, int savedLine, int savedLineStart, int savedCont, long savedRefBudget)
    {
        i = savedI;
        line = savedLine;
        lineStart = savedLineStart;
        cont = savedCont;
        referenceBudget = savedRefBudget;
        return Step.Declined;
    }

    private Step TryReadCloseTag(XmlReader.Cursor cursor)
    {
        var s = rawBytes.AsSpan(0, rawLength);
        var gt = s[i..].IndexOf((byte)'>');
        if (gt < 0)
        {
            return eof || s.Length - i >= tokenLimit ? Step.Declined : Step.NeedInput;
        }
        if (depth == 0 || gt + 1 >= tokenLimit)
        {
            return Step.Declined;
        }
        ref var frame = ref frames[depth];
        int savedI = i, savedLine = line, savedLineStart = lineStart, savedCont = cont;
        int tagStart = i, closeLine = line, closeColumn = Column(i);
        var p = i + 2;
        ref readonly var openName = ref names[frame.NameId];
        var openKey = s.Slice(openName.KeyStart, openName.KeyLength);
        if (s.Length - p < openKey.Length || !s.Slice(p, openKey.Length).SequenceEqual(openKey))
        {
            return Step.Declined;
        }
        p += openKey.Length;
        i = p;
        if (!SkipTagWhitespace(s) || (uint)i >= (uint)s.Length || s[i] != '>')
        {
            return RestoreAndDecline(savedI, savedLine, savedLineStart, savedCont);
        }
        i++;
        scope = frame.SavedScope;
        bindingCount = frame.BindingStart;
        var uri = frame.Uri;
        depth--;
        cursor.SetOwnedEndElement(openName.Local, openName.Prefix, uri, Loc(closeLine, closeColumn, tagStart));
        return Step.Token;
    }

    private static int FindTagEnd(ReadOnlySpan<byte> s, int start)
    {
        var slice = s[start..];
        var gt = slice.IndexOf((byte)'>');
        if (gt < 0)
        {
            return -1;
        }
        var prefix = slice[..gt];
        if (!prefix.ContainsAny((byte)'<', (byte)'"', (byte)'\''))
        {
            return start + gt;
        }
        byte quote = 0;
        for (var p = start; p < s.Length; p++)
        {
            var b = s[p];
            if (quote != 0)
            {
                if (b == quote)
                {
                    quote = 0;
                }
            }
            else if (b == '>')
            {
                return p;
            }
            else if (b is (byte)'"' or (byte)'\'')
            {
                quote = b;
            }
            else if (b == '<')
            {
                return -1;
            }
        }
        return -1;
    }

    private void SkipWhitespace(ReadOnlySpan<byte> s)
    {
        while ((uint)i < (uint)s.Length)
        {
            var c = s[i];
            if (c is (byte)' ' or (byte)'\t')
            {
                i++;
            }
            else if (c == '\n')
            {
                i++;
                line++;
                lineStart = i;
                cont = 0;
            }
            else
            {
                break;
            }
        }
    }

    private bool SkipTagWhitespace(ReadOnlySpan<byte> s)
    {
        while ((uint)i < (uint)s.Length)
        {
            var c = s[i];
            if (c is (byte)' ' or (byte)'\t')
            {
                i++;
            }
            else if (c == '\n')
            {
                i++;
                line++;
                lineStart = i;
                cont = 0;
            }
            else if (c == '\r')
            {
                return false;
            }
            else
            {
                break;
            }
        }
        return true;
    }

    private bool Eat(ReadOnlySpan<byte> s, char ch)
    {
        if ((uint)i < (uint)s.Length && s[i] == ch)
        {
            i++;
            return true;
        }
        return false;
    }

    private bool Keyword(ReadOnlySpan<byte> s, ReadOnlySpan<byte> word)
    {
        if (s.Length - i >= word.Length && s.Slice(i, word.Length).SequenceEqual(word))
        {
            i += word.Length;
            return true;
        }
        return false;
    }

    private void EnsureAttributeChars(int needed)
    {
        if (attributeChars.Length >= needed)
        {
            return;
        }
        var grown = ArrayPool<char>.Shared.Rent(Math.Max(needed, attributeChars.Length * 2));
        attributeChars.AsSpan(0, attributeLength).CopyTo(grown);
        ArrayPool<char>.Shared.Return(attributeChars);
        attributeChars = grown;
    }

    private bool AttributeValue(ReadOnlySpan<byte> s, byte quote, out int valueStart, out int valueLength)
    {
        int start = i, p = i;
        valueStart = attributeLength;
        valueLength = 0;
        while ((uint)p < (uint)s.Length)
        {
            var b = s[p];
            if (b == quote)
            {
                if (p - start >= tokenLimit)
                {
                    return false;
                }
                i = p + 1;
                var length = p - start;
                if (length > 0)
                {
                    EnsureAttributeChars(attributeLength + length);
                    Ascii.ToUtf16(s.Slice(start, length), attributeChars.AsSpan(attributeLength), out _);
                    attributeLength += length;
                    valueLength = length;
                }
                return true;
            }
            if (b == '<' || b < 0x20 || b == '&' || b >= 0x80)
            {
                break;
            }
            p++;
        }
        return ComplexAttributeValue(s, quote, start, p, out valueStart, out valueLength);
    }

    private bool ComplexAttributeValue(ReadOnlySpan<byte> s, byte quote, int start, int p, out int valueStart, out int valueLength)
    {
        valueStart = attributeLength;
        valueLength = 0;
        EnsureAttributeChars(attributeLength + (p - start) + 16);
        for (var k = start; k < p; k++)
        {
            attributeChars[attributeLength++] = (char)s[k];
        }

        while (true)
        {
            if ((uint)p >= (uint)s.Length)
            {
                return false;
            }
            var b = s[p];
            if (attributeLength + 3 > attributeChars.Length)
            {
                EnsureAttributeChars(attributeLength + 3);
            }
            if (b == quote)
            {
                break;
            }
            if (b == '&')
            {
                if (!Reference(s, p, out var value, out var refLen))
                {
                    return false;
                }
                attributeLength = AppendScalar(attributeChars, attributeLength, value);
                p += refLen;
            }
            else if (b == '\t')
            {
                attributeChars[attributeLength++] = ' ';
                p++;
            }
            else if (b == '\n')
            {
                attributeChars[attributeLength++] = ' ';
                p++;
                line++;
                lineStart = p;
                cont = 0;
            }
            else if (b < 0x20 || b == '<')
            {
                return false;
            }
            else if (b < 0x80)
            {
                attributeChars[attributeLength++] = (char)b;
                p++;
            }
            else
            {
                if (Rune.DecodeFromUtf8(s[p..], out var rune, out var consumed) != OperationStatus.Done || rune.Value is 0xFFFE or 0xFFFF)
                {
                    return false;
                }
                attributeLength += rune.EncodeToUtf16(attributeChars.AsSpan(attributeLength));
                cont += consumed - 1;
                p += consumed;
            }
        }
        if (p - start >= tokenLimit)
        {
            return false;
        }
        i = p + 1;
        valueLength = attributeLength - valueStart;
        return true;
    }

    private bool Reference(ReadOnlySpan<byte> s, int at, out int value, out int length)
    {
        value = 0;
        length = 0;
        var n = s.Length;
        if (--referenceBudget < 0 || at + 3 >= n)
        {
            return false;
        }

        switch (s[at + 1])
        {
            case (byte)'#':
                return NumericReference(s, at, out value, out length);
            case (byte)'l':
                if (s[at + 2] == 't' && s[at + 3] == ';')
                {
                    value = '<';
                    length = 4;
                    return true;
                }
                return false;
            case (byte)'g':
                if (s[at + 2] == 't' && s[at + 3] == ';')
                {
                    value = '>';
                    length = 4;
                    return true;
                }
                return false;
            case (byte)'a':
                if (s[at + 2] == 'm' && s[at + 3] == 'p' && at + 4 < n && s[at + 4] == ';')
                {
                    value = '&';
                    length = 5;
                    return true;
                }
                if (s[at + 2] == 'p' && s[at + 3] == 'o' && at + 5 < n && s[at + 4] == 's' && s[at + 5] == ';')
                {
                    value = '\'';
                    length = 6;
                    return true;
                }
                return false;
            case (byte)'q':
                if (s[at + 2] == 'u' && s[at + 3] == 'o' && at + 5 < n && s[at + 4] == 't' && s[at + 5] == ';')
                {
                    value = '"';
                    length = 6;
                    return true;
                }
                return false;
        }
        return false;
    }

    private static bool NumericReference(ReadOnlySpan<byte> s, int at, out int value, out int length)
    {
        value = 0;
        length = 0;
        var n = s.Length;
        int p = at + 2, digits = 0;
        long code = 0;
        if (s[p] == 'x')
        {
            p++;
            for (; p < n && digits < 8; p++, digits++)
            {
                var b = s[p];
                int d;
                if (b is >= (byte)'0' and <= (byte)'9')
                {
                    d = b - '0';
                }
                else if (b is >= (byte)'a' and <= (byte)'f')
                {
                    d = b - 'a' + 10;
                }
                else if (b is >= (byte)'A' and <= (byte)'F')
                {
                    d = b - 'A' + 10;
                }
                else
                {
                    break;
                }
                code = code * 16 + d;
            }
        }
        else
        {
            for (; p < n && digits < 8; p++, digits++)
            {
                var b = s[p];
                if (b is < (byte)'0' or > (byte)'9')
                {
                    break;
                }
                code = code * 10 + (b - '0');
            }
        }
        if (digits == 0 || p >= n || s[p] != ';')
        {
            return false;
        }
        if ((code < 0x20 && code is not (9 or 10 or 13)) || code > 0x10FFFF || code is >= 0xD800 and <= 0xDFFF or 0xFFFE or 0xFFFF)
        {
            return false;
        }
        value = (int)code;
        length = p + 1 - at;
        return true;
    }

    private static int AppendScalar(char[] buffer, int length, int codePoint)
    {
        if (codePoint <= 0xFFFF)
        {
            buffer[length] = (char)codePoint;
            return length + 1;
        }
        codePoint -= 0x10000;
        buffer[length] = (char)(0xD800 + (codePoint >> 10));
        buffer[length + 1] = (char)(0xDC00 + (codePoint & 0x3FF));
        return length + 2;
    }

    private int FindOrCreateName(ReadOnlySpan<byte> s, int start, int length, uint hash)
    {
        var key = s.Slice(start, length);
        var mask = (uint)tableSize - 1;
        var slot = (hash ^ (hash >> 15)) & mask;
        var probes = 0;
        while (true)
        {
            var id = table[slot];
            if (id < 0)
            {
                break;
            }
            ref readonly var entry = ref names[id];
            if (entry.Hash == hash && entry.KeyLength == length && s.Slice(entry.KeyStart, length).SequenceEqual(key))
            {
                return id;
            }
            if (++probes > MaxProbes)
            {
                return -1;
            }
            slot = (slot + 1) & mask;
        }
        return CreateName(key, start, length, hash, slot);
    }

    private int CreateName(ReadOnlySpan<byte> key, int keyStart, int keyLength, uint hash, uint slot)
    {
        if ((FlagTable[key[0]] & 2) == 0)
        {
            return -1;
        }
        var colon = key.IndexOf((byte)':');
        if (colon >= 0 && key[(colon + 1)..].Contains((byte)':'))
        {
            return -1;
        }
        if (colon == 0 || colon == key.Length - 1)
        {
            return -1;
        }
        if (colon > 0 && (FlagTable[key[colon + 1]] & 2) == 0)
        {
            return -1;
        }
        internCount += 2;
        if (internCount + 8 > nameLimit)
        {
            return -1;
        }
        var prefix = colon < 0 ? "" : Encoding.Latin1.GetString(key[..colon]);
        var local = colon < 0 ? Encoding.Latin1.GetString(key) : Encoding.Latin1.GetString(key[(colon + 1)..]);
        byte kind = 0;
        if (colon < 0 && key.SequenceEqual("xmlns"u8))
        {
            kind = 1;
            local = "";
        }
        else if (prefix == "xmlns")
        {
            if (local is "xmlns" or "xml")
            {
                return -1;
            }
            kind = 2;
            prefix = "";
        }
        var id = nameCount;
        if (id == names.Length)
        {
            GrowNameArrays();
        }
        names[id] = new(keyStart, keyLength, hash, local, prefix, kind);
        nameElemScope[id] = -1;
        nameElemUri[id] = null;
        nameAttrScope[id] = -1;
        nameAttrUri[id] = null;
        nameCount++;
        table[slot] = id;
        if (nameCount * 2 > tableSize)
        {
            GrowTable();
        }
        return id;
    }

    private void GrowNameArrays()
    {
        var nextSize = names.Length * 2;
        var nextNames = ArrayPool<NameEntry>.Shared.Rent(nextSize);
        names.AsSpan(0, nameCount).CopyTo(nextNames);
        names.AsSpan(0, nameCount).Clear();
        ArrayPool<NameEntry>.Shared.Return(names);
        names = nextNames;

        var nextElemScope = ArrayPool<int>.Shared.Rent(nextSize);
        nameElemScope.AsSpan(0, nameCount).CopyTo(nextElemScope);
        ArrayPool<int>.Shared.Return(nameElemScope);
        nameElemScope = nextElemScope;

        var nextElemUri = ArrayPool<string?>.Shared.Rent(nextSize);
        nameElemUri.AsSpan(0, nameCount).CopyTo(nextElemUri);
        nameElemUri.AsSpan(0, nameCount).Clear();
        ArrayPool<string?>.Shared.Return(nameElemUri);
        nameElemUri = nextElemUri;

        var nextAttrScope = ArrayPool<int>.Shared.Rent(nextSize);
        nameAttrScope.AsSpan(0, nameCount).CopyTo(nextAttrScope);
        ArrayPool<int>.Shared.Return(nameAttrScope);
        nameAttrScope = nextAttrScope;

        var nextAttrUri = ArrayPool<string?>.Shared.Rent(nextSize);
        nameAttrUri.AsSpan(0, nameCount).CopyTo(nextAttrUri);
        nameAttrUri.AsSpan(0, nameCount).Clear();
        ArrayPool<string?>.Shared.Return(nameAttrUri);
        nameAttrUri = nextAttrUri;
    }

    private void GrowTable()
    {
        var nextSize = tableSize * 2;
        var grown = ArrayPool<int>.Shared.Rent(nextSize);
        grown.AsSpan(0, nextSize).Fill(-1);
        var mask = (uint)nextSize - 1;
        for (var id = 0; id < nameCount; id++)
        {
            var hash = names[id].Hash;
            var slot = (hash ^ (hash >> 15)) & mask;
            while (grown[slot] >= 0)
            {
                slot = (slot + 1) & mask;
            }
            grown[slot] = id;
        }
        ArrayPool<int>.Shared.Return(table);
        table = grown;
        tableSize = nextSize;
    }

    private string? Lookup(string prefix)
    {
        for (var b = bindingCount - 1; b >= 0; b--)
        {
            if (bindingPrefixes![b] == prefix)
            {
                return bindingUris![b];
            }
        }
        return prefix.Length == 0 ? "" : null;
    }

    private string? ElementUri(int nameId)
    {
        var cachedScope = nameElemScope[nameId];
        if (cachedScope == scope || cachedScope == ScopeAny)
        {
            return nameElemUri[nameId];
        }
        var prefix = names[nameId].Prefix;
        string? uri;
        var validFor = scope;
        if (prefix == "xml")
        {
            uri = XmlNamespace;
            validFor = ScopeAny;
        }
        else
        {
            uri = Lookup(prefix);
        }
        if (uri is null)
        {
            return null;
        }
        nameElemUri[nameId] = uri;
        nameElemScope[nameId] = validFor;
        return uri;
    }

    private string? AttributeUri(int nameId)
    {
        var cachedScope = nameAttrScope[nameId];
        if (cachedScope == scope || cachedScope == ScopeAny)
        {
            return nameAttrUri[nameId];
        }
        var prefix = names[nameId].Prefix;
        string? uri;
        var validFor = scope;
        if (prefix.Length == 0)
        {
            uri = "";
            validFor = ScopeAny;
        }
        else if (prefix == "xml")
        {
            uri = XmlNamespace;
            validFor = ScopeAny;
        }
        else
        {
            uri = Lookup(prefix);
        }
        if (uri is null)
        {
            return null;
        }
        nameAttrUri[nameId] = uri;
        nameAttrScope[nameId] = validFor;
        return uri;
    }

    private bool AttributesAreUnique()
    {
        for (var a = 1; a < attributeCount; a++)
        {
            ref var first = ref attributes[a];
            for (var b = 0; b < a; b++)
            {
                ref var second = ref attributes[b];
                if (first.NameId == second.NameId || (names[first.NameId].Local == names[second.NameId].Local && first.NamespaceUri == second.NamespaceUri))
                {
                    return false;
                }
            }
        }
        return true;
    }

    private bool Declare()
    {
        for (var d = 0; d < declarationCount; d++)
        {
            ref var declaration = ref declarations[d];
            for (var e = 0; e < d; e++)
            {
                if (declarations[e].NameId == declaration.NameId)
                {
                    return false;
                }
            }
            ref readonly var declName = ref names[declaration.NameId];
            var prefix = declName.Kind == 1 ? "" : declName.Local;
            var value = attributeChars.AsSpan(declaration.ValueStart, declaration.ValueLength);
            if (value.SequenceEqual(XmlnsNamespace) || value.SequenceEqual(XmlNamespace) || (prefix.Length != 0 && value.IsEmpty))
            {
                return false;
            }
            if (internCount + 9 > nameLimit)
            {
                return false;
            }
            internCount++;
            var uri = InternUri(value);
            declaration.NamespaceUri = uri;
            if (bindingPrefixes is null)
            {
                bindingPrefixes = new string[8];
                bindingUris = new string[8];
            }
            else if (bindingCount == bindingPrefixes.Length)
            {
                Array.Resize(ref bindingPrefixes, bindingCount * 2);
                Array.Resize(ref bindingUris, bindingCount * 2);
            }
            bindingPrefixes[bindingCount] = prefix;
            bindingUris![bindingCount++] = uri;
        }
        scope = ++scopeCounter;
        return true;
    }

    private string InternUri(ReadOnlySpan<char> value)
    {
        if (value.IsEmpty)
        {
            return "";
        }
        uris ??= new(StringComparer.Ordinal);
        var lookup = uris.GetAlternateLookup<ReadOnlySpan<char>>();
        if (lookup.TryGetValue(value, out var existing))
        {
            return existing;
        }
        string owned = new(value);
        uris.Add(owned, owned);
        return owned;
    }

    private void TrackLines(ReadOnlySpan<byte> s, int start, int end, bool high)
    {
        var run = s[start..end];
        var newline = run.LastIndexOf((byte)'\n');
        if (newline >= 0)
        {
            line += run.Count((byte)'\n');
            lineStart = start + newline + 1;
            cont = high ? ContinuationBytes(run[(newline + 1)..]) : 0;
        }
        else if (high)
        {
            cont += ContinuationBytes(run);
        }
    }

    private static int ContinuationBytes(ReadOnlySpan<byte> bytes)
    {
        int count = 0, at = 0;
        if (Vector128.IsHardwareAccelerated && bytes.Length >= Vector128<byte>.Count)
        {
            ref var origin = ref MemoryMarshal.GetReference(bytes);
            var threshold = Vector128.Create((sbyte)-64);
            for (; at <= bytes.Length - Vector128<byte>.Count; at += Vector128<byte>.Count)
            {
                var block = Vector128.LoadUnsafe(ref origin, (nuint)at).AsSByte();
                count += BitOperations.PopCount(Vector128.LessThan(block, threshold).ExtractMostSignificantBits());
            }
        }
        for (; at < bytes.Length; at++)
        {
            if ((bytes[at] & 0xC0) == 0x80)
            {
                count++;
            }
        }
        return count;
    }

    private static bool HasNonCharacter(ReadOnlySpan<byte> bytes)
    {
        while (true)
        {
            var at = bytes.IndexOf((byte)0xEF);
            if (at < 0 || at + 2 >= bytes.Length)
            {
                return false;
            }
            if (bytes[at + 1] == 0xBF && bytes[at + 2] is 0xBE or 0xBF)
            {
                return true;
            }
            bytes = bytes[(at + 1)..];
        }
    }

    private static byte[] BuildFlags()
    {
        var flags = new byte[256];
        for (var c = 'A'; c <= 'Z'; c++)
        {
            flags[c] = 3;
        }
        for (var c = 'a'; c <= 'z'; c++)
        {
            flags[c] = 3;
        }
        for (var c = '0'; c <= '9'; c++)
        {
            flags[c] = 1;
        }
        flags['_'] = 3;
        flags[':'] = 3;
        flags['-'] = 1;
        flags['.'] = 1;
        return flags;
    }
}
