using Xemele.Parsing.Dtd;
namespace Xemele.Resolution.XInclude;

internal sealed partial class IncludeOperation
{
    private static string QName(string prefix, string local) => prefix.Length == 0 ? local : prefix + ":" + local;
    private Dictionary<int, HashSet<string>> CaptureIds(XmlDocument document)
    {
        var ids = new Dictionary<int, HashSet<string>>();
        var pending = new Stack<(XmlNode Node, int Depth)>();
        pending.Push((document.DocumentNode, 0));
        while (pending.Count != 0)
        {
            var next = pending.Pop();
            if (options.Limits.Depth is int maximum && next.Depth > maximum) throw new Rejection("Limit");
            Visit(next.Node);
            foreach (var child in next.Node.Children) pending.Push((child, next.Depth + 1));
        }
        return ids;
        void Visit(XmlNode node)
        {
            token.ThrowIfCancellationRequested();
            CheckToken(Scalars(node.LocalName)); CheckToken(Scalars(node.Value));
            if (options.Limits.AttributesPerElement is int maximum && (long)node.Attributes.Count + node.NamespaceDeclarations.Count > maximum) throw new Rejection("Limit");
            foreach (var declaration in node.NamespaceDeclarations)
            {
                token.ThrowIfCancellationRequested(); CheckToken(Scalars(declaration.Prefix)); CheckToken(Scalars(declaration.Value));
            }
            foreach (var attribute in node.Attributes)
            {
                token.ThrowIfCancellationRequested();
                CheckToken(Scalars(attribute.LocalName)); CheckToken(Scalars(attribute.Value));
                bool id = attribute.LocalName == "id" && attribute.NamespaceUri == Xml;
                if (!id && document.ValidationSourceContentRevision == document.ContentRevision && document.ContentRevision >= 0 && document.Dtd is { } model)
                    foreach (var declaration in model.Attributes)
                        if (declaration.Type == DtdAttributeType.Id && declaration.ElementName == QName(node.Prefix, node.LocalName) &&
                            declaration.Name == QName(attribute.Prefix, attribute.LocalName)) { id = true; break; }
                if (id)
                {
                    if (!ids.TryGetValue(node.Slot, out var values)) ids.Add(node.Slot, values = new(StringComparer.Ordinal));
                    ReadOnlySpan<char> normalized = attribute.Value.AsSpan().Trim(" \t\r\n");
                    values.Add(normalized.Length == attribute.Value.Length ? attribute.Value : normalized.ToString());
                }
            }

        }
    }
    private void CopyIds(XmlNode source, XmlNode copy, Dictionary<int, HashSet<string>> from, Dictionary<int, HashSet<string>> to)
    {
        var pending = new Stack<(XmlNode Source, XmlNode Copy)>();
        pending.Push((source, copy));
        while (pending.Count != 0)
        {
            token.ThrowIfCancellationRequested();
            var pair = pending.Pop();
            if (from.TryGetValue(pair.Source.Slot, out var values)) to.Add(pair.Copy.Slot, values);
            var a = pair.Source.FirstChild; var b = pair.Copy.FirstChild;
            while (!a.IsDefault) { pending.Push((a, b)); a = a.NextSibling; b = b.NextSibling; }
        }
    }
    private readonly record struct Pointer(string? Id, int[] Children);
    private static Pointer ParsePointer(string expression)
    {
        if (expression.Length == 0) throw new Rejection("Syntax");
        ReadOnlySpan<char> value = expression.AsSpan();
        bool scheme = value.StartsWith("element(", StringComparison.Ordinal);
        if (scheme)
        {
            if (value.IndexOf(')') != value.Length - 1) throw new Rejection("Unsupported");
            if (!value.EndsWith(")", StringComparison.Ordinal)) throw new Rejection("Syntax");
            value = value[8..^1];
        }
        else
        {
            if (value.IndexOfAny('(', ')') >= 0 || value.Contains(' ') || value.Contains('@') || value.Contains("::", StringComparison.Ordinal)) throw new Rejection("Unsupported");
            if (!NcName(value)) throw new Rejection("Syntax");
            return new(expression, []);
        }
        int slash = value.IndexOf('/');
        string? id = null;
        if (slash != 0)
        {
            var head = slash < 0 ? value : value[..slash];
            if (!NcName(head)) throw new Rejection("Syntax");
            id = head.ToString();
            if (slash < 0) return new(id, []);
            value = value[slash..];
        }
        var children = new List<int>();
        while (!value.IsEmpty)
        {
            if (value[0] != '/') throw new Rejection("Syntax");
            value = value[1..]; int end = value.IndexOf('/');
            var index = end < 0 ? value : value[..end];
            if (index.IsEmpty) throw new Rejection("Syntax");
            int number = 0;
            foreach (char c in index)
            {
                if (c is < '0' or > '9' || number > (int.MaxValue - (c - '0')) / 10) throw new Rejection("Syntax");
                number = number * 10 + c - '0';
            }
            if (number == 0) throw new Rejection("Syntax");
            children.Add(number);
            value = end < 0 ? [] : value[end..];
        }
        if (id is null && children.Count == 0) throw new Rejection("Syntax");
        return new(id, children.ToArray());
    }
    private static bool NcName(ReadOnlySpan<char> value)
    {
        if (value.IsEmpty || value.Contains(':')) return false;
        try { TreeValidation.Name(value.ToString()); return true; } catch (ArgumentException) { return false; }
    }
    private List<XmlNode> Select(Source source, Pointer? pointer)
    {
        if (pointer is null)
        {
            var items = new List<XmlNode>();
            foreach (var child in source.Document.DocumentNode.Children) items.Add(child);
            return items;
        }
        var p = pointer.Value;
        XmlNode selected = source.Document.DocumentNode;
        if (p.Id is not null)
        {
            selected = default;
            foreach (var entry in source.Ids)
            {
                token.ThrowIfCancellationRequested();
                if (!entry.Value.Contains(p.Id)) continue;
                if (!selected.IsDefault) throw new Rejection("Syntax", uri: source.BaseUri);
                selected = source.Document.Handle(entry.Key);
            }
            if (selected.IsDefault) throw new Rejection("Selection", true, source.BaseUri);
        }
        foreach (int index in p.Children)
        {
            XmlNode child = selected.FirstChild; int count = 0;
            while (!child.IsDefault)
            {
                token.ThrowIfCancellationRequested();
                if (child.Kind == XmlNodeKind.Element && ++count == index) break;
                child = child.NextSibling;
            }
            if (child.IsDefault) throw new Rejection("Selection", true, source.BaseUri);
            selected = child;
        }
        return [selected];
    }
    private static string Path(XmlNode node)
    {
        if (node.Kind == XmlNodeKind.Document) return "/";
        var indexes = new List<int>();
        while (node.Kind != XmlNodeKind.Document)
        {
            int index = 1;
            for (var preceding = node.PreviousSibling; !preceding.IsDefault; preceding = preceding.PreviousSibling)
                if (preceding.Kind == XmlNodeKind.Element) index++;
            indexes.Add(index); node = node.Parent;
        }
        var path = new System.Text.StringBuilder();
        for (int i = indexes.Count - 1; i >= 0; i--) path.Append('/').Append(indexes[i]);
        return path.ToString();
    }
    private static bool ContainsPath(string ancestor, string child) => ancestor == "/" || ancestor == child ||
        (child.StartsWith(ancestor, StringComparison.Ordinal) && child.Length > ancestor.Length && child[ancestor.Length] == '/');
    private void CheckCycle(Source target, XmlNode selection, Source origin, XmlNode include)
    {
        string selectedPath = selection.Kind == XmlNodeKind.Element ? Path(selection) : "/";
        if (target.Identity == origin.Identity && ContainsPath(selectedPath, Path(include))) throw new Rejection("Cycle", uri: target.BaseUri);
        foreach (var entry in active)
            if (entry.Identity == target.Identity && (ContainsPath(entry.Path, selectedPath) || ContainsPath(selectedPath, entry.Path)))
                throw new Rejection("Cycle", uri: target.BaseUri);
    }
}
