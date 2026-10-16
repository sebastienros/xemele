using Xemele.Validation;
using Xunit;
using static Xemele.Tests.M8XsdTypeRelations.NeighborTests;
namespace Xemele.Tests.M8XsdTypeRelations;

public sealed class AnySimpleTests
{
    private static string Schema(string restriction = "", string modifiers = "") => Wrap($"<xs:complexType name='B' {modifiers}><xs:simpleContent><xs:extension base='xs:anySimpleType'><xs:attribute name='required' use='required'/></xs:extension></xs:simpleContent></xs:complexType><xs:complexType name='D'><xs:simpleContent><xs:restriction base='B'>{restriction}</xs:restriction></xs:simpleContent></xs:complexType><xs:element name='root' type='D'/>");
    [Fact]
    public void VacuousContentRestrictionRetainsDatatypeButNotComplexTypeIdentity()
    {
        var xml = Schema();
        Oracle(xml, "<root required='yes'>arbitrary text</root>");
        var graph = Graph(xml);
        var b = graph.Types.Single(t => t.Name.LocalName == "B");
        var d = graph.Types.Single(t => t.Name.LocalName == "D");
        Assert.NotSame(b, d);
        Assert.Equal(b.Name, d.BaseType);
        Assert.Equal(XsdDerivation.Restriction, d.Derivation);
        Assert.Equal(new(Xs, "anySimpleType"), d.SimpleType);
        Assert.Equal(XsdContentKind.Simple, d.ContentKind);
        Assert.DoesNotContain(graph.SimpleTypes, t => t.Name.NamespaceUri != Xs);
        var schema = XmlSchema.Compile(xml);
        Result(schema, "<root required='yes'>arbitrary text</root>", true);
        Result(schema, "<root>text</root>", false);
        Result(schema, "<root required='yes'><child/></root>", false);
    }
    [Theory]
    [InlineData("<xs:minLength value='0'/>", "")]
    [InlineData("", "final='restriction'")]
    [InlineData("<xs:attribute name='required' use='prohibited'/>", "")]
    [InlineData("<xs:simpleType><xs:restriction base='xs:anySimpleType'/></xs:simpleType>", "")]
    public void NonVacuousOrForbiddenRestrictionsStillReject(string local, string modifiers)
    {
        var error = Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(Schema(local, modifiers)));
        Assert.DoesNotContain(error.Diagnostics, d => d.Code.StartsWith("SCHEMA.", StringComparison.Ordinal));
    }
    [Fact]
    public void ExplicitSimpleDefinitionRestrictionOfUrTypeStillRejects()
    {
        var xml = Wrap("<xs:simpleType name='D'><xs:restriction base='xs:anySimpleType'/></xs:simpleType>");
        Assert.Throws<System.Xml.Schema.XmlSchemaException>(() => Oracle(xml));
        Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(xml));
    }
    [Fact]
    public void InlineSimpleTypeUsesOrdinaryFacetValidation()
    {
        var xml = Schema("<xs:simpleType><xs:restriction base='xs:int'/></xs:simpleType><xs:minInclusive value='2'/>");
        Oracle(xml, "<root required='yes'>2</root>");
        Oracle(xml, "<root required='yes'>1</root>", false);
        var schema = XmlSchema.Compile(xml);
        Result(schema, "<root required='yes'>2</root>", true);
        Result(schema, "<root required='yes'>1</root>", false);
        Assert.NotEqual(new(Xs, "anySimpleType"), Graph(xml).Types.Single(t => t.Name.LocalName == "D").SimpleType);
    }
}
