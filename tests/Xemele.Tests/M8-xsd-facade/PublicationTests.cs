using Xemele.Validation;
using Xunit;
namespace Xemele.Tests.M8XsdFacade;
public sealed class PublicationTests
{
    [Theory]
    [InlineData("<xs:simpleType name='Unused'><xs:restriction base='xs:int'><xs:minInclusive value='bad'/></xs:restriction></xs:simpleType>")]
    [InlineData("<xs:simpleType name='Unused'><xs:restriction base='xs:string'><xs:pattern value='['/></xs:restriction></xs:simpleType>")]
    [InlineData("<xs:element name='unused' type='xs:int' default='bad'/>")]
    [InlineData("<xs:attribute name='unused' type='xs:ID' default='a'/>")]
    [InlineData("<xs:attribute name='unused' type='xs:NOTATION'/>")]
    [InlineData("<xs:simpleType name='Unused'><xs:restriction base='xs:NOTATION'/></xs:simpleType>")]
    [InlineData("<xs:simpleType name='Unused'><xs:restriction base='xs:NOTATION'><xs:enumeration value='p:missing'/></xs:restriction></xs:simpleType>")]
    [InlineData("<xs:element name='unused'><xs:complexType><xs:attribute name='a'><xs:simpleType><xs:restriction base='xs:int'><xs:maxInclusive value='bad'/></xs:restriction></xs:simpleType></xs:attribute></xs:complexType></xs:element>")]
    [InlineData("<xs:element name='unused' default='text'><xs:complexType><xs:sequence><xs:element name='required'/></xs:sequence></xs:complexType></xs:element>")]
    [InlineData("<xs:element name='unused' default='text'><xs:complexType mixed='true'><xs:sequence><xs:element name='required'/></xs:sequence></xs:complexType></xs:element>")]
    [InlineData("<xs:element name='unused'><xs:complexType><xs:attribute name='a' type='xs:ID'/><xs:attribute name='b' type='xs:ID'/></xs:complexType></xs:element>")]
    [InlineData("<xs:attribute name='a' type='xs:QName' fixed='p:v'/><xs:complexType name='Unused'><xs:attribute ref='a' xmlns:p='urn:other' fixed='p:v'/></xs:complexType>")]
    public void InvalidRetainedDefinitionsRejectBeforePublication(string body)
    { Assert.NotEmpty(Assert.Throws<SchemaCompileException>(() => PublicTests.Compile(body)).Diagnostics); }
    [Fact]
    public void EqualQNameBindingFixedUsesDeclarationContexts()
    {
        var schema = PublicTests.Compile("<xs:attribute name='a' type='xs:QName' fixed='p:v'/><xs:element name='root'><xs:complexType><xs:attribute ref='a' xmlns:q='urn:p' fixed='q:v'/></xs:complexType></xs:element>");
        PublicTests.AssertResult(schema.Validate("<root xmlns:z='urn:p' a='z:v'/>"), true);
    }
    [Fact]
    public void NotationInheritedEnumerationAndNamespace()
    {
        var schema = PublicTests.Compile("<xs:notation name='N' public='notation'/><xs:simpleType name='NType'><xs:restriction base='xs:NOTATION'><xs:enumeration value='N'/></xs:restriction></xs:simpleType><xs:simpleType name='Derived'><xs:restriction base='NType'/></xs:simpleType><xs:element name='root' type='Derived'/>");
        PublicTests.AssertResult(schema.Validate("<root>N</root>"), true);
        PublicTests.AssertResult(schema.Validate("<root>Other</root>"), false);
    }
    [Fact]
    public void DefaultCanonicalLexicalStillAssessedAgainstActualType()
    {
        var schema = PublicTests.Compile("<xs:simpleType name='P'><xs:restriction base='xs:int'><xs:pattern value='0[0-9]'/></xs:restriction></xs:simpleType><xs:element name='root' type='xs:int' default='01'/>");
        PublicTests.AssertResult(schema.Validate("<root xmlns:xsi='http://www.w3.org/2001/XMLSchema-instance' xsi:type='P'/>"), false);
        PublicTests.AssertResult(schema.Validate("<root xmlns:xsi='http://www.w3.org/2001/XMLSchema-instance' xsi:type='P'>01</root>"), true);
    }
    [Theory]
    [InlineData("<xs:schema")] [InlineData("<wrong/>")]
    public void MalformedSchemas(string schema)
    { Assert.NotEmpty(Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(schema)).Diagnostics); }
}
