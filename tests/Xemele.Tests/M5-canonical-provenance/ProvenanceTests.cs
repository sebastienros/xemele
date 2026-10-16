using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Xemele.XPath;
using Xunit;
namespace Xemele.Tests.M5CanonicalProvenance;

public sealed class ProvenanceTests
{
    [Fact]
    public void EveryCommittedMutationInvalidatesCapturedRevisionIncludingRoundTrips()
    {
        var d = XmlDocument.Parse("<r xmlns:p='urn:p' xmlns:q='urn:p' p:a='A'>A<c/></r>", cancellationToken: TestContext.Current.CancellationToken);
        var r = d.RootElement;
        var text = r.FirstChild;
        var a = r.Attributes[0];
        var item = XPathItem.FromAttribute(a);
        var structural = d.MutationVersion;
        void Mutation(Action action)
        {
            var prior = d.ContentRevision;
            action();
            Assert.True(d.ContentRevision > prior);
            Assert.NotEqual(item.CapturedContentRevision, d.ContentRevision);
        }
        Mutation(() => text.Value = "B");
        Mutation(() => text.Value = "A");
        Mutation(() => a.Value = "B");
        Mutation(() => a.Value = "A");
        Mutation(() => r.SetAttribute("a", "A", "urn:p", "q"));
        Mutation(() => r.SetAttribute("a", "A", "urn:p", "p"));
        Mutation(() => r.SetNamespaceDeclaration("p", "urn:other"));
        Mutation(() => r.SetNamespaceDeclaration("p", "urn:p"));
        Assert.Equal(structural, d.MutationVersion);
        Assert.Equal("A", item.StringValue);
        Assert.Equal("p:a", item.Name);
        Assert.Equal(item, XPathItem.FromAttribute(a));
        var ns = XPathItem.FromNamespace(r, "p");
        var child = r.LastChild;
        Mutation(() => child.Remove());
        Mutation(() => r.AppendChild(child));
        Mutation(() => r.RemoveAttribute("a", "urn:p"));
        Mutation(() => r.SetAttribute("a", "A", "urn:p", "p"));
        Mutation(() => r.RemoveNamespaceDeclaration("q"));
        Mutation(() => r.SetNamespaceDeclaration("q", "urn:p"));
        Assert.Equal("urn:p", ns.StringValue);
        Assert.NotEqual(ns.CapturedContentRevision, d.ContentRevision);
        Assert.True(d.MutationVersion > structural);
    }
    [Fact]
    public void CapturedRevisionDoesNotChangeStructShapeOrSnapshotSemantics()
    {
        var d = XmlDocument.Parse("<r>A</r>", cancellationToken: TestContext.Current.CancellationToken);
        var root = XPathItem.FromNode(d.RootElement);
        var text = XPathItem.FromNode(d.RootElement.FirstChild);
        Assert.Equal(d.ContentRevision, root.CapturedContentRevision);
        Assert.Equal(d.ContentRevision, text.CapturedContentRevision);
        var currentText = d.RootElement.FirstChild;
        currentText.Value = "B";
        Assert.Equal("A", root.StringValue);
        Assert.Equal("A", text.StringValue);
        Assert.Equal(root, XPathItem.FromNode(d.RootElement));
        Assert.Equal(text, XPathItem.FromNode(d.RootElement.FirstChild));
        Assert.Equal(root.GetHashCode(), XPathItem.FromNode(d.RootElement).GetHashCode());
        Assert.Equal(56, Unsafe.SizeOf<XPathItem>());
        Assert.Throws<InvalidOperationException>(() => default(XPathItem).CapturedContentRevision);
    }
    [Fact]
    public void ExhaustionIsPermanentInvalidSentinelAndDoesNotThrowAfterCommit()
    {
        var d = XmlDocument.Parse("<r>A</r>", cancellationToken: TestContext.Current.CancellationToken);
        var field = typeof(XmlDocument).GetField("<ContentRevision>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!;
        field.SetValue(d, long.MaxValue - 1);
        var currentText = d.RootElement.FirstChild;
        currentText.Value = "B";
        Assert.Equal(long.MaxValue, d.ContentRevision);
        var last = XPathItem.FromNode(d.RootElement.FirstChild);
        currentText.Value = "C";
        Assert.Equal(-1, d.ContentRevision);
        Assert.Equal("C", d.RootElement.FirstChild.Value);
        Assert.NotEqual(last.CapturedContentRevision, d.ContentRevision);
        currentText.Value = "A";
        Assert.Equal(-1, d.ContentRevision);
        Assert.Equal(-1, XPathItem.FromNode(d.RootElement.FirstChild).CapturedContentRevision);
    }
    [Theory]
    [InlineData("<r/>", true)]
    [InlineData("<r>", true)]
    [InlineData("<r><a>text", true)]
    [InlineData("<r>&#0;</r>", false)]
    [InlineData("<r><a a=unquoted>", false)]
    [InlineData("<r><![CDATA[broken", false)]
    public async Task CompletenessMatchesActualRecoveryOutcomeAcrossAllTransports(string source, bool complete)
    {
        var options = new XmlParseOptions { Recover = true };
        var bytes = Encoding.UTF8.GetBytes(source);
        var results = new[]
        {
            XmlDocument.ParseWithDiagnostics(source, options, TestContext.Current.CancellationToken),
            XmlDocument.ParseWithDiagnostics(source.AsSpan(), options, TestContext.Current.CancellationToken),
            XmlDocument.ParseWithDiagnostics(bytes, options, TestContext.Current.CancellationToken),
            XmlDocument.LoadWithDiagnostics(new MemoryStream(bytes), options, TestContext.Current.CancellationToken),
            await XmlDocument.LoadWithDiagnosticsAsync(new MemoryStream(bytes), options, TestContext.Current.CancellationToken)
        };
        foreach (var result in results)
        {
            Assert.Equal(complete, result.IsComplete);
            Assert.NotNull(result.Value);
            Assert.Equal(complete, result.Value.SourceInputComplete);
            result.Value.RootElement.SetAttribute("a", "edited");
            Assert.Equal(complete, result.Value.SourceInputComplete);
            Assert.Equal(complete, result.Value.DocumentNode.Clone().OwnerDocument.SourceInputComplete);
        }
    }
    [Fact]
    public void ProgrammaticAndStrictDocumentsAreCompleteAndCannotRestoreIncompleteSource()
    {
        var d = new XmlDocument();
        Assert.True(d.SourceInputComplete);
        Assert.True(XmlDocument.Parse("<r/>", cancellationToken: TestContext.Current.CancellationToken).SourceInputComplete);
        d.MarkSourceIncomplete();
        var root = d.CreateElement("r");
        d.DocumentNode.AppendChild(root);
        Assert.False(d.SourceInputComplete);
        d.MarkSourceIncomplete();
        Assert.False(d.SourceInputComplete);
    }
}
