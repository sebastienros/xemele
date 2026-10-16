using System.Globalization;
using Xemele.Css;
using Xemele.Html;
using Xunit;

namespace Xemele.Tests.Css;

public sealed class SelectorTests
{
    private static (XmlDocument X, HtmlDocument H) Trees()
    {
        var x = new XmlDocument();
        var h = new HtmlDocument();
        var xr = x.CreateElement("root");
        var hr = h.CreateElement("root");
        x.DocumentNode.AppendChild(xr);
        h.DocumentNode.AppendChild(hr);
        for (var i = 0; i < 4; i++)
        {
            var xe = x.CreateElement(i == 2 ? "other" : "item");
            var he = h.CreateElement(i == 2 ? "other" : "item");
            xr.AppendChild(xe);
            hr.AppendChild(he);
            xe.SetAttribute("id", "i" + i);
            he.SetAttribute(new("id", "i" + i));
            xe.SetAttribute("class", i % 2 == 0 ? "a b" : "b");
            he.SetAttribute(new("class", i % 2 == 0 ? "a b" : "b"));
            xe.SetAttribute("data", "en-US tail");
            he.SetAttribute(new("data", "en-US tail"));
        }
        xr.InsertBefore(x.CreateComment("ignored"), xr.Children[1]);
        hr.InsertBefore(h.CreateComment("ignored"), hr.Children[1]);
        return (x, h);
    }
    [Theory]
    [InlineData("*", 5)]
    [InlineData("root", 1)]
    [InlineData("item", 3)]
    [InlineData("#i1", 1)]
    [InlineData(".a.b", 2)]
    [InlineData("[id]", 4)]
    [InlineData("[id=i0]", 1)]
    [InlineData("[class~=a]", 2)]
    [InlineData("[data|=en]", 4)]
    [InlineData("[data^=en]", 4)]
    [InlineData("[data$=tail]", 4)]
    [InlineData("[data*=US]", 4)]
    [InlineData("[data^='']", 0)]
    [InlineData("[data$='']", 0)]
    [InlineData("[data*='']", 0)]
    [InlineData("[class~='']", 0)]
    [InlineData("root item", 3)]
    [InlineData("root > item", 3)]
    [InlineData("item + item", 1)]
    [InlineData("item ~ item", 2)]
    [InlineData("item, .a, root", 5)]
    [InlineData(":root", 1)]
    [InlineData(":scope", 1)]
    [InlineData(":empty", 4)]
    [InlineData(":first-child", 2)]
    [InlineData("item:last-child", 1)]
    [InlineData("root:only-child", 1)]
    [InlineData("item:nth-child(odd)", 1)]
    [InlineData("item:nth-child(\\6e +1)", 3)]
    [InlineData("item:nth-child(2\\6e +1)", 1)]
    [InlineData("item:nth-child(\\6f dd)", 1)]
    [InlineData("item:nth-child(n-1)", 3)]
    [InlineData("item:nth-child(even)", 2)]
    [InlineData("item:nth-child(2n+1)", 1)]
    [InlineData("item:nth-child(-n+2)", 2)]
    [InlineData("item:nth-child(+2)", 1)]
    [InlineData("item:nth-child(999999999999999999999999999n-999999999999999999999999998)", 1)]
    [InlineData("item:nth-child(-999999999999999999999999999n+999999999999999999999999999)", 0)]
    [InlineData("item:nth-last-child(2n)", 1)]
    [InlineData("item:first-of-type", 1)]
    [InlineData("item:last-of-type", 1)]
    [InlineData("other:only-of-type", 1)]
    [InlineData("item:nth-of-type(2)", 1)]
    [InlineData("item:nth-last-of-type(2)", 1)]
    [InlineData("item:not(.a, #i1)", 1)]
    [InlineData("#\\69 0", 1)]
    [InlineData("i\\74 em", 3)]
    [InlineData("[id='i\\30']", 1)]
    [InlineData("item/**/.b", 3)]
    [InlineData("item:NOT(.a)", 2)]
    public void MatrixOnBothActualModels(string selector, int count)
    {
        var (x, h) = Trees();
        var compiled = CssSelector.Compile(selector);
        Assert.Equal(count, compiled.Select(x.DocumentNode).Count);
        Assert.Equal(count, compiled.Select(h.DocumentNode).Count);
        foreach (var node in compiled.Select(x.DocumentNode))
        {
            Assert.True(compiled.Matches(node));
        }

        foreach (var node in compiled.Select(h.DocumentNode))
        {
            Assert.True(compiled.Matches(node));
        }
    }
    [Theory]
    [InlineData("", 0)]
    [InlineData(" ", 1)]
    [InlineData("item,", 5)]
    [InlineData("item >", 6)]
    [InlineData("item::before", 4)]
    [InlineData(":is(item)", 0)]
    [InlineData(":where(item)", 0)]
    [InlineData(":has(item)", 0)]
    [InlineData(":lang(en)", 0)]
    [InlineData(":dir(ltr)", 0)]
    [InlineData(":hover", 0)]
    [InlineData(":link", 0)]
    [InlineData(":checked", 0)]
    [InlineData(":enabled", 0)]
    [InlineData(":visited", 0)]
    [InlineData(":target", 0)]
    [InlineData("item || item", 5)]
    [InlineData("[id=x i]", 6)]
    [InlineData("[id=x s]", 6)]
    [InlineData(":not(item > x)", 10)]
    [InlineData(":not(:not(item))", 5)]
    [InlineData(":not()", 5)]
    [InlineData(":nth-child(+odd)", 12)]
    [InlineData(":nth-child(2n of item)", 14)]
    [InlineData(":nth-child(2 n)", 13)]
    [InlineData(":nth-child(n + 1 2)", 17)]
    [InlineData("missing|item", 0)]
    [InlineData("[missing|a]", 1)]
    [InlineData("item, :has(x)", 6)]
    [InlineData("[id='a\n']", 6)]
    [InlineData("item/*", 4)]
    [InlineData("#1", 1)]
    [InlineData("item\\", 4)]
    public void RejectionsHaveUtf16Offsets(string selector, int offset) => Assert.Equal(offset, Assert.Throws<CssSelectorException>(() => CssSelector.Compile(selector)).Offset);

    [Fact]
    public void NamespaceOptionsAreSnapshotsAndGraphCaseRulesAreIndependent()
    {
        var x = new XmlDocument();
        var h = new HtmlDocument();
        var xe = x.CreateElement("Item", "http://www.w3.org/1999/xhtml", "p");
        var he = h.CreateElement("Item");
        x.DocumentNode.AppendChild(xe);
        h.DocumentNode.AppendChild(he);
        xe.SetAttribute("DATA", "V");
        he.SetAttribute(new("DATA", "V"));
        var mappings = new Dictionary<string, string> { ["p"] = "http://www.w3.org/1999/xhtml", [""] = "urn:no" };
        var c = CssSelector.Compile("p|ITEM[DATA=V]", new()
        {
            Namespaces = mappings
        });
        mappings["p"] = "bad";
        Assert.False(c.Matches(xe));
        Assert.True(c.Matches(he));
        Assert.True(CssSelector.Compile("p|Item[DATA=V]", new()
        {
            Namespaces = new Dictionary<string, string> { ["p"] = xe.NamespaceUri }
        }).Matches(xe));
        Assert.False(CssSelector.Compile("Item[data=V]").Matches(xe));
        Assert.False(CssSelector.Compile("ITEM[DATA=v]").Matches(he));
        Assert.False(CssSelector.Compile("Item", new()
        {
            Namespaces = mappings
        }).Matches(xe));
        Assert.True(CssSelector.Compile("*|Item").Matches(xe));
        Assert.False(CssSelector.Compile("|Item").Matches(xe));
        xe.SetAttribute("a", "yes", "urn:a", "q");
        he.SetAttribute(new("a", "yes", "urn:a", "q"));
        Assert.True(CssSelector.Compile("[*|a=yes]").Matches(xe));
        Assert.True(CssSelector.Compile("[*|a=yes]").Matches(he));
        Assert.False(CssSelector.Compile("[a]").Matches(xe));
        Assert.False(CssSelector.Compile("[a]").Matches(he));
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            Assert.True(c.Matches(he));
            Assert.False(c.Matches(xe));
        }
        finally { CultureInfo.CurrentCulture = culture; }
        var xf = x.CreateElement("Camel", "urn:f");
        var hf = h.CreateElement("Camel", "urn:f");
        Assert.False(CssSelector.Compile("camel").Matches(xf));
        Assert.False(CssSelector.Compile("camel").Matches(hf));
    }
    [Fact]
    public void ScopeDetachedMutationAndImmutableSnapshots()
    {
        var (x, h) = Trees();
        var c = CssSelector.Compile("item,.a");
        var xs = c.Select(x.DocumentNode);
        var hs = c.Select(h.DocumentNode);
        Assert.Equal(["i0", "i1", "i2", "i3"], xs.Select(n => n.GetAttribute("id")));
        Assert.Equal(["i0", "i1", "i2", "i3"], hs.Select(n => n.GetAttribute("id")));
        Assert.Throws<NotSupportedException>(() => ((IList<XmlNode>)xs).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<HtmlNode>)hs).Clear());
        xs[0].Remove();
        hs[0].Remove();
        xs[0].SetAttribute("id", "new");
        hs[0].SetAttribute(new("id", "new"));
        Assert.Equal(4, xs.Count);
        Assert.Equal(4, hs.Count);
        Assert.Equal("new", xs[0].GetAttribute("id"));
        Assert.Equal("new", hs[0].GetAttribute("id"));
        Assert.Equal(3, c.Select(x.DocumentNode).Count);
        Assert.Equal(3, c.Select(h.DocumentNode).Count);
        Assert.Single(CssSelector.Compile(":scope").Select(xs[0]));
        Assert.Single(CssSelector.Compile(":scope").Select(hs[0]));
        Assert.False(CssSelector.Compile(":root").Matches(xs[0]));
        Assert.False(CssSelector.Compile(":root").Matches(hs[0]));
        Assert.Same(h, hs[0].OwnerDocument);
        Assert.Same(x, xs[0].OwnerDocument);
        var xf = x.CreateFragment();
        var hf = h.CreateFragment();
        xf.AppendChild(xs[0]);
        hf.AppendChild(hs[0]);
        Assert.Empty(CssSelector.Compile(":scope").Select(xf));
        Assert.Empty(CssSelector.Compile(":scope").Select(hf));
        Assert.Single(c.Select(xf));
        Assert.Single(c.Select(hf));
    }
    [Fact]
    public void EmptyNonElementsAndTemplateEdges()
    {
        var x = new XmlDocument();
        var h = new HtmlDocument();
        var xe = x.CreateElement("e");
        var he = h.CreateElement("e");
        xe.AppendChild(x.CreateComment("c"));
        xe.AppendChild(x.CreateProcessingInstruction("p", "v"));
        he.AppendChild(h.CreateComment("c"));
        he.AppendChild(h.CreateProcessingInstruction("p", "v"));
        var c = CssSelector.Compile(":empty");
        Assert.True(c.Matches(xe));
        Assert.True(c.Matches(he));
        xe.AppendChild(x.CreateCData(""));
        he.AppendChild(h.CreateText(""));
        Assert.False(c.Matches(xe));
        Assert.False(c.Matches(he));
        var template = h.CreateElement("template");
        template.TemplateContent!.AppendChild(h.CreateElement("hidden"));
        Assert.True(c.Matches(template));
        Assert.Empty(CssSelector.Compile("hidden").Select(template));
        Assert.False(c.Matches(default(XmlNode)));
        Assert.False(c.Matches(x.DocumentNode));
        Assert.False(c.Matches(h.DocumentNode));
        Assert.Throws<ArgumentException>(() => c.Select(default(XmlNode)));
        Assert.Throws<ArgumentNullException>(() => c.Select((HtmlNode)null!));
        Assert.Throws<ArgumentNullException>(() => c.Matches((HtmlNode)null!));
        Assert.Throws<ArgumentNullException>(() => CssSelector.Compile(null!));
        Assert.Empty(c.Select(x.CreateText(" ")));
        Assert.Empty(c.Select(h.CreateText(" ")));
        Assert.Empty(CssSelector.Compile(":scope").Select(x.DocumentNode));
        Assert.Empty(CssSelector.Compile(":scope").Select(h.DocumentNode));
    }
    [Fact]
    public void ExpandedTypeNamesExcludePrefixesAndXmlNamespaceDeclarations()
    {
        var x = new XmlDocument();
        var h = new HtmlDocument();
        var xr = x.CreateElement("r");
        var hr = h.CreateElement("r");
        x.DocumentNode.AppendChild(xr);
        h.DocumentNode.AppendChild(hr);
        var a = x.CreateElement("Item", "urn:a", "a");
        var b = x.CreateElement("Item", "urn:a", "b");
        var c = x.CreateElement("item", "urn:a");
        var d = x.CreateElement("Item", "urn:b");
        foreach (var node in new[] { a, b, c, d })
        {
            xr.AppendChild(node);
        }

        a.SetNamespaceDeclaration("p", "urn:p");
        Assert.False(CssSelector.Compile("[p]").Matches(a));
        Assert.False(CssSelector.Compile("[*|p]").Matches(a));
        Assert.True(CssSelector.Compile("Item:nth-of-type(2)").Matches(b));
        Assert.True(CssSelector.Compile("item:only-of-type").Matches(c));
        Assert.True(CssSelector.Compile("Item:only-of-type").Matches(d));
        var ha = h.CreateElement("Item");
        var hb = h.CreateElement("ITEM");
        var hc = h.CreateElement("Item", "urn:a");
        hr.AppendChild(ha);
        hr.AppendChild(hb);
        hr.AppendChild(hc);
        Assert.True(CssSelector.Compile("ITEM:nth-of-type(2)").Matches(hb));
        Assert.True(CssSelector.Compile("Item:only-of-type").Matches(hc));
        Assert.False(CssSelector.Compile("item:only-of-type").Matches(hc));
        Assert.True(CssSelector.Compile("[|data]", new()
        {
            Namespaces = new Dictionary<string, string> { [""] = "urn:a" }
        }).Matches(Attribute(a)));
        static XmlNode Attribute(XmlNode n)
        {
            n.SetAttribute("data", "v");
            return n;
        }
    }
    [Fact]
    public void EscapesStringsUnicodeAndCssWhitespace()
    {
        var x = new XmlDocument();
        var h = new HtmlDocument();
        var xe = x.CreateElement("é");
        var he = h.CreateElement("é");
        xe.SetAttribute("id", "😀");
        he.SetAttribute(new("id", "😀"));
        xe.SetAttribute("class", "a\tb\rc\nd e");
        he.SetAttribute(new("class", "a\tb\rc\nd\fe"));
        xe.SetAttribute("data", "a'b\"c");
        he.SetAttribute(new("data", "a'b\"c"));
        foreach (var selector in new[] { "é", "\\e9", "#\\1f600", ".a.b.c.d.e", "[data=\"a'b\\\"c\"]" })
        {
            var compiled = CssSelector.Compile(selector);
            Assert.True(compiled.Matches(xe));
            Assert.True(compiled.Matches(he));
        }
        xe.SetAttribute("class", "a\u00a0b");
        he.SetAttribute(new("class", "a\u00a0b"));
        Assert.False(CssSelector.Compile(".a").Matches(xe));
        Assert.False(CssSelector.Compile(".a").Matches(he));
        Assert.False(CssSelector.Compile(".a\\ b").Matches(xe));
        var replacementXml = x.CreateElement("�");
        var replacementHtml = h.CreateElement("�");
        foreach (var selector in new[] { "\0", "\uD800", "\\0", "\\d800", "\\110000" })
        {
            var compiled = CssSelector.Compile(selector);
            Assert.True(compiled.Matches(replacementXml));
            Assert.True(compiled.Matches(replacementHtml));
        }
        Assert.Equal(3, Assert.Throws<CssSelectorException>(() => CssSelector.Compile("é, ::before")).Offset);
    }

    [Fact]
    public void PublicConstructorIsUniversalOnBothGraphs()
    {
        var (x, h) = Trees();
        var constructed = new CssSelector();
        var compiled = CssSelector.Compile("*");
        Assert.Equal(compiled.Select(x.DocumentNode), constructed.Select(x.DocumentNode));
        Assert.Equal(compiled.Select(h.DocumentNode), constructed.Select(h.DocumentNode));
        Assert.False(constructed.Matches(x.DocumentNode));
        Assert.False(constructed.Matches(h.DocumentNode));
        Assert.False(constructed.Matches(default(XmlNode)));
        Assert.True(constructed.Matches(x.CreateElement("Item", "urn:foreign")));
        Assert.True(constructed.Matches(h.CreateElement("Item", "urn:foreign")));
        var xe = x.CreateElement("detached");
        var he = h.CreateElement("detached");
        xe.AppendChild(x.CreateElement("child"));
        he.AppendChild(h.CreateElement("child"));
        Assert.Equal(compiled.Select(xe), constructed.Select(xe));
        Assert.Equal(compiled.Select(he), constructed.Select(he));
        Assert.Same(x, constructed.Select(xe)[0].OwnerDocument);
        Assert.Same(h, constructed.Select(he)[0].OwnerDocument);
        Assert.Throws<ArgumentException>(() => constructed.Select(default(XmlNode)));
        Assert.Throws<ArgumentNullException>(() => constructed.Matches((HtmlNode)null!));
        Assert.Throws<ArgumentNullException>(() => constructed.Select((HtmlNode)null!));
        var options = new CssSelectorOptions { Namespaces = new Dictionary<string, string> { [""] = "urn:no" } };
        Assert.Empty(CssSelector.Compile("*", options).Select(x.DocumentNode));
        Assert.Equal(5, constructed.Select(x.DocumentNode).Count);
    }

}
