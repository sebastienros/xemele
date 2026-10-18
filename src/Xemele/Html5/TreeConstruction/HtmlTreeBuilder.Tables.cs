namespace Xemele.Html;

internal sealed partial class HtmlTreeBuilder
{
    private bool InTable(in Token t)
    {
        var n = t.Name;
        bool start = t.Kind == HtmlTokenKind.StartTag, end = t.Kind == HtmlTokenKind.EndTag;
        if (t.Kind == HtmlTokenKind.Character && Current.NamespaceUri == HtmlDocument.HtmlNamespace && Current.LocalName is "table" or "tbody" or "tfoot" or "thead" or "tr")
        {
            tableText.Clear();
            original = mode;
            mode = Mode.InTableText;
            return true;
        }
        if (t.Kind is HtmlTokenKind.Comment or HtmlTokenKind.ProcessingInstruction)
        {
            Misc(t);
            return false;
        }
        if (t.Kind == HtmlTokenKind.Doctype)
        {
            return false;
        }

        if (start)
        {
            switch (n)
            {
                case "caption":
                    ClearTo("table");
                    formatting.Add(null);
                    Insert(t);
                    mode = Mode.InCaption;
                    return false;
                case "colgroup":
                    ClearTo("table");
                    Insert(t);
                    mode = Mode.InColumnGroup;
                    return false;
                case "col":
                    InTable(Start("colgroup"));
                    return true;
                case "tbody":
                case "tfoot":
                case "thead":
                    ClearTo("table");
                    Insert(t);
                    mode = Mode.InTableBody;
                    return false;
                case "td":
                case "th":
                case "tr":
                    InTable(Start("tbody"));
                    return true;
                case "table":
                    if (!InScope("table", Scope.Table))
                    {
                        return false;
                    }

                    PopThrough("table");
                    ResetMode();
                    return true;
                case "style":
                case "script":
                case "template":
                    return InHead(t);
                case "input":
                    foreach (var a in t.AttributeList)
                    {
                        if (a.LocalName == "type" && HtmlAscii.Equals(a.Value, "hidden"))
                        {
                            Insert(t);
                            Pop();
                            return false;
                        }
                    }

                    break;
                case "form":
                    if (InScope("template") || form is not null)
                    {
                        return false;
                    }

                    form = Insert(t);
                    Pop();
                    return false;
            }
        }
        if (end && n == "table")
        {
            if (!InScope("table", Scope.Table))
            {
                return false;
            }

            PopThrough("table");
            ResetMode();
            return false;
        }
        if (end && n is "body" or "caption" or "col" or "colgroup" or "html" or "tbody" or "td" or "tfoot" or "th" or "thead" or "tr")
        {
            return false;
        }

        if (end && n == "template")
        {
            return InHead(t);
        }

        if (t.Kind == HtmlTokenKind.EndOfFile)
        {
            return InBody(t);
        }

        foster = true;
        var again = InBody(t);
        foster = false;
        return again;
    }
    private bool InTableBody(in Token t)
    {
        var n = t.Name;
        bool start = t.Kind == HtmlTokenKind.StartTag, end = t.Kind == HtmlTokenKind.EndTag;
        if (start && n == "tr")
        {
            ClearTo("body");
            Insert(t);
            mode = Mode.InRow;
            return false;
        }
        if (start && n is "td" or "th")
        {
            InTableBody(Start("tr"));
            return true;
        }
        if (end && n is "tbody" or "tfoot" or "thead")
        {
            if (!InScope(n, Scope.Table))
            {
                return false;
            }

            ClearTo("body");
            Pop();
            mode = Mode.InTable;
            return false;
        }
        if (start && n is "caption" or "col" or "colgroup" or "tbody" or "tfoot" or "thead" || end && n == "table")
        {
            if (!InScope("tbody", Scope.Table) && !InScope("thead", Scope.Table) && !InScope("tfoot", Scope.Table))
            {
                return false;
            }

            ClearTo("body");
            Pop();
            mode = Mode.InTable;
            return true;
        }
        if (end && n is "body" or "caption" or "col" or "colgroup" or "html" or "td" or "th" or "tr")
        {
            return false;
        }

        return InTable(t);
    }
    private bool InRow(in Token t)
    {
        var n = t.Name;
        bool start = t.Kind == HtmlTokenKind.StartTag, end = t.Kind == HtmlTokenKind.EndTag;
        if (start && n is "td" or "th")
        {
            ClearTo("row");
            Insert(t);
            mode = Mode.InCell;
            formatting.Add(null);
            return false;
        }
        if (end && n == "tr")
        {
            if (!InScope("tr", Scope.Table))
            {
                return false;
            }

            ClearTo("row");
            Pop();
            mode = Mode.InTableBody;
            return false;
        }
        if (start && n is "caption" or "col" or "colgroup" or "tbody" or "tfoot" or "thead" or "tr" || end && n == "table")
        {
            if (!InScope("tr", Scope.Table))
            {
                return false;
            }

            ClearTo("row");
            Pop();
            mode = Mode.InTableBody;
            return true;
        }
        if (end && n is "tbody" or "tfoot" or "thead")
        {
            if (!InScope(n, Scope.Table) || !InScope("tr", Scope.Table))
            {
                return false;
            }

            ClearTo("row");
            Pop();
            mode = Mode.InTableBody;
            return true;
        }
        if (end && n is "body" or "caption" or "col" or "colgroup" or "html" or "td" or "th")
        {
            return false;
        }

        return InTable(t);
    }
    private void CloseCell()
    {
        Implied();
        while (!Html(Current, "td") && !Html(Current, "th"))
        {
            Pop();
        }

        Pop();
        ClearFormatting();
        mode = Mode.InRow;
    }
}
