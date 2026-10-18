namespace Xemele.Html;

internal sealed partial class HtmlTreeBuilder
{
    private bool InBody(in Token t)
    {
        var n = t.Name;
        switch (t.Kind)
        {
            case HtmlTokenKind.Character:
                var data = t.Data.Contains('\0') ? t.Data.Replace("\0", "", StringComparison.Ordinal) : t.Data;
                Reconstruct();
                Characters(data);
                if (framesetOk && !White(data))
                {
                    framesetOk = false;
                }

                return false;
            case HtmlTokenKind.Comment:
            case HtmlTokenKind.ProcessingInstruction:
                Misc(t);
                return false;
            case HtmlTokenKind.Doctype:
                return false;
            case HtmlTokenKind.EndOfFile:
                if (templatesField is { Count: > 0 })
                {
                    mode = Mode.InTemplate;
                    return true;
                }
                return false;
            case HtmlTokenKind.StartTag:
                return BodyStart(t);
            case HtmlTokenKind.EndTag:
                switch (n)
                {
                    case "template":
                        return InHead(t);
                    case "body":
                        if (InScope("body"))
                        {
                            mode = Mode.AfterBody;
                        }

                        return false;
                    case "html":
                        if (!InScope("body"))
                        {
                            return false;
                        }

                        mode = Mode.AfterBody;
                        return true;
                    case "address" or "article" or "aside" or "blockquote" or "button" or "center" or "details" or "dialog" or "dir" or "div" or "dl" or "fieldset" or "figcaption" or "figure" or "footer" or "header" or "hgroup" or "listing" or "main" or "menu" or "nav" or "ol" or "pre" or "search" or "section" or "summary" or "ul":
                        if (!InScope(n))
                        {
                            return false;
                        }

                        Implied();
                        PopThrough(n);
                        return false;
                    case "form":
                        if (!ParsingTemplate)
                        {
                            var pointer = form;
                            form = null;
                            if (pointer is null || !InScope(pointer))
                            {
                                return false;
                            }

                            Implied();
                            RemoveOpen(pointer);
                        }
                        else
                        {
                            if (!InScope("form"))
                            {
                                return false;
                            }

                            Implied();
                            PopThrough("form");
                        }
                        return false;
                    case "p":
                        if (!InScope("p", Scope.Button))
                        {
                            BodyStart(Start("p"));
                        }

                        CloseP();
                        return false;
                    case "li":
                        if (!InScope(n, Scope.List))
                        {
                            return false;
                        }

                        Implied(n);
                        PopThrough(n);
                        return false;
                    case "dd" or "dt":
                        if (!InScope(n))
                        {
                            return false;
                        }

                        Implied(n);
                        PopThrough(n);
                        return false;
                    case "h1" or "h2" or "h3" or "h4" or "h5" or "h6":
                        var found = InScope("h1") || InScope("h2") || InScope("h3") || InScope("h4") || InScope("h5") || InScope("h6");
                        if (!found)
                        {
                            return false;
                        }

                        Implied();
                        while (stack.Count > 0)
                        {
                            var heading = Current.NamespaceUri == HtmlDocument.HtmlNamespace && Current.LocalName is "h1" or "h2" or "h3" or "h4" or "h5" or "h6";
                            Pop();
                            if (heading)
                            {
                                break;
                            }
                        }
                        return false;
                    case "a" or "b" or "big" or "code" or "em" or "font" or "i" or "nobr" or "s" or "small" or "strike" or "strong" or "tt" or "u":
                        Adoption(n);
                        return false;
                    case "applet" or "marquee" or "object":
                        if (!InScope(n))
                        {
                            return false;
                        }

                        Implied();
                        PopThrough(n);
                        ClearFormatting();
                        return false;
                    case "br":
                        Reconstruct();
                        Insert(Start("br"));
                        Pop();
                        framesetOk = false;
                        return false;
                    default:
                        GenericEnd(n);
                        return false;
                }
            default:
                return false;
        }
    }
    private bool BodyStart(in Token t)
    {
        var n = t.Name;
        switch (n)
        {
            case "html":
                if (!ParsingTemplate)
                {
                    MergeAttributes(stack[0], t);
                }

                return false;
            case "base" or "basefont" or "bgsound" or "link" or "meta" or "noframes" or "script" or "style" or "template" or "title":
                return InHead(t);
            case "body":
                if (stack.Count > 1 && Html(stack[1], "body") && !ParsingTemplate)
                {
                    framesetOk = false;
                    MergeAttributes(stack[1], t);
                }
                return false;
            case "frameset":
                if (stack.Count < 2 || !Html(stack[1], "body") || !framesetOk)
                {
                    return false;
                }

                FlushObservedText(stack[1]);
                stack[1].Remove();
                for (var i = 1; i < stack.Count; i++)
                {
                    Closed(stack[i]);
                }

                stack.RemoveRange(1, stack.Count - 1);
                Insert(t);
                mode = Mode.InFrameset;
                return false;
            case "address" or "article" or "aside" or "blockquote" or "center" or "details" or "dialog" or "dir" or "div" or "dl" or "fieldset" or "figcaption" or "figure" or "footer" or "header" or "hgroup" or "main" or "menu" or "nav" or "ol" or "p" or "search" or "section" or "summary" or "ul":
                if (InScope("p", Scope.Button))
                {
                    CloseP();
                }

                Insert(t);
                return false;
            case "h1" or "h2" or "h3" or "h4" or "h5" or "h6":
                if (InScope("p", Scope.Button))
                {
                    CloseP();
                }

                if (Current.NamespaceUri == HtmlDocument.HtmlNamespace && Current.LocalName is "h1" or "h2" or "h3" or "h4" or "h5" or "h6")
                {
                    Pop();
                }

                Insert(t);
                return false;
            case "pre" or "listing":
                if (InScope("p", Scope.Button))
                {
                    CloseP();
                }

                Insert(t);
                skipLf = true;
                framesetOk = false;
                return false;
            case "form":
                if (form is not null && !ParsingTemplate)
                {
                    return false;
                }

                if (InScope("p", Scope.Button))
                {
                    CloseP();
                }

                var node = Insert(t);
                if (!ParsingTemplate)
                {
                    form = node;
                }

                return false;
            case "li" or "dd" or "dt":
                framesetOk = false;
                for (var i = stack.Count - 1; i >= 0; i--)
                {
                    var item = stack[i];
                    if (item.NamespaceUri == HtmlDocument.HtmlNamespace && (n == "li" ? item.LocalName == "li" : item.LocalName is "dd" or "dt"))
                    {
                        Implied(item.LocalName);
                        PopThrough(item);
                        break;
                    }
                    if (Special(item) && !(item.NamespaceUri == HtmlDocument.HtmlNamespace && item.LocalName is "address" or "div" or "p"))
                    {
                        break;
                    }
                }
                if (InScope("p", Scope.Button))
                {
                    CloseP();
                }

                Insert(t);
                return false;
            case "plaintext":
                if (InScope("p", Scope.Button))
                {
                    CloseP();
                }

                Insert(t);
                Tokenizer.SetState(HtmlTokenizerState.PlainText);
                return false;
            case "button":
                if (InScope("button"))
                {
                    Implied();
                    PopThrough("button");
                }
                Reconstruct();
                Insert(t);
                framesetOk = false;
                return false;
            case "a":
                if (Formatting("a") is { } old)
                {
                    Adoption("a");
                    formatting.Remove(old);
                    RemoveOpen(old);
                }
                Reconstruct();
                AddFormatting(Insert(t));
                return false;
            case "b" or "big" or "code" or "em" or "font" or "i" or "s" or "small" or "strike" or "strong" or "tt" or "u":
                Reconstruct();
                AddFormatting(Insert(t));
                return false;
            case "nobr":
                Reconstruct();
                if (InScope("nobr"))
                {
                    Adoption("nobr");
                    Reconstruct();
                }
                AddFormatting(Insert(t));
                return false;
            case "applet" or "marquee" or "object":
                Reconstruct();
                Insert(t);
                formatting.Add(null);
                framesetOk = false;
                return false;
            case "table":
                if (document.QuirksMode != HtmlQuirksMode.Quirks && InScope("p", Scope.Button))
                {
                    CloseP();
                }

                Insert(t);
                framesetOk = false;
                mode = Mode.InTable;
                return false;
            case "area" or "br" or "embed" or "img" or "keygen" or "wbr":
                Reconstruct();
                Insert(t);
                Pop();
                framesetOk = false;
                return false;
            case "input":
                if (context is not null && Html(context, "select"))
                {
                    return false;
                }

                if (InScope("select"))
                {
                    PopThrough("select");
                }

                Reconstruct();
                Insert(t);
                Pop();
                var hidden = false;
                foreach (var a in t.AttributeList)
                {
                    if (a.LocalName == "type" && HtmlAscii.Equals(a.Value, "hidden"))
                    {
                        hidden = true;
                    }
                }

                if (!hidden)
                {
                    framesetOk = false;
                }

                return false;
            case "param" or "source" or "track":
                Insert(t);
                Pop();
                return false;
            case "hr":
                if (InScope("p", Scope.Button))
                {
                    CloseP();
                }

                if (InScope("select"))
                {
                    Implied();
                }

                Insert(t);
                Pop();
                framesetOk = false;
                return false;
            case "image":
                return BodyStart(t with { Name = "img" });
            case "textarea":
                Insert(t);
                skipLf = true;
                Tokenizer.SetState(HtmlTokenizerState.RcData, n);
                original = mode;
                framesetOk = false;
                mode = Mode.Text;
                return false;
            case "xmp":
                if (InScope("p", Scope.Button))
                {
                    CloseP();
                }

                Reconstruct();
                framesetOk = false;
                Text(t, HtmlTokenizerState.RawText);
                return false;
            case "iframe":
                framesetOk = false;
                Text(t, HtmlTokenizerState.RawText);
                return false;
            case "noembed":
            case "noscript" when options.ScriptingEnabled:
                Text(t, HtmlTokenizerState.RawText);
                return false;
            case "select":
                if (context is not null && Html(context, "select"))
                {
                    return false;
                }

                if (InScope("select"))
                {
                    PopThrough("select");
                    return false;
                }
                Reconstruct();
                Insert(t);
                framesetOk = false;
                return false;
            case "optgroup" or "option":
                if (InScope("select"))
                {
                    Implied(n == "option" ? "optgroup" : null);
                }
                else if (Html(Current, "option"))
                {
                    Pop();
                }

                Reconstruct();
                Insert(t);
                return false;
            case "rb" or "rtc":
                if (InScope("ruby"))
                {
                    Implied();
                }

                Insert(t);
                return false;
            case "rp" or "rt":
                if (InScope("ruby"))
                {
                    Implied("rtc");
                }

                Insert(t);
                return false;
            case "math" or "svg":
                Reconstruct();
                Insert(t, n == "math" ? HtmlForeignNames.MathMlNamespace : HtmlForeignNames.SvgNamespace);
                if (t.SelfClosing)
                {
                    Pop();
                }

                return false;
            case "caption" or "col" or "colgroup" or "frame" or "head" or "tbody" or "td" or "tfoot" or "th" or "thead" or "tr":
                return false;
            default:
                Reconstruct();
                Insert(t);
                return false;
        }
    }
    private static void MergeAttributes(HtmlNode node, Token token)
    {
        foreach (var attribute in token.AttributeList)
        {
            if (node.GetAttribute(attribute.LocalName, attribute.NamespaceUri) is null)
            {
                node.SetAttribute(attribute);
            }
        }
    }
}
