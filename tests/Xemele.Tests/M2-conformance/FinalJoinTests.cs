using Xunit;
namespace Xemele.Tests.ConformanceJoin;

public sealed class FinalJoinTests
{
    [Fact]
    public void NamespaceResolutionAndNonvalidatingDefaultsComposeWithEntityExpansion()
    {
        var document = XmlDocument.Parse("<?xml version='1.10'?><!DOCTYPE r [<!ELEMENT r EMPTY><!ENTITY e 'A&amp;B'><!ATTLIST r mode NMTOKENS ' alpha   beta '>]><r xmlns='urn:root' xmlns:p='urn:part' p:id='7'>&e;</r>", new() { DtdProcessing = XmlDtdProcessing.Parse }, TestContext.Current.CancellationToken);
        Assert.Equal("urn:root", document.RootElement.NamespaceUri);
        Assert.Equal("7", document.RootElement.GetAttribute("id", "urn:part"));
        Assert.Equal("alpha beta", document.RootElement.GetAttribute("mode"));
        Assert.Equal("A&B", document.RootElement.GetTextContent());
        Assert.Contains("A&amp;B", document.ToXmlString());
    }

    [Fact]
    public void JoinedSymbolicDefaultAndContentProvenanceRefuseLoss()
    {
        var document = XmlDocument.Parse("<!DOCTYPE r [<!ENTITY % p ''>%p;<!ATTLIST r a NMTOKENS ' before &unknown; after '>]><r>left&missing;right</r>", new() { DtdProcessing = XmlDtdProcessing.Parse }, TestContext.Current.CancellationToken);
        var value = Assert.Single(document.IncompleteAttributeValues);
        Assert.Same(Assert.Single(document.IncompleteAttributeDefaults), value.Default);
        Assert.Same(value.Default!.Parts, value.Parts);
        Assert.Equal("unknown", Assert.IsType<XmlUnknownAttributeReference>(value.Parts[1]).Name);
        var reference = Assert.Single(document.UnresolvedReferences);
        Assert.Equal(document.RootElement, reference.Parent);
        Assert.Equal(1, reference.ChildBoundary); Assert.Equal("left", reference.PreviousSibling.Value);
        Assert.Equal("missing", reference.Name);
        Assert.Throws<InvalidOperationException>(() => document.RootElement.GetAttribute("a"));
        Assert.Throws<InvalidOperationException>(() => document.RootElement.GetTextContent());
        Assert.Throws<InvalidOperationException>(() => document.ToXmlString());
    }
}
