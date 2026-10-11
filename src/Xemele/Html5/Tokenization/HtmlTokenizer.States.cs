namespace Xemele.Html;

internal sealed partial class HtmlTokenizer
{
    private void Step(int c)
    {
        // Reconsumption changes state without charging source or moving its location twice.
        while (true)
        {
            switch (state)
            {
                case State.Data:
                    if (c == '&') StartReference();
                    else if (c == '<') { opening = current; state = State.TagOpen; }
                    else { if (c == 0) Error("unexpected-null-character"); Character(c); }
                    return;
                case State.RcData:
                case State.RawText:
                case State.ScriptData:
                case State.PlainText:
                    if (c == '&' && state == State.RcData) StartReference();
                    else if (c == '<' && state != State.PlainText)
                    {
                        opening = current; textState = state;
                        state = state == State.ScriptData ? State.ScriptLess : State.TextLess;
                    }
                    else if (c >= 0) Character(ReplaceNull(c));
                    return;
                case State.TagOpen:
                    if (c == '!') { Clear(temporary); state = State.Declaration; return; }
                    if (c == '/') { state = State.EndTagOpen; return; }
                    if (Alpha(c)) { BeginTag(false); state = State.TagName; continue; }
                    if (c == '?') { Clear(name); Clear(data); state = State.PiOpen; return; }
                    Error(c < 0 ? "eof-before-tag-name" : "invalid-first-character-of-tag-name");
                    Character('<', opening); state = State.Data; continue;
                case State.PiOpen:
                    if (Alpha(c) || c == '_') { state = State.PiTarget; continue; }
                    if (c < 0) { Error("eof-in-processing-instruction"); return; }
                    Error("invalid-first-character-of-processing-instruction-target"); PiComment(); continue;
                case State.PiTarget:
                    if (Alnum(c) || c is '-' or '_') { Append(name, c, false); return; }
                    if (c < 0) { Error("eof-in-processing-instruction"); return; }
                    if (Space(c) || c is '?' or '>')
                    {
                        if (BuilderEquals(name, "xml", true) || BuilderEquals(name, "xml-stylesheet", true))
                        { Error("disallowed-processing-instruction-target"); PiComment(); continue; }
                        state = State.PiAfterTarget; continue;
                    }
                    Error("invalid-processing-instruction-target"); PiComment(); continue;
                case State.PiAfterTarget:
                    if (Space(c)) return;
                    state = State.PiData; continue;
                case State.PiData:
                    if (c == '?') { state = State.PiQuestionable; return; }
                    if (c == '>') { EmitPi(); return; }
                    if (c < 0) { Error("eof-in-processing-instruction"); return; }
                    Append(data, c, true); return;
                case State.PiQuestionable:
                    if (c == '>') { EmitPi(); return; }
                    if (c < 0) { Error("eof-in-processing-instruction"); return; }
                    Append(data, '?', true); state = State.PiData; continue;
                case State.EndTagOpen:
                    if (Alpha(c)) { BeginTag(true); state = State.TagName; continue; }
                    if (c == '>') { Error("missing-end-tag-name"); state = State.Data; return; }
                    if (c < 0) { Error("eof-before-tag-name"); Character("</", opening); state = State.Data; continue; }
                    Error("invalid-first-character-of-tag-name"); Clear(data); state = State.BogusComment; continue;
                case State.TagName:
                    if (Space(c)) state = State.BeforeAttr;
                    else if (c == '/') state = State.SelfClosing;
                    else if (c == '>') EmitTag();
                    else if (c < 0) Error("eof-in-tag");
                    else Append(name, Fold(ReplaceNull(c)), false);
                    return;
                case State.TextLess:
                case State.ScriptLess:
                    if (c == '/') { Clear(temporary); state = State.TextEndOpen; return; }
                    if (c == '!' && state == State.ScriptLess) { Character("<!", opening); state = State.EscapeStart; return; }
                    Character('<', opening); state = textState; continue;
                case State.TextEndOpen:
                case State.EscapedEndOpen:
                    if (Alpha(c)) { BeginTag(true); state = state == State.TextEndOpen ? State.TextEndName : State.EscapedEndName; continue; }
                    Character("</", opening); state = state == State.TextEndOpen ? textState : State.Escaped; continue;
                case State.TextEndName:
                case State.EscapedEndName:
                    if ((Space(c) || c is '/' or '>') && NameEquals(lastStartTag))
                    {
                        if (c == '>') EmitTag();
                        else state = c == '/' ? State.SelfClosing : State.BeforeAttr;
                        return;
                    }
                    if (Alpha(c)) { Append(name, Fold(c), false); Append(temporary, c, false); return; }
                    Character("</", opening); for (int i = 0; i < temporary.Length; i++) Character(temporary[i], opening);
                    state = state == State.TextEndName ? textState : State.Escaped; continue;
                case State.EscapeStart:
                    if (c == '-') { Character(c); state = State.EscapeStartDash; return; }
                    state = State.ScriptData; continue;
                case State.EscapeStartDash:
                    if (c == '-') { Character(c); state = State.EscapedDashDash; return; }
                    state = State.ScriptData; continue;
                case State.Escaped:
                case State.EscapedDash:
                case State.EscapedDashDash:
                    if (c < 0) { Error("eof-in-script-html-comment-like-text"); return; }
                    if (c == '-') { Character(c); state = state == State.Escaped ? State.EscapedDash : State.EscapedDashDash; return; }
                    if (c == '<') { opening = current; state = State.EscapedLess; return; }
                    if (c == '>' && state == State.EscapedDashDash) { Character(c); state = State.ScriptData; return; }
                    Character(ReplaceNull(c)); state = State.Escaped; return;
                case State.EscapedLess:
                    if (c == '/') { Clear(temporary); state = State.EscapedEndOpen; return; }
                    Character('<', opening);
                    if (Alpha(c)) { Clear(temporary); state = State.DoubleStart; continue; }
                    state = State.Escaped; continue;
                case State.DoubleStart:
                case State.DoubleEnd:
                    if (Alpha(c)) { if (temporary.Length < 7) AppendScalar(temporary, Fold(c)); Character(c); return; }
                    if (Space(c) || c is '/' or '>')
                    {
                        bool script = BuilderEquals(temporary, "script");
                        state = state == State.DoubleStart
                            ? script ? State.DoubleEscaped : State.Escaped
                            : script ? State.Escaped : State.DoubleEscaped;
                        Character(c); return;
                    }
                    state = state == State.DoubleStart ? State.Escaped : State.DoubleEscaped; continue;
                case State.DoubleEscaped:
                case State.DoubleDash:
                case State.DoubleDashDash:
                    if (c < 0) { Error("eof-in-script-html-comment-like-text"); return; }
                    if (c == '-') { Character(c); state = state == State.DoubleEscaped ? State.DoubleDash : State.DoubleDashDash; return; }
                    if (c == '<') { Character(c); state = State.DoubleLess; return; }
                    if (c == '>' && state == State.DoubleDashDash) { Character(c); state = State.ScriptData; return; }
                    Character(ReplaceNull(c)); state = State.DoubleEscaped; return;
                case State.DoubleLess:
                    if (c == '/') { Character(c); Clear(temporary); state = State.DoubleEnd; return; }
                    state = State.DoubleEscaped; continue;
                case State.BeforeAttr:
                    if (Space(c)) return;
                    if (c is '/' or '>' || c < 0) { state = State.AfterAttr; continue; }
                    BeginAttribute(); state = State.AttrName;
                    if (c == '=') { Error("unexpected-equals-sign-before-attribute-name"); AttributeChar(attrName, c); return; }
                    continue;
                case State.AttrName:
                    if (Space(c) || c is '/' or '>' || c < 0) { CheckAttributeName(); state = State.AfterAttr; continue; }
                    if (c == '=') { CheckAttributeName(); state = State.BeforeValue; return; }
                    if (c is '"' or '\'' or '<') Error("unexpected-character-in-attribute-name");
                    AttributeChar(attrName, Fold(ReplaceNull(c))); return;
                case State.AfterAttr:
                    if (Space(c)) return;
                    if (c == '/') { FinishAttribute(); state = State.SelfClosing; return; }
                    if (c == '=') { state = State.BeforeValue; return; }
                    if (c == '>') { EmitTag(); return; }
                    if (c < 0) { Error("eof-in-tag"); return; }
                    BeginAttribute(); state = State.AttrName; continue;
                case State.BeforeValue:
                    if (Space(c)) return;
                    if (c == '"') { state = State.DoubleValue; return; }
                    if (c == '\'') { state = State.SingleValue; return; }
                    if (c == '>') { Error("missing-attribute-value"); EmitTag(); return; }
                    state = State.UnquotedValue; continue;
                case State.DoubleValue:
                case State.SingleValue:
                    if (c == (state == State.DoubleValue ? '"' : '\'')) { FinishAttribute(); state = State.AfterValue; return; }
                    if (c == '&') { StartReference(); return; }
                    if (c < 0) { Error("eof-in-tag"); return; }
                    AttributeChar(attrValue, ReplaceNull(c)); return;
                case State.UnquotedValue:
                    if (Space(c)) { FinishAttribute(); state = State.BeforeAttr; return; }
                    if (c == '&') { StartReference(); return; }
                    if (c == '>') { EmitTag(); return; }
                    if (c < 0) { Error("eof-in-tag"); return; }
                    if (c is '"' or '\'' or '<' or '=' or '`') Error("unexpected-character-in-unquoted-attribute-value");
                    AttributeChar(attrValue, ReplaceNull(c)); return;
                case State.AfterValue:
                    if (Space(c)) { state = State.BeforeAttr; return; }
                    if (c == '/') { state = State.SelfClosing; return; }
                    if (c == '>') { EmitTag(); return; }
                    if (c < 0) { Error("eof-in-tag"); return; }
                    Error("missing-whitespace-between-attributes"); state = State.BeforeAttr; continue;
                case State.SelfClosing:
                    if (c == '>') { selfClosing = true; EmitTag(); return; }
                    if (c < 0) { Error("eof-in-tag"); return; }
                    Error("unexpected-solidus-in-tag"); state = State.BeforeAttr; continue;
                case State.Declaration:
                    if (temporary.Length == 0) lookaheadOpening = current;
                    if (c >= 0) AppendScalar(temporary, c);
                    if (BuilderEquals(temporary, "--")) { Clear(data); state = State.CommentStart; return; }
                    if (BuilderEquals(temporary, "doctype", true)) { BeginDoctype(); state = State.Doctype; return; }
                    if (BuilderEquals(temporary, "[CDATA["))
                    {
                        if (AllowCdata) state = State.Cdata;
                        else { Error("cdata-in-html-content"); Clear(data); foreach (char ch in "[CDATA[") Append(data, ch); state = State.BogusComment; }
                        return;
                    }
                    if (c >= 0 && (BuilderPrefix(temporary, "--") || BuilderPrefix(temporary, "doctype", true) || BuilderPrefix(temporary, "[CDATA["))) return;
                    Error("incorrectly-opened-comment", lookaheadOpening); Clear(data);
                    int previousLength = temporary.Length - (c < 0 ? 0 : c > 0xFFFF ? 2 : 1);
                    for (int i = 0; i < previousLength; i++) Append(data, ReplaceNull(temporary[i]));
                    state = State.BogusComment; continue;
                case State.BogusComment:
                    if (c == '>' || c < 0) { EmitComment(); if (c < 0) continue; return; }
                    Append(data, ReplaceNull(c)); return;
                case State.CommentStart:
                    if (c == '-') { state = State.CommentStartDash; return; }
                    if (c == '>') { Error("abrupt-closing-of-empty-comment"); EmitComment(); return; }
                    state = State.Comment; continue;
                case State.CommentStartDash:
                    if (c == '-') { state = State.CommentEnd; return; }
                    if (c == '>') { Error("abrupt-closing-of-empty-comment"); EmitComment(); return; }
                    if (c < 0) { Error("eof-in-comment"); EmitComment(); continue; }
                    Append(data, '-'); state = State.Comment; continue;
                case State.Comment:
                    if (c == '<') { Append(data, c); state = State.CommentLess; return; }
                    if (c == '-') { state = State.CommentEndDash; return; }
                    if (c < 0) { Error("eof-in-comment"); EmitComment(); continue; }
                    Append(data, ReplaceNull(c)); return;
                case State.CommentLess:
                    if (c == '!') { Append(data, c); state = State.CommentBang; return; }
                    if (c == '<') { Append(data, c); return; }
                    state = State.Comment; continue;
                case State.CommentBang:
                    if (c == '-') { state = State.CommentBangDash; return; }
                    state = State.Comment; continue;
                case State.CommentBangDash:
                    if (c == '-') { state = State.CommentBangDashDash; return; }
                    state = State.CommentEndDash; continue;
                case State.CommentBangDashDash:
                    if (c != '>' && c >= 0) Error("nested-comment");
                    state = State.CommentEnd; continue;
                case State.CommentEndDash:
                    if (c == '-') { state = State.CommentEnd; return; }
                    if (c < 0) { Error("eof-in-comment"); EmitComment(); continue; }
                    Append(data, '-'); state = State.Comment; continue;
                case State.CommentEnd:
                    if (c == '>') { EmitComment(); return; }
                    if (c == '!') { state = State.CommentEndBang; return; }
                    if (c == '-') { Append(data, c); return; }
                    if (c < 0) { Error("eof-in-comment"); EmitComment(); continue; }
                    Append(data, '-'); Append(data, '-'); state = State.Comment; continue;
                case State.CommentEndBang:
                    if (c == '-') { Append(data, '-'); Append(data, '-'); Append(data, '!'); state = State.CommentEndDash; return; }
                    if (c == '>') { Error("incorrectly-closed-comment"); EmitComment(); return; }
                    if (c < 0) { Error("eof-in-comment"); EmitComment(); continue; }
                    Append(data, '-'); Append(data, '-'); Append(data, '!'); state = State.Comment; continue;
                case State.Cdata:
                    if (c == ']') { opening = current; state = State.CdataBracket; return; }
                    if (c < 0) { Error("eof-in-cdata"); return; }
                    Character(c); return;
                case State.CdataBracket:
                    if (c == ']') { state = State.CdataEnd; return; }
                    Character(']', opening); state = State.Cdata; continue;
                case State.CdataEnd:
                    if (c == ']') { Character(']', opening); return; }
                    if (c == '>') { state = State.Data; return; }
                    Character("]]", opening); state = State.Cdata; continue;
                case >= State.Doctype and <= State.BogusDoctype:
                    if (DoctypeStep(c)) continue;
                    return;
                case >= State.Reference and <= State.AmbiguousReference:
                    if (ReferenceStep(c)) continue;
                    return;
                default: throw new InvalidOperationException("Unknown tokenizer state.");
            }
        }
    }

    private bool NameEquals(string value) => BuilderEquals(name, value);
    private static bool BuilderEquals(System.Text.StringBuilder builder, ReadOnlySpan<char> text, bool fold = false)
    {
        if (builder.Length != text.Length) return false;
        for (int i = 0; i < text.Length; i++) if ((fold ? Fold(builder[i]) : builder[i]) != text[i]) return false;
        return true;
    }
    private static bool BuilderPrefix(System.Text.StringBuilder builder, ReadOnlySpan<char> text, bool fold = false)
    {
        if (builder.Length > text.Length) return false;
        for (int i = 0; i < builder.Length; i++) if ((fold ? Fold(builder[i]) : builder[i]) != text[i]) return false;
        return true;
    }
}
