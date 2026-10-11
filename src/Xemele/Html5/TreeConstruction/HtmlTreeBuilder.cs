namespace Xemele.Html;

// All stack and formatting entries are operation-owned DOM nodes. Tokens already
// own their spelling and values; those are persisted only at DOM insertion.
internal sealed partial class HtmlTreeBuilder
{
    private enum Mode { Initial, BeforeHtml, BeforeHead, InHead, InHeadNoscript, AfterHead, InBody, Text, InTable, InTableText, InCaption, InColumnGroup, InTableBody, InRow, InCell, InTemplate, AfterBody, InFrameset, AfterFrameset, AfterAfterBody, AfterAfterFrameset }
    private readonly HtmlDocument document;
    private readonly Html5ParseOptions options;
    private readonly List<HtmlNode> stack = new();
    private readonly List<HtmlNode?> formatting = new();
    private readonly List<Mode> templates = new();
    private readonly HtmlFragmentSnapshot? fragment;
    private readonly HtmlNode? context;
    private HtmlNode? head, form;
    private Mode mode, original;
    private bool framesetOk = true, foster, skipLf;
    private readonly System.Text.StringBuilder tableText = new();
    private readonly System.Text.StringBuilder pendingText = new();
    // Resumed text nodes can alternate with unrelated table-cell text. Their
    // operation-owned append buffers survive location switches; the DOM receives
    // an owned string only when that subtree is observed/moved or at completion.
    private Dictionary<HtmlNode, System.Text.StringBuilder>? resumedText;
    private HtmlNode? textParent, textBefore, textNode;
    internal HtmlTokenizer Tokenizer { get; }
    internal HtmlDocument Document => document;
    internal HtmlTreeBuilder(Html5ParseOptions options, HtmlFragmentSnapshot? fragment = null, CancellationToken cancellation = default)
    {
        this.options = options; this.fragment = fragment;
        document = new(options.Limits);
        document.SetMetadata(fragment?.QuirksMode ?? HtmlQuirksMode.NoQuirks, options.ScriptingEnabled);
        Tokenizer = new(Process, new() { Limits = options.Limits }, cancellation);
        if (fragment is null) { mode = Mode.Initial; return; }
        var snapshot = fragment.Ancestry[0];
        context = document.CreateElement(snapshot.LocalName, snapshot.NamespaceUri, snapshot.Prefix);
        foreach (var attribute in snapshot.Attributes) context.SetAttribute(attribute);
        var html = document.CreateElement("html"); document.DocumentNode.AppendChild(html); Push(html);
        if (context.NamespaceUri == HtmlDocument.HtmlNamespace)
        {
            switch (context.LocalName)
            {
                case "title": case "textarea": Tokenizer.SetState(HtmlTokenizerState.RcData); break;
                case "style": case "xmp": case "iframe": case "noembed": case "noframes": Tokenizer.SetState(HtmlTokenizerState.RawText); break;
                case "script": Tokenizer.SetState(HtmlTokenizerState.ScriptData); break;
                case "noscript" when options.ScriptingEnabled: Tokenizer.SetState(HtmlTokenizerState.RawText); break;
                case "plaintext": Tokenizer.SetState(HtmlTokenizerState.PlainText); break;
                case "template": templates.Add(Mode.InTemplate); break;
            }
        }
        foreach (var ancestor in fragment.Ancestry)
            if (ancestor.NamespaceUri == HtmlDocument.HtmlNamespace && ancestor.LocalName == "form") { form = document.CreateElement("form"); break; }
        ResetMode(); UpdateCdata();
    }
    internal HtmlNode FinishFragment()
    {
        var result = document.CreateFragment(); var html = document.RootElement;
        while (html.FirstChild is { } child) result.AppendChild(child);
        return result;
    }
    private HtmlNode Current => stack[^1];
    private HtmlNode Adjusted => fragment is not null && stack.Count == 1 ? context! : Current;
    private static bool Html(HtmlNode node, string name) => node.NamespaceUri == HtmlDocument.HtmlNamespace && node.LocalName == name;
    private static bool White(string text) { foreach (char c in text) if (c is not ('\t' or '\n' or '\f' or '\r' or ' ')) return false; return true; }
    private static bool OneOf(string name, params ReadOnlySpan<string> names) { foreach (string candidate in names) if (name == candidate) return true; return false; }
    private static HtmlToken Start(string name) => new(HtmlTokenKind.StartTag, name, "", [], false, null, default);
    private static HtmlToken End(string name) => new(HtmlTokenKind.EndTag, name, "", [], false, null, default);
    private void Push(HtmlNode node)
    {
        if (options.Limits.Depth is int max && stack.Count + 1 > max) throw new InvalidOperationException("HTML open-element depth quota exceeded.");
        stack.Add(node);
    }
    private void Pop() { var node = Current; stack.RemoveAt(stack.Count - 1); PoppedFormControl(node); }
    private void PopThrough(HtmlNode node) { int index = stack.LastIndexOf(node); if (index >= 0) while (stack.Count > index) Pop(); }
    private void PopThrough(string name) { for (int i = stack.Count - 1; i >= 0; i--) if (Html(stack[i], name)) { while (stack.Count > i) Pop(); return; } }
    private (HtmlNode Parent, HtmlNode? Before) Location(HtmlNode? target = null)
    {
        target ??= stack.Count == 0 ? document.DocumentNode : Current;
        HtmlNode? before = null;
        if (foster && target.NamespaceUri == HtmlDocument.HtmlNamespace && OneOf(target.LocalName, "table", "tbody", "tfoot", "thead", "tr"))
        {
            int template = -1, table = -1;
            for (int i = stack.Count - 1; i >= 0; i--) { if (template < 0 && Html(stack[i], "template")) template = i; if (table < 0 && Html(stack[i], "table")) table = i; }
            if (template >= 0 && (table < 0 || template > table)) target = stack[template];
            else if (table < 0) target = stack[0];
            else if (stack[table].Parent is { } parent) { before = stack[table]; target = parent; }
            else target = stack[table - 1];
        }
        if (target.TemplateContent is { } content) { if (patches.TryGetValue(target, out var patch)) { target = patch.Parent; before = patch.End?.Parent == target ? patch.End : null; } else { target = content; before = null; } }
        return (target, before);
    }
    private void InsertNode(HtmlNode node, HtmlNode? target = null)
    {
        if (node.Parent is not null) FlushObservedText(node);
        var location = Location(target);
        if (textParent == location.Parent) FlushText();
        if (location.Before is { } before) location.Parent.InsertBefore(node, before); else location.Parent.AppendChild(node);
    }
    private HtmlNode Insert(HtmlToken token, string? ns = null)
    {
        ns ??= HtmlDocument.HtmlNamespace;
        string name = ns == HtmlForeignNames.SvgNamespace ? HtmlForeignNames.AdjustSvgElement(token.Name) : token.Name;
        var node = document.CreateElement(name, ns);
        foreach (var attribute in token.Attributes)
        {
            var adjusted = ns == HtmlForeignNames.SvgNamespace ? attribute with { LocalName = HtmlForeignNames.AdjustSvgAttribute(attribute.LocalName) }
                : ns == HtmlForeignNames.MathMlNamespace ? attribute with { LocalName = HtmlForeignNames.AdjustMathMlAttribute(attribute.LocalName) } : attribute;
            if (ns != HtmlDocument.HtmlNamespace) adjusted = HtmlForeignNames.AdjustForeignAttribute(adjusted);
            node.SetAttribute(adjusted);
        }
        InsertNode(node); InsertedFormControl(node); Push(node); return node;
    }
    private void Characters(string data)
    {
        if (data.Length == 0) return;
        var location = Location();
        if (location.Parent != textParent || location.Before != textBefore) FlushText();
        if (textParent is null)
        {
            textParent = location.Parent; textBefore = location.Before;
            var previous = textBefore?.PreviousSibling ?? (textBefore is null ? textParent.LastChild : null);
            if (previous is { Kind: HtmlNodeKind.Text }) textNode = previous;
        }
        pendingText.Append(data);
    }
    private void FlushText()
    {
        if (textParent is null) return;
        if (textNode is not null)
        {
            resumedText ??= new();
            if (!resumedText.TryGetValue(textNode, out var accumulated))
            {
                accumulated = new(textNode.Value); resumedText.Add(textNode, accumulated);
            }
            accumulated.Append(pendingText);
        }
        else
        {
            var node = document.CreateParsedText(pendingText.ToString());
            if (textBefore is not null) textParent.InsertBefore(node, textBefore); else textParent.AppendChild(node);
        }
        pendingText.Clear();
        textParent = textBefore = textNode = null;
    }
    private static bool Within(HtmlNode node, HtmlNode root)
    {
        for (var current = node; current is not null; current = current.StructuralParent) if (current == root) return true;
        return false;
    }
    private void FlushObservedText(HtmlNode? root = null)
    {
        if (textParent is not null && (root is null || Within(textParent, root))) FlushText();
        if (resumedText is null || resumedText.Count == 0) return;
        // Dictionary removal during enumeration is supported by net10.0 and does
        // not allocate a temporary key snapshot. No entries are added in this loop.
        foreach (var entry in resumedText)
            if (root is null || Within(entry.Key, root))
            {
                entry.Key.SetParsedText(entry.Value.ToString()); resumedText.Remove(entry.Key);
            }
    }
    private void Misc(HtmlToken token, HtmlNode? target = null) => InsertNode(token.Kind == HtmlTokenKind.Comment ? document.CreateComment(token.Data) : document.CreateProcessingInstruction(token.Name, token.Data), target);
    private void Text(HtmlToken token, HtmlTokenizerState state)
    {
        Insert(token); Tokenizer.SetState(state, token.Name); original = mode; mode = Mode.Text;
    }
    private void Process(HtmlToken token)
    {
        if (token.Kind == HtmlTokenKind.Character && mode is Mode.Initial or Mode.BeforeHtml or Mode.BeforeHead or Mode.InHead or Mode.InHeadNoscript or Mode.AfterHead or Mode.AfterBody or Mode.AfterFrameset or Mode.AfterAfterBody or Mode.AfterAfterFrameset or Mode.InColumnGroup or Mode.InFrameset)
        {
            int prefix = 0; while (prefix < token.Data.Length && token.Data[prefix] is '\t' or '\n' or '\f' or '\r' or ' ') prefix++;
            if (prefix > 0 && prefix < token.Data.Length)
            {
                ProcessCore(new(token.Kind, token.Name, token.Data[..prefix], token.Attributes, token.SelfClosing, token.Doctype, token.Location));
                Process(new(token.Kind, token.Name, token.Data[prefix..], token.Attributes, token.SelfClosing, token.Doctype, token.Location));
                return;
            }
        }
        ProcessCore(token);
    }
    private void ProcessCore(HtmlToken token)
    {
        if (skipLf) { skipLf = false; if (token.Kind == HtmlTokenKind.Character && token.Data.StartsWith('\n')) token = new(token.Kind, token.Name, token.Data[1..], token.Attributes, token.SelfClosing, token.Doctype, token.Location); }
        for (int reprocess = 0; ; reprocess++)
        {
            if (reprocess > 128) throw new InvalidOperationException("Tree construction failed to advance.");
            bool again = stack.Count > 0 && Foreign(token) ? ProcessForeign(token) : ProcessHtml(token);
            UpdateCdata(); if (!again) { if (token.Kind == HtmlTokenKind.EndOfFile) { while (stack.Count > 0) Pop(); FlushObservedText(); } break; }
        }
    }
    private void UpdateCdata() => Tokenizer.AllowCdata = stack.Count > 0 && Adjusted.NamespaceUri != HtmlDocument.HtmlNamespace;
}
