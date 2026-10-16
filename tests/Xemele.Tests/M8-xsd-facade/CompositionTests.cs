using Xunit;
namespace Xemele.Tests.M8XsdFacade;

public sealed class CompositionTests
{
    [Theory]
    [InlineData("<root><member>text</member></root>", true)]
    [InlineData("<root><head>text</head></root>", false)]
    public void SubstitutionAndAbstractHead(string xml, bool expected)
    {
        var schema = PublicTests.Compile("<xs:element name='head' type='xs:string' abstract='true'/><xs:element name='member' type='xs:token' substitutionGroup='head'/><xs:element name='root'><xs:complexType><xs:sequence><xs:element ref='head'/></xs:sequence></xs:complexType></xs:element>");
        PublicTests.AssertResult(schema.Validate(xml), expected);
    }
    [Theory]
    [InlineData("<root><a/><b/></root>", true)]
    [InlineData("<root><b/><a/></root>", false)]
    [InlineData("<root><a/></root>", false)]
    public void ExtensionPreservesEffectiveContent(string xml, bool expected)
    {
        var schema = PublicTests.Compile("<xs:complexType name='B'><xs:sequence><xs:element name='a'/></xs:sequence></xs:complexType><xs:complexType name='D'><xs:complexContent><xs:extension base='B'><xs:sequence><xs:element name='b'/></xs:sequence></xs:extension></xs:complexContent></xs:complexType><xs:element name='root' type='D'/>");
        PublicTests.AssertResult(schema.Validate(xml), expected);
    }
    [Theory]
    [InlineData("<root/>", true)]
    [InlineData("<root><a/></root>", true)]
    public void EmptyExtensionRetainsOptionalBaseParticle(string xml, bool expected)
    {
        var schema = PublicTests.Compile("<xs:complexType name='B'><xs:sequence><xs:element name='a' minOccurs='0'/></xs:sequence></xs:complexType><xs:complexType name='D'><xs:complexContent><xs:extension base='B'/></xs:complexContent></xs:complexType><xs:element name='root' type='D'/>");
        PublicTests.AssertResult(schema.Validate(xml), expected);
    }
    [Fact]
    public void ComposedAttributeGroupWildcardUsesIntersection()
    {
        var schema = PublicTests.Compile("<xs:attributeGroup name='A'><xs:anyAttribute namespace='urn:a urn:b' processContents='skip'/></xs:attributeGroup><xs:attributeGroup name='B'><xs:anyAttribute namespace='urn:b urn:c' processContents='skip'/></xs:attributeGroup><xs:element name='root'><xs:complexType><xs:attributeGroup ref='A'/><xs:attributeGroup ref='B'/></xs:complexType></xs:element>");
        PublicTests.AssertResult(schema.Validate("<root xmlns:b='urn:b' b:y='2'/>"), true);
        PublicTests.AssertResult(schema.Validate("<root xmlns:a='urn:a' a:x='1'/>"), false);
        PublicTests.AssertResult(schema.Validate("<root xmlns:c='urn:c' c:x='1'/>"), false);
    }
}
