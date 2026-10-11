using Xemele.Html;
using Xunit;
namespace Xemele.Tests.HtmlTreeConstruction;
public sealed class BuilderBehaviorTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NoscriptFragmentHasNoLastStartTag(bool scripting)
    {
        // Fragment context is not a start-tag token. WHATWG fragment steps select
        // RAWTEXT, but the appropriate-end-tag algorithm has no last start tag.
        const string markup = "a</noscript><b>x";
        var owner = new HtmlDocument(); var context = owner.CreateElement("noscript");
        var fragment = Html5Parser.ParseFragment(markup, new(context), new() { ScriptingEnabled = scripting });
        Assert.Empty(context.Children);
        Assert.Equal(scripting, fragment.OwnerDocument.ScriptingEnabled);
        if (scripting)
        {
            Assert.Single(fragment.Children); Assert.Equal(HtmlNodeKind.Text, fragment.FirstChild!.Kind);
            Assert.Equal(markup, fragment.FirstChild.Value);
        }
        else
        {
            Assert.Equal(2, fragment.Children.Count); Assert.Equal("a", fragment.FirstChild!.Value);
            Assert.Equal("b", fragment.Children[1].LocalName); Assert.Equal("x", fragment.Children[1].GetTextContent());
        }
    }

    [Fact]
    public void CorrectedTreeFormattingFosterTemplatesAndFramesets()
    {
        var d = Html5Parser.Parse("<!doctype html><p><b>one<i>two</b>three</i><table>x<tr><td>y</table><template><tr><td>z</template>");
        var body = d.RootElement.Children.Single(n => n.LocalName == "body");
        Assert.Equal("onetwothreexy", body.GetTextContent());
        var template = body.Children.Single(n => n.LocalName == "template"); Assert.Empty(template.Children); Assert.Equal("z", template.TemplateContent!.GetTextContent());
        var p = body.Children[0]; Assert.Equal("b", p.Children[0].LocalName); Assert.Equal("i", p.Children[1].LocalName); Assert.Equal("three", p.Children[1].GetTextContent());
        var frames = Html5Parser.Parse("<!doctype html><frameset><frame></frameset>"); Assert.Equal(new[] { "head", "frameset" }, frames.RootElement.Children.Select(n => n.LocalName));
        Assert.Equal(HtmlQuirksMode.NoQuirks, d.QuirksMode); Assert.Equal(HtmlQuirksMode.Quirks, Html5Parser.Parse("x").QuirksMode);
        Assert.Equal(HtmlQuirksMode.LimitedQuirks, Html5Parser.Parse("<!doctype html PUBLIC '-//W3C//DTD XHTML 1.0 Transitional//EN'>").QuirksMode);
        Assert.Equal(HtmlQuirksMode.Quirks, Html5Parser.Parse("<!doctype html PUBLIC '-//W3C//DTD HTML 4.01 Transitional//EN' ''>").QuirksMode);
    }
    [Fact]
    public void ContextSnapshotFreshOwnerAndFormTemplateSemantics()
    {
        var owner = new HtmlDocument(); var form = owner.CreateElement("form"); var table = owner.CreateElement("table"); form.AppendChild(table); owner.DocumentNode.AppendChild(form);
        var fragment = Html5Parser.ParseFragment("<form><tr><td>x", new(table));
        Assert.NotSame(owner, fragment.OwnerDocument); Assert.Null(fragment.Parent); Assert.Equal(HtmlNodeKind.Fragment, fragment.Kind);
        Assert.Empty(table.Children); Assert.Equal("tbody", fragment.FirstChild!.LocalName); Assert.Equal("x", fragment.GetTextContent());
        Assert.Throws<ArgumentException>(() => table.AppendChild(fragment)); table.AppendChild(owner.Import(fragment)); Assert.Equal("x", table.GetTextContent());
        var template = owner.CreateElement("template"); var content = Html5Parser.ParseFragment("<form><form>x", new(template)); Assert.Equal("form", content.FirstChild!.FirstChild!.LocalName); Assert.Empty(template.TemplateContent!.Children);
        Assert.Throws<ArgumentException>(() => Html5Parser.ParseFragment("x", new(owner.CreateComment("x"))));
        var foreign = owner.CreateElement("foreignObject", "http://www.w3.org/2000/svg"); var mixed = Html5Parser.ParseFragment("<p>x<svg viewbox='0'><text>y", new(foreign)); Assert.Equal("http://www.w3.org/1999/xhtml", mixed.FirstChild!.NamespaceUri); Assert.Equal("viewBox", mixed.FirstChild.Children[1].Attributes[0].LocalName);
    }
    [Theory]
    [InlineData("<select><button><selectedcontent></button><option>X", "X")]
    [InlineData("<select><button><selectedcontent></button><option>X<option selected>Y", "Y")]
    [InlineData("<select><button><selectedcontent></button><option selected>X<option selected>Y</select>", "Y")]
    [InlineData("<select multiple><button><selectedcontent></button><option selected>X", "")]
    [InlineData("<select size=2><button><selectedcontent></button><option>X", "")]
    [InlineData("<select><button><selectedcontent></button><option disabled>X<option>Y", "Y")]
    [InlineData("<select><button><selectedcontent></button><optgroup disabled><option>X</optgroup><option>Y", "Y")]
    [InlineData("<select><button><selectedcontent></button><optgroup><option>X</optgroup><option>Y", "X")]
    [InlineData("<select><button><selectedcontent></button><option label=ignored>X</option>", "X")]
    public void SelectedContentUsesInsertionImplicitExplicitAndEofPopHooks(string input, string expected)
    {
        var select = Html5Parser.Parse(input).RootElement.Children[1].FirstChild!;
        var content = select.FirstChild!.FirstChild!; Assert.Equal("selectedcontent", content.LocalName); Assert.Equal(expected, content.GetTextContent());
        foreach (var child in content.Children) Assert.Same(content.OwnerDocument, child.OwnerDocument);
    }

    [Theory]
    [InlineData("<body><div><?marker name='x'?></div><template for=x><b>new</b></template>", "new")]
    [InlineData("<body><div><?start name='x'?>old<?end?></div><template for=x>new</template>", "new")]
    [InlineData("<body><div><?start name='x'?>old</div><template for=x>new</template>", "new")]
    [InlineData("<body><div><?marker name='x' name='x'?>old</div><template for=x>new</template>", "old")]
    [InlineData("<body><div><?marker name='&#120;'?></div><template for=x>new</template>", "new")]
    public void TemplateForPatchesPiMarkerRangesWithoutScript(string input, string text)
    {
        var body = Html5Parser.Parse(input).RootElement.Children[1]; Assert.Equal(text, body.Children[0].GetTextContent());
        if (text == "new") { Assert.Single(body.Children); Assert.DoesNotContain(body.Children[0].Children, n => n.Kind == HtmlNodeKind.ProcessingInstruction); }
        else Assert.Equal("template", body.Children[1].LocalName);
    }
    [Theory]
    [InlineData("open", false)]
    [InlineData("closed", false)]
    [InlineData("OPEN", false)]
    [InlineData("invalid", true)]
    public void ShadowTemplateBranchPrecedesForPatching(string mode, bool patches)
    {
        var body = Html5Parser.Parse("<body><div><?marker name='x'?></div><template for=x shadowrootmode='" + mode + "'>new</template>").RootElement.Children[1];
        Assert.Equal(patches ? "new" : "", body.Children[0].GetTextContent());
        Assert.Equal(patches ? 1 : 2, body.Children.Count);
        if (!patches) Assert.Equal(HtmlNodeKind.ProcessingInstruction, body.Children[0].FirstChild!.Kind);
    }

    [Fact]
    public void ForeignClosingNamesUseBorrowedAsciiComparison()
    {
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("tr-TR");
            var svg = Html5Parser.Parse("<svg><linearGradient><foreignObject><p>x</p></foreignobject></lineargradient><circle/></svg>").RootElement.Children[1].FirstChild!;
            Assert.Equal(new[] { "linearGradient", "circle" }, svg.Children.Select(n => n.LocalName));
            Assert.Equal("foreignObject", svg.FirstChild!.FirstChild!.LocalName); Assert.Equal("x", svg.GetTextContent());
            Assert.False(HtmlAscii.Equals("K", "k"));
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = previous; }
    }

    [Fact]
    public void RepeatedFosterAndAdoptionTextPreservesOneCoalescedNode()
    {
        string rows = string.Concat(Enumerable.Repeat("<tr><td>cell</td></tr>x", 2000));
        var body = Html5Parser.Parse("<body>prefix<table>" + rows + "</table>suffix").RootElement.Children[1];
        Assert.Equal("prefix" + new string('x', 2000), body.FirstChild!.Value); Assert.Equal(HtmlNodeKind.Text, body.FirstChild.Kind); Assert.Equal("table", body.Children[1].LocalName); Assert.Equal("suffix", body.Children[2].Value);
        var formatting = Html5Parser.Parse("<p>" + string.Concat(Enumerable.Repeat("<b><i>x</b>y</i>", 2000))).RootElement.Children[1]; Assert.Equal(string.Concat(Enumerable.Repeat("xy", 2000)), formatting.GetTextContent());
    }

    [Fact]
    public void IndependentCurrentTemplateFramesetRule()
    {
        var doc = Html5Parser.Parse("<!doctype html><template>independent</template><p><frameset>");
        Assert.Equal(new[] { "head", "body" }, doc.RootElement.Children.Select(n => n.LocalName));
        Assert.Equal("p", doc.RootElement.Children[1].FirstChild!.LocalName);
    }

    [Fact]
    public void ScriptingNoscriptNulAndProcessingInstructionLocations()
    {
        var no = Html5Parser.Parse("<body><noscript><b>x</b></noscript>"); Assert.Equal("b", no.RootElement.Children[1].FirstChild!.FirstChild!.LocalName);
        var yes = Html5Parser.Parse("<body><noscript><b>x</b></noscript>", new() { ScriptingEnabled = true }); Assert.True(yes.ScriptingEnabled); Assert.Equal("<b>x</b>", yes.RootElement.GetTextContent());
        Assert.Equal("ab", Html5Parser.Parse("<p>a\0b").RootElement.GetTextContent()); Assert.Equal("a�b", Html5Parser.Parse("<textarea>a\0b").RootElement.GetTextContent());
        var d = Html5Parser.Parse("<?Before a?><html><?Head b?><head></head><?Body c?><body><table><?Table d?><tr><td><?Cell e?></table><template><?Template f?></template></body></html><?After g?>");
        Assert.Equal(HtmlNodeKind.ProcessingInstruction, d.DocumentNode.FirstChild!.Kind); Assert.Equal("Before", d.DocumentNode.FirstChild.LocalName); Assert.Equal("After", d.DocumentNode.LastChild!.LocalName);
        var body = d.RootElement.Children.Single(n => n.LocalName == "body"); var template = body.Children.Single(n => n.LocalName == "template"); Assert.Equal("Template", template.TemplateContent!.FirstChild!.LocalName);
        var fragment = Html5Parser.ParseFragment("<?FoO 😀?>", new(new HtmlDocument().CreateElement("div"))); Assert.Equal("FoO", fragment.FirstChild!.LocalName); Assert.Equal("😀", fragment.FirstChild.Value);
    }
}
