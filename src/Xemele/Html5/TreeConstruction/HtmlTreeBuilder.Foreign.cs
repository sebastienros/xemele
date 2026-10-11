namespace Xemele.Html;
internal sealed partial class HtmlTreeBuilder
{
    private static bool MathText(HtmlNode n) => n.NamespaceUri == HtmlForeignNames.MathMlNamespace && OneOf(n.LocalName, "mi", "mo", "mn", "ms", "mtext");
    private static bool Integration(HtmlNode n) => n.NamespaceUri == HtmlForeignNames.SvgNamespace && OneOf(n.LocalName, "foreignObject", "desc", "title") ||
        n.NamespaceUri == HtmlForeignNames.MathMlNamespace && n.LocalName == "annotation-xml" && n.GetAttribute("encoding") is { } encoding && (HtmlAscii.Equals(encoding, "text/html") || HtmlAscii.Equals(encoding, "application/xhtml+xml"));
    private bool Foreign(HtmlToken token)
    {
        var n = Adjusted;
        if (n.NamespaceUri == HtmlDocument.HtmlNamespace || token.Kind == HtmlTokenKind.EndOfFile) return false;
        if (MathText(n) && (token.Kind == HtmlTokenKind.Character || token.Kind == HtmlTokenKind.StartTag && token.Name is not ("mglyph" or "malignmark"))) return false;
        if (n.NamespaceUri == HtmlForeignNames.MathMlNamespace && n.LocalName == "annotation-xml" && token.Kind == HtmlTokenKind.StartTag && token.Name == "svg") return false;
        if (Integration(n) && token.Kind is HtmlTokenKind.StartTag or HtmlTokenKind.Character) return false;
        return true;
    }
    private bool ProcessForeign(HtmlToken token)
    {
        switch (token.Kind)
        {
            case HtmlTokenKind.Character:
                Characters(token.Data.Replace('\0', '\uFFFD')); if (!White(token.Data.Replace("\0", "", StringComparison.Ordinal))) framesetOk = false; return false;
            case HtmlTokenKind.Comment: case HtmlTokenKind.ProcessingInstruction: Misc(token); return false;
            case HtmlTokenKind.Doctype: return false;
            case HtmlTokenKind.StartTag:
                if (OneOf(token.Name, "b", "big", "blockquote", "body", "br", "center", "code", "dd", "div", "dl", "dt", "em", "embed", "h1", "h2", "h3", "h4", "h5", "h6", "head", "hr", "i", "img", "li", "listing", "menu", "meta", "nobr", "ol", "p", "pre", "ruby", "s", "small", "span", "strong", "strike", "sub", "sup", "table", "tt", "u", "ul", "var") || token.Name == "font" && HasAttribute(token, "color", "face", "size"))
                {
                    while (stack.Count > 1 && Current.NamespaceUri != HtmlDocument.HtmlNamespace && !MathText(Current) && !Integration(Current)) Pop();
                    return ProcessHtml(token);
                }
                Insert(token, Adjusted.NamespaceUri); if (token.SelfClosing) Pop(); return false;
            case HtmlTokenKind.EndTag:
                if (token.Name is "br" or "p") { while (stack.Count > 1 && Current.NamespaceUri != HtmlDocument.HtmlNamespace && !MathText(Current) && !Integration(Current)) Pop(); return ProcessHtml(token); }
                for (int i = stack.Count - 1; i >= 0; i--)
                {
                    var n = stack[i]; if (n.NamespaceUri == HtmlDocument.HtmlNamespace) return ProcessHtml(token); if (HtmlNames.Fold(n.LocalName) == token.Name) { while (stack.Count > i) Pop(); return false; }
                    if (n.NamespaceUri == HtmlDocument.HtmlNamespace) return ProcessHtml(token);
                }
                return false;
            default: return false;
        }
    }
    private static bool HasAttribute(HtmlToken token, params ReadOnlySpan<string> names) { foreach (var a in token.Attributes) if (OneOf(a.LocalName, names)) return true; return false; }
}
