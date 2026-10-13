using Xunit;

namespace Xemele.Tests.M9DomMemory;

public sealed class StorageTests
{
    [Fact]
    public void ChangingOneAttributePrefixKeepsOtherHandlesAndCopiesIndependent()
    {
        var document = new XmlDocument();
        var root = document.CreateElement("r"); document.DocumentNode.AppendChild(root);
        var first = document.CreateElement("n", "urn:n", "p");
        var second = document.CreateElement("n", "urn:n", "p");
        root.AppendChild(first); root.AppendChild(second);
        first.SetAttribute("a", "one", "urn:a", "p");
        second.SetAttribute("a", "two", "urn:a", "p");
        var handle = first.Attributes[0];
        first.SetAttribute("a", "changed", "urn:a", "q");
        Assert.Equal(handle, first.Attributes[0]);
        Assert.Equal("q", handle.Prefix); Assert.Equal("changed", handle.Value);
        Assert.Equal("p", second.Attributes[0].Prefix); Assert.Equal("two", second.Attributes[0].Value);
        Assert.Equal("p", first.Prefix); Assert.Equal("urn:n", second.NamespaceUri);
        var target = new XmlDocument(); var copy = target.ImportNode(first);
        copy.SetAttribute("a", "copy", "urn:a", "s");
        Assert.Equal("q", handle.Prefix); Assert.Equal("s", copy.Attributes[0].Prefix);
    }

    [Fact]
    public void InternQuotaCountsNamesRatherThanRepeatedTriplesAndRejectsAtomically()
    {
        var document = new XmlDocument(new() { InternedNames = 3 });
        var first = document.CreateElement("n", "urn:n", "p");
        var repeated = document.CreateElement("n", "urn:n", "p");
        Assert.Equal(first.LocalName, repeated.LocalName);
        Assert.Throws<InvalidOperationException>(() => document.CreateElement("extra"));
        Assert.Equal("p", first.Prefix); Assert.Equal("n", repeated.LocalName);
        Assert.Equal("", document.CreateText("text").LocalName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(long.MinValue)]
    [InlineData(-1L)]
    [InlineData(0L)]
    [InlineData(long.MaxValue)]
    public void LocationsPreserveAllOffsetBitsAndSparseSlots(long? offset)
    {
        var source = new Uri("urn:source");
        var document = new XmlDocument();
        var gap = document.CreateText("gap");
        var element = document.CreateElement("r");
        element.SetAttribute("a", "v"); element.SetNamespaceDeclaration("p", "urn:p");
        document.RecordSourceOrigin(new(source, 1, 1, 0));
        var location = new XmlLocation(source, int.MaxValue, int.MinValue, offset);
        document.RecordSourceLocation(element, location);
        document.RecordSourceLocation(element.Attributes[0], location);
        document.RecordSourceLocation(element.NamespaceDeclarations[0], location);
        document.MarkValidationSourceComplete();
        Assert.Equal(location, document.OriginalLocation(element));
        Assert.Equal(location, document.OriginalLocation(element.Attributes[0]));
        Assert.Equal(location, document.OriginalLocation(element.NamespaceDeclarations[0]));
        Assert.Equal(new(source, 0, 0, null), document.OriginalLocation(gap));
        Assert.Throws<ArgumentException>(() => document.RecordSourceLocation(element, location));
        element.SetAttribute("a", "changed");
        Assert.Equal(new(source, 0, 0, null), document.OriginalLocation(element));
    }

    [Fact]
    public void UriIdentityAndNullOriginArePreservedAcrossLocationKinds()
    {
        var document = new XmlDocument(); var root = document.CreateElement("r");
        root.SetAttribute("a", "v"); root.SetNamespaceDeclaration("p", "urn:p");
        var first = new Uri("urn:equal"); var second = new Uri("urn:equal");
        document.RecordSourceOrigin(new(first, 1, 1, null));
        document.RecordSourceLocation(root, new(second, 2, 3, 4));
        document.RecordSourceLocation(root.Attributes[0], new(null, 5, 6, 7));
        document.RecordSourceLocation(root.NamespaceDeclarations[0], new(second, 8, 9, 10));
        document.MarkValidationSourceComplete();
        Assert.Same(first, document.OriginalLocation(document.DocumentNode).SourceUri);
        Assert.Same(second, document.OriginalLocation(root).SourceUri);
        Assert.Same(second, document.OriginalLocation(root.NamespaceDeclarations[0]).SourceUri);
        Assert.Null(document.OriginalLocation(root.Attributes[0]).SourceUri);
        var nullOrigin = new XmlDocument(); var child = nullOrigin.CreateElement("r");
        nullOrigin.RecordSourceOrigin(default);
        nullOrigin.RecordSourceLocation(child, new(first, 1, 2, null));
        nullOrigin.MarkValidationSourceComplete();
        Assert.Same(first, nullOrigin.OriginalLocation(child).SourceUri);
    }
}
