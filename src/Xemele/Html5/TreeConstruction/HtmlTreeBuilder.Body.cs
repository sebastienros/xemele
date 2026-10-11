namespace Xemele.Html;
internal sealed partial class HtmlTreeBuilder
{
    private bool InBody(HtmlToken t)
    {
        string n = t.Name;
        switch (t.Kind)
        {
            case HtmlTokenKind.Character:
                string data = t.Data.Replace("\0", "", StringComparison.Ordinal); Reconstruct(); Characters(data); if (!White(data)) framesetOk = false; return false;
            case HtmlTokenKind.Comment: case HtmlTokenKind.ProcessingInstruction: Misc(t); return false;
            case HtmlTokenKind.Doctype: return false;
            case HtmlTokenKind.EndOfFile:
                if (templates.Count > 0) { mode = Mode.InTemplate; return true; } return false;
            case HtmlTokenKind.StartTag: return BodyStart(t);
            case HtmlTokenKind.EndTag:
                if (n == "template") return InHead(t);
                if (n == "body") { if (InScope("body")) mode = Mode.AfterBody; return false; }
                if (n == "html") { if (!InScope("body")) return false; mode = Mode.AfterBody; return true; }
                if (OneOf(n, "address", "article", "aside", "blockquote", "button", "center", "details", "dialog", "dir", "div", "dl", "fieldset", "figcaption", "figure", "footer", "header", "hgroup", "listing", "main", "menu", "nav", "ol", "pre", "search", "section", "summary", "ul"))
                { if (!InScope(n)) return false; Implied(); PopThrough(n); return false; }
                if (n == "form")
                {
                    if (!ParsingTemplate) { var pointer = form; form = null; if (pointer is null || !InScope(pointer)) return false; Implied(); stack.Remove(pointer); }
                    else { if (!InScope("form")) return false; Implied(); PopThrough("form"); } return false;
                }
                if (n == "p") { if (!InScope("p", Scope.Button)) BodyStart(Start("p")); CloseP(); return false; }
                if (n == "li") { if (!InScope(n, Scope.List)) return false; Implied(n); PopThrough(n); return false; }
                if (n is "dd" or "dt") { if (!InScope(n)) return false; Implied(n); PopThrough(n); return false; }
                if (OneOf(n, "h1", "h2", "h3", "h4", "h5", "h6"))
                { bool found = InScope("h1") || InScope("h2") || InScope("h3") || InScope("h4") || InScope("h5") || InScope("h6"); if (!found) return false; Implied(); while (stack.Count > 0) { bool heading = Current.NamespaceUri == HtmlDocument.HtmlNamespace && OneOf(Current.LocalName, "h1", "h2", "h3", "h4", "h5", "h6"); Pop(); if (heading) break; } return false; }
                if (OneOf(n, "a", "b", "big", "code", "em", "font", "i", "nobr", "s", "small", "strike", "strong", "tt", "u")) { Adoption(n); return false; }
                if (n is "applet" or "marquee" or "object") { if (!InScope(n)) return false; Implied(); PopThrough(n); ClearFormatting(); return false; }
                if (n == "br") { Reconstruct(); Insert(Start("br")); Pop(); framesetOk = false; return false; }
                GenericEnd(n); return false;
            default: return false;
        }
    }
    private bool BodyStart(HtmlToken t)
    {
        string n = t.Name;
        if (n == "html") { if (!ParsingTemplate) MergeAttributes(stack[0], t); return false; }
        if (OneOf(n, "base", "basefont", "bgsound", "link", "meta", "noframes", "script", "style", "template", "title")) return InHead(t);
        if (n == "body") { if (stack.Count > 1 && Html(stack[1], "body") && !ParsingTemplate) { framesetOk = false; MergeAttributes(stack[1], t); } return false; }
        if (n == "frameset")
        {
            if (stack.Count < 2 || !Html(stack[1], "body") || !framesetOk) return false;
            FlushObservedText(stack[1]); stack[1].Remove(); stack.RemoveRange(1, stack.Count - 1); Insert(t); mode = Mode.InFrameset; return false;
        }
        if (OneOf(n, "address", "article", "aside", "blockquote", "center", "details", "dialog", "dir", "div", "dl", "fieldset", "figcaption", "figure", "footer", "header", "hgroup", "main", "menu", "nav", "ol", "p", "search", "section", "summary", "ul"))
        { if (InScope("p", Scope.Button)) CloseP(); Insert(t); return false; }
        if (OneOf(n, "h1", "h2", "h3", "h4", "h5", "h6")) { if (InScope("p", Scope.Button)) CloseP(); if (Current.NamespaceUri == HtmlDocument.HtmlNamespace && OneOf(Current.LocalName, "h1", "h2", "h3", "h4", "h5", "h6")) Pop(); Insert(t); return false; }
        if (n is "pre" or "listing") { if (InScope("p", Scope.Button)) CloseP(); Insert(t); skipLf = true; framesetOk = false; return false; }
        if (n == "form") { if (form is not null && !ParsingTemplate) return false; if (InScope("p", Scope.Button)) CloseP(); var node = Insert(t); if (!ParsingTemplate) form = node; return false; }
        if (n is "li" or "dd" or "dt")
        {
            framesetOk = false;
            for (int i = stack.Count - 1; i >= 0; i--)
            {
                var node = stack[i];
                if (node.NamespaceUri == HtmlDocument.HtmlNamespace && (n == "li" ? node.LocalName == "li" : node.LocalName is "dd" or "dt")) { Implied(node.LocalName); PopThrough(node); break; }
                if (Special(node) && !(node.NamespaceUri == HtmlDocument.HtmlNamespace && OneOf(node.LocalName, "address", "div", "p"))) break;
            }
            if (InScope("p", Scope.Button)) CloseP(); Insert(t); return false;
        }
        if (n == "plaintext") { if (InScope("p", Scope.Button)) CloseP(); Insert(t); Tokenizer.SetState(HtmlTokenizerState.PlainText); return false; }
        if (n == "button") { if (InScope("button")) { Implied(); PopThrough("button"); } Reconstruct(); Insert(t); framesetOk = false; return false; }
        if (n == "a")
        {
            if (Formatting("a") is { } old) { Adoption("a"); formatting.Remove(old); stack.Remove(old); }
            Reconstruct(); AddFormatting(Insert(t)); return false;
        }
        if (OneOf(n, "b", "big", "code", "em", "font", "i", "s", "small", "strike", "strong", "tt", "u")) { Reconstruct(); AddFormatting(Insert(t)); return false; }
        if (n == "nobr") { Reconstruct(); if (InScope("nobr")) { Adoption("nobr"); Reconstruct(); } AddFormatting(Insert(t)); return false; }
        if (n is "applet" or "marquee" or "object") { Reconstruct(); Insert(t); formatting.Add(null); framesetOk = false; return false; }
        if (n == "table") { if (document.QuirksMode != HtmlQuirksMode.Quirks && InScope("p", Scope.Button)) CloseP(); Insert(t); framesetOk = false; mode = Mode.InTable; return false; }
        if (OneOf(n, "area", "br", "embed", "img", "keygen", "wbr")) { Reconstruct(); Insert(t); Pop(); framesetOk = false; return false; }
        if (n == "input") { if (context is not null && Html(context, "select")) return false; if (InScope("select")) PopThrough("select"); Reconstruct(); Insert(t); Pop(); bool hidden = false; foreach (var a in t.Attributes) if (a.LocalName == "type" && HtmlAscii.Equals(a.Value, "hidden")) hidden = true; if (!hidden) framesetOk = false; return false; }
        if (n is "param" or "source" or "track") { Insert(t); Pop(); return false; }
        if (n == "hr") { if (InScope("p", Scope.Button)) CloseP(); if (InScope("select")) Implied(); Insert(t); Pop(); framesetOk = false; return false; }
        if (n == "image") return BodyStart(new(t.Kind, "img", t.Data, t.Attributes, t.SelfClosing, t.Doctype, t.Location));
        if (n == "textarea") { Insert(t); skipLf = true; Tokenizer.SetState(HtmlTokenizerState.RcData, n); original = mode; framesetOk = false; mode = Mode.Text; return false; }
        if (n == "xmp") { if (InScope("p", Scope.Button)) CloseP(); Reconstruct(); framesetOk = false; Text(t, HtmlTokenizerState.RawText); return false; }
        if (n == "iframe") { framesetOk = false; Text(t, HtmlTokenizerState.RawText); return false; }
        if (n == "noembed" || n == "noscript" && options.ScriptingEnabled) { Text(t, HtmlTokenizerState.RawText); return false; }
        if (n == "select") { if (context is not null && Html(context, "select")) return false; if (InScope("select")) { PopThrough("select"); return false; } Reconstruct(); Insert(t); framesetOk = false; return false; }
        if (n is "optgroup" or "option") { if (InScope("select")) Implied(n == "option" ? "optgroup" : null); else if (Html(Current, "option")) Pop(); Reconstruct(); Insert(t); return false; }
        if (n is "rb" or "rtc") { if (InScope("ruby")) Implied(); Insert(t); return false; }
        if (n is "rp" or "rt") { if (InScope("ruby")) Implied("rtc"); Insert(t); return false; }
        if (n is "math" or "svg") { Reconstruct(); Insert(t, n == "math" ? HtmlForeignNames.MathMlNamespace : HtmlForeignNames.SvgNamespace); if (t.SelfClosing) Pop(); return false; }
        if (OneOf(n, "caption", "col", "colgroup", "frame", "head", "tbody", "td", "tfoot", "th", "thead", "tr")) return false;
        Reconstruct(); Insert(t); return false;
    }
    private static void MergeAttributes(HtmlNode node, HtmlToken token) { foreach (var attribute in token.Attributes) if (node.GetAttribute(attribute.LocalName, attribute.NamespaceUri) is null) node.SetAttribute(attribute); }
}
