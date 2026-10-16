using Xemele.Streaming;
using Xunit;
namespace Xemele.Tests.Callbacks;

public sealed class ViewTests
{
    [Fact]
    public void CollectionsBorrowRowsAndPreserveMetadata()
    {
        var rows = (AttributeRow[])CallbackTests.Rows.Clone();
        var view = new XmlAttributeView(rows);
        var a = view[0];
        var b = view[1];
        Assert.Equal("a", a.LocalName.ToString());
        Assert.Equal("p", a.Prefix.ToString());
        Assert.Equal("urn:p", a.NamespaceUri.ToString());
        Assert.Equal("value", a.Value.ToString());
        Assert.False(a.IsDefaulted);
        Assert.True(a.IsValueComplete);
        Assert.True(a.ValueParts.IsEmpty);
        Assert.Equal(CallbackTests.Location, a.Location);
        Assert.True(b.IsDefaulted);
        Assert.False(b.IsValueComplete);
        Assert.True(b.Value.IsEmpty);
        Assert.Equal(2, b.ValueParts.Length);
        Assert.Same(CallbackTests.Parts[1], b.ValueParts[1]);
        Assert.Equal(CallbackTests.Location, b.Location);
        rows[0] = rows[0] with
        {
            Value = "changed"
        };
        Assert.Equal("changed", view[0].Value.ToString()); // Borrowed collection, no hidden owned copy.
        var count = 0;
        foreach (var item in view)
        {
            Assert.Equal(rows[count].LocalName, item.LocalName.ToString());
            count++;
        }
        Assert.Equal(2, count);
        var namespaces = new XmlNamespaceView(CallbackTests.Namespaces);
        foreach (var item in namespaces)
        {
            Assert.Equal("p", item.Prefix.ToString());
            Assert.Equal("urn:p", item.NamespaceUri.ToString());
            Assert.Equal(CallbackTests.Location, item.Location);
        }
    }
    [Fact]
    public void DefaultsBoundsAndEnumeratorStatesAreChecked()
    {
        XmlAttributeView a = default;
        XmlNamespaceView n = default;
        Assert.Equal(0, a.Count);
        Assert.Equal(0, n.Count);
        var ae = a.GetEnumerator();
        var ne = n.GetEnumerator();
        Assert.False(ae.MoveNext());
        Assert.False(ae.MoveNext());
        Assert.False(ne.MoveNext());
        Assert.False(ne.MoveNext());
        CheckAttributeIndex(-1);
        CheckAttributeIndex(2);
        CheckNamespaceIndex(-1);
        CheckNamespaceIndex(1);
        var active = new XmlAttributeView(CallbackTests.Rows).GetEnumerator();
        try
        {
            _ = active.Current;
            Assert.Fail("Current before MoveNext");
        }
        catch (ArgumentOutOfRangeException) { }
        Assert.True(active.MoveNext());
        Assert.True(active.MoveNext());
        Assert.False(active.MoveNext());
        Assert.False(active.MoveNext());
        try
        {
            _ = active.Current;
            Assert.Fail("Current after end");
        }
        catch (ArgumentOutOfRangeException) { }
        var ns = new XmlNamespaceView(CallbackTests.Namespaces).GetEnumerator();
        try
        {
            _ = ns.Current;
            Assert.Fail("Current before MoveNext");
        }
        catch (ArgumentOutOfRangeException) { }
        Assert.True(ns.MoveNext());
        Assert.False(ns.MoveNext());
        try
        {
            _ = ns.Current;
            Assert.Fail("Current after end");
        }
        catch (ArgumentOutOfRangeException) { }
    }
    private static void CheckAttributeIndex(int index)
    {
        try
        {
            _ = new XmlAttributeView(CallbackTests.Rows)[index];
            Assert.Fail("Bad index");
        }
        catch (ArgumentOutOfRangeException e) { Assert.Equal("index", e.ParamName); }
    }
    private static void CheckNamespaceIndex(int index)
    {
        try
        {
            _ = new XmlNamespaceView(CallbackTests.Namespaces)[index];
            Assert.Fail("Bad index");
        }
        catch (ArgumentOutOfRangeException e) { Assert.Equal("index", e.ParamName); }
    }
    [Fact]
    public void ViewsHaveNoPublicConstructorsAndCannotBeStoredOnHeap()
    {
        Type[] types = [typeof(XmlAttributeItem), typeof(XmlNamespaceItem), typeof(XmlStartElement), typeof(XmlEndElement), typeof(XmlDeclaration), typeof(XmlDocumentTypeView), typeof(XmlDtdDeclaration), typeof(XmlSkippedEntity), typeof(XmlAttributeView), typeof(XmlNamespaceView)];
        foreach (var type in types)
        {
            Assert.True(type.IsByRefLike);
            Assert.Empty(type.GetConstructors());
        }
    }
}
