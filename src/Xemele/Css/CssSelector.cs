using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using Xemele.Html;

namespace Xemele.Css;

/// <summary>Provides namespace bindings used when compiling a CSS selector.</summary>
public sealed record CssSelectorOptions
{
    /// <summary>Gets the prefix-to-namespace-URI bindings copied when a selector is compiled.</summary>
    public IReadOnlyDictionary<string, string> Namespaces { get; init; } = new Dictionary<string, string>();
}

/// <summary>Reports an invalid or unsupported CSS selector.</summary>
/// <param name="message">The description of the selector error.</param>
/// <param name="offset">The zero-based character offset of the error in the selector.</param>
public sealed class CssSelectorException(string message, int offset) : ArgumentException(message)
{
    /// <summary>Gets the zero-based character offset of the error in the selector.</summary>
    public int Offset { get; } = offset;
}

/// <summary>An immutable compiled selector. Matches reads the current graph; Select owns a membership snapshot.</summary>
public sealed partial class CssSelector
{
    private static readonly ReadOnlyCollection<HtmlNode> NoHtmlNodes = Array.AsReadOnly(Array.Empty<HtmlNode>());
    private static readonly ReadOnlyCollection<XmlNode> NoXmlNodes = Array.AsReadOnly(Array.Empty<XmlNode>());
    private static readonly ConcurrentDictionary<string, CssSelector> DefaultCache = new(StringComparer.Ordinal);
    private static CssSelector? lastSelector;
    private static string? lastSelectorText;
    private readonly Complex[] selectors;
    /// <summary>Creates the universal selector, equivalent to Compile("*") with default options.</summary>
    public CssSelector() => selectors = [new Complex([new Compound(new NameTest("*", "*", null), [])], [])];
    private CssSelector(Complex[] selectors) => this.selectors = selectors;

    /// <summary>Compiles a CSS selector with a snapshot of the supplied namespace bindings.</summary>
    /// <param name="selector">The selector text.</param>
    /// <param name="options">The namespace bindings, or <see langword="null"/> for no bindings.</param>
    /// <returns>An immutable selector that can be reused against XML or HTML trees.</returns>
    /// <exception cref="ArgumentNullException">The selector or a supplied namespace binding is null.</exception>
    /// <exception cref="CssSelectorException">The selector is invalid or uses unsupported syntax.</exception>
    public static CssSelector Compile(string selector, CssSelectorOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(selector);
        if (options is null && ReferenceEquals(selector, lastSelectorText) && lastSelector is { } fast)
        {
            return fast;
        }
        Dictionary<string, string>? namespaces = null;
        if (options is not null)
        {
            ArgumentNullException.ThrowIfNull(options.Namespaces);
            if (options.Namespaces.Count > 0)
            {
                namespaces = new Dictionary<string, string>(options.Namespaces.Count, StringComparer.Ordinal);
                foreach (var pair in options.Namespaces)
                {
                    ArgumentNullException.ThrowIfNull(pair.Key);
                    ArgumentNullException.ThrowIfNull(pair.Value);
                    namespaces.Add(pair.Key, pair.Value);
                }
            }
        }
        if (namespaces is null && selector.Length <= 512)
        {
            if (DefaultCache.TryGetValue(selector, out var cached))
            {
                lastSelector = cached;
                lastSelectorText = selector;
                return cached;
            }

            var compiled = new CssSelector(new Parser(selector, null).Parse());
            if (DefaultCache.Count < 1024)
            {
                DefaultCache.TryAdd(selector, compiled);
            }
            lastSelector = compiled;
            lastSelectorText = selector;
            return compiled;
        }
        return new(new Parser(selector, namespaces).Parse());
    }

    /// <summary>Tests an XML element against this selector using that element as the scope.</summary>
    /// <param name="element">The element to test.</param>
    /// <returns>Whether the current element matches; false for a default or non-element node.</returns>
    public bool Matches(XmlNode element) => Match(new Node(element), new Node(element));
    /// <summary>Tests an HTML element against this selector using that element as the scope.</summary>
    /// <param name="element">The element to test.</param>
    /// <returns>Whether the current element matches; false for a non-element node.</returns>
    /// <exception cref="ArgumentNullException">The element is null.</exception>
    public bool Matches(HtmlNode element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return MatchHtml(element, element);
    }
    /// <summary>Captures the matching XML elements in document traversal order, including the root when it matches.</summary>
    /// <param name="root">The root of the subtree to search.</param>
    /// <returns>A read-only membership snapshot whose node handles still refer to the live document.</returns>
    /// <exception cref="ArgumentException">The root is a default node.</exception>
    public IReadOnlyList<XmlNode> Select(XmlNode root)
    {
        if (root.IsDefault)
        {
            throw new ArgumentException("Default node.", nameof(root));
        }

        List<XmlNode>? result = null;
        var start = new Node(root);
        var scope = start.Element ? start : start.DocumentElement;
        for (var node = start; !node.Absent; node = Next(node, start))
        {
            if (Match(node, scope))
            {
                (result ??= []).Add(node.Xml);
            }
        }

        return result is null ? NoXmlNodes : result.AsReadOnly();
    }
    /// <summary>Captures the matching HTML elements in document traversal order, including the root when it matches.</summary>
    /// <param name="root">The root of the subtree to search.</param>
    /// <returns>A read-only membership snapshot whose nodes still refer to the live document.</returns>
    /// <exception cref="ArgumentNullException">The root is null.</exception>
    public IReadOnlyList<HtmlNode> Select(HtmlNode root)
    {
        ArgumentNullException.ThrowIfNull(root);
        List<HtmlNode>? result = null;
        var scope = root.Kind == HtmlNodeKind.Element ? root : HtmlDocumentElement(root);
        HtmlNode? lastParent = null;
        HtmlNode? lastPositionSibling = null;
        long lastPosition = 0;
        for (var node = root; node is not null; node = NextHtml(node, root))
        {
            if (node.Kind == HtmlNodeKind.Element && MatchHtml(node, scope, ref lastParent, ref lastPositionSibling, ref lastPosition))
            {
                (result ??= []).Add(node);
            }
        }

        return result is null ? NoHtmlNodes : result.AsReadOnly();
    }
    private static HtmlNode? HtmlDocumentElement(HtmlNode node)
    {
        for (var child = node.OwnerDocument.DocumentNode.FirstChild; child is not null; child = child.NextSibling)
        {
            if (child.Kind == HtmlNodeKind.Element)
            {
                return child;
            }
        }

        return null;
    }
    private static HtmlNode? NextHtml(HtmlNode node, HtmlNode root)
    {
        for (var child = node.FirstChild; child is not null; child = child.NextSibling)
        {
            if (child.Kind == HtmlNodeKind.Element)
            {
                return child;
            }
        }

        while (!ReferenceEquals(node, root))
        {
            for (var sibling = node.NextSibling; sibling is not null; sibling = sibling.NextSibling)
            {
                if (sibling.Kind == HtmlNodeKind.Element)
                {
                    return sibling;
                }
            }

            node = node.Parent!;
        }
        return null;
    }
    private bool MatchHtml(HtmlNode node, HtmlNode? scope)
    {
        HtmlNode? lastParent = null;
        HtmlNode? lastPositionSibling = null;
        long lastPosition = 0;
        return MatchHtml(node, scope, ref lastParent, ref lastPositionSibling, ref lastPosition);
    }
    private bool MatchHtml(HtmlNode node, HtmlNode? scope, ref HtmlNode? lastParent, ref HtmlNode? lastPositionSibling, ref long lastPosition)
    {
        if (node.Kind != HtmlNodeKind.Element)
        {
            return false;
        }

        var selectors = this.selectors;
        for (var i = 0; i < selectors.Length; i++)
        {
            var selector = selectors[i];
            if (MatchComplexHtml(selector, selector.Parts.Length - 1, node, scope, ref lastParent, ref lastPositionSibling, ref lastPosition))
            {
                return true;
            }
        }

        return false;
    }
    private static bool MatchComplexHtml(Complex selector, int index, HtmlNode? node, HtmlNode? scope, ref HtmlNode? lastParent, ref HtmlNode? lastPositionSibling, ref long lastPosition)
    {
        if (node is null || node.Kind != HtmlNodeKind.Element || !MatchCompoundHtml(selector.Parts[index], node, scope, ref lastParent, ref lastPositionSibling, ref lastPosition))
        {
            return false;
        }

        if (index == 0)
        {
            return true;
        }

        switch (selector.Links[index - 1])
        {
            case '>':
                return MatchComplexHtml(selector, index - 1, node.Parent, scope, ref lastParent, ref lastPositionSibling, ref lastPosition);
            case '+':
                return MatchComplexHtml(selector, index - 1, PreviousHtmlElement(node), scope, ref lastParent, ref lastPositionSibling, ref lastPosition);
            case '~':
                for (var previous = PreviousHtmlElement(node); previous is not null; previous = PreviousHtmlElement(previous))
                {
                    if (MatchComplexHtml(selector, index - 1, previous, scope, ref lastParent, ref lastPositionSibling, ref lastPosition))
                    {
                        return true;
                    }
                }

                return false;
            default:
                for (var parent = node.Parent; parent is not null; parent = parent.Parent)
                {
                    if (MatchComplexHtml(selector, index - 1, parent, scope, ref lastParent, ref lastPositionSibling, ref lastPosition))
                    {
                        return true;
                    }
                }

                return false;
        }
    }
    private static HtmlNode? PreviousHtmlElement(HtmlNode node)
    {
        for (var previous = node.PreviousSibling; previous is not null; previous = previous.PreviousSibling)
        {
            if (previous.Kind == HtmlNodeKind.Element)
            {
                return previous;
            }
        }

        return null;
    }
    private static bool MatchCompoundHtml(Compound compound, HtmlNode node, HtmlNode? scope, ref HtmlNode? lastParent, ref HtmlNode? lastPositionSibling, ref long lastPosition)
    {
        if (compound.Type is { } type && !type.MatchHtmlElement(node))
        {
            return false;
        }

        var tests = compound.Tests;
        for (var i = 0; i < tests.Length; i++)
        {
            var test = tests[i];
            if (test.Attribute is { } attribute)
            {
                if (!AttributeMatchesHtml(node, attribute))
                {
                    return false;
                }

                continue;
            }
            if (test.Not is { } not)
            {
                for (var j = 0; j < not.Length; j++)
                {
                    if (MatchCompoundHtml(not[j], node, scope, ref lastParent, ref lastPositionSibling, ref lastPosition))
                    {
                        return false;
                    }
                }

                continue;
            }
            switch (test.Pseudo)
            {
                case PseudoKind.Root:
                    if (!ReferenceEquals(node, HtmlDocumentElement(node)))
                    {
                        return false;
                    }

                    break;
                case PseudoKind.Scope:
                    if (!ReferenceEquals(node, scope))
                    {
                        return false;
                    }

                    break;
                case PseudoKind.Empty:
                    for (var child = node.FirstChild; child is not null; child = child.NextSibling)
                    {
                        if (child.Kind is HtmlNodeKind.Element or HtmlNodeKind.Text)
                        {
                            return false;
                        }
                    }

                    break;
                case PseudoKind.FirstChild:
                    if (PreviousHtmlElement(node) is not null)
                    {
                        return false;
                    }

                    break;
                case PseudoKind.LastChild:
                    for (var sibling = node.NextSibling; sibling is not null; sibling = sibling.NextSibling)
                    {
                        if (sibling.Kind == HtmlNodeKind.Element)
                        {
                            return false;
                        }
                    }

                    break;
                case PseudoKind.OnlyChild:
                    if (PreviousHtmlElement(node) is not null)
                    {
                        return false;
                    }

                    for (var sibling = node.NextSibling; sibling is not null; sibling = sibling.NextSibling)
                    {
                        if (sibling.Kind == HtmlNodeKind.Element)
                        {
                            return false;
                        }
                    }

                    break;
                case PseudoKind.FirstOfType:
                    for (var sibling = node.PreviousSibling; sibling is not null; sibling = sibling.PreviousSibling)
                    {
                        if (sibling.Kind == HtmlNodeKind.Element && SameTypeHtml(node, sibling))
                        {
                            return false;
                        }
                    }

                    break;
                case PseudoKind.LastOfType:
                    for (var sibling = node.NextSibling; sibling is not null; sibling = sibling.NextSibling)
                    {
                        if (sibling.Kind == HtmlNodeKind.Element && SameTypeHtml(node, sibling))
                        {
                            return false;
                        }
                    }

                    break;
                case PseudoKind.OnlyOfType:
                    for (var sibling = node.PreviousSibling; sibling is not null; sibling = sibling.PreviousSibling)
                    {
                        if (sibling.Kind == HtmlNodeKind.Element && SameTypeHtml(node, sibling))
                        {
                            return false;
                        }
                    }

                    for (var sibling = node.NextSibling; sibling is not null; sibling = sibling.NextSibling)
                    {
                        if (sibling.Kind == HtmlNodeKind.Element && SameTypeHtml(node, sibling))
                        {
                            return false;
                        }
                    }

                    break;
                case PseudoKind.NthChild:
                {
                    long position;
                    var parent = node.Parent;
                    if (parent is not null && ReferenceEquals(parent, lastParent))
                    {
                        if (ReferenceEquals(node, lastPositionSibling))
                        {
                            position = lastPosition;
                        }
                        else
                        {
                            position = 0;
                            long delta = 0;
                            for (var sibling = node.PreviousSibling; sibling is not null; sibling = sibling.PreviousSibling)
                            {
                                if (sibling.Kind == HtmlNodeKind.Element)
                                {
                                    delta++;
                                    if (ReferenceEquals(sibling, lastPositionSibling))
                                    {
                                        position = lastPosition + delta;
                                        break;
                                    }
                                }
                            }
                            if (position == 0)
                            {
                                position = delta + 1;
                            }
                            lastPositionSibling = node;
                            lastPosition = position;
                        }
                    }
                    else
                    {
                        position = 1;
                        for (var sibling = node.PreviousSibling; sibling is not null; sibling = sibling.PreviousSibling)
                        {
                            if (sibling.Kind == HtmlNodeKind.Element)
                            {
                                position++;
                            }
                        }
                        if (parent is not null)
                        {
                            lastParent = parent;
                            lastPositionSibling = node;
                            lastPosition = position;
                        }
                    }

                    var difference = position - test.B;
                    if (test.A == 0 ? difference != 0 : difference % test.A != 0 || difference / test.A < 0)
                    {
                        return false;
                    }

                    break;
                }
                case PseudoKind.NthOfType:
                {
                    long position = 1;
                    for (var sibling = node.PreviousSibling; sibling is not null; sibling = sibling.PreviousSibling)
                    {
                        if (sibling.Kind == HtmlNodeKind.Element && SameTypeHtml(node, sibling))
                        {
                            position++;
                        }
                    }

                    var difference = position - test.B;
                    if (test.A == 0 ? difference != 0 : difference % test.A != 0 || difference / test.A < 0)
                    {
                        return false;
                    }

                    break;
                }
                case PseudoKind.NthLastChild:
                case PseudoKind.NthLastOfType:
                {
                    var ofType = test.Pseudo == PseudoKind.NthLastOfType;
                    long position = 1;
                    for (var sibling = node.NextSibling; sibling is not null; sibling = sibling.NextSibling)
                    {
                        if (sibling.Kind == HtmlNodeKind.Element && (!ofType || SameTypeHtml(node, sibling)))
                        {
                            position++;
                        }
                    }

                    var difference = position - test.B;
                    if (test.A == 0 ? difference != 0 : difference % test.A != 0 || difference / test.A < 0)
                    {
                        return false;
                    }

                    break;
                }
            }
        }
        return true;
    }
    private static bool SameTypeHtml(HtmlNode left, HtmlNode right) => left.NamespaceUri == right.NamespaceUri && left.LocalName == right.LocalName;
    private static bool AttributeMatchesHtml(HtmlNode html, AttributeTest test)
    {
        var attributes = html.RawAttributes;
        for (var i = 0; i < attributes.Count; i++)
        {
            var attribute = attributes[i];
            if (test.Name.MatchHtmlAttribute(attribute, html.NamespaceUri == HtmlDocument.HtmlNamespace) && ValueMatches(attribute.Value, test))
            {
                return true;
            }
        }

        return false;
    }
    private static Node Next(Node node, Node root)
    {
        var child = node.First;
        if (!child.Absent)
        {
            return child;
        }

        while (!node.Same(root))
        {
            var sibling = node.Next;
            if (!sibling.Absent)
            {
                return sibling;
            }

            node = node.Parent;
        }
        return default;
    }
    private bool Match(Node node, Node scope)
    {
        if (!node.Element)
        {
            return false;
        }

        foreach (var selector in selectors)
        {
            if (MatchComplex(selector, selector.Parts.Length - 1, node, scope))
            {
                return true;
            }
        }

        return false;
    }
    private static bool MatchComplex(Complex selector, int index, Node node, Node scope)
    {
        if (!node.Element || !MatchCompound(selector.Parts[index], node, scope))
        {
            return false;
        }

        if (index == 0)
        {
            return true;
        }

        switch (selector.Links[index - 1])
        {
            case '>':
                return MatchComplex(selector, index - 1, node.Parent, scope);
            case '+':
                return MatchComplex(selector, index - 1, PreviousElement(node), scope);
            case '~':
                for (var previous = PreviousElement(node); !previous.Absent; previous = PreviousElement(previous))
                {
                    if (MatchComplex(selector, index - 1, previous, scope))
                    {
                        return true;
                    }
                }

                return false;
            default:
                for (var parent = node.Parent; !parent.Absent; parent = parent.Parent)
                {
                    if (MatchComplex(selector, index - 1, parent, scope))
                    {
                        return true;
                    }
                }

                return false;
        }
    }
    private static Node PreviousElement(Node node)
    {
        do
        {
            node = node.Previous;
        } while (!node.Absent && !node.Element);
        return node;
    }
    private static bool MatchCompound(Compound compound, Node node, Node scope)
    {
        if (compound.Type is { } type && !type.Match(node.Name, node.Uri, node.Fold))
        {
            return false;
        }

        foreach (var test in compound.Tests)
        {
            if (test.Attribute is { } attribute)
            {
                if (!AttributeMatches(node, attribute))
                {
                    return false;
                }

                continue;
            }
            if (test.Not is { } not)
            {
                foreach (var argument in not)
                {
                    if (MatchCompound(argument, node, scope))
                    {
                        return false;
                    }
                }

                continue;
            }
            switch (test.Pseudo)
            {
                case PseudoKind.Root:
                    if (!node.Same(node.DocumentElement))
                    {
                        return false;
                    }

                    break;
                case PseudoKind.Scope:
                    if (!node.Same(scope))
                    {
                        return false;
                    }

                    break;
                case PseudoKind.Empty:
                    for (var child = node.First; !child.Absent; child = child.Next)
                    {
                        if (child.Element || child.Text)
                        {
                            return false;
                        }
                    }

                    break;
                default:
                    var ofType = test.Pseudo is PseudoKind.FirstOfType or PseudoKind.LastOfType or PseudoKind.OnlyOfType or PseudoKind.NthOfType or PseudoKind.NthLastOfType;
                    var reverse = test.Pseudo is PseudoKind.LastChild or PseudoKind.LastOfType or PseudoKind.NthLastChild or PseudoKind.NthLastOfType;
                    var only = test.Pseudo is PseudoKind.OnlyChild or PseudoKind.OnlyOfType;
                    var nth = test.Pseudo is PseudoKind.NthChild or PseudoKind.NthLastChild or PseudoKind.NthOfType or PseudoKind.NthLastOfType;
                    long position = 1, total = 1;
                    if (!reverse || only)
                    {
                        for (var sibling = node.Previous; !sibling.Absent; sibling = sibling.Previous)
                        {
                            if (sibling.Element && (!ofType || SameType(node, sibling)))
                            {
                                position++;
                            }
                        }
                    }

                    if (reverse || only)
                    {
                        for (var sibling = node.Next; !sibling.Absent; sibling = sibling.Next)
                        {
                            if (sibling.Element && (!ofType || SameType(node, sibling)))
                            {
                                total++;
                            }
                        }
                    }

                    if (only)
                    {
                        if (total + position - 1 != 1)
                        {
                            return false;
                        }
                    }
                    else if (nth)
                    {
                        var difference = (reverse ? total : position) - test.B;
                        if (test.A == 0 ? difference != 0 : difference % test.A != 0 || difference / test.A < 0)
                        {
                            return false;
                        }
                    }
                    else if ((reverse ? total : position) != 1)
                    {
                        return false;
                    }

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
            return AttributeMatchesHtml(html, test);
        }

        foreach (var attribute in node.Xml.Attributes)
        {
            if (test.Name.Match(attribute.LocalName, attribute.NamespaceUri, false) && ValueMatches(attribute.Value, test))
            {
                return true;
            }
        }
        return false;
    }
    private static bool ValueMatches(string value, AttributeTest test)
    {
        var target = test.Value.AsSpan();
        return test.Operator switch
        {
            AttrOp.Present => true,
            AttrOp.Equals => value == test.Value,
            AttrOp.Token => ContainsToken(value, test.Value),
            AttrOp.DashPrefix => value == test.Value || value.Length > target.Length && value[target.Length] == '-' && value.AsSpan(0, target.Length).SequenceEqual(target),
            AttrOp.Prefix => !target.IsEmpty && value.AsSpan().StartsWith(target, StringComparison.Ordinal),
            AttrOp.Suffix => !target.IsEmpty && value.AsSpan().EndsWith(target, StringComparison.Ordinal),
            _ => !target.IsEmpty && value.AsSpan().Contains(target, StringComparison.Ordinal),
        };
    }
    private static bool ContainsToken(string value, string token)
    {
        var target = token.AsSpan();
        if (target.IsEmpty || value.Length < target.Length)
        {
            return false;
        }

        if (value == token)
        {
            return true;
        }

        var span = value.AsSpan();
        var i = 0;
        while (i < span.Length)
        {
            while (i < span.Length && White(span[i]))
            {
                i++;
            }

            var start = i;
            while (i < span.Length && !White(span[i]))
            {
                i++;
            }

            if (i - start == target.Length && span.Slice(start, target.Length).SequenceEqual(target))
            {
                return true;
            }
        }
        return false;
    }
    private static bool Equal(string left, string right, bool fold)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        for (var i = 0; i < left.Length; i++)
        {
            if ((fold ? Lower(left[i]) : left[i]) != (fold ? Lower(right[i]) : right[i]))
            {
                return false;
            }
        }

        return true;
    }
    private static char Lower(char c) => c is >= 'A' and <= 'Z' ? (char)(c + 32) : c;
    private static bool White(char c) => c is ' ' or '\t' or '\r' or '\n' or '\f';
    private readonly struct Node
    {
        public readonly XmlNode Xml;
        public readonly HtmlNode? Html;
        public Node(XmlNode node)
        {
            Xml = node;
            Html = null;
        }
        public Node(HtmlNode? node)
        {
            Html = node;
            Xml = default;
        }
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
                if (Absent)
                {
                    return default;
                }

                var document = Html is { } h ? new Node(h.OwnerDocument.DocumentNode) : new Node(Xml.OwnerDocument.DocumentNode);
                for (var child = document.First; !child.Absent; child = child.Next)
                {
                    if (child.Element)
                    {
                        return child;
                    }
                }

                return default;
            }
        }
    }
    private enum AttrOp : byte
    {
        Present, Equals, Token, DashPrefix, Prefix, Suffix, Substring
    }
    private enum PseudoKind : byte
    {
        None, Root, Scope, Empty, FirstChild, LastChild, OnlyChild, FirstOfType, LastOfType, OnlyOfType, NthChild, NthLastChild, NthOfType, NthLastOfType
    }
    private sealed record NameTest(string Local, string FoldedLocal, string? Namespace)
    {
        public bool Match(string name, string uri, bool fold) => (Namespace is null || Namespace == uri) && (Local == "*" || Equal(Local, name, fold));
        public bool MatchHtmlElement(HtmlNode node) =>
            (Namespace is null || Namespace == node.NamespaceUri) &&
            (Local == "*" || (node.NamespaceUri == HtmlDocument.HtmlNamespace ? FoldedLocal == node.LocalName : Local == node.LocalName));
        public bool MatchHtmlAttribute(HtmlAttribute attribute, bool htmlElement) =>
            (Namespace is null || Namespace == attribute.NamespaceUri) &&
            (htmlElement && attribute.NamespaceUri.Length == 0 ? FoldedLocal == attribute.LocalName : Local == attribute.LocalName);
    }
    private sealed record AttributeTest(NameTest Name, AttrOp Operator, string Value);
    private sealed record Test(AttributeTest? Attribute = null, PseudoKind Pseudo = PseudoKind.None, long A = 0, long B = 0, Compound[]? Not = null);
    private sealed record Compound(NameTest? Type, Test[] Tests);
    private sealed record Complex(Compound[] Parts, char[] Links);
}
