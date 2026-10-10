using System.Reflection;
using Xunit;

namespace Xemele.Tests.DomConstruction;

public class DomBuilderTests
{
    [Fact]
    public void DeepWorkIsOneBoundedCheckPerEdgeAndIgnoresInputOnlyLimits()
    {
        foreach (int count in new[] { 500, 12000 })
        {
            var d = new XmlDocument(new() { Depth = 1, DocumentCharacters = 1, TokenCharacters = 1 });
            var b = d.BeginDomConstruction(); var parent = d.DocumentNode;
            for (int i = 0; i < count; i++) { var child = d.CreateElement("e"); b.AppendChild(parent, child); parent = child; }
            var text = d.CreateText("payload"); b.AppendChild(parent, text);
            Assert.Equal(count + 1L, b.ChildChecks); Assert.Equal(count + 1L, d.MutationVersion);
            Assert.Equal("payload", d.RootElement.GetTextContent());
            for (int i = 0; i < count; i++) parent = parent.Parent;
            Assert.Equal(d.DocumentNode, parent);
        }
    }

    [Fact]
    public void UniqueMembersScaleLinearlyAndPreserveOrderedIndependentIdentities()
    {
        foreach (int count in new[] { 64, 1024 })
        {
            var d = new XmlDocument(new() { InternedNames = null, AttributesPerElement = 1 });
            var b = d.BeginDomConstruction(); var e = d.CreateElement("e"); b.AppendChild(d.DocumentNode, e);
            var attrs = new XmlAttribute[count]; var declarations = new XmlNamespaceDeclaration[count];
            for (int i = 0; i < count; i++)
            {
                attrs[i] = b.AppendAttribute(e, "a" + i, "value", "urn:a", "p");
                declarations[i] = b.AppendNamespaceDeclaration(e, "p" + i, "urn:" + i);
            }
            Assert.Equal(2L * count, b.MemberChecks); Assert.Equal(1 + 2L * count, d.MutationVersion);
            int index = 0; foreach (var a in e.Attributes) { Assert.Equal(attrs[index++], a); Assert.Equal(e, a.OwnerElement); }
            index = 0; foreach (var n in e.NamespaceDeclarations) { Assert.Equal(declarations[index++], n); Assert.Equal(e, n.OwnerElement); }
            Assert.Equal(count, index); Assert.NotEqual(attrs[0], attrs[^1]); Assert.NotEqual(declarations[0], declarations[^1]);
        }
    }

    [Fact]
    public void DocumentPrologEpilogAndSingleRootUseBoundedBookkeeping()
    {
        var d = new XmlDocument(); var b = d.BeginDomConstruction();
        for (int i = 0; i < 2000; i++) b.AppendChild(d.DocumentNode, d.CreateComment("before"));
        var root = d.CreateElement("root"); b.AppendChild(d.DocumentNode, root);
        for (int i = 0; i < 2000; i++) b.AppendChild(d.DocumentNode, d.CreateProcessingInstruction("pi", "after"));
        Assert.Equal(4001, b.ChildChecks); Assert.Equal(root, d.RootElement);
        var second = d.CreateElement("second"); long version = d.MutationVersion;
        Assert.Throws<ArgumentException>(() => b.AppendChild(d.DocumentNode, second));
        Assert.Equal(version, d.MutationVersion); Assert.True(second.Parent.IsDefault); Assert.Equal(4001, d.DocumentNode.Children.Count);
        b.AppendChild(root, second); Assert.Equal(root, second.Parent);
        foreach (var kind in new[] { XmlNodeKind.Text, XmlNodeKind.CData, XmlNodeKind.Fragment })
        {
            var other = new XmlDocument(); var builder = other.BeginDomConstruction();
            var child = kind == XmlNodeKind.Text ? other.CreateText("x") : kind == XmlNodeKind.CData ? other.CreateCData("x") : other.CreateFragment();
            Assert.Throws<ArgumentException>(() => builder.AppendChild(other.DocumentNode, child));
            Assert.Equal(0, other.MutationVersion); Assert.True(child.Parent.IsDefault);
        }
    }

    [Fact]
    public void ForeignDefaultNonfreshAndCycleAttemptsAreRejected()
    {
        var d = new XmlDocument(); var b = d.BeginDomConstruction(); var e = d.CreateElement("e");
        Assert.Throws<InvalidOperationException>(() => b.AppendChild(d.DocumentNode, default));
        Assert.Throws<ArgumentException>(() => b.AppendChild(d.DocumentNode, new XmlDocument().CreateElement("x")));
        Assert.Throws<InvalidOperationException>(() => b.AppendChild(default, e));
        Assert.Throws<ArgumentException>(() => b.AppendChild(new XmlDocument().DocumentNode, e));
        Assert.Throws<ArgumentException>(() => b.AppendChild(e, e));
        Assert.Throws<ArgumentException>(() => b.AppendChild(d.DocumentNode, d.DocumentNode));
        b.AppendChild(d.DocumentNode, e);
        var child = d.CreateElement("child");
        Assert.Throws<ArgumentException>(() => b.AppendChild(child, e));
        Assert.Throws<ArgumentException>(() => b.AppendChild(e, e));
        b.AppendChild(e, child);
        Assert.Throws<InvalidOperationException>(() => b.AppendChild(child, e));
        child.Remove();
        Assert.Throws<InvalidOperationException>(() => b.AppendChild(e, child));
        Assert.Throws<InvalidOperationException>(() => d.BeginDomConstruction());
        var unused = new XmlDocument(); unused.CreateText("detached");
        Assert.Throws<InvalidOperationException>(() => unused.BeginDomConstruction());
    }

    [Fact]
    public void OutsideStructuralMutationOrExtraCreationInvalidatesBuilder()
    {
        var d = new XmlDocument(); var b = d.BeginDomConstruction(); var e = d.CreateElement("e"); b.AppendChild(d.DocumentNode, e);
        e.SetAttribute("a", "v");
        Assert.Throws<InvalidOperationException>(() => b.AppendAttribute(e, "b", "v"));
        var other = new XmlDocument(); var builder = other.BeginDomConstruction();
        var first = other.CreateElement("first"); var second = other.CreateElement("second");
        Assert.Throws<InvalidOperationException>(() => builder.AppendChild(other.DocumentNode, second));
        Assert.Equal(0, other.MutationVersion); Assert.True(first.Parent.IsDefault);
        var fresh = new XmlDocument(); var one = fresh.BeginDomConstruction(); var two = fresh.BeginDomConstruction();
        one.AppendChild(fresh.DocumentNode, fresh.CreateElement("e"));
        Assert.Throws<InvalidOperationException>(() => two.AppendAttribute(fresh.RootElement, "a", "v"));
    }

    [Fact]
    public void MembersStillValidateXmlScalarsNamesBindingsAndContainers()
    {
        var d = new XmlDocument(); var b = d.BeginDomConstruction(); var e = d.CreateElement("e"); b.AppendChild(d.DocumentNode, e);
        Assert.Throws<InvalidOperationException>(() => b.AppendAttribute(default, "a", "v"));
        Assert.Throws<ArgumentException>(() => b.AppendAttribute(new XmlDocument().CreateElement("foreign"), "a", "v"));
        Assert.Throws<InvalidOperationException>(() => b.AppendAttribute(d.DocumentNode, "a", "v"));
        foreach (string bad in new[] { "", "1bad", "xmlns", "a:b", "\uD800" }) Assert.Throws<ArgumentException>(() => b.AppendAttribute(e, bad, "v"));
        foreach (string bad in new[] { "\0", "\uD800", "\uDC00", "\uFFFE" }) Assert.Throws<ArgumentException>(() => b.AppendAttribute(e, "a", bad));
        foreach (var binding in new[] { ("p", ""), ("xml", "wrong"), ("p", TreeValidation.XmlUri), ("xmlns", "u"), ("p", TreeValidation.XmlnsUri) })
            Assert.Throws<ArgumentException>(() => b.AppendNamespaceDeclaration(e, binding.Item1, binding.Item2));
        Assert.Throws<ArgumentNullException>(() => b.AppendAttribute(e, null!, "v"));
        Assert.Throws<ArgumentNullException>(() => b.AppendNamespaceDeclaration(e, null!, "u"));
        Assert.Equal(1, d.MutationVersion); Assert.Equal(0, e.Attributes.Count); Assert.Equal(0, e.NamespaceDeclarations.Count);
        b.AppendNamespaceDeclaration(e, "xml", TreeValidation.XmlUri); b.AppendNamespaceDeclaration(e, "", "");
        var a = b.AppendAttribute(e, "根𐀀", "😀", "urn:任意", "前"); Assert.Equal("😀", a.Value);
    }

    [Fact]
    public void QuotaAndVersionFailuresLeaveMembershipAndIdentityUnchanged()
    {
        var d = new XmlDocument(new() { InternedNames = 2 }); var b = d.BeginDomConstruction(); var e = d.CreateElement("e"); b.AppendChild(d.DocumentNode, e);
        var a = b.AppendAttribute(e, "a", "v"); long version = d.MutationVersion;
        Assert.Throws<InvalidOperationException>(() => b.AppendAttribute(e, "b", "new"));
        Assert.Throws<InvalidOperationException>(() => b.AppendNamespaceDeclaration(e, "p", "urn:p"));
        Assert.Equal(version, d.MutationVersion); Assert.Equal(a, e.Attributes[0]); Assert.Equal(0, e.NamespaceDeclarations.Count);
        // A failed multi-name charge does not consume the remaining intern budget.
        var limited = new XmlDocument(new() { InternedNames = 3 }); var lb = limited.BeginDomConstruction(); var le = limited.CreateElement("e"); lb.AppendChild(limited.DocumentNode, le);
        Assert.Throws<InvalidOperationException>(() => lb.AppendAttribute(le, "a", "v", "urn:a", "p"));
        lb.AppendAttribute(le, "a", "v"); lb.AppendAttribute(le, "b", "v"); Assert.Equal(2, le.Attributes.Count);
        // Inject a representational limit in both owner and builder, avoiding the outside-mutation guard.
        SetVersion(d, b, long.MaxValue);
        Assert.Throws<InvalidOperationException>(() => b.AppendAttribute(e, "e", "v"));
        var text = d.CreateText("x"); Assert.Throws<InvalidOperationException>(() => b.AppendChild(e, text));
        Assert.True(text.Parent.IsDefault); Assert.Equal(1, e.Attributes.Count); Assert.Equal(long.MaxValue, d.MutationVersion);
    }

    [Fact]
    public void MemberArenaCapacityFailurePrecedesQuotaAndMutation()
    {
        var d = new XmlDocument(new() { InternedNames = 2 }); var b = d.BeginDomConstruction(); var e = d.CreateElement("e"); b.AppendChild(d.DocumentNode, e);
        var count = typeof(SlotArena<XmlDocument.MemberData>).GetProperty("Count", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var tracked = typeof(XmlDocument.DomBuilder).GetField("attributes", BindingFlags.Instance | BindingFlags.NonPublic)!;
        count.SetValue(d.AttributeSlots, Array.MaxLength); tracked.SetValue(b, Array.MaxLength);
        Assert.Throws<InvalidOperationException>(() => b.AppendAttribute(e, "a", "v"));
        Assert.Equal(1, d.MutationVersion); Assert.Equal(0, e.Attributes.Count);
        count.SetValue(d.AttributeSlots, 0); tracked.SetValue(b, 0);
        b.AppendAttribute(e, "a", "v"); Assert.Equal(1, e.Attributes.Count);
    }

    [Fact]
    public void GlobalEnumeratorsInvalidateAndHandlesOwnDurableLifetime()
    {
        var d = new XmlDocument(); var b = d.BeginDomConstruction(); var e = d.CreateElement("e"); b.AppendChild(d.DocumentNode, e);
        var children = e.Children.GetEnumerator(); var attrs = e.Attributes.GetEnumerator(); var ns = e.NamespaceDeclarations.GetEnumerator();
        var a = b.AppendAttribute(e, "a", new string('v', 100));
        Assert.Throws<InvalidOperationException>(() => children.MoveNext()); Assert.Throws<InvalidOperationException>(() => attrs.MoveNext()); Assert.Throws<InvalidOperationException>(() => ns.MoveNext());
        attrs = e.Attributes.GetEnumerator(); Assert.True(attrs.MoveNext()); a.Value = "updated"; Assert.Equal(a, attrs.Current);
        b.AppendNamespaceDeclaration(e, "p", "urn:p"); Assert.Throws<InvalidOperationException>(() => attrs.Current);
        var weak = new WeakReference(d); GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); Assert.True(weak.IsAlive); Assert.Equal("updated", a.Value); GC.KeepAlive(a);
    }

    [Fact]
    public void WarmFreshLeafAttachmentAllocatesZeroAndPublicMutationStillWorks()
    {
        var d = new XmlDocument(); var b = d.BeginDomConstruction(); var root = d.CreateElement("e"); b.AppendChild(d.DocumentNode, root);
        for (int i = 0; i < 100; i++) b.AppendChild(root, d.CreateText("x"));
        long allocated = 0;
        for (int i = 0; i < 1000; i++)
        {
            var text = d.CreateText("x"); long before = GC.GetAllocatedBytesForCurrentThread(); b.AppendChild(root, text); allocated += GC.GetAllocatedBytesForCurrentThread() - before;
        }
        Assert.Equal(0, allocated); Assert.Equal(1100, root.Children.Count);
        var a = b.AppendAttribute(root, "a", "first"); var n = b.AppendNamespaceDeclaration(root, "p", "urn:p");
        Assert.Equal(a, root.SetAttribute("a", "updated")); Assert.Equal(n, root.SetNamespaceDeclaration("p", "urn:new"));
        var child = root.LastChild; child.Remove(); root.InsertBefore(child, root.FirstChild); Assert.Equal(child, root.FirstChild);
        Assert.Throws<ArgumentException>(() => root.AppendChild(root));
    }

    [Fact]
    public void WarmUniqueMemberHelpersAllocateZeroWithCapacityAndNamesPrepared()
    {
        var d = new XmlDocument(); var b = d.BeginDomConstruction(); var root = d.CreateElement("e"); b.AppendChild(d.DocumentNode, root);
        d.Nodes.EnsureCapacity(2048); d.AttributeSlots.EnsureCapacity(2048); d.NamespaceSlots.EnsureCapacity(2048);
        // Every element has a distinct expanded-key/prefix set: repeated names across
        // elements remain valid unique members, not duplicate unchecked insertion.
        for (int i = 0; i < 100; i++)
        {
            var e = d.CreateElement("e"); b.AppendChild(root, e); b.AppendAttribute(e, "a", "v"); b.AppendNamespaceDeclaration(e, "p", "urn:p");
        }
        long allocated = 0;
        for (int i = 0; i < 1000; i++)
        {
            var e = d.CreateElement("e"); b.AppendChild(root, e);
            long before = GC.GetAllocatedBytesForCurrentThread(); b.AppendAttribute(e, "a", "v"); b.AppendNamespaceDeclaration(e, "p", "urn:p");
            allocated += GC.GetAllocatedBytesForCurrentThread() - before;
        }
        Assert.Equal(0, allocated); Assert.Equal(2200, b.MemberChecks);
        Assert.Equal(1100, root.Children.Count); Assert.Equal("v", root.LastChild.Attributes[0].Value);
    }

    private static void SetVersion(XmlDocument d, XmlDocument.DomBuilder b, long value)
    {
        typeof(XmlDocument).GetProperty("MutationVersion", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(d, value);
        typeof(XmlDocument.DomBuilder).GetField("version", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(b, value);
    }
}
