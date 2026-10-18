namespace Xemele.Html;

internal sealed partial class HtmlTreeBuilder
{
    private enum Scope
    {
        Normal, List, Button, Table
    }
    private bool InScope(string name, Scope scope = Scope.Normal) => OpenCount(name) != 0 && FindScope(null, name, scope);
    private bool InScope(HtmlNode node, Scope scope = Scope.Normal) => node.Open && FindScope(node, null, scope);
    private bool FindScope(HtmlNode? sought, string? name, Scope scope)
    {
        var boundary = scope switch
        {
            Scope.Table => HtmlElementFlags.TableBoundary,
            Scope.List => HtmlElementFlags.ScopeBoundary | HtmlElementFlags.ListBoundary,
            Scope.Button => HtmlElementFlags.ScopeBoundary | HtmlElementFlags.ButtonBoundary,
            _ => HtmlElementFlags.ScopeBoundary,
        };
        for (var i = stack.Count - 1; i >= 0; i--)
        {
            var node = stack[i];
            if (sought is null ? Html(node, name!) : node == sought)
            {
                return true;
            }

            if ((node.Flags & boundary) != 0)
            {
                return false;
            }
        }
        return false;
    }
    private static bool Special(HtmlNode n) => (n.Flags & HtmlElementFlags.Special) != 0;
    private void Implied(string? except = null, bool thorough = false)
    {
        var mask = thorough ? HtmlElementFlags.ImpliedEnd | HtmlElementFlags.ImpliedThorough : HtmlElementFlags.ImpliedEnd;
        while (stack.Count > 0 && (Current.Flags & mask) != 0 && Current.LocalName != except)
        {
            Pop();
        }
    }
    private void CloseP()
    {
        Implied("p");
        PopThrough("p");
    }
    private void ClearFormatting()
    {
        if (formattingField is null)
        {
            return;
        }
        while (formattingField.Count > 0)
        {
            var node = formattingField[^1];
            formattingField.RemoveAt(formattingField.Count - 1);
            if (node is null)
            {
                break;
            }
        }
    }
    private void Reconstruct()
    {
        if (formattingField is null || formattingField.Count == 0 || formattingField[^1] is null || formattingField[^1]!.Open)
        {
            return;
        }

        var i = formattingField.Count - 1;
        while (i > 0 && formattingField[i - 1] is { } before && !before.Open)
        {
            i--;
        }

        for (; i < formattingField.Count; i++)
        {
            var clone = formattingField[i]!.Clone(false);
            InsertNode(clone);
            Push(clone);
            formattingField[i] = clone;
        }
    }
    private static bool SameFormatting(HtmlNode a, HtmlNode b)
    {
        if (a.LocalName != b.LocalName || a.NamespaceUri != b.NamespaceUri || a.RawAttributes.Count != b.RawAttributes.Count)
        {
            return false;
        }

        foreach (var attribute in a.RawAttributes)
        {
            if (b.GetAttribute(attribute.LocalName, attribute.NamespaceUri) != attribute.Value)
            {
                return false;
            }
        }

        return true;
    }
    private void AddFormatting(HtmlNode node)
    {
        var list = formatting;
        int matches = 0, first = -1;
        for (var i = list.Count - 1; i >= 0 && list[i] is { } existing; i--)
        {
            if (SameFormatting(existing, node))
            {
                matches++;
                first = i;
            }
        }

        if (matches >= 3)
        {
            list.RemoveAt(first);
        }

        list.Add(node);
    }
    private HtmlNode? Formatting(string name)
    {
        if (formattingField is null)
        {
            return null;
        }
        for (var i = formattingField.Count - 1; i >= 0 && formattingField[i] is { } node; i--)
        {
            if (Html(node, name))
            {
                return node;
            }
        }

        return null;
    }
    private void GenericEnd(string name)
    {
        for (var i = stack.Count - 1; i >= 0; i--)
        {
            var node = stack[i];
            if (Html(node, name))
            {
                Implied(name);
                PopThrough(node);
                return;
            }
            if (Special(node))
            {
                return;
            }
        }
    }
    private void Adoption(string name)
    {
        if (Html(Current, name) && (formattingField is null || !formattingField.Contains(Current)))
        {
            Pop();
            return;
        }
        for (var outer = 0; outer < 8; outer++)
        {
            var formattingElement = Formatting(name);
            if (formattingElement is null)
            {
                GenericEnd(name);
                return;
            }
            var formattingIndex = formattingElement.Open ? stack.LastIndexOf(formattingElement) : -1;
            if (formattingIndex < 0)
            {
                formatting.Remove(formattingElement);
                return;
            }
            if (!InScope(formattingElement))
            {
                return;
            }

            var furthestIndex = -1;
            for (var i = formattingIndex + 1; i < stack.Count; i++)
            {
                if (Special(stack[i]))
                {
                    furthestIndex = i;
                    break;
                }
            }

            if (furthestIndex < 0)
            {
                PopThrough(formattingElement);
                formatting.Remove(formattingElement);
                return;
            }
            var furthest = stack[furthestIndex];
            var common = stack[formattingIndex - 1];
            var bookmark = formatting.IndexOf(formattingElement);
            var last = furthest;
            var cursor = furthestIndex;
            for (var inner = 1; ; inner++)
            {
                var node = stack[--cursor];
                if (node == formattingElement)
                {
                    break;
                }

                var activeIndex = formatting.IndexOf(node);
                if (inner > 3 && activeIndex >= 0)
                {
                    formatting.RemoveAt(activeIndex);
                    if (activeIndex < bookmark)
                    {
                        bookmark--;
                    }

                    activeIndex = -1;
                }
                if (activeIndex < 0)
                {
                    stack.RemoveAt(cursor);
                    Closed(node);
                    continue;
                }
                var replacement = node.Clone(false);
                formatting[activeIndex] = replacement;
                stack[cursor] = replacement;
                Closed(node);
                Opened(replacement);
                if (last == furthest)
                {
                    bookmark = activeIndex + 1;
                }

                FlushObservedText(last);
                replacement.AppendChild(last);
                last = replacement;
            }
            var saved = foster;
            foster = true;
            InsertNode(last, common);
            foster = saved;
            var newFormatting = formattingElement.Clone(false);
            FlushObservedText(furthest);
            while (furthest.FirstChild is { } child)
            {
                newFormatting.AppendChild(child);
            }

            furthest.AppendChild(newFormatting);
            var old = formatting.IndexOf(formattingElement);
            formatting.RemoveAt(old);
            if (old < bookmark)
            {
                bookmark--;
            }

            formatting.Insert(bookmark, newFormatting);
            RemoveOpen(formattingElement);
            stack.Insert(stack.LastIndexOf(furthest) + 1, newFormatting);
            Opened(newFormatting);
        }
    }
    private void ClearTo(string kind)
    {
        while (stack.Count > 0 && !(Current.NamespaceUri == HtmlDocument.HtmlNamespace && (Current.LocalName is "html" or "template" || kind switch
        {
            "table" => Current.LocalName == "table",
            "body" => OneOf(Current.LocalName, "tbody", "tfoot", "thead"),
            _ => Current.LocalName == "tr"
        })))
        {
            Pop();
        }
    }
    private void ResetMode()
    {
        for (var i = stack.Count - 1; i >= 0; i--)
        {
            var node = i == 0 && fragment is not null ? context! : stack[i];
            var last = i == 0;
            if (node.NamespaceUri != HtmlDocument.HtmlNamespace)
            {
                if (last)
                {
                    mode = Mode.InBody;
                    return;
                }
                continue;
            }
            switch (node.LocalName)
            {
                case "td":
                case "th":
                    if (!last)
                    {
                        mode = Mode.InCell;
                        return;
                    }
                    break;
                case "tr":
                    mode = Mode.InRow;
                    return;
                case "tbody":
                case "thead":
                case "tfoot":
                    mode = Mode.InTableBody;
                    return;
                case "caption":
                    mode = Mode.InCaption;
                    return;
                case "colgroup":
                    mode = Mode.InColumnGroup;
                    return;
                case "table":
                    mode = Mode.InTable;
                    return;
                case "template":
                    mode = templates[^1];
                    return;
                case "head":
                    if (!last)
                    {
                        mode = Mode.InHead;
                        return;
                    }
                    break;
                case "body":
                    mode = Mode.InBody;
                    return;
                case "frameset":
                    mode = Mode.InFrameset;
                    return;
                case "html":
                    mode = head is null ? Mode.BeforeHead : Mode.AfterHead;
                    return;
            }
            if (last)
            {
                mode = Mode.InBody;
                return;
            }
        }
    }
    private bool HasTemplate => openTemplates != 0;
    private bool ParsingTemplate => HasTemplate || context is not null && Html(context, "template");
    private void CloseTemplate(bool cleanupPatch = true)
    {
        if (!HasTemplate)
        {
            return;
        }

        Implied(thorough: true);
        if (cleanupPatch)
        {
            for (var i = stack.Count - 1; i >= 0; i--)
            {
                if (Html(stack[i], "template"))
                {
                    ClosePatch(stack[i]);
                    break;
                }
            }
        }
        PopThrough("template");
        ClearFormatting();
        templates.RemoveAt(templates.Count - 1);
        ResetMode();
    }
}
