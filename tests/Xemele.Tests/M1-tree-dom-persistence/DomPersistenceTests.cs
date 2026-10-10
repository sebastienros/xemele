using System.Reflection;
using Xunit;
namespace Xemele.Tests.DomPersistence;

public class DomPersistenceTests
{
    private static XmlNode Element(XmlDocument.DomBuilder b, string name = "e") => b.CreateSyntaxValidatedNode(XmlNodeKind.Element, name, "", "", "", TestContext.Current.CancellationToken);
    private static (XmlDocument, XmlDocument.DomBuilder, XmlNode) Root(XmlResourceLimits? limits = null)
    {
        var d = limits is null ? new XmlDocument() : new XmlDocument(limits);
        var b = d.BeginDomConstruction(); var e = Element(b); b.AppendSyntaxValidatedChild(d.DocumentNode, e, TestContext.Current.CancellationToken); return (d, b, e);
    }
    [Fact]
    public void IndependentlyValidatedLongExactStringsPersistAllKinds()
    {
        var (d, b, e) = Root();
        string name = "根" + new string('n', 32768), uri = "urn:" + new string('u', 32768), prefix = "前" + new string('p', 32768);
        TreeValidation.Expanded(name, uri, prefix);
        var child = b.CreateSyntaxValidatedNode(XmlNodeKind.Element, name, uri, prefix, "", TestContext.Current.CancellationToken); b.AppendSyntaxValidatedChild(e, child, TestContext.Current.CancellationToken);
        string value = new string('α', 131072) + "😀";
        foreach (var kind in new[] { XmlNodeKind.Text, XmlNodeKind.CData, XmlNodeKind.Comment, XmlNodeKind.ProcessingInstruction })
        {
            string target = kind == XmlNodeKind.ProcessingInstruction ? name : "";
            TreeValidation.Value(kind, value); if (target.Length != 0) TreeValidation.Name(target, true);
            var n = b.CreateSyntaxValidatedNode(kind, target, "", "", value, TestContext.Current.CancellationToken); b.AppendSyntaxValidatedChild(child, n, TestContext.Current.CancellationToken);
            Assert.Same(value, n.Value); Assert.Equal(kind, n.Kind); Assert.Equal(target, n.LocalName);
        }
        TreeValidation.Text(value); var a = b.AppendSyntaxValidatedAttribute(child, name, value, uri, prefix, TestContext.Current.CancellationToken);
        TreeValidation.Binding(prefix, uri); var ns = b.AppendSyntaxValidatedNamespaceDeclaration(child, prefix, uri, TestContext.Current.CancellationToken);
        Assert.Same(value, a.Value); Assert.Same(child.LocalName, a.LocalName); Assert.Same(child.NamespaceUri, ns.Value);
        Assert.Equal(6, b.ChildChecks); Assert.Equal(2, b.MemberChecks); Assert.Equal(8, d.MutationVersion);
        GC.Collect(); Assert.Equal(value, child.LastChild.Value);
    }
    [Fact]
    public void CanonicalShapesAndNullsRejectBeforeCreation()
    {
        var d = new XmlDocument(); var b = d.BeginDomConstruction();
        foreach (var kind in new[] { XmlNodeKind.Document, XmlNodeKind.Fragment, (XmlNodeKind)999 })
            Assert.Throws<ArgumentException>(() => b.CreateSyntaxValidatedNode(kind, "", "", "", "", TestContext.Current.CancellationToken));
        foreach (var kind in new[] { XmlNodeKind.Text, XmlNodeKind.CData, XmlNodeKind.Comment })
            foreach (var fields in new[] { ("x", "", ""), ("", "u", ""), ("", "", "p") })
                Assert.Throws<ArgumentException>(() => b.CreateSyntaxValidatedNode(kind, fields.Item1, fields.Item2, fields.Item3, "v", TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentException>(() => b.CreateSyntaxValidatedNode(XmlNodeKind.Element, "e", "", "", "v", TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentException>(() => b.CreateSyntaxValidatedNode(XmlNodeKind.Element, "", "", "", "", TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentException>(() => b.CreateSyntaxValidatedNode(XmlNodeKind.ProcessingInstruction, "pi", "u", "", "v", TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentException>(() => b.CreateSyntaxValidatedNode(XmlNodeKind.ProcessingInstruction, "", "", "", "v", TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentNullException>(() => b.CreateSyntaxValidatedNode(XmlNodeKind.Text, "", "", "", null!, TestContext.Current.CancellationToken));
        Assert.Equal(1, d.Nodes.Count); Assert.Equal(0, d.MutationVersion);
    }
    [Fact]
    public void CancellationAtEveryBoundaryLeavesOperationUncommitted()
    {
        var (d, b, e) = Root(); var token = new CancellationToken(true);
        Assert.Throws<OperationCanceledException>(() => b.CreateSyntaxValidatedNode(XmlNodeKind.Text, "", "", "", "v", token));
        Assert.Throws<OperationCanceledException>(() => b.AppendSyntaxValidatedAttribute(e, "a", "v", "", "", token));
        Assert.Throws<OperationCanceledException>(() => b.AppendSyntaxValidatedNamespaceDeclaration(e, "p", "urn:p", token));
        var text = b.CreateSyntaxValidatedNode(XmlNodeKind.Text, "", "", "", "v", TestContext.Current.CancellationToken);
        Assert.Throws<OperationCanceledException>(() => b.AppendSyntaxValidatedChild(e, text, token));
        Assert.Equal(1, d.MutationVersion); Assert.Equal(0, e.Children.Count); Assert.True(text.Parent.IsDefault);
        b.AppendSyntaxValidatedChild(e, text, TestContext.Current.CancellationToken); Assert.Equal(2, d.MutationVersion);
    }
    [Fact]
    public void FreshnessOwnerContainerAndRootSafetyRemainChecked()
    {
        var (d, b, e) = Root();
        Assert.Throws<ArgumentException>(() => b.AppendSyntaxValidatedAttribute(new XmlDocument().CreateElement("x"), "a", "v", "", "", TestContext.Current.CancellationToken));
        Assert.Throws<InvalidOperationException>(() => b.AppendSyntaxValidatedNamespaceDeclaration(d.DocumentNode, "p", "u", TestContext.Current.CancellationToken));
        var n = Element(b);
        Assert.Throws<InvalidOperationException>(() => Element(b));
        Assert.Throws<ArgumentException>(() => b.AppendSyntaxValidatedChild(d.DocumentNode, n, TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentException>(() => b.AppendSyntaxValidatedChild(n, n, TestContext.Current.CancellationToken));
        b.AppendSyntaxValidatedChild(e, n, TestContext.Current.CancellationToken);
        Assert.Throws<InvalidOperationException>(() => b.AppendSyntaxValidatedChild(e, n, TestContext.Current.CancellationToken));
        e.SetAttribute("outside", "v"); Assert.Throws<InvalidOperationException>(() => Element(b));
        Assert.Throws<InvalidOperationException>(() => b.AppendSyntaxValidatedAttribute(e, "a", "v", "", "", TestContext.Current.CancellationToken));
    }
    [Fact]
    public void QuotaVersionAndCapacityFailureRemainPrecommit()
    {
        var (d, b, e) = Root(new() { InternedNames = 2 });
        var a = b.AppendSyntaxValidatedAttribute(e, "a", "v", "", "", TestContext.Current.CancellationToken);
        Assert.Throws<InvalidOperationException>(() => b.AppendSyntaxValidatedAttribute(e, "b", "v", "", "", TestContext.Current.CancellationToken));
        Assert.Throws<InvalidOperationException>(() => b.AppendSyntaxValidatedNamespaceDeclaration(e, "p", "urn:p", TestContext.Current.CancellationToken));
        Assert.Throws<InvalidOperationException>(() => Element(b, "new")); Assert.Equal(2, d.Nodes.Count);
        typeof(XmlDocument).GetProperty("MutationVersion", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(d, long.MaxValue);
        typeof(XmlDocument.DomBuilder).GetField("version", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(b, long.MaxValue);
        Assert.Throws<InvalidOperationException>(() => b.AppendSyntaxValidatedAttribute(e, "e", "v", "", "", TestContext.Current.CancellationToken));
        var text = b.CreateSyntaxValidatedNode(XmlNodeKind.Text, "", "", "", "v", TestContext.Current.CancellationToken);
        Assert.Throws<InvalidOperationException>(() => b.AppendSyntaxValidatedChild(e, text, TestContext.Current.CancellationToken));
        Assert.True(text.Parent.IsDefault); Assert.Equal(a, e.Attributes[0]);
        var (cd, cb, ce) = Root();
        typeof(SlotArena<XmlDocument.MemberData>).GetProperty("Count", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(cd.AttributeSlots, Array.MaxLength);
        typeof(XmlDocument.DomBuilder).GetField("attributes", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(cb, Array.MaxLength);
        Assert.Throws<InvalidOperationException>(() => cb.AppendSyntaxValidatedAttribute(ce, "a", "v", "", "", TestContext.Current.CancellationToken));
        Assert.Equal(1, cd.MutationVersion); Assert.Equal(0, ce.Attributes.Count);
    }
    [Fact]
    public void LinearDeepAndMemberWorkAndEnumeratorInvalidation()
    {
        foreach (int count in new[] { 64, 4096 })
        {
            var (d, b, e) = Root(new() { InternedNames = null }); var parent = e;
            for (int i = 0; i < count; i++)
            {
                string name = "n" + i; TreeValidation.Name(name);
                var child = Element(b); b.AppendSyntaxValidatedChild(parent, child, TestContext.Current.CancellationToken); parent = child;
                b.AppendSyntaxValidatedAttribute(e, name, "v", "", "", TestContext.Current.CancellationToken);
                TreeValidation.Binding(name, "urn:p"); b.AppendSyntaxValidatedNamespaceDeclaration(e, name, "urn:p", TestContext.Current.CancellationToken);
            }
            Assert.Equal(count + 1L, b.ChildChecks); Assert.Equal(count * 2L, b.MemberChecks); Assert.Equal(count * 3L + 1, d.MutationVersion);
            int index = 0; foreach (var a in e.Attributes) Assert.Equal("n" + index++, a.LocalName); Assert.Equal(count, index);
            var enumerator = e.Attributes.GetEnumerator(); var final = Element(b); b.AppendSyntaxValidatedChild(parent, final, TestContext.Current.CancellationToken);
            Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
        }
    }
    [Fact]
    public void WarmPersistenceWithPreparedNamesAndCapacityAllocatesZero()
    {
        var (d, b, e) = Root(); d.Nodes.EnsureCapacity(2048); d.AttributeSlots.EnsureCapacity(2048); d.NamespaceSlots.EnsureCapacity(2048);
        TreeValidation.Name("a"); TreeValidation.Binding("p", "urn:p"); TreeValidation.Text("v");
        long allocated = 0;
        for (int i = 0; i < 1100; i++)
        {
            long start = GC.GetAllocatedBytesForCurrentThread();
            var child = Element(b); b.AppendSyntaxValidatedChild(e, child, TestContext.Current.CancellationToken);
            b.AppendSyntaxValidatedAttribute(child, "a", "v", "", "", TestContext.Current.CancellationToken); b.AppendSyntaxValidatedNamespaceDeclaration(child, "p", "urn:p", TestContext.Current.CancellationToken);
            if (i >= 100) allocated += GC.GetAllocatedBytesForCurrentThread() - start;
        }
        Assert.Equal(0, allocated); Assert.Same(e.LocalName, d.FindInternedName("e".AsSpan())); Assert.Equal(1100, e.Children.Count);
    }
    [Fact]
    public void CheckedAndPublicValidationIsUnchanged()
    {
        var (d, b, e) = Root();
        Assert.Throws<ArgumentException>(() => d.CreateText("\0")); Assert.Throws<ArgumentException>(() => d.CreateComment("--"));
        Assert.Throws<ArgumentException>(() => d.CreateCData("]]>")); Assert.Throws<ArgumentException>(() => d.CreateProcessingInstruction("XML", "v"));
        Assert.Throws<ArgumentException>(() => b.AppendAttribute(e, "1bad", "v")); Assert.Throws<ArgumentException>(() => b.AppendAttribute(e, "a", "\0"));
        Assert.Throws<ArgumentException>(() => b.AppendNamespaceDeclaration(e, "xml", "wrong"));
        Assert.Throws<ArgumentException>(() => e.SetAttribute("a", "\0")); Assert.Equal(0, e.Attributes.Count);
    }
}
