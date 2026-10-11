namespace Xemele.Html;
internal sealed partial class HtmlTreeBuilder
{
    private bool ProcessHtml(HtmlToken t)
    {
        bool start = t.Kind == HtmlTokenKind.StartTag, end = t.Kind == HtmlTokenKind.EndTag;
        bool white = t.Kind == HtmlTokenKind.Character && White(t.Data);
        bool misc = t.Kind is HtmlTokenKind.Comment or HtmlTokenKind.ProcessingInstruction;
        string n = t.Name;
        switch (mode)
        {
            case Mode.Initial:
                if (white) return false;
                if (misc) { Misc(t, document.DocumentNode); return false; }
                if (t.Kind == HtmlTokenKind.Doctype) { document.DocumentNode.AppendChild(document.CreateDocumentType(t.Doctype!)); document.SetMetadata(Quirks(t.Doctype!), options.ScriptingEnabled); mode = Mode.BeforeHtml; return false; }
                document.SetMetadata(HtmlQuirksMode.Quirks, options.ScriptingEnabled); mode = Mode.BeforeHtml; return true;
            case Mode.BeforeHtml:
                if (t.Kind == HtmlTokenKind.Doctype || white) return false;
                if (misc) { Misc(t, document.DocumentNode); return false; }
                if (start && n == "html") { Insert(t); mode = Mode.BeforeHead; return false; }
                if (end && !OneOf(n, "head", "body", "html", "br")) return false;
                Insert(Start("html")); mode = Mode.BeforeHead; return true;
            case Mode.BeforeHead:
                if (white || t.Kind == HtmlTokenKind.Doctype) return false;
                if (misc) { Misc(t); return false; }
                if (start && n == "html") return InBody(t);
                if (start && n == "head") { head = Insert(t); mode = Mode.InHead; return false; }
                if (end && !OneOf(n, "head", "body", "html", "br")) return false;
                head = Insert(Start("head")); mode = Mode.InHead; return true;
            case Mode.InHead: return InHead(t);
            case Mode.InHeadNoscript:
                if (t.Kind == HtmlTokenKind.Doctype) return false;
                if (start && n == "html") return InBody(t);
                if (end && n == "noscript") { Pop(); mode = Mode.InHead; return false; }
                if (white || misc || start && OneOf(n, "basefont", "bgsound", "link", "meta", "noframes", "style")) return InHead(t);
                if (start && n is "head" or "noscript" || end && n != "br") return false;
                Pop(); mode = Mode.InHead; return true;
            case Mode.AfterHead:
                if (white) { Characters(t.Data); return false; }
                if (misc) { Misc(t); return false; }
                if (t.Kind == HtmlTokenKind.Doctype) return false;
                if (start && n == "html") return InBody(t);
                if (start && n == "body") { Insert(t); framesetOk = false; mode = Mode.InBody; return false; }
                if (start && n == "frameset") { Insert(t); mode = Mode.InFrameset; return false; }
                if (start && OneOf(n, "base", "basefont", "bgsound", "link", "meta", "noframes", "script", "style", "template", "title")) { Push(head!); bool again = InHead(t); stack.Remove(head!); return again; }
                if (end && n == "template") return InHead(t);
                if (start && n == "head" || end && !OneOf(n, "body", "html", "br")) return false;
                Insert(Start("body")); mode = Mode.InBody; return true;
            case Mode.InBody: return InBody(t);
            case Mode.Text:
                if (t.Kind == HtmlTokenKind.Character) { Characters(t.Data); return false; }
                if (t.Kind == HtmlTokenKind.EndOfFile) { Pop(); mode = original; return true; }
                if (end) { Pop(); mode = original; } return false;
            case Mode.InTable: return InTable(t);
            case Mode.InTableText:
                if (t.Kind == HtmlTokenKind.Character) { tableText.Append(t.Data.Replace("\0", "", StringComparison.Ordinal)); return false; }
                string data = tableText.ToString(); tableText.Clear();
                if (White(data)) Characters(data); else { foster = true; InBody(new(HtmlTokenKind.Character, "", data, [], false, null, t.Location)); foster = false; }
                mode = original; return true;
            case Mode.InCaption:
                if (end && n == "caption" || start && OneOf(n, "caption", "col", "colgroup", "tbody", "td", "tfoot", "th", "thead", "tr") || end && n == "table")
                {
                    if (!InScope("caption", Scope.Table)) return false; Implied(); PopThrough("caption"); ClearFormatting(); mode = Mode.InTable; return !(end && n == "caption");
                }
                if (end && OneOf(n, "body", "col", "colgroup", "html", "tbody", "td", "tfoot", "th", "thead", "tr")) return false;
                return InBody(t);
            case Mode.InColumnGroup:
                if (white) { Characters(t.Data); return false; }
                if (misc) { Misc(t); return false; }
                if (t.Kind == HtmlTokenKind.Doctype) return false;
                if (start && n == "html") return InBody(t);
                if (start && n == "col") { Insert(t); Pop(); return false; }
                if (end && n == "colgroup") { if (!Html(Current, "colgroup")) return false; Pop(); mode = Mode.InTable; return false; }
                if (end && n == "col") return false;
                if (start && n == "template" || end && n == "template") return InHead(t);
                if (t.Kind == HtmlTokenKind.EndOfFile) return InBody(t);
                if (!Html(Current, "colgroup")) return false; Pop(); mode = Mode.InTable; return true;
            case Mode.InTableBody: return InTableBody(t);
            case Mode.InRow: return InRow(t);
            case Mode.InCell:
                if (end && n is "td" or "th") { if (!InScope(n, Scope.Table)) return false; Implied(); PopThrough(n); ClearFormatting(); mode = Mode.InRow; return false; }
                if (start && OneOf(n, "caption", "col", "colgroup", "tbody", "td", "tfoot", "th", "thead", "tr") || end && OneOf(n, "table", "tbody", "tfoot", "thead", "tr"))
                {
                    if (end && !InScope(n, Scope.Table)) return false; if (!InScope("td", Scope.Table) && !InScope("th", Scope.Table)) return false; CloseCell(); return true;
                }
                if (end && OneOf(n, "body", "caption", "col", "colgroup", "html")) return false;
                return InBody(t);
            case Mode.InTemplate:
                if (t.Kind is HtmlTokenKind.Character or HtmlTokenKind.Comment or HtmlTokenKind.ProcessingInstruction or HtmlTokenKind.Doctype) return InBody(t);
                if (start && OneOf(n, "base", "basefont", "bgsound", "link", "meta", "noframes", "script", "style", "template", "title") || end && n == "template") return InHead(t);
                if (end) return false;
                if (t.Kind == HtmlTokenKind.EndOfFile) { if (!HasTemplate) return false; CloseTemplate(); return true; }
                mode = n switch { "caption" or "colgroup" or "tbody" or "tfoot" or "thead" => Mode.InTable, "col" => Mode.InColumnGroup, "tr" => Mode.InTableBody, "td" or "th" => Mode.InRow, _ => Mode.InBody }; templates[^1] = mode; return true;
            case Mode.AfterBody:
                if (white) return InBody(t);
                if (misc) { Misc(t, stack[0]); return false; }
                if (t.Kind == HtmlTokenKind.Doctype) return false;
                if (start && n == "html") return InBody(t);
                if (end && n == "html") { if (fragment is null) mode = Mode.AfterAfterBody; return false; }
                if (t.Kind == HtmlTokenKind.EndOfFile) return false;
                mode = Mode.InBody; return true;
            case Mode.InFrameset:
                if (t.Kind == HtmlTokenKind.Character && !white) { foreach (char c in t.Data) if (c is '\t' or '\n' or '\f' or '\r' or ' ') Characters(c.ToString()); return false; }
                if (white) { Characters(t.Data); return false; }
                if (misc) { Misc(t); return false; }
                if (t.Kind == HtmlTokenKind.Doctype) return false;
                if (start && n == "html") return InBody(t);
                if (start && n == "frameset") { Insert(t); return false; }
                if (end && n == "frameset") { if (Html(Current, "html")) return false; Pop(); if (fragment is null && !Html(Current, "frameset")) mode = Mode.AfterFrameset; return false; }
                if (start && n == "frame") { Insert(t); Pop(); return false; }
                if (start && n == "noframes") return InHead(t);
                return false;
            case Mode.AfterFrameset:
                if (t.Kind == HtmlTokenKind.Character && !white) { foreach (char c in t.Data) if (c is '\t' or '\n' or '\f' or '\r' or ' ') Characters(c.ToString()); return false; }
                if (white) { Characters(t.Data); return false; }
                if (misc) { Misc(t); return false; }
                if (start && n == "html") return InBody(t);
                if (end && n == "html") { mode = Mode.AfterAfterFrameset; return false; }
                if (start && n == "noframes") return InHead(t);
                return false;
            case Mode.AfterAfterBody: case Mode.AfterAfterFrameset:
                if (misc) { Misc(t, document.DocumentNode); return false; }
                if (t.Kind == HtmlTokenKind.Doctype || white || start && n == "html") return InBody(t);
                if (t.Kind == HtmlTokenKind.EndOfFile) return false;
                if (mode == Mode.AfterAfterFrameset) { if (start && n == "noframes") return InHead(t); return false; }
                mode = Mode.InBody; return true;
            default: throw new InvalidOperationException("Unknown insertion mode.");
        }
    }
    private bool InHead(HtmlToken t)
    {
        bool start = t.Kind == HtmlTokenKind.StartTag, end = t.Kind == HtmlTokenKind.EndTag; string n = t.Name;
        if (t.Kind == HtmlTokenKind.Character && White(t.Data)) { Characters(t.Data); return false; }
        if (t.Kind is HtmlTokenKind.Comment or HtmlTokenKind.ProcessingInstruction) { Misc(t); return false; }
        if (t.Kind == HtmlTokenKind.Doctype) return false;
        if (start)
        {
            switch (n)
            {
                case "html": return InBody(t);
                case "base": case "basefont": case "bgsound": case "link": case "meta": Insert(t); Pop(); return false;
                case "title": Text(t, HtmlTokenizerState.RcData); return false;
                case "noframes": case "style": Text(t, HtmlTokenizerState.RawText); return false;
                case "noscript": if (options.ScriptingEnabled) Text(t, HtmlTokenizerState.RawText); else { Insert(t); mode = Mode.InHeadNoscript; } return false;
                case "script": Text(t, HtmlTokenizerState.ScriptData); return false;
                case "template": Insert(t); formatting.Add(null); framesetOk = false; mode = Mode.InTemplate; templates.Add(mode); return false;
                case "head": return false;
            }
        }
        if (end && n == "head") { Pop(); mode = Mode.AfterHead; return false; }
        if (end && n == "template") { CloseTemplate(); return false; }
        if (end && !OneOf(n, "body", "html", "br")) return false;
        Pop(); mode = Mode.AfterHead; return true;
    }
}
