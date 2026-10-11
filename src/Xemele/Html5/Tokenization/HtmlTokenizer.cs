using System.Text;

namespace Xemele.Html;

// Incremental scalar state machine. Only a current token and fixed-size reference/prefix
// candidates are retained. The callback receives owned immutable lexical snapshots.
internal sealed partial class HtmlTokenizer
{
    private enum State
    {
        Data, RcData, RawText, ScriptData, PlainText,
        TagOpen, PiOpen, PiTarget, PiAfterTarget, PiData, PiQuestionable, EndTagOpen, TagName, TextLess, TextEndOpen, TextEndName,
        ScriptLess, EscapeStart, EscapeStartDash, Escaped, EscapedDash, EscapedDashDash,
        EscapedLess, EscapedEndOpen, EscapedEndName, DoubleStart, DoubleEscaped,
        DoubleDash, DoubleDashDash, DoubleLess, DoubleEnd,
        BeforeAttr, AttrName, AfterAttr, BeforeValue, DoubleValue, SingleValue,
        UnquotedValue, AfterValue, SelfClosing,
        Declaration, BogusComment, CommentStart, CommentStartDash, Comment,
        CommentLess, CommentBang, CommentBangDash, CommentBangDashDash,
        CommentEndDash, CommentEnd, CommentEndBang,
        Doctype, BeforeDoctypeName, DoctypeName, AfterDoctypeName, DoctypeKeyword,
        AfterPublicKeyword, BeforePublic, PublicDouble, PublicSingle, AfterPublic,
        BetweenIdentifiers, AfterSystemKeyword, BeforeSystem, SystemDouble, SystemSingle,
        AfterSystem, BogusDoctype, Cdata, CdataBracket, CdataEnd,
        Reference, NamedReference, NumericStart, HexStart, DecimalStart, Numeric, AmbiguousReference,
    }

    private State state, textState, returnState;
    private readonly Action<HtmlToken> callback;
    private readonly CancellationToken cancellation;
    private readonly XmlResourceLimits limits;
    private readonly StringBuilder name = new(), data = new(), attrName = new(), attrValue = new(), temporary = new();
    private readonly StringBuilder characters = new(1024);
    private readonly List<HtmlAttribute> attributes = new();
    private readonly char[] reference = new char[HtmlNamedReferences.MaximumNameLength + 1];
    private int referenceLength, numericBase;
    private uint numericValue;
    private string lastStartTag;
    private string? publicId, systemId;
    private bool endTag, selfClosing, forceQuirks, attributeActive, duplicateAttribute;
    private bool previousCr, completed, faulted, feeding;
    private char highSurrogate;
    private long? highOffset;
    private long documentScalars, expandedScalars, attributeScalars;
    private int line = 1, column = 1, cancellationTicks, diagnosticCount;
    private XmlLocation current, opening, characterOpening, referenceOpening, lookaheadOpening;
    internal bool AllowCdata { get; set; }
    internal Action<string, XmlLocation>? ParseError { get; set; }
    internal Uri? SourceUri { get; set; }

    internal HtmlTokenizer(Action<HtmlToken> callback, HtmlTokenizerOptions? options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(callback);
        options = HtmlOperationSnapshots.Snapshot(options);
        this.callback = callback;
        cancellation = cancellationToken;
        cancellation.ThrowIfCancellationRequested();
        limits = options.Limits;
        lastStartTag = options.LastStartTagName;
        SetState(options.InitialState);
    }

    // Called by a tree builder synchronously from token delivery, before the next scalar.
    internal void SetState(HtmlTokenizerState initialState, string? lastStartTagName = null)
    {
        state = initialState switch
        {
            HtmlTokenizerState.Data => State.Data, HtmlTokenizerState.RcData => State.RcData,
            HtmlTokenizerState.RawText => State.RawText, HtmlTokenizerState.ScriptData => State.ScriptData,
            HtmlTokenizerState.PlainText => State.PlainText, _ => throw new ArgumentOutOfRangeException(nameof(initialState)),
        };
        if (lastStartTagName is not null) lastStartTag = HtmlNames.Fold(lastStartTagName);
    }

    internal void StartCdata() => state = State.Cdata;

    // Byte offsets, when supplied by an incremental decoder, map each UTF-16 unit to
    // its original scalar's first byte. String callers supply no offsets.
    internal void Feed(ReadOnlySpan<char> input, ReadOnlySpan<long> byteOffsets = default)
    {
        if (completed || faulted || feeding) throw new InvalidOperationException("Tokenizer is complete, faulted, or reentered.");
        if (!byteOffsets.IsEmpty && byteOffsets.Length != input.Length) throw new ArgumentException("Offset map length differs from input.", nameof(byteOffsets));
        cancellation.ThrowIfCancellationRequested();
        feeding = true;
        try
        {
            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
                long? offset = byteOffsets.IsEmpty ? null : byteOffsets[i];
                if (highSurrogate != 0)
                {
                    char high = highSurrogate; highSurrogate = '\0';
                    if (char.IsLowSurrogate(c)) { Scalar(char.ConvertToUtf32(high, c), highOffset); continue; }
                    Scalar(high, highOffset);
                }
                if (char.IsHighSurrogate(c)) { highSurrogate = c; highOffset = offset; }
                else Scalar(c, offset);
            }
        }
        catch { faulted = true; throw; }
        finally { feeding = false; }
    }

    internal void Complete(long? endByteOffset = null)
    {
        if (completed || faulted || feeding) throw new InvalidOperationException("Tokenizer is complete, faulted, or reentered.");
        cancellation.ThrowIfCancellationRequested();
        feeding = true;
        try
        {
            if (highSurrogate != 0) { char high = highSurrogate; highSurrogate = '\0'; Scalar(high, highOffset); }
            current = new(SourceUri, line, column, endByteOffset);
            Step(-1);
            FlushCharacters();
            completed = true;
            callback(new(HtmlTokenKind.EndOfFile, "", "", [], false, null, current));
        }
        catch { faulted = true; throw; }
        finally { feeding = false; }
    }

    private void Scalar(int c, long? offset)
    {
        if ((++cancellationTicks & 255) == 0) cancellation.ThrowIfCancellationRequested();
        documentScalars = checked(documentScalars + 1);
        if (limits.DocumentCharacters is long cap && documentScalars > cap) Limit("DocumentCharacters");
        if (previousCr && c == '\n') { previousCr = false; return; }
        previousCr = c == '\r';
        if (c == '\r') c = '\n';
        current = new(SourceUri, line, column, offset);
        string? inputError = c is >= 0xD800 and <= 0xDFFF ? "surrogate-in-input-stream"
            : c is >= 0xFDD0 and <= 0xFDEF || (c & 0xFFFF) is 0xFFFE or 0xFFFF ? "noncharacter-in-input-stream"
            : c is >= 1 and <= 8 or 11 or >= 14 and <= 31 or >= 0x7F and <= 0x9F ? "control-character-in-input-stream" : null;
        bool lookahead = state == State.PiOpen || state == State.Declaration && temporary.Length > 0;
        if (!lookahead && inputError is not null) Error(inputError);
        Step(c);
        if (lookahead && inputError is not null) Error(inputError);
        if (c == '\n') { line = checked(line + 1); column = 1; }
        else column = checked(column + 1);
    }

    private void Error(string code) => Error(code, current);
    private void Error(string code, XmlLocation location)
    {
        if (ParseError is null || limits.Diagnostics is int cap && diagnosticCount >= cap) return;
        diagnosticCount = checked(diagnosticCount + 1);
        ParseError(code, location);
    }
    private static void Limit(string quota) => throw new InvalidOperationException("HTML tokenizer quota exceeded: " + quota);
    private static bool Alpha(int c) => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z';
    private static bool Alnum(int c) => Alpha(c) || c is >= '0' and <= '9';
    private static bool Space(int c) => c is '\t' or '\n' or '\f' or ' ';
    private static int Fold(int c) => c is >= 'A' and <= 'Z' ? c + 32 : c;
    private int ReplaceNull(int c) { if (c != 0) return c; Error("unexpected-null-character"); return 0xFFFD; }
    private static void AppendScalar(StringBuilder builder, int c)
    {
        if (c <= 0xFFFF) builder.Append((char)c);
        else { c -= 0x10000; builder.Append((char)(0xD800 + (c >> 10))); builder.Append((char)(0xDC00 + (c & 1023))); }
    }
    private int nameScalars, dataScalars, temporaryScalars;
    private void Clear(StringBuilder builder)
    {
        builder.Clear();
        if (builder == name) nameScalars = 0;
        else if (builder == data) dataScalars = 0;
        else if (builder == temporary) temporaryScalars = 0;
    }
    private void Append(StringBuilder builder, int c, bool expanded = true)
    {
        ref int count = ref (builder == name ? ref nameScalars : ref builder == data ? ref dataScalars : ref temporaryScalars);
        int next = checked(count + 1);
        if (limits.TokenCharacters is int cap && next > cap) Limit("TokenCharacters");
        if (builder.Length > int.MaxValue - 2) Limit("TokenCharacters");
        AppendScalar(builder, c); count = next;
        if (expanded) ChargeExpanded();
    }
    private void ChargeExpanded()
    {
        expandedScalars = checked(expandedScalars + 1);
        if (limits.ExpandedCharacters is long cap && expandedScalars > cap) Limit("ExpandedCharacters");
    }
    private void Character(int c, XmlLocation? location = null)
    {
        if (c < 0) return;
        if (characters.Length == 0) characterOpening = location ?? current;
        AppendScalar(characters, c); ChargeExpanded();
        if (characters.Length >= 1024 || limits.TokenCharacters is int cap && characters.Length >= cap) FlushCharacters();
    }
    private void Character(ReadOnlySpan<char> value, XmlLocation? location = null)
    {
        for (int i = 0; i < value.Length; i++)
        {
            int c = value[i];
            if (char.IsHighSurrogate(value[i]) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1])) c = char.ConvertToUtf32(value[i], value[++i]);
            Character(c, location);
        }
    }
    private void FlushCharacters()
    {
        if (characters.Length == 0) return;
        string owned = characters.ToString(); characters.Clear();
        callback(new(HtmlTokenKind.Character, "", owned, [], false, null, characterOpening));
    }
    private void BeginTag(bool end)
    {
        Clear(name); attributes.Clear(); Clear(attrName); Clear(attrValue); attributeActive = false;
        endTag = end; selfClosing = false; attributeScalars = 0;
    }
    private void EmitTag()
    {
        FinishAttribute();
        string ownedName = name.ToString();
        if (endTag && attributes.Count > 0) Error("end-tag-with-attributes");
        if (endTag && selfClosing) Error("end-tag-with-trailing-solidus");
        if (!endTag) lastStartTag = ownedName;
        state = State.Data;
        FlushCharacters();
        callback(new(endTag ? HtmlTokenKind.EndTag : HtmlTokenKind.StartTag, ownedName, "", attributes, selfClosing, null, opening));
        attributes.Clear();
    }
    private void BeginAttribute()
    {
        FinishAttribute(); Clear(attrName); Clear(attrValue); attributeActive = true; duplicateAttribute = false;
    }
    private void AttributeChar(StringBuilder builder, int c)
    {
        attributeScalars = checked(attributeScalars + 1);
        if (limits.TokenCharacters is int cap && attributeScalars > cap) Limit("TokenCharacters");
        AppendScalar(builder, c); if (builder == attrValue) ChargeExpanded();
    }
    private void CheckAttributeName()
    {
        foreach (var a in attributes)
            if (BuilderEquals(attrName, a.LocalName)) { duplicateAttribute = true; Error("duplicate-attribute"); break; }
    }
    private void FinishAttribute()
    {
        if (!attributeActive) return;
        if (!duplicateAttribute)
        {
            if (limits.AttributesPerElement is int cap && attributes.Count >= cap) Limit("AttributesPerElement");
            attributes.Add(new(attrName.ToString(), attrValue.ToString()));
        }
        attributeActive = false;
    }
    private void EmitComment()
    {
        string owned = data.ToString(); Clear(data); state = State.Data;
        FlushCharacters(); callback(new(HtmlTokenKind.Comment, "", owned, [], false, null, opening));
    }
    private void EmitPi()
    {
        string target = name.ToString(), owned = data.ToString();
        Clear(name); Clear(data); state = State.Data;
        FlushCharacters(); callback(new(HtmlTokenKind.ProcessingInstruction, target, owned, [], false, null, opening));
    }
    private void PiComment()
    {
        Clear(data); Append(data, '?', false);
        for (int i = 0; i < name.Length; i++) Append(data, name[i], false);
        Clear(name); state = State.BogusComment;
    }
    private void BeginDoctype()
    {
        Clear(name); Clear(data); publicId = systemId = null; forceQuirks = false;
    }
    private void EmitDoctype()
    {
        string ownedName = name.ToString(); state = State.Data;
        FlushCharacters(); callback(new(HtmlTokenKind.Doctype, ownedName, "", [], false, new(ownedName, publicId, systemId, forceQuirks), opening));
    }
}
