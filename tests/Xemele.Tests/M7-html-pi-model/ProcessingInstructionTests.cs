using Xunit;
using Xemele.Html;
namespace Xemele.Tests.HtmlProcessingInstructions;

public sealed class ProcessingInstructionTests
{
    [Fact]
    public void AppendOnlyKindsAndStructuralTokens()
    {
        Assert.Equal([0, 1, 2, 3, 4, 5, 6], Enum.GetValues<HtmlNodeKind>().Select(x => (int)x));
        Assert.Equal([0, 1, 2, 3, 4, 5, 6], Enum.GetValues<HtmlTokenKind>().Select(x => (int)x));
        var attributes = new List<HtmlAttribute> { new("x", "v") };
        var token = new HtmlToken(HtmlTokenKind.ProcessingInstruction, "not a valid target", "\0?>", attributes, true, new("html", null, null, false), new(null, 2, 3, null));
        attributes.Clear();
        Assert.Single(token.Attributes);
        Assert.True(token.SelfClosing);
        Assert.NotNull(token.Doctype);
        Assert.Equal("not a valid target", token.Name);
        Assert.Equal("\0?>", token.Data);
        Assert.Equal(2, token.Location.LineNumber);
    }
    [Theory]
    [InlineData("")]
    [InlineData("1foo")]
    [InlineData("-foo")]
    [InlineData("a:b")]
    [InlineData("a.b")]
    [InlineData("a b")]
    [InlineData("é")]
    [InlineData("a\0")]
    [InlineData("xml")]
    [InlineData("XmL")]
    [InlineData("XML-STYLESHEET")]
    public void InvalidTargetsDoNotConsumeNameQuota(string target)
    {
        var owner = new HtmlDocument(new()
        {
            InternedNames = 1
        });
        var error = Assert.Throws<ArgumentException>(() => owner.CreateProcessingInstruction(target, "data"));
        Assert.Equal("target", error.ParamName);
        Assert.Equal("valid", owner.CreateProcessingInstruction("valid", "").LocalName);
    }
    [Fact]
    public void FactoryAndMutableValuePreflightAllQuotas()
    {
        var owner = new HtmlDocument(new()
        {
            TokenCharacters = 3,
            InternedNames = 1
        });
        Assert.Throws<ArgumentNullException>(() => owner.CreateProcessingInstruction(null!, ""));
        Assert.Throws<ArgumentNullException>(() => owner.CreateProcessingInstruction("a", null!));
        Assert.Throws<InvalidOperationException>(() => owner.CreateProcessingInstruction("long", ""));
        Assert.Throws<InvalidOperationException>(() => owner.CreateProcessingInstruction("bad", "😀xyz"));
        var pi = owner.CreateProcessingInstruction("Go", "😀x\0");
        Assert.Equal("😀x\0", pi.Value);
        Assert.Throws<InvalidOperationException>(() => owner.CreateProcessingInstruction("new", ""));
        var same = owner.CreateProcessingInstruction(new string(['G', 'o']), "?>");
        Assert.Same(pi.LocalName, same.LocalName);
        Assert.Throws<InvalidOperationException>(() => pi.Value = "😀xyz");
        Assert.Throws<ArgumentNullException>(() => pi.Value = null!);
        Assert.Equal("😀x\0", pi.Value);
        pi.Value = "\0?>";
        Assert.Equal("\0?>", pi.Value);
    }
    [Fact]
    public void LeavesAttachInDocumentOrderAndExcludeTheirData()
    {
        var owner = new HtmlDocument();
        var before = owner.CreateProcessingInstruction("_Go-2", "\0?>");
        var after = owner.CreateProcessingInstruction("GO", "");
        var root = owner.CreateElement("html");
        owner.DocumentNode.AppendChild(before);
        owner.DocumentNode.AppendChild(root);
        owner.DocumentNode.AppendChild(after);
        Assert.Same(root, owner.RootElement);
        Assert.Same(root, before.NextSibling);
        Assert.Same(after, root.NextSibling);
        Assert.Equal(HtmlNodeKind.ProcessingInstruction, before.Kind);
        Assert.Equal("_Go-2", before.LocalName);
        Assert.Equal("", before.NamespaceUri);
        Assert.Equal("", before.Prefix);
        Assert.Empty(before.Attributes);
        Assert.Empty(before.Children);
        Assert.Null(before.Doctype);
        Assert.Null(before.TemplateContent);
        Assert.Same(owner, before.OwnerDocument);
        Assert.Throws<InvalidOperationException>(() => before.AppendChild(owner.CreateText("x")));
        Assert.Throws<InvalidOperationException>(() => before.SetAttribute(new("a", "v")));
        root.AppendChild(owner.CreateText("visible"));
        root.AppendChild(after);
        Assert.Equal("", before.GetTextContent());
        Assert.Equal("visible", owner.DocumentNode.GetTextContent());
        var fragment = owner.CreateFragment();
        fragment.AppendChild(before);
        fragment.AppendChild(after);
        root.AppendChild(fragment);
        Assert.Empty(fragment.Children);
        Assert.Same(root, before.Parent);
        Assert.Same(after, before.NextSibling);
        Assert.Equal("visible", root.GetTextContent());
    }
    [Fact]
    public void CloneImportAndTemplateCopiesPreservePermanentOwners()
    {
        var owner = new HtmlDocument();
        var pi = owner.CreateProcessingInstruction("Case", "data\0?>");
        var template = owner.CreateElement("template");
        template.TemplateContent!.AppendChild(pi);
        foreach (var deep in new[] { true, false })
        {
            var copy = pi.Clone(deep);
            Assert.NotSame(pi, copy);
            Assert.Same(owner, copy.OwnerDocument);
            Assert.Null(copy.Parent);
            Assert.Equal(pi.LocalName, copy.LocalName);
            Assert.Equal(pi.Value, copy.Value);
            Assert.Empty(copy.Children);
        }
        var other = new HtmlDocument();
        var imported = other.Import(template);
        var inner = Assert.Single(imported.TemplateContent!.Children);
        Assert.Same(other, inner.OwnerDocument);
        Assert.Equal(HtmlNodeKind.ProcessingInstruction, inner.Kind);
        Assert.Equal("Case", inner.LocalName);
        Assert.Equal("data\0?>", inner.Value);
        Assert.Same(imported.TemplateContent, inner.Parent);
        Assert.Empty(other.Import(template, false).TemplateContent!.Children);
        Assert.Throws<ArgumentException>(() => other.DocumentNode.AppendChild(pi));
        var limited = new HtmlDocument(new()
        {
            TokenCharacters = 3,
            InternedNames = 1
        });
        Assert.Throws<InvalidOperationException>(() => limited.Import(pi));
        Assert.Equal("ok", limited.CreateProcessingInstruction("ok", "").LocalName);
    }
    [Fact]
    public void FragmentRefusalLeavesAllPiLinksAndDocumentUnchanged()
    {
        var owner = new HtmlDocument(new()
        {
            Depth = 1
        });
        var root = owner.CreateElement("root");
        owner.DocumentNode.AppendChild(root);
        root.AppendChild(owner.CreateProcessingInstruction("p", "ok"));
        var fragment = owner.CreateFragment();
        var pi = owner.CreateProcessingInstruction("first", "");
        fragment.AppendChild(pi);
        fragment.AppendChild(owner.CreateElement("second"));
        Assert.Throws<InvalidOperationException>(() => root.AppendChild(fragment));
        Assert.Equal(2, fragment.Children.Count);
        Assert.Same(fragment, pi.Parent);
        Assert.Single(root.Children);
        Assert.Throws<InvalidOperationException>(() => owner.DocumentNode.AppendChild(fragment));
        Assert.Equal(2, fragment.Children.Count);
        Assert.Same(fragment, pi.Parent);
        Assert.Same(root, owner.RootElement);
    }
}
