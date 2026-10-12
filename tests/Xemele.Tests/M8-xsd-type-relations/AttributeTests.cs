using Xemele.Validation;
using Xunit;
using static Xemele.Tests.M8XsdTypeRelations.NeighborTests;
namespace Xemele.Tests.M8XsdTypeRelations;
public sealed class AttributeTests
{
    private static string Restrict(string attributes, string local, string definitions = "", bool simple = false)
    {
        string baseBody = simple ? "<xs:simpleContent><xs:extension base='xs:string'>" + attributes + "</xs:extension></xs:simpleContent>" : attributes;
        string content = simple ? "simpleContent" : "complexContent";
        return Wrap(definitions + $"<xs:complexType name='B'>{baseBody}</xs:complexType><xs:complexType name='D'><xs:{content}><xs:restriction base='B'>{local}</xs:restriction></xs:{content}></xs:complexType><xs:element name='root' type='D'/>");
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void UnmentionedUsesRetainRequiredDefaultFixedAndAuthoredQNameContext(bool simple)
    {
        string xml = Restrict("<xs:attribute name='required' use='required'/><xs:attribute name='defaulted' type='xs:int' default='02'/><xs:attribute name='q' type='xs:QName' xmlns:p='urn:value' fixed='p:item'/>", "", simple: simple);
        const string valid = "<root required='yes' xmlns:z='urn:value' q='z:item'/>";
        Oracle(xml, valid); var schema = XmlSchema.Compile(xml); Result(schema, valid, true);
        Result(schema, "<root/>", false); Result(schema, "<root required='yes' defaulted='bad'/>", false);
        Result(schema, "<root required='yes' xmlns:z='urn:wrong' q='z:item'/>", false);
        var graph = Graph(xml); var b = graph.Types.Single(t => t.Name.LocalName == "B"); var d = graph.Types.Single(t => t.Name.LocalName == "D");
        foreach (var use in b.Attributes) Assert.Same(use, d.Attributes.Single(a => a.DeclarationId == use.DeclarationId));
    }
    [Theory]
    [InlineData("<xs:attribute name='a' use='prohibited'/>", false)]
    [InlineData("<xs:attributeGroup ref='G'/>", true)]
    public void DirectAndGroupProhibitionsHaveDifferentInheritanceMeaning(string local, bool allowed)
    {
        string xml = Restrict("<xs:attribute name='a'/>", local, "<xs:attributeGroup name='G'><xs:attribute name='a' use='prohibited'/></xs:attributeGroup>");
        Oracle(xml, "<root a='yes'/>", allowed); Result(XmlSchema.Compile(xml), "<root a='yes'/>", allowed);
        var graph = Graph(xml);
        Assert.DoesNotContain(graph.Types.Where(t => t.Name.LocalName.StartsWith("#attribute-group", StringComparison.Ordinal)).SelectMany(t => t.Attributes), a => a.Use == XsdAttributeUse.Prohibited);
    }
    [Fact]
    public void ProhibitedGroupDeclarationDoesNotCollideWithIncludedEffectiveUse()
    {
        string xml = Restrict("<xs:attribute name='a'/>", "<xs:attributeGroup ref='G'/>", "<xs:attributeGroup name='H'><xs:attribute name='a'/></xs:attributeGroup><xs:attributeGroup name='G'><xs:attribute name='a' use='prohibited'/><xs:attributeGroup ref='H'/></xs:attributeGroup>");
        Oracle(xml, "<root a='yes'/>"); Result(XmlSchema.Compile(xml), "<root a='yes'/>", true);
    }
    [Fact]
    public void EffectiveGroupOverrideKeepsItsNarrowedDatatype()
    {
        string xml = Restrict("<xs:attribute name='a' type='xs:decimal'/>", "<xs:attributeGroup ref='G'/>", "<xs:attributeGroup name='G'><xs:attribute name='a' type='xs:int'/></xs:attributeGroup>");
        Oracle(xml, "<root a='2'/>"); Oracle(xml, "<root a='2.5'/>", false);
        var schema = XmlSchema.Compile(xml); Result(schema, "<root a='2'/>", true); Result(schema, "<root a='2.5'/>", false);
    }
    [Theory]
    [InlineData("<xs:attribute name='a' use='required'/>", "<xs:attribute name='a' use='prohibited'/>")]
    [InlineData("<xs:attribute name='a' use='required'/>", "<xs:attribute name='a'/>")]
    [InlineData("<xs:attribute name='a' type='xs:int'/>", "<xs:attribute name='a' type='xs:string'/>")]
    [InlineData("<xs:attribute name='a' type='xs:int' fixed='2'/>", "<xs:attribute name='a' type='xs:int' fixed='3'/>")]
    public void IncompatibleEffectiveOverridesReject(string basis, string local)
    {
        string xml = Restrict(basis, local); Assert.Throws<System.Xml.Schema.XmlSchemaException>(() => Oracle(xml));
        Assert.Contains(Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(xml)).Diagnostics, d => d.Code == "XSD.SchemaRestriction");
    }
    [Fact]
    public void RestrictionDoesNotInheritBaseWildcard()
    {
        string xml = Restrict("<xs:attribute name='a'/><xs:anyAttribute processContents='lax'/>", "");
        Oracle(xml, "<root a='yes'/>"); Oracle(xml, "<root extra='yes'/>", false);
        var schema = XmlSchema.Compile(xml); Result(schema, "<root a='yes'/>", true); Result(schema, "<root extra='yes'/>", false);
        Assert.Null(Graph(xml).Types.Single(t => t.Name.LocalName == "D").AnyAttribute);
    }
}
