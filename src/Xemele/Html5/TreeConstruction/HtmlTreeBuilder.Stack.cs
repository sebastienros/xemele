namespace Xemele.Html;
internal sealed partial class HtmlTreeBuilder
{
    private enum Scope { Normal, List, Button, Table }
    private bool InScope(string name, Scope scope = Scope.Normal) => FindScope(null, name, scope);
    private bool InScope(HtmlNode node, Scope scope = Scope.Normal) => FindScope(node, null, scope);
    private bool FindScope(HtmlNode? sought, string? name, Scope scope)
    {
        for (int i = stack.Count - 1; i >= 0; i--)
        {
            var node = stack[i]; if (sought is null ? Html(node, name!) : node == sought) return true;
            if (scope == Scope.Table) { if (node.NamespaceUri == HtmlDocument.HtmlNamespace && OneOf(node.LocalName, "html", "table", "template")) return false; continue; }
            if (node.NamespaceUri == HtmlDocument.HtmlNamespace && (OneOf(node.LocalName, "applet", "caption", "html", "table", "td", "th", "marquee", "object", "select", "template") || scope == Scope.List && OneOf(node.LocalName, "ol", "ul") || scope == Scope.Button && node.LocalName == "button")) return false;
            if (node.NamespaceUri == HtmlForeignNames.MathMlNamespace && OneOf(node.LocalName, "mi", "mo", "mn", "ms", "mtext", "annotation-xml")) return false;
            if (node.NamespaceUri == HtmlForeignNames.SvgNamespace && OneOf(node.LocalName, "foreignObject", "desc", "title")) return false;
        }
        return false;
    }
    private static bool Special(HtmlNode n)
    {
        if (n.NamespaceUri == HtmlForeignNames.MathMlNamespace) return OneOf(n.LocalName, "mi", "mo", "mn", "ms", "mtext", "annotation-xml");
        if (n.NamespaceUri == HtmlForeignNames.SvgNamespace) return OneOf(n.LocalName, "foreignObject", "desc", "title");
        return n.NamespaceUri == HtmlDocument.HtmlNamespace && OneOf(n.LocalName,
            "address", "applet", "area", "article", "aside", "base", "basefont", "bgsound", "blockquote", "body", "br", "button", "caption", "center", "col", "colgroup", "dd", "details", "dir", "div", "dl", "dt", "embed", "fieldset", "figcaption", "figure", "footer", "form", "frame", "frameset", "h1", "h2", "h3", "h4", "h5", "h6", "head", "header", "hgroup", "hr", "html", "iframe", "img", "input", "keygen", "li", "link", "listing", "main", "marquee", "menu", "meta", "nav", "noembed", "noframes", "noscript", "object", "ol", "p", "param", "plaintext", "pre", "script", "search", "section", "select", "source", "style", "summary", "table", "tbody", "td", "template", "textarea", "tfoot", "th", "thead", "title", "tr", "track", "ul", "wbr", "xmp");
    }
    private void Implied(string? except = null, bool thorough = false)
    {
        while (stack.Count > 0 && Current.NamespaceUri == HtmlDocument.HtmlNamespace && Current.LocalName != except &&
            (OneOf(Current.LocalName, "dd", "dt", "li", "optgroup", "option", "p", "rb", "rp", "rt", "rtc") || thorough && OneOf(Current.LocalName, "caption", "colgroup", "tbody", "td", "tfoot", "th", "thead", "tr"))) Pop();
    }
    private void CloseP() { Implied("p"); PopThrough("p"); }
    private void ClearFormatting() { while (formatting.Count > 0) { var node = formatting[^1]; formatting.RemoveAt(formatting.Count - 1); if (node is null) break; } }
    private void Reconstruct()
    {
        if (formatting.Count == 0 || formatting[^1] is null || stack.Contains(formatting[^1]!)) return;
        int i = formatting.Count - 1;
        while (i > 0 && formatting[i - 1] is { } before && !stack.Contains(before)) i--;
        for (; i < formatting.Count; i++) { var clone = formatting[i]!.Clone(false); InsertNode(clone); Push(clone); formatting[i] = clone; }
    }
    private static bool SameFormatting(HtmlNode a, HtmlNode b)
    {
        if (a.LocalName != b.LocalName || a.NamespaceUri != b.NamespaceUri || a.Attributes.Count != b.Attributes.Count) return false;
        foreach (var attribute in a.Attributes) if (b.GetAttribute(attribute.LocalName, attribute.NamespaceUri) != attribute.Value) return false;
        return true;
    }
    private void AddFormatting(HtmlNode node)
    {
        int matches = 0, first = -1;
        for (int i = formatting.Count - 1; i >= 0 && formatting[i] is { } existing; i--) if (SameFormatting(existing, node)) { matches++; first = i; }
        if (matches >= 3) formatting.RemoveAt(first);
        formatting.Add(node);
    }
    private HtmlNode? Formatting(string name) { for (int i = formatting.Count - 1; i >= 0 && formatting[i] is { } node; i--) if (Html(node, name)) return node; return null; }
    private void GenericEnd(string name)
    {
        for (int i = stack.Count - 1; i >= 0; i--) { var node = stack[i]; if (Html(node, name)) { Implied(name); PopThrough(node); return; } if (Special(node)) return; }
    }
    private void Adoption(string name)
    {
        if (Html(Current, name) && !formatting.Contains(Current)) { Pop(); return; }
        for (int outer = 0; outer < 8; outer++)
        {
            var formattingElement = Formatting(name);
            if (formattingElement is null) { GenericEnd(name); return; }
            int formattingIndex = stack.IndexOf(formattingElement);
            if (formattingIndex < 0) { formatting.Remove(formattingElement); return; }
            if (!InScope(formattingElement)) return;
            int furthestIndex = -1;
            for (int i = formattingIndex + 1; i < stack.Count; i++) if (Special(stack[i])) { furthestIndex = i; break; }
            if (furthestIndex < 0) { PopThrough(formattingElement); formatting.Remove(formattingElement); return; }
            var furthest = stack[furthestIndex]; var common = stack[formattingIndex - 1];
            int bookmark = formatting.IndexOf(formattingElement); var last = furthest; int cursor = furthestIndex;
            for (int inner = 1; ; inner++)
            {
                var node = stack[--cursor]; if (node == formattingElement) break;
                int activeIndex = formatting.IndexOf(node);
                if (inner > 3 && activeIndex >= 0) { formatting.RemoveAt(activeIndex); if (activeIndex < bookmark) bookmark--; activeIndex = -1; }
                if (activeIndex < 0) { stack.RemoveAt(cursor); continue; }
                var replacement = node.Clone(false); formatting[activeIndex] = replacement; stack[cursor] = replacement;
                if (last == furthest) bookmark = activeIndex + 1;
                replacement.AppendChild(last); last = replacement;
            }
            bool saved = foster; foster = true; InsertNode(last, common); foster = saved;
            var newFormatting = formattingElement.Clone(false);
            while (furthest.FirstChild is { } child) newFormatting.AppendChild(child);
            furthest.AppendChild(newFormatting);
            int old = formatting.IndexOf(formattingElement); formatting.RemoveAt(old); if (old < bookmark) bookmark--;
            formatting.Insert(bookmark, newFormatting); stack.Remove(formattingElement);
            stack.Insert(stack.IndexOf(furthest) + 1, newFormatting);
        }
    }
    private void ClearTo(string kind)
    {
        while (stack.Count > 0 && !(Current.NamespaceUri == HtmlDocument.HtmlNamespace && (Current.LocalName is "html" or "template" || kind switch { "table" => Current.LocalName == "table", "body" => OneOf(Current.LocalName, "tbody", "tfoot", "thead"), _ => Current.LocalName == "tr" }))) Pop();
    }
    private void ResetMode()
    {
        for (int i = stack.Count - 1; i >= 0; i--)
        {
            var node = i == 0 && fragment is not null ? context! : stack[i]; bool last = i == 0;
            if (node.NamespaceUri != HtmlDocument.HtmlNamespace) { if (last) { mode = Mode.InBody; return; } continue; }
            switch (node.LocalName)
            {
                case "td": case "th": if (!last) { mode = Mode.InCell; return; } break;
                case "tr": mode = Mode.InRow; return;
                case "tbody": case "thead": case "tfoot": mode = Mode.InTableBody; return;
                case "caption": mode = Mode.InCaption; return;
                case "colgroup": mode = Mode.InColumnGroup; return;
                case "table": mode = Mode.InTable; return;
                case "template": mode = templates[^1]; return;
                case "head": if (!last) { mode = Mode.InHead; return; } break;
                case "body": mode = Mode.InBody; return;
                case "frameset": mode = Mode.InFrameset; return;
                case "html": mode = head is null ? Mode.BeforeHead : Mode.AfterHead; return;
            }
            if (last) { mode = Mode.InBody; return; }
        }
    }
    private bool HasTemplate => stack.Exists(static node => Html(node, "template"));
    private bool ParsingTemplate => HasTemplate || context is not null && Html(context, "template");
    private void CloseTemplate(bool cleanupPatch = true)
    {
        if (!HasTemplate) return;
        Implied(thorough: true); if (cleanupPatch) { for (int i = stack.Count - 1; i >= 0; i--) if (Html(stack[i], "template")) { ClosePatch(stack[i]); break; } } PopThrough("template"); ClearFormatting(); templates.RemoveAt(templates.Count - 1); ResetMode();
    }
}
