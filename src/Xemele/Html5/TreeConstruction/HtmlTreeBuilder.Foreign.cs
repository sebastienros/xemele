namespace Xemele.Html;

internal sealed partial class HtmlTreeBuilder
{
    private static bool MathText(HtmlNode n) => n.NamespaceUri == HtmlForeignNames.MathMlNamespace && n.LocalName is "mi" or "mo" or "mn" or "ms" or "mtext";
    private static bool Integration(HtmlNode n) => n.NamespaceUri == HtmlForeignNames.SvgNamespace && n.LocalName is "foreignObject" or "desc" or "title" ||
        n.NamespaceUri == HtmlForeignNames.MathMlNamespace && n.LocalName == "annotation-xml" && n.GetAttribute("encoding") is { } encoding && (HtmlAscii.Equals(encoding, "text/html") || HtmlAscii.Equals(encoding, "application/xhtml+xml"));
    private bool Foreign(in Token token)
    {
        var n = Adjusted;
        if (n.NamespaceUri == HtmlDocument.HtmlNamespace || token.Kind == HtmlTokenKind.EndOfFile)
        {
            return false;
        }

        if (MathText(n) && (token.Kind == HtmlTokenKind.Character || token.Kind == HtmlTokenKind.StartTag && token.Name is not ("mglyph" or "malignmark")))
        {
            return false;
        }

        if (n.NamespaceUri == HtmlForeignNames.MathMlNamespace && n.LocalName == "annotation-xml" && token.Kind == HtmlTokenKind.StartTag && token.Name == "svg")
        {
            return false;
        }

        if (Integration(n) && token.Kind is HtmlTokenKind.StartTag or HtmlTokenKind.Character)
        {
            return false;
        }

        return true;
    }
    private bool ProcessForeign(in Token token)
    {
        switch (token.Kind)
        {
            case HtmlTokenKind.Character:
                var hasNull = token.Data.Contains('\0');
                Characters(hasNull ? token.Data.Replace('\0', '\uFFFD') : token.Data);
                if (framesetOk && !White(hasNull ? token.Data.Replace("\0", "", StringComparison.Ordinal) : token.Data))
                {
                    framesetOk = false;
                }

                return false;
            case HtmlTokenKind.Comment:
            case HtmlTokenKind.ProcessingInstruction:
                Misc(token);
                return false;
            case HtmlTokenKind.Doctype:
                return false;
            case HtmlTokenKind.StartTag:
                if (token.Name is "b" or "big" or "blockquote" or "body" or "br" or "center" or "code" or "dd" or "div" or "dl" or "dt" or "em" or "embed" or "h1" or "h2" or "h3" or "h4" or "h5" or "h6" or "head" or "hr" or "i" or "img" or "li" or "listing" or "menu" or "meta" or "nobr" or "ol" or "p" or "pre" or "ruby" or "s" or "small" or "span" or "strong" or "strike" or "sub" or "sup" or "table" or "tt" or "u" or "ul" or "var" || token.Name == "font" && HasFontAttribute(token))
                {
                    while (stack.Count > 1 && Current.NamespaceUri != HtmlDocument.HtmlNamespace && !MathText(Current) && !Integration(Current))
                    {
                        Pop();
                    }

                    return ProcessHtml(token);
                }
                Insert(token, Adjusted.NamespaceUri);
                if (token.SelfClosing)
                {
                    Pop();
                }

                return false;
            case HtmlTokenKind.EndTag:
                if (token.Name is "br" or "p")
                {
                    while (stack.Count > 1 && Current.NamespaceUri != HtmlDocument.HtmlNamespace && !MathText(Current) && !Integration(Current))
                    {
                        Pop();
                    }

                    return ProcessHtml(token);
                }
                for (var i = stack.Count - 1; i >= 0; i--)
                {
                    var n = stack[i];
                    if (n.NamespaceUri == HtmlDocument.HtmlNamespace)
                    {
                        return ProcessHtml(token);
                    }

                    if (HtmlAscii.Equals(n.LocalName, token.Name))
                    {
                        while (stack.Count > i)
                        {
                            Pop();
                        }

                        return false;
                    }
                    if (n.NamespaceUri == HtmlDocument.HtmlNamespace)
                    {
                        return ProcessHtml(token);
                    }
                }
                return false;
            default:
                return false;
        }
    }
    private static bool HasFontAttribute(in Token token)
    {
        foreach (var a in token.AttributeList)
        {
            if (a.LocalName is "color" or "face" or "size")
            {
                return true;
            }
        }

        return false;
    }
}
