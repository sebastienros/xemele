namespace Xemele.Html;
internal sealed partial class HtmlTreeBuilder
{
    // Selectedness is parser-private state. It is not an attribute and is not
    // copied into the public model. Static option insertion/pop hooks are required
    // by WHATWG form-elements even without script execution or rendering.
    private readonly Dictionary<HtmlNode, bool> selected = new();
    private static HtmlNode? OptionSelect(HtmlNode option)
    {
        bool optgroup = false;
        for (var ancestor = option.Parent; ancestor is not null; ancestor = ancestor.Parent)
        {
            if (ancestor.NamespaceUri != HtmlDocument.HtmlNamespace) continue;
            if (ancestor.LocalName is "datalist" or "hr" or "option") return null;
            if (ancestor.LocalName == "optgroup") { if (optgroup) return null; optgroup = true; }
            if (ancestor.LocalName == "select") return ancestor;
        }
        return null;
    }
    private static HtmlNode? SelectedContentSelect(HtmlNode content)
    {
        HtmlNode? select = null;
        for (var ancestor = content.Parent; ancestor is not null; ancestor = ancestor.Parent)
        {
            if (ancestor.NamespaceUri != HtmlDocument.HtmlNamespace) continue;
            if (ancestor.LocalName is "option" or "selectedcontent") return null;
            if (ancestor.LocalName == "select") { if (select is not null) return null; select = ancestor; }
        }
        return select;
    }
    private static IEnumerable<HtmlNode> Descendants(HtmlNode root)
    {
        var pending = new Stack<HtmlNode>();
        for (int i = root.Children.Count - 1; i >= 0; i--) pending.Push(root.Children[i]);
        while (pending.TryPop(out var n)) { yield return n; for (int i = n.Children.Count - 1; i >= 0; i--) pending.Push(n.Children[i]); }
    }
    private List<HtmlNode> SelectOptions(HtmlNode select)
    {
        var result = new List<HtmlNode>();
        foreach (var node in Descendants(select)) if (Html(node, "option") && OptionSelect(node) == select) result.Add(node);
        return result;
    }
    private bool Selected(HtmlNode option) { if (!selected.TryGetValue(option, out bool value)) { value = option.GetAttribute("selected") is not null; selected.Add(option, value); } return value; }
    private static bool DisabledOption(HtmlNode option)
    {
        if (option.GetAttribute("disabled") is not null) return true;
        for (var n = option.Parent; n is not null; n = n.Parent) { if (Html(n, "select")) break; if (Html(n, "optgroup") && n.GetAttribute("disabled") is not null) return true; }
        return false;
    }
    private void SetSelectedness(HtmlNode select, HtmlNode? inserted = null)
    {
        if (select.GetAttribute("multiple") is not null) return;
        var options = SelectOptions(select);
        if (inserted is not null && Selected(inserted)) foreach (var option in options) if (option != inserted) selected[option] = false;
        HtmlNode? first = null, last = null;
        foreach (var option in options)
        {
            if (Selected(option)) { if (last is not null) selected[last] = false; last = option; }
            if (first is null && !DisabledOption(option)) first = option;
        }
        bool sizeOne = true;
        if (select.GetAttribute("size") is { } size)
        {
            var text = size.AsSpan().TrimStart(" \t\n\r\f".AsSpan()); int i = 0; if (i < text.Length && text[i] == '+') i++;
            int start = i; long value = 0; while (i < text.Length && text[i] is >= '0' and <= '9') { value = Math.Min(int.MaxValue, value * 10 + text[i++] - '0'); }
            if (i > start) sizeOne = value == 1;
        }
        if (last is null && first is not null && sizeOne) selected[first] = true;
    }
    private void UpdateSelectedContents(HtmlNode select)
    {
        if (select.GetAttribute("multiple") is not null) return;
        HtmlNode? chosen = null; foreach (var option in SelectOptions(select)) if (Selected(option)) { chosen = option; break; }
        var contents = new List<HtmlNode>(); foreach (var node in Descendants(select)) if (Html(node, "selectedcontent") && SelectedContentSelect(node) == select) contents.Add(node);
        foreach (var content in contents)
        {
            var replacement = document.CreateFragment(); if (chosen is not null) foreach (var child in chosen.Children) replacement.AppendChild(child.Clone(true));
            while (content.FirstChild is { } child) child.Remove(); content.AppendChild(replacement);
        }
    }
    private void InsertedFormControl(HtmlNode node)
    {
        if (Html(node, "option"))
        {
            if (OptionSelect(node) is { } select) { SetSelectedness(select, node); if (Selected(node)) UpdateSelectedContents(select); }
        }
        else if (Html(node, "selectedcontent") && SelectedContentSelect(node) is { } select) UpdateSelectedContents(select);
    }
    private void PoppedFormControl(HtmlNode node)
    {
        if (Html(node, "option") && Selected(node) && OptionSelect(node) is { } select) UpdateSelectedContents(select);
    }
}
