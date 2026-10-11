using Xemele.Html;

namespace Xemele.Css;

public sealed record CssSelectorOptions
{
    public IReadOnlyDictionary<string, string> Namespaces { get; init; } = new Dictionary<string, string>();
}

public sealed class CssSelectorException(string message, int offset) : ArgumentException(message)
{
    public int Offset { get; } = offset;
}

/// <summary>An immutable compiled selector. Matches reads the current graph; Select owns a membership snapshot.</summary>
public sealed partial class CssSelector
{
    private readonly Complex[] selectors;
    private CssSelector(Complex[] selectors) => this.selectors = selectors;

    public static CssSelector Compile(string selector, CssSelectorOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(selector);
        var namespaces = new Dictionary<string, string>(StringComparer.Ordinal);
        if (options is not null)
        {
            ArgumentNullException.ThrowIfNull(options.Namespaces);
            foreach (var pair in options.Namespaces)
            {
                ArgumentNullException.ThrowIfNull(pair.Key);
                ArgumentNullException.ThrowIfNull(pair.Value);
                namespaces.Add(pair.Key, pair.Value);
            }
        }
        return new(new Parser(selector, namespaces).Parse());
    }

    public bool Matches(XmlNode element) => Match(new Node(element), new Node(element));
    public bool Matches(HtmlNode element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return Match(new Node(element), new Node(element));
    }
    public IReadOnlyList<XmlNode> Select(XmlNode root)
    {
        if (root.IsDefault) throw new ArgumentException("Default node.", nameof(root));
        var result = new List<XmlNode>();
        var start = new Node(root);
        var scope = start.Element ? start : start.DocumentElement;
        for (var node = start; !node.Absent; node = Next(node, start))
            if (Match(node, scope)) result.Add(node.Xml);
        return Array.AsReadOnly(result.ToArray());
    }
    public IReadOnlyList<HtmlNode> Select(HtmlNode root)
    {
        ArgumentNullException.ThrowIfNull(root);
        var result = new List<HtmlNode>();
        var start = new Node(root);
        var scope = start.Element ? start : start.DocumentElement;
        for (var node = start; !node.Absent; node = Next(node, start))
            if (Match(node, scope)) result.Add(node.Html!);
        return Array.AsReadOnly(result.ToArray());
    }
    private static Node Next(Node node, Node root)
    {
        var child = node.First;
        if (!child.Absent) return child;
        while (!node.Same(root))
        {
            var sibling = node.Next;
            if (!sibling.Absent) return sibling;
            node = node.Parent;
        }
        return default;
    }
    private bool Match(Node node, Node scope)
    {
        if (!node.Element) return false;
        foreach (var selector in selectors)
            if (MatchComplex(selector, selector.Parts.Length - 1, node, scope)) return true;
        return false;
    }
    private static bool MatchComplex(Complex selector, int index, Node node, Node scope)
    {
        if (!node.Element || !MatchCompound(selector.Parts[index], node, scope)) return false;
        if (index == 0) return true;
        switch (selector.Links[index - 1])
        {
            case '>': return MatchComplex(selector, index - 1, node.Parent, scope);
            case '+': return MatchComplex(selector, index - 1, PreviousElement(node), scope);
            case '~':
                for (var previous = PreviousElement(node); !previous.Absent; previous = PreviousElement(previous))
                    if (MatchComplex(selector, index - 1, previous, scope)) return true;
                return false;
            default:
                for (var parent = node.Parent; !parent.Absent; parent = parent.Parent)
                    if (MatchComplex(selector, index - 1, parent, scope)) return true;
                return false;
        }
    }
    private static Node PreviousElement(Node node)
    {
        do { node = node.Previous; } while (!node.Absent && !node.Element);
        return node;
    }
    private static bool MatchCompound(Compound compound, Node node, Node scope)
    {
        if (compound.Type is { } type && !type.Match(node.Name, node.Uri, node.Fold)) return false;
        foreach (var test in compound.Tests)
        {
            if (test.Attribute is { } attribute)
            {
                if (!AttributeMatches(node, attribute)) return false;
                continue;
            }
            if (test.Not is { } not)
            {
                foreach (var argument in not)
                    if (MatchCompound(argument, node, scope)) return false;
                continue;
            }
            switch (test.Pseudo)
            {
                case "root": if (!node.Same(node.DocumentElement)) return false; break;
                case "scope": if (!node.Same(scope)) return false; break;
                case "empty":
                    for (var child = node.First; !child.Absent; child = child.Next)
                        if (child.Element || child.Text) return false;
                    break;
                default:
                    bool ofType = test.Pseudo!.EndsWith("of-type", StringComparison.Ordinal);
                    bool reverse = test.Pseudo.StartsWith("last", StringComparison.Ordinal) || test.Pseudo.StartsWith("nth-last", StringComparison.Ordinal);
                    long position = 1, total = 1;
                    for (var sibling = node.Previous; !sibling.Absent; sibling = sibling.Previous)
                        if (sibling.Element && (!ofType || SameType(node, sibling))) position++;
                    for (var sibling = node.Next; !sibling.Absent; sibling = sibling.Next)
                        if (sibling.Element && (!ofType || SameType(node, sibling))) total++;
                    total += position - 1;
                    if (test.Pseudo.StartsWith("only", StringComparison.Ordinal)) { if (total != 1) return false; }
                    else if (test.Pseudo.StartsWith("nth", StringComparison.Ordinal))
                    {
                        long difference = (reverse ? total - position + 1 : position) - test.B;
                        if (test.A == 0 ? difference != 0 : difference % test.A != 0 || difference / test.A < 0) return false;
                    }
                    else if ((reverse ? total - position + 1 : position) != 1) return false;
                    break;
            }
        }
        return true;
    }
    private static bool SameType(Node left, Node right) => left.Uri == right.Uri && Equal(left.Name, right.Name, left.Fold);
    private static bool AttributeMatches(Node node, AttributeTest test)
    {
        if (node.Html is { } html)
        {
            for (int i = 0; i < html.Attributes.Count; i++)
            {
                var attribute = html.Attributes[i];
                if (test.Name.Match(attribute.LocalName, attribute.NamespaceUri, node.Fold && attribute.NamespaceUri.Length == 0) && ValueMatches(attribute.Value, test)) return true;
            }
        }
        else
        {
            foreach (var attribute in node.Xml.Attributes)
                if (test.Name.Match(attribute.LocalName, attribute.NamespaceUri, false) && ValueMatches(attribute.Value, test)) return true;
        }
        return false;
    }
    private static bool ValueMatches(string value, AttributeTest test)
    {
        var target = test.Value.AsSpan();
        switch (test.Operator)
        {
            case "": return true;
            case "=": return value.AsSpan().SequenceEqual(target);
            case "~=" : return ContainsToken(value, test.Value);
            case "|=": return value.AsSpan().SequenceEqual(target) || value.Length > target.Length && value[target.Length] == '-' && value.AsSpan(0, target.Length).SequenceEqual(target);
            case "^=": return !target.IsEmpty && value.AsSpan().StartsWith(target, StringComparison.Ordinal);
            case "$=": return !target.IsEmpty && value.AsSpan().EndsWith(target, StringComparison.Ordinal);
            default: return !target.IsEmpty && value.AsSpan().Contains(target, StringComparison.Ordinal);
        }
    }
    private static bool ContainsToken(string value, string token)
    {
        var target = token.AsSpan();
        if (target.IsEmpty) return false;
        var span = value.AsSpan();
        int i = 0;
        while (i < span.Length)
        {
            while (i < span.Length && White(span[i])) i++;
            int start = i;
            while (i < span.Length && !White(span[i])) i++;
            if (span[start..i].SequenceEqual(target)) return true;
        }
        return false;
    }
    private static bool Equal(string left, string right, bool fold)
    {
        if (left.Length != right.Length) return false;
        for (int i = 0; i < left.Length; i++) if ((fold ? Lower(left[i]) : left[i]) != (fold ? Lower(right[i]) : right[i])) return false;
        return true;
    }
    private static char Lower(char c) => c is >= 'A' and <= 'Z' ? (char)(c + 32) : c;
    private static bool White(char c) => c is ' ' or '\t' or '\r' or '\n' or '\f';
    private readonly struct Node
    {
        public readonly XmlNode Xml;
        public readonly HtmlNode? Html;
        public Node(XmlNode node) { Xml = node; Html = null; }
        public Node(HtmlNode? node) { Html = node; Xml = default; }
        public bool Absent => Html is null && Xml.IsDefault;
        public bool Element => !Absent && (Html is { } h ? h.Kind == HtmlNodeKind.Element : Xml.Kind == XmlNodeKind.Element);
        public bool Text => Html is { } h ? h.Kind == HtmlNodeKind.Text : Xml.Kind is XmlNodeKind.Text or XmlNodeKind.CData;
        public string Name => Html is { } h ? h.LocalName : Xml.LocalName;
        public string Uri => Html is { } h ? h.NamespaceUri : Xml.NamespaceUri;
        public bool Fold => Html is not null && Uri == "http://www.w3.org/1999/xhtml";
        public Node Parent => Absent ? default : Html is { } h ? new(h.Parent) : new(Xml.Parent);
        public Node First => Html is { } h ? new(h.FirstChild) : new(Xml.FirstChild);
        public Node Next => Absent ? default : Html is { } h ? new(h.NextSibling) : new(Xml.NextSibling);
        public Node Previous => Absent ? default : Html is { } h ? new(h.PreviousSibling) : new(Xml.PreviousSibling);
        public bool Same(Node other) => Html is not null ? ReferenceEquals(Html, other.Html) : other.Html is null && Xml == other.Xml;
        public Node DocumentElement
        {
            get
            {
                if (Absent) return default;
                var document = Html is { } h ? new Node(h.OwnerDocument.DocumentNode) : new Node(Xml.OwnerDocument.DocumentNode);
                for (var child = document.First; !child.Absent; child = child.Next) if (child.Element) return child;
                return default;
            }
        }
    }
    private sealed record NameTest(string Local, string? Namespace)
    {
        public bool Match(string name, string uri, bool fold) => (Namespace is null || Namespace == uri) && (Local == "*" || Equal(Local, name, fold));
    }
    private sealed record AttributeTest(NameTest Name, string Operator, string Value);
    private sealed record Test(AttributeTest? Attribute = null, string? Pseudo = null, long A = 0, long B = 0, Compound[]? Not = null);
    private sealed record Compound(NameTest? Type, Test[] Tests);
    private sealed record Complex(Compound[] Parts, char[] Links);
}
