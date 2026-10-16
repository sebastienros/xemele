namespace Xemele.Html;

// Per-element classification used by tree construction. It is derived from the
// (namespace, local name) pair, which never changes after node creation.
[Flags]
internal enum HtmlElementFlags : byte
{
    None = 0,
    ScopeBoundary = 1,
    ListBoundary = 2,
    ButtonBoundary = 4,
    TableBoundary = 8,
    Special = 16,
    ImpliedEnd = 32,
    ImpliedThorough = 64,
}

internal static class HtmlElementFlagTable
{
    internal static HtmlElementFlags Of(string namespaceUri, string localName)
    {
        if (namespaceUri == HtmlDocument.HtmlNamespace)
        {
            return Html(localName);
        }

        if (namespaceUri == HtmlForeignNames.MathMlNamespace)
        {
            return localName is "mi" or "mo" or "mn" or "ms" or "mtext" or "annotation-xml" ? HtmlElementFlags.Special | HtmlElementFlags.ScopeBoundary : HtmlElementFlags.None;
        }

        if (namespaceUri == HtmlForeignNames.SvgNamespace)
        {
            return localName is "foreignObject" or "desc" or "title" ? HtmlElementFlags.Special | HtmlElementFlags.ScopeBoundary : HtmlElementFlags.None;
        }

        return HtmlElementFlags.None;
    }

    private static HtmlElementFlags Html(string name)
    {
        const HtmlElementFlags S = HtmlElementFlags.Special, B = HtmlElementFlags.ScopeBoundary, T = HtmlElementFlags.TableBoundary, I = HtmlElementFlags.ImpliedEnd, H = HtmlElementFlags.ImpliedThorough;
        return name switch
        {
            "applet" or "marquee" or "object" or "select" => S | B,
            "caption" or "td" or "th" => S | B | H,
            "html" or "table" or "template" => S | B | T,
            "ol" or "ul" => S | HtmlElementFlags.ListBoundary,
            "button" => S | HtmlElementFlags.ButtonBoundary,
            "dd" or "dt" or "li" or "p" => S | I,
            "optgroup" or "option" or "rb" or "rp" or "rt" or "rtc" => I,
            "colgroup" or "tbody" or "tfoot" or "thead" or "tr" => S | H,
            "address" or "area" or "article" or "aside" or "base" or "basefont" or "bgsound" or "blockquote" or "body" or "br" or "center" or "col" or "details" or "dir" or "div" or "dl" or "embed" or "fieldset" or "figcaption" or "figure" or "footer" or "form" or "frame" or "frameset" or "h1" or "h2" or "h3" or "h4" or "h5" or "h6" or "head" or "header" or "hgroup" or "hr" or "iframe" or "img" or "input" or "keygen" or "link" or "listing" or "main" or "menu" or "meta" or "nav" or "noembed" or "noframes" or "noscript" or "param" or "plaintext" or "pre" or "script" or "search" or "section" or "source" or "style" or "summary" or "textarea" or "title" or "track" or "wbr" or "xmp" => S,
            _ => HtmlElementFlags.None,
        };
    }
}
