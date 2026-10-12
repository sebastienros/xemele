using Xemele.Validation;
using Xunit;
namespace Xemele.Tests.M8XsdTypeRelations;

public sealed class NeighborTests
{
    internal const string Xs = "http://www.w3.org/2001/XMLSchema";
    internal static string Wrap(string body) => $"<xs:schema xmlns:xs='{Xs}'>{body}</xs:schema>";
    internal static void Oracle(string schema, string? instance = null, bool valid = true)
    {
        var set = new System.Xml.Schema.XmlSchemaSet { XmlResolver = null };
        using var source = System.Xml.XmlReader.Create(new StringReader(schema)); set.Add(null, source); set.Compile();
        if (instance is null) return;
        bool result = true;
        var settings = new System.Xml.XmlReaderSettings { Schemas = set, ValidationType = System.Xml.ValidationType.Schema, XmlResolver = null };
        settings.ValidationEventHandler += (_, _) => result = false;
        using var reader = System.Xml.XmlReader.Create(new StringReader(instance), settings); while (reader.Read()) { }
        Assert.Equal(valid, result);
    }
    internal static void Result(XmlSchema schema, string instance, bool valid)
    {
        var result = schema.Validate(instance); Assert.True(result.IsComplete); Assert.Equal(valid, result.IsValid);
        Assert.DoesNotContain(result.Diagnostics, d => d.Code.StartsWith("SCHEMA.", StringComparison.Ordinal));
    }
    internal static XsdCompiledSchema Graph(string schema)
    {
        var context = new SchemaCompileContext();
        return new XmlSchemaCompiler().Compile(context.Parse(schema).Document, context.Options, context.Operation);
    }
    private const string Union = "<xs:simpleType name='U'><xs:union memberTypes='xs:integer xs:boolean'/></xs:simpleType>";
    internal static string Substitution(string member = "xs:integer", string head = "U", string modifiers = "", string definitions = Union, string particle = "<xs:element ref='h'/>") =>
        Wrap(definitions + $"<xs:element name='h' type='{head}' abstract='true' {modifiers}/><xs:element name='m' type='{member}' substitutionGroup='h'/><xs:element name='root'><xs:complexType><xs:sequence>{particle}</xs:sequence></xs:complexType></xs:element>");
    [Theory]
    [InlineData("xs:integer", "U", Union)]
    [InlineData("xs:positiveInteger", "U", Union)]
    [InlineData("xs:positiveInteger", "N", Union + "<xs:simpleType name='N'><xs:union memberTypes='U xs:string'/></xs:simpleType>")]
    [InlineData("xs:integer", "R", Union + "<xs:simpleType name='R'><xs:restriction base='U'/></xs:simpleType>")]
    [InlineData("C", "U", Union + "<xs:complexType name='C'><xs:simpleContent><xs:extension base='xs:integer'/></xs:simpleContent></xs:complexType>")]
    public void UnionSubstitutionIsCompiledAndMatched(string member, string head, string definitions)
    {
        string xml = Substitution(member, head, definitions: definitions);
        Oracle(xml, "<root><m>2</m></root>"); Oracle(xml, "<root><m>no</m></root>", false);
        var graph = Graph(xml);
        Assert.Contains(graph.GlobalElements[new("", "m")], XsdSubstitutionGroups.Members(graph, graph.GlobalElements[new("", "h")], new SchemaCompileOperation()));
        var compiled = XmlSchema.Compile(xml); Result(compiled, "<root><m>2</m></root>", true); Result(compiled, "<root><m>no</m></root>", false);
    }
    [Fact]
    public void Xsd10UnionMemberRuleDoesNotImportXsd11FacetGuard()
    {
        string xml = Substitution(head: "R", definitions: Union + "<xs:simpleType name='R'><xs:restriction base='U'><xs:enumeration value='1'/></xs:restriction></xs:simpleType>");
        // System.Xml rejects a facet-bearing union target. Second Edition
        // 3.14.6 clause 2.2.4 has no XSD 1.1 empty-facet guard. This synthetic
        // witness does not alter or reclassify the disputed original stZ073b.
        Assert.Throws<System.Xml.Schema.XmlSchemaException>(() => Oracle(xml));
        Result(XmlSchema.Compile(xml), "<root><m>2</m></root>", true);
    }
    [Theory]
    [InlineData("", true)]
    [InlineData("block='extension'", false)]
    [InlineData("block='restriction'", false)]
    public void IntermediateBlocksApplyToWholePathIncludingLaterMethods(string block, bool valid)
    {
        string xml = Wrap("<xs:complexType name='B'/><xs:complexType name='M' " + block + "><xs:complexContent><xs:extension base='B'/></xs:complexContent></xs:complexType><xs:complexType name='D'><xs:complexContent><xs:restriction base='M'/></xs:complexContent></xs:complexType><xs:element name='h' type='B' abstract='true'/><xs:element name='m' type='D' substitutionGroup='h'/><xs:element name='root'><xs:complexType><xs:sequence><xs:element ref='h'/></xs:sequence></xs:complexType></xs:element>");
        // System.Xml accepts all three; 3.3.6 clause 2.3 independently requires
        // the union of every intermediate prohibited-substitution set.
        Oracle(xml, "<root><m/></root>"); Result(XmlSchema.Compile(xml), "<root><m/></root>", valid);
        var graph = Graph(xml); Assert.Equal(valid, XsdSubstitutionGroups.Members(graph, graph.GlobalElements[new("", "h")], new SchemaCompileOperation()).Contains(graph.GlobalElements[new("", "m")]));
    }
    [Theory]
    [InlineData("block='restriction'")]
    [InlineData("block='substitution'")]
    public void HeadBlockPreservesAffiliationButRefusesInstance(string modifier)
    {
        string xml = Substitution(modifiers: modifier); Oracle(xml, "<root><m>2</m></root>", false);
        var graph = Graph(xml); Assert.Empty(XsdSubstitutionGroups.Members(graph, graph.GlobalElements[new("", "h")], new SchemaCompileOperation()));
        Result(XmlSchema.Compile(xml), "<root><m>2</m></root>", false);
    }
    [Theory]
    [InlineData("xs:date", "")]
    [InlineData("xs:integer", "final='restriction'")]
    public void UnrelatedAndFinalSubstitutionReject(string member, string modifiers)
    {
        string xml = Substitution(member, modifiers: modifiers);
        Assert.Throws<System.Xml.Schema.XmlSchemaException>(() => Oracle(xml));
        Assert.Contains(Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(xml)).Diagnostics, d => d.Code == "XSD.SchemaRestriction");
    }
    [Fact]
    public void NewlyAdmittedMemberParticipatesInUpa()
    {
        string xml = Substitution(particle: "<xs:choice><xs:element ref='h'/><xs:element ref='m'/></xs:choice>");
        // System.Xml 10.0.11 accepts this ambiguous union-substitution model.
        // Keep the independent oracle discrepancy explicit: Second Edition
        // 3.8.6 requires attribution to include substitution members.
        Oracle(xml);
        Assert.Contains(Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(xml)).Diagnostics, d => d.Code == "XSD.SchemaRestriction" && d.Message.Contains("attribution", StringComparison.OrdinalIgnoreCase));
    }
    [Theory]
    [InlineData("")]
    [InlineData("<xs:sequence/>")]
    public void EmptyComplexExtensionInheritsSimpleContentAndRequiredAttributes(string particle)
    {
        string xml = Wrap("<xs:complexType name='B'><xs:simpleContent><xs:extension base='xs:int'><xs:attribute name='a' use='required'/></xs:extension></xs:simpleContent></xs:complexType>" +
            $"<xs:complexType name='D'><xs:complexContent><xs:extension base='B'>{particle}</xs:extension></xs:complexContent></xs:complexType><xs:element name='root' type='D'/>");
        Oracle(xml, "<root a='yes'>2</root>"); var schema = XmlSchema.Compile(xml);
        Result(schema, "<root a='yes'>2</root>", true); Result(schema, "<root>2</root>", false); Result(schema, "<root a='yes'>bad</root>", false);
        var d = Graph(xml).Types.Single(t => t.Name.LocalName == "D");
        Assert.Equal(new("", "B"), d.BaseType); Assert.Equal(XsdDerivation.Extension, d.Derivation); Assert.Equal(XsdContentKind.Simple, d.ContentKind);
    }
    [Theory]
    [InlineData("xs:int", "")]
    [InlineData("B", "<xs:sequence><xs:element name='e' minOccurs='0'/></xs:sequence>")]
    public void SimpleDefinitionOrElementExtensionRejects(string basis, string particle)
    {
        string xml = Wrap("<xs:complexType name='B'><xs:simpleContent><xs:extension base='xs:int'/></xs:simpleContent></xs:complexType>" +
            $"<xs:complexType name='D'><xs:complexContent><xs:extension base='{basis}'>{particle}</xs:extension></xs:complexContent></xs:complexType>");
        Assert.Throws<System.Xml.Schema.XmlSchemaException>(() => Oracle(xml)); Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(xml));
    }
}
