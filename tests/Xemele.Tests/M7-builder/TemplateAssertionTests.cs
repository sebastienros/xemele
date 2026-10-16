using System.Text.Json;
using Xemele.Html;
using Xunit;

namespace Xemele.Tests.HtmlTreeConstruction;

public sealed class TemplateAssertionTests
{
    [Fact]
    public void OriginalStaticTemplateAssertionsAndAllRetainedSupportFiles()
    {
        var fixtures = Path.Combine(BuilderInputTests.FindRoot(), "tests/fixtures/M7-builder");
        using var vectors = JsonDocument.Parse(File.ReadAllText(Path.Combine(fixtures, "template-assertions.json")));
        int configurations = 0, assertions = 0;
        var supports = new HashSet<string>(StringComparer.Ordinal);
        foreach (var vector in vectors.RootElement.GetProperty("cases").EnumerateArray())
        {
            var context = vector.GetProperty("context").GetString()!;
            HtmlDocument doc;
            HtmlNode? created = null;
            if (context == "document")
            {
                var isSupport = vector.TryGetProperty("support", out var supportName);
                var name = (isSupport ? supportName : vector.GetProperty("source")).GetString()!;
                if (isSupport)
                {
                    supports.Add(name);
                }

                doc = Html5Parser.Parse(File.ReadAllText(Path.Combine(fixtures, isSupport ? "wpt-template-support" : "wpt-parsing", name)));
            }
            else
            {
                var owner = new HtmlDocument();
                var fragment = Html5Parser.ParseFragment(vector.GetProperty("html").GetString()!, new(owner.CreateElement(context)));
                doc = fragment.OwnerDocument;
                var html = doc.RootElement;
                var head = doc.CreateElement("head");
                var body = doc.CreateElement("body");
                html.AppendChild(head);
                html.AppendChild(body);
                if (context == "template")
                {
                    created = doc.CreateElement("template");
                    body.AppendChild(created);
                    created.TemplateContent!.AppendChild(fragment);
                }
                else
                {
                    (context == "head" ? head : body).AppendChild(fragment);
                }
            }
            foreach (var assertion in vector.GetProperty("assertions").EnumerateArray())
            {
                var actual = Evaluate(assertion.GetProperty("actual"), doc, created);
                var expected = Evaluate(assertion.GetProperty("expected"), doc, created);
                var equal = Equals(actual, expected);
                var source = vector.GetProperty("file").GetString() + ": " + vector.GetProperty("title").GetString();
                Assert.True(assertion.GetProperty("kind").GetString() == "equal" ? equal : !equal,
                    source + "; " + assertion.GetProperty("message").GetString() + $"; actual={actual ?? "null"}, expected={expected ?? "null"}");
                assertions++;
            }
            configurations++;
        }
        Assert.Equal(15, supports.Count);
        Assert.Equal(336, configurations);
        Assert.Equal(899, assertions);
    }

    private static object? Evaluate(JsonElement expression, HtmlDocument doc, HtmlNode? created)
    {
        if (expression.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (expression.ValueKind == JsonValueKind.String)
        {
            return expression.GetString();
        }

        if (expression.ValueKind == JsonValueKind.Number)
        {
            return expression.GetInt32();
        }

        if (expression.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return expression.GetBoolean();
        }

        object? value = null;
        foreach (var step in expression.GetProperty("steps").EnumerateArray())
        {
            var key = step[0].GetString()!;
            if (key == "doc")
            {
                value = doc.DocumentNode;
                continue;
            }
            if (key == "created")
            {
                value = created ?? throw new InvalidOperationException("Missing created context");
                continue;
            }
            if (key == "length")
            {
                value = ((IReadOnlyList<HtmlNode>)value!).Count;
                continue;
            }
            if (int.TryParse(key, out var index))
            {
                value = value is IReadOnlyList<HtmlAttribute> attributes ? attributes[index] : ((IReadOnlyList<HtmlNode>)value!)[index];
                continue;
            }
            if (value is HtmlAttribute attr)
            {
                value = key switch
                {
                    "namespaceURI" => attr.NamespaceUri,
                    "prefix" => attr.Prefix.Length == 0 ? null : attr.Prefix,
                    "localName" => attr.LocalName,
                    "name" => attr.Prefix.Length == 0 ? attr.LocalName : attr.Prefix + ":" + attr.LocalName,
                    "value" => attr.Value,
                    _ => throw new InvalidOperationException("Unrecognized attribute property " + key)
                };
                continue;
            }
            var node = (HtmlNode?)value ?? throw new InvalidOperationException("Null receiver for " + key);
            string Arg() => step[1].GetString()!;
            value = key switch
            {
                "body" => doc.RootElement.Children.First(n => n.LocalName == "body"),
                "head" => doc.RootElement.Children.First(n => n.LocalName == "head"),
                "documentElement" => doc.RootElement,
                "querySelector" => Descendants(node).FirstOrDefault(n => n.Kind == HtmlNodeKind.Element &&
                    (Arg().StartsWith('#') ? Attribute(n, "id") == Arg()[1..] : n.LocalName == Arg())),
                "getElementById" => Descendants(node).FirstOrDefault(n => n.Kind == HtmlNodeKind.Element && Attribute(n, "id") == Arg()),
                "getElementsByTagName" => Descendants(node).Where(n => n.Kind == HtmlNodeKind.Element && n.LocalName == (n.NamespaceUri == "http://www.w3.org/1999/xhtml" ? Arg().ToLowerInvariant() : Arg())).ToArray(),
                "getElementsByTagNameNS" => Descendants(node).Where(n => n.Kind == HtmlNodeKind.Element && n.NamespaceUri == Arg() && n.LocalName == step[2].GetString()).ToArray(),
                "getAttribute" => Attribute(node, Arg()),
                "hasAttribute" => Attribute(node, Arg()) is not null,
                "attributes" => node.Attributes,
                "namespaceURI" => node.NamespaceUri,
                "localName" => node.LocalName,
                "prefix" => node.Prefix.Length == 0 ? null : node.Prefix,
                "nodeValue" => node.Value,
                "content" => node.TemplateContent,
                "childNodes" => node.Children,
                "children" => node.Children.Where(n => n.Kind == HtmlNodeKind.Element).ToArray(),
                "firstChild" => node.FirstChild,
                "lastChild" => node.LastChild,
                "parentNode" => node.Parent,
                "ownerDocument" => node.OwnerDocument,
                "textContent" => node.GetTextContent(),
                "tagName" or "nodeName" => node.NamespaceUri == "http://www.w3.org/1999/xhtml" ? node.LocalName.ToUpperInvariant() : node.LocalName,
                "nodeType" => node.Kind == HtmlNodeKind.Text ? 3 : node.Kind == HtmlNodeKind.Element ? 1 : 11,
                "id" => Attribute(node, "id") ?? "",
                "rows" => Rows(node).ToArray(),
                "cells" => node.Children.Where(n => n.LocalName is "td" or "th").ToArray(),
                "tBodies" => node.Children.Where(n => n.LocalName == "tbody").ToArray(),
                "caption" => node.Children.FirstOrDefault(n => n.LocalName == "caption"),
                "tFoot" => node.Children.FirstOrDefault(n => n.LocalName == "tfoot"),
                "tHead" => node.Children.FirstOrDefault(n => n.LocalName == "thead"),
                _ => throw new InvalidOperationException("Unrecognized original assertion property " + key)
            };
        }
        return value;
    }
    private static string? Attribute(HtmlNode node, string name) => node.Attributes.FirstOrDefault(a => a.LocalName == name)?.Value;
    private static IEnumerable<HtmlNode> Rows(HtmlNode table)
    {
        foreach (var child in table.Children)
        {
            if (child.LocalName == "tr")
            {
                yield return child;
            }
            else if (child.LocalName is "tbody" or "thead" or "tfoot")
            {
                foreach (var row in child.Children.Where(n => n.LocalName == "tr"))
                {
                    yield return row;
                }
            }
        }
    }
    private static IEnumerable<HtmlNode> Descendants(HtmlNode node)
    {
        foreach (var child in node.Children)
        {
            yield return child;
            foreach (var descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }
}
