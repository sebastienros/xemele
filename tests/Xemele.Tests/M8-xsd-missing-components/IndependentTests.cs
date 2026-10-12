using Xemele.Validation;
using Xunit;
using static Xemele.Tests.M8XsdMissingComponents.Support;
namespace Xemele.Tests.M8XsdMissingComponents;

public sealed class IndependentTests
{
    [Theory]
    [InlineData("<xs:element name='r' type='missing' default='a' fixed='b'/>")]
    [InlineData("<xs:element name='r' type='unbound:T'/>")]
    [InlineData("<xs:element name='r' type='bad:name:bad'/>")]
    [InlineData("<xs:element name='r' type='b:T'/>")]
    [InlineData("<xs:element name='r' type='xs:int' default='bad'/>")]
    [InlineData("<xs:simpleType name='T'><xs:restriction base='missing'/></xs:simpleType>")]
    [InlineData("<xs:complexType name='T'><xs:complexContent><xs:extension base='missing'/></xs:complexContent></xs:complexType>")]
    [InlineData("<xs:element name='a' substitutionGroup='b'/><xs:element name='b' substitutionGroup='a'/>")]
    [InlineData("<xs:simpleType name='T'><xs:restriction base='T'/></xs:simpleType>")]
    [InlineData("<xs:complexType name='C'/><xs:simpleType name='L'><xs:list itemType='C'/></xs:simpleType>")]
    [InlineData("<xs:simpleType name='T' final='list'><xs:restriction base='xs:int'/></xs:simpleType><xs:simpleType name='L'><xs:list itemType='T'/></xs:simpleType>")]
    [InlineData("<xs:element name='a' type='xs:int' final='restriction'/><xs:element name='b' type='xs:byte' substitutionGroup='a'/>")]
    [InlineData("<xs:element name='r'><xs:complexType><xs:choice><xs:element name='same'/><xs:element name='same'/></xs:choice></xs:complexType></xs:element>")]
    [InlineData("<xs:element name='r'><xs:complexType><xs:sequence><xs:element name='same' type='xs:int'/><xs:element name='same' type='xs:string'/></xs:sequence></xs:complexType></xs:element>")]
    [InlineData("<xs:element name='r'><xs:keyref name='k' refer='missing'><xs:selector xpath='.'/><xs:field xpath='.'/></xs:keyref></xs:element>")]
    [InlineData("<xs:element name='r'><xs:key name='k'><xs:selector xpath='not('//><xs:field xpath='.'/></xs:key></xs:element>")]
    public void KnownInvalidNeighborIsNeverSuppressed(string invalid)
    {
        Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(Wrap("<xs:element name='unrelated' type='absent'/>" + invalid, "xmlns:b='urn:b'")));
    }
    [Theory]
    [InlineData("<xs:pattern value='['/>")]
    [InlineData("<xs:length value='-1'/>")]
    [InlineData("<xs:length value='1'/><xs:length value='1'/>")]
    [InlineData("<xs:minLength value='5'/><xs:maxLength value='3'/>")]
    [InlineData("<xs:length value='2'/><xs:minLength value='1'/>")]
    [InlineData("<xs:whiteSpace value='preserve'/>")]
    [InlineData("<xs:minInclusive value='0'/>")]
    [InlineData("<xs:pattern value='a' fixed='true'/>")]
    [InlineData("<xs:enumeration value='a' fixed='true'/>")]
    public void MissingItemNeverSuppressesIndependentFacetOrRegexFailure(string facets)
    {
        Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(Wrap("<xs:simpleType name='L'><xs:list itemType='absent'/></xs:simpleType><xs:simpleType name='R'><xs:restriction base='L'>" + facets + "</xs:restriction></xs:simpleType>")));
    }
    [Fact]
    public void InheritedIndependentLengthWeakeningStillFails()
    {
        Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(Wrap("<xs:simpleType name='L'><xs:list itemType='absent'/></xs:simpleType><xs:simpleType name='B'><xs:restriction base='L'><xs:minLength value='5'/></xs:restriction></xs:simpleType><xs:simpleType name='R'><xs:restriction base='B'><xs:minLength value='4'/></xs:restriction></xs:simpleType>")));
    }
    [Fact]
    public void UnknownEcdOperandDefersButEqualNullsDoNotEstablishConsistency()
    {
        var schema = XmlSchema.Compile(Wrap("<xs:element name='m' substitutionGroup='absent'/><xs:complexType name='C'><xs:sequence><xs:element ref='m'/><xs:element name='m' type='xs:int'/></xs:sequence></xs:complexType><xs:element name='r' type='C'/><xs:element name='good' type='xs:int'/>"));
        Assert.True(schema.Validate("<good>7</good>").IsValid); Missing(schema.Validate("<r><m/><m>7</m></r>"));
    }
    [Fact]
    public void DecidableUpaStillFailsBesideUnresolvedAffiliation()
    {
        Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(Wrap("<xs:element name='m' substitutionGroup='absent'/><xs:element name='h'/><xs:complexType name='C'><xs:choice><xs:element ref='h'/><xs:element ref='h'/><xs:element ref='m'/></xs:choice></xs:complexType>")));
    }
}
