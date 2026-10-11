using Xemele.Html;
using Xunit;
namespace Xemele.Tests.HtmlTreeConstruction;
public sealed class StandaloneWptTests
{
    [Fact]
    public void CdataInIntegrationPointFragment()
    {
        foreach (var (ns, names) in new[] { ("http://www.w3.org/1998/Math/MathML", new[] { "mi", "mo", "mn", "ms", "mtext", "annotation-xml" }), ("http://www.w3.org/2000/svg", new[] { "foreignObject", "desc", "title", "path" }) })
        foreach (var name in names) foreach (var prefix in new[] { "", "x" })
        {
            var owner = new HtmlDocument(); var fragment = Html5Parser.ParseFragment(prefix + "<![CDATA[y]]>", new(owner.CreateElement(name, ns)));
            Assert.Equal(prefix + "y", fragment.GetTextContent()); Assert.Single(fragment.Children); Assert.Equal(HtmlNodeKind.Text, fragment.FirstChild!.Kind);
        }
        foreach (var prefix in new[] { "", "x" })
        {
            var owner = new HtmlDocument(); var fragment = Html5Parser.ParseFragment(prefix + "<![CDATA[y]]>", new(owner.CreateElement("div")));
            Assert.Equal(prefix, fragment.GetTextContent()); Assert.Equal(prefix.Length + 1, fragment.Children.Count); Assert.Equal(HtmlNodeKind.Comment, fragment.Children[^1].Kind); Assert.Equal("[CDATA[y]]", fragment.Children[^1].Value);
        }
    }
    [Fact]
    public void DoctypeSystemIdentifierDistinction()
    {
        string root = Path.Combine(BuilderInputTests.FindRoot(), "tests/fixtures/M7-builder/wpt-parsing/support");
        foreach (string type in new[] { "frameset", "transitional" }) foreach (string id in new[] { "missing", "empty", "nonempty" })
        {
            var document = Html5Parser.Parse(File.ReadAllText(Path.Combine(root, $"doctype-system-id-{id}-{type}.html")));
            Assert.Equal(id == "nonempty" ? HtmlQuirksMode.LimitedQuirks : HtmlQuirksMode.Quirks, document.QuirksMode);
        }
    }
    [Fact]
    public void AdoptionAgencyChecksEndTagName()
    {
        const string markup = "<code some-attribute=\"\"><div><code><code><code><code></code></code></code></code></div></code>";
        var owner = new HtmlDocument(); var fragment = Html5Parser.ParseFragment(markup, new(owner.CreateElement("div")));
        var code = fragment.FirstChild!; Assert.Equal("code", code.LocalName); Assert.Equal("div", code.FirstChild!.LocalName); Assert.Single(fragment.Children);
        var nested = code.FirstChild.FirstChild!; for (int i = 0; i < 4; i++) { Assert.Equal("code", nested.LocalName); if (i == 3) Assert.Empty(nested.Children); else { Assert.Single(nested.Children); nested = nested.FirstChild!; } }
    }
    [Fact]
    public void StaticDoctypeAndTruncatedMarkup()
    {
        string root = Path.Combine(BuilderInputTests.FindRoot(), "tests/fixtures/M7-builder/wpt-parsing");
        Assert.Equal(HtmlQuirksMode.NoQuirks, Html5Parser.Parse(File.ReadAllText(Path.Combine(root, "empty-doctype-ids.html"))).QuirksMode);
        foreach (var name in new[] { "no-doctype-name.html", "support/no-doctype-name-space.html", "support/no-doctype-name-line.html", "support/no-doctype-name-eof.html" })
            Assert.Equal("", Html5Parser.Parse(File.ReadAllText(Path.Combine(root, name))).DocumentNode.Children.First(n => n.Kind == HtmlNodeKind.DocumentType).Doctype!.Name);
        var doc = Html5Parser.Parse("Abc<!d>Hi"); var body = doc.RootElement.Children[1];
        Assert.Equal(new[] { HtmlNodeKind.Text, HtmlNodeKind.Comment, HtmlNodeKind.Text }, body.Children.Select(n => n.Kind));
        Assert.Equal(new[] { "Abc", "d", "Hi" }, body.Children.Select(n => n.Value));
        Assert.Equal("html", Html5Parser.Parse("<!DOCTYPE html pu").DocumentNode.Children.First(n => n.Kind == HtmlNodeKind.DocumentType).Doctype!.Name);
    }
    [Fact]
    public void StaticMisnestedFormInsideTemplate()
    {
        var doc = Html5Parser.Parse("<div>A<template><br>BC<form>D<div>E</form>F</div>G</template>H</div>");
        var template = Descendants(doc.DocumentNode).First(n => n.LocalName == "template"); var content = template.TemplateContent!;
        Assert.Equal(new[] { "br", "", "form", "" }, content.Children.Select(n => n.LocalName));
        var form = content.Children[2]; Assert.Equal("D", form.FirstChild!.Value); Assert.Equal("div", form.Children[1].LocalName); Assert.Equal("E", form.Children[1].GetTextContent()); Assert.Equal("FG", content.Children[3].Value);
    }
    [Fact]
    public void StaticIntegrationRawTextAndDisabledNoscriptHead()
    {
        string root = Path.Combine(BuilderInputTests.FindRoot(), "tests/fixtures/M7-builder/wpt-parsing");
        var doc = Html5Parser.Parse(File.ReadAllText(Path.Combine(root, "html-integration-point.html")));
        foreach (var point in Descendants(doc.DocumentNode).Where(n => n.Attributes.Any(a => a.LocalName == "id" && a.Value.StartsWith("point-", StringComparison.Ordinal))))
        { Assert.NotEqual("http://www.w3.org/1999/xhtml", point.NamespaceUri); Assert.Equal("http://www.w3.org/1999/xhtml", point.FirstChild!.NamespaceUri); Assert.StartsWith("&lt;", point.FirstChild.GetTextContent(), StringComparison.Ordinal); }
        var noscript = Descendants(Html5Parser.Parse("<!DOCTYPE html><head><noscript><head><style></style>", new() { ScriptingEnabled = false }).DocumentNode).First(n => n.LocalName == "noscript");
        Assert.Equal("style", noscript.FirstChild!.LocalName);
    }
    [Fact]
    public void StaticTemplateFosterParentPositions()
    {
        const string rows = "<tr><td>Cell 1</td></tr><div id=orphanDiv>Orphan div content</div><tr><td>Cell 2</td></tr>";
        foreach (var (markup, expected) in new[] {
            ("<div><template><table>" + rows + "</table></template></div>", "#template-content"),
            ("<div><template>" + rows + "</template></div>", "#template-content"),
            ("<div><template><div id=fosterParent><table>" + rows + "</table></div></template></div>", "div"),
            ("<div id=fosterParent><table><tr><td><template>Template content</template></td></tr><div id=orphanDiv>Orphan div content</div><tr><td>Cell 2</td></tr></table></div></div>", "div") })
        {
            var owner = new HtmlDocument(); var fragment = Html5Parser.ParseFragment(markup, new(owner.CreateElement("body")));
            var orphan = Descendants(fragment, true).First(n => n.Attributes.Any(a => a.LocalName == "id" && a.Value == "orphanDiv"));
            Assert.Equal(expected == "#template-content" ? HtmlNodeKind.Fragment : HtmlNodeKind.Element, orphan.Parent!.Kind);
            if (expected == "div") Assert.Equal("fosterParent", orphan.Parent.Attributes.First(a => a.LocalName == "id").Value);
        }
    }
    [Fact]
    public void StaticForeignDomPropertiesAndHtmlBreakouts()
    {
        var svg = Descendants(Html5Parser.Parse("<svg xmlns:xlink='http://www.w3.org/1999/xlink'><use xlink:href='#test'></use></svg>").DocumentNode).First(n => n.LocalName == "svg");
        Assert.Equal("http://www.w3.org/2000/svg", svg.NamespaceUri); Assert.Equal("", svg.Prefix);
        var xmlns = svg.Attributes[0]; Assert.Equal("http://www.w3.org/2000/xmlns/", xmlns.NamespaceUri); Assert.Equal("xmlns", xmlns.Prefix); Assert.Equal("xlink", xmlns.LocalName); Assert.Equal("http://www.w3.org/1999/xlink", xmlns.Value);
        var use = svg.FirstChild!; Assert.Equal("use", use.LocalName); Assert.Equal(svg.NamespaceUri, use.NamespaceUri); Assert.Equal("", use.Prefix);
        var href = use.Attributes[0]; Assert.Equal("http://www.w3.org/1999/xlink", href.NamespaceUri); Assert.Equal("xlink", href.Prefix); Assert.Equal("href", href.LocalName); Assert.Equal("#test", href.Value);
        const string names = "/p /br b big blockquote br center code dd div dl dt em embed h1 h2 h3 h4 h5 h6 hr i img li listing menu meta nobr ol p pre ruby s small span strong strike sub sup table tt u ul var";
        foreach (string context in new[] { "svg", "math" }) foreach (string name in names.Split(' '))
        {
            var owner = new HtmlDocument(); var fragment = Html5Parser.ParseFragment($"<{context}><{name}></{context}", new(owner.CreateElement("div")));
            var child = Descendants(fragment).First(n => n.LocalName == name.TrimStart('/')); Assert.Same(fragment, child.Parent); Assert.Equal("http://www.w3.org/1999/xhtml", child.NamespaceUri);
        }
    }
    [Fact]
    public void StaticNewlineNormalizationAndNoscriptTemplate()
    {
        foreach (string source in new[] { "before\rABCDEF\nafter", "before\r\nafter" })
        {
            string expected = source.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
            var div = Descendants(Html5Parser.Parse("<body><div>" + source + "</div></body>").DocumentNode).First(n => n.LocalName == "div"); Assert.Equal(expected, div.GetTextContent());
            foreach (char quote in new[] { '\'', '"' })
            { var input = Descendants(Html5Parser.Parse("<body><input value=" + quote + source + quote + "></body>").DocumentNode).First(n => n.LocalName == "input"); Assert.Equal(expected, input.Attributes[0].Value); }
        }
        var doc = Html5Parser.Parse("<template id=template1><noscript><div></div></noscript></template><template id=template2><noscript><div id=noScriptContent></div></noscript></template>", new() { ScriptingEnabled = true });
        foreach (var template in Descendants(doc.DocumentNode).Where(n => n.LocalName == "template")) { var noscript = template.TemplateContent!.FirstChild!; Assert.Equal("noscript", noscript.LocalName); Assert.Single(noscript.Children); Assert.Equal(HtmlNodeKind.Text, noscript.FirstChild!.Kind); Assert.StartsWith("<div", noscript.GetTextContent(), StringComparison.Ordinal); Assert.DoesNotContain(Descendants(noscript), n => n.Kind == HtmlNodeKind.Element); }
    }
    [Fact]
    public void StaticMathParsingAndCaseAdjustments()
    {
        string root = Path.Combine(BuilderInputTests.FindRoot(), "tests/fixtures/M7-builder/wpt-parsing");
        var doc = Html5Parser.Parse(File.ReadAllText(Path.Combine(root, "math-parse01.html"))); var nodes = Descendants(doc.DocumentNode).ToArray();
        HtmlNode Id(string id) => nodes.First(n => n.Attributes.Any(a => a.LocalName == "id" && a.Value == id));
        Assert.Equal("math", Id("m1").LocalName); Assert.Equal("http://www.w3.org/1998/Math/MathML", Id("d1").FirstChild!.NamespaceUri); Assert.Equal(2, Id("d1").FirstChild!.Children.Count); Assert.Single(Id("d2").FirstChild!.Children); Assert.Equal("⟨⟩", Id("d3").GetTextContent()); Assert.Equal("𝕂", Id("d4").GetTextContent()); Assert.Equal(2, Id("d5").FirstChild!.FirstChild!.Children[1].Children.Count); Assert.Equal(2, Id("d6").FirstChild!.Children.Count);
        doc = Html5Parser.Parse(File.ReadAllText(Path.Combine(root, "math-parse03.html"))); nodes = Descendants(doc.DocumentNode).ToArray();
        var mi = Id("d1").FirstChild!.FirstChild!; Assert.Equal("mi", mi.LocalName); Assert.Equal("http://www.w3.org/1998/Math/MathML", mi.NamespaceUri); Assert.Equal("BOLD", mi.Attributes.First(a => a.LocalName == "mathvariant").Value); Assert.Equal("www.example.org/FOO", Id("d2").FirstChild!.FirstChild!.Attributes.First(a => a.LocalName == "definitionURL").Value);
        foreach (string id in new[] { "m3span-mtext", "m3span-mi", "m3span-mrow", "m3p-mtext", "m3p-mi", "m4", "m5" }) Assert.Equal("http://www.w3.org/1999/xhtml", Id(id).FirstChild!.FirstChild!.NamespaceUri);
        Assert.Equal(new[] { "math", "p", "mi" }, Id("d3p-mrow").Children.Select(n => n.LocalName)); Assert.Single(Id("d3p-mrow").FirstChild!.Children); Assert.Empty(Id("d3p-mrow").FirstChild!.FirstChild!.Children);
        foreach (string id in new[] { "p6default", "p6foo" }) Assert.Equal("div", Id(id).Parent!.LocalName);
        foreach (string id in new[] { "p6texthtml", "p6uctexthtml", "p6applicationxhtmlxml" }) Assert.Equal("annotation-xml", Id(id).Parent!.LocalName);
    }
    private static IEnumerable<HtmlNode> Descendants(HtmlNode node, bool templates = false)
    {
        foreach (var child in node.Children) { yield return child; foreach (var descendant in Descendants(child, templates)) yield return descendant; }
        if (templates && node.TemplateContent is { } content) { yield return content; foreach (var child in Descendants(content, true)) yield return child; }
    }
}
