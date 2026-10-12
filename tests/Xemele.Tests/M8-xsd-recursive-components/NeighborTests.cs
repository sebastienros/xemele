using System.Text;
using Xemele.Validation;
using Xunit;
namespace Xemele.Tests.M8XsdRecursiveComponents;

public sealed class NeighborTests
{
    internal static string Wrap(string body) => "<xs:schema xmlns:xs='http://www.w3.org/2001/XMLSchema'>\n" + body + "\n</xs:schema>";
    internal const string Group = "<xs:group name='g'><xs:sequence><xs:element name='node'><xs:complexType><xs:sequence><xs:group ref='g' minOccurs='0'/></xs:sequence></xs:complexType></xs:element></xs:sequence></xs:group>";
    internal const string Root = "<xs:element name='root'><xs:complexType><xs:group ref='g'/></xs:complexType></xs:element>";
    internal static void Oracle(string schema, string? instance = null, bool valid = true)
    {
        var set = new System.Xml.Schema.XmlSchemaSet { XmlResolver = null };
        using var input = System.Xml.XmlReader.Create(new StringReader(schema));
        set.Add(null, input); set.Compile();
        if (instance is null) return;
        bool accepted = true;
        var settings = new System.Xml.XmlReaderSettings { Schemas = set, ValidationType = System.Xml.ValidationType.Schema, XmlResolver = null };
        settings.ValidationEventHandler += (_, _) => accepted = false;
        using var reader = System.Xml.XmlReader.Create(new StringReader(instance), settings);
        while (reader.Read()) { }
        Assert.Equal(valid, accepted);
    }
    internal static void Result(XmlSchema schema, string instance, bool valid)
    {
        var result = schema.Validate(instance);
        Assert.True(result.IsComplete); Assert.Equal(valid, result.IsValid);
        if (!valid) Assert.Contains(result.Diagnostics, d => d.Severity is XmlDiagnosticSeverity.Error);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void RecursiveGroupAcrossElementHasOrderIndependentFiniteValidation(bool reverse)
    {
        string xml = Wrap(reverse ? Root + Group : Group + Root);
        const string instance = "<root><node><node><node/></node></node></root>";
        Oracle(xml, instance); Oracle(xml, "<root><node><wrong/></node></root>", false);
        var schema = XmlSchema.Compile(xml);
        Result(schema, instance, true); Result(schema, "<root><node><wrong/></node></root>", false);
        Result(schema, "<root/>", false);
        var context = new SchemaCompileContext(); var document = context.Parse(xml).Document;
        var graph = new XmlSchemaCompiler().Compile(document, context.Options, context.Operation);
        Assert.Equal(2, graph.Elements.Count);
        Assert.Contains(graph.Types, t => t.Name.LocalName == "#model-group:g");
        Assert.All(graph.Types.Where(t => t.Anonymous && t.Name.LocalName.StartsWith("#anonymous", StringComparison.Ordinal)), t => Assert.NotNull(t.Particle));
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void MutualGroupsAcrossElementsTerminateAtOptionalContent(bool reverse)
    {
        const string a = "<xs:group name='a'><xs:sequence><xs:element name='a'><xs:complexType><xs:group ref='b' minOccurs='0'/></xs:complexType></xs:element></xs:sequence></xs:group>";
        const string b = "<xs:group name='b'><xs:sequence><xs:element name='b'><xs:complexType><xs:group ref='a' minOccurs='0'/></xs:complexType></xs:element></xs:sequence></xs:group>";
        string xml = Wrap((reverse ? b + a : a + b) + "<xs:element name='root'><xs:complexType><xs:group ref='a'/></xs:complexType></xs:element>");
        const string instance = "<root><a><b><a/></b></a></root>";
        Oracle(xml, instance); Oracle(xml, "<root><a><a/></a></root>", false);
        var schema = XmlSchema.Compile(xml);
        Result(schema, instance, true); Result(schema, "<root><a><a/></a></root>", false);
    }
    [Theory]
    [InlineData("extension", false)] [InlineData("extension", true)]
    [InlineData("restriction", false)] [InlineData("restriction", true)]
    public void InlineDerivationMayUseEnclosingCompletedType(string derivation, bool reverse)
    {
        string own = derivation == "restriction" ? "<xs:sequence><xs:element name='leaf'/></xs:sequence>" : "";
        string type = "<xs:complexType name='T'><xs:sequence><xs:element name='leaf'/><xs:element name='child' minOccurs='0'><xs:complexType><xs:complexContent><xs:" + derivation + " base='T'>" + own + "</xs:" + derivation + "></xs:complexContent></xs:complexType></xs:element></xs:sequence></xs:complexType>";
        const string root = "<xs:element name='root' type='T'/>";
        string xml = Wrap(reverse ? root + type : type + root);
        const string instance = "<root><leaf/><child><leaf/></child></root>";
        Oracle(xml, instance); Oracle(xml, "<root><child><leaf/></child></root>", false);
        var schema = XmlSchema.Compile(xml);
        Result(schema, instance, true); Result(schema, "<root><child><leaf/></child></root>", false);
        string nested = "<root><leaf/><child><leaf/><child><leaf/></child></child></root>";
        Oracle(xml, nested, derivation == "extension"); Result(schema, nested, derivation == "extension");
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void MutualGlobalInlineElementsUseFiniteDeclarationIds(bool reverse)
    {
        const string a = "<xs:element name='a'><xs:complexType><xs:sequence><xs:element ref='b' minOccurs='0'/></xs:sequence></xs:complexType></xs:element>";
        const string b = "<xs:element name='b'><xs:complexType><xs:sequence><xs:element ref='a' minOccurs='0'/></xs:sequence></xs:complexType></xs:element>";
        string xml = Wrap(reverse ? b + a : a + b);
        const string instance = "<a><b><a><b/></a></b></a>";
        Oracle(xml, instance); var schema = XmlSchema.Compile(xml);
        Result(schema, instance, true); Result(schema, "<a><a/></a>", false);
    }
    [Fact]
    public void CompletionDrainsNewlyRegisteredNestedInlineTypes()
    {
        string xml = Wrap("<xs:element name='root'><xs:complexType><xs:sequence><xs:element name='a'><xs:complexType><xs:sequence><xs:element name='b'><xs:complexType><xs:attribute name='required' use='required'/></xs:complexType></xs:element></xs:sequence></xs:complexType></xs:element></xs:sequence></xs:complexType></xs:element>");
        Oracle(xml, "<root><a><b required='yes'/></a></root>");
        var schema = XmlSchema.Compile(xml);
        Result(schema, "<root><a><b required='yes'/></a></root>", true);
        Result(schema, "<root><a><b/></a></root>", false);
    }
    [Theory]
    [InlineData("group-direct", "<xs:group name='g'><xs:sequence><xs:group ref='g'/></xs:sequence></xs:group>")]
    [InlineData("group-indirect", "<xs:group name='g'><xs:sequence><xs:group ref='h'/></xs:sequence></xs:group><xs:group name='h'><xs:sequence><xs:group ref='g'/></xs:sequence></xs:group>")]
    [InlineData("group-after-element", "<xs:group name='g'><xs:sequence><xs:element name='a'><xs:complexType><xs:group ref='h'/></xs:complexType></xs:element></xs:sequence></xs:group><xs:group name='h'><xs:sequence><xs:group ref='h'/></xs:sequence></xs:group>")]
    [InlineData("complex-direct", "<xs:complexType name='T'><xs:complexContent><xs:extension base='T'/></xs:complexContent></xs:complexType>")]
    [InlineData("complex-indirect", "<xs:complexType name='T'><xs:complexContent><xs:extension base='U'/></xs:complexContent></xs:complexType><xs:complexType name='U'><xs:complexContent><xs:restriction base='T'/></xs:complexContent></xs:complexType>")]
    [InlineData("simple-direct", "<xs:simpleType name='T'><xs:restriction base='T'/></xs:simpleType>")]
    [InlineData("simple-indirect", "<xs:simpleType name='T'><xs:restriction base='U'/></xs:simpleType><xs:simpleType name='U'><xs:restriction base='T'/></xs:simpleType>")]
    [InlineData("attribute-group", "<xs:attributeGroup name='g'><xs:attributeGroup ref='h'/></xs:attributeGroup><xs:attributeGroup name='h'><xs:attributeGroup ref='g'/></xs:attributeGroup>")]
    public void ForbiddenCyclesStillReject(string label, string body)
    {
        string xml = Wrap(body);
        Assert.Throws<System.Xml.Schema.XmlSchemaException>(() => Oracle(xml));
        var error = Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(xml));
        Assert.Contains(error.Diagnostics, d => d.Code == "XSD.SchemaRestriction" && d.Message.StartsWith("Circular", StringComparison.Ordinal));
        Assert.NotEmpty(label);
    }
    [Theory]
    [InlineData("bad-facet", "<xs:attribute name='a'><xs:simpleType><xs:restriction base='xs:int'><xs:minInclusive value='bad'/></xs:restriction></xs:simpleType></xs:attribute>")]
    [InlineData("content-consistency", "<xs:choice><xs:element name='a' type='xs:string'/><xs:element name='a' type='xs:int'/></xs:choice>")]
    [InlineData("upa", "<xs:choice><xs:element name='a'/><xs:element name='a'/></xs:choice>")]
    [InlineData("missing-group", "<xs:group ref='missing'/>")]
    public void UnusedDeferredBodiesMustPassPublicationChecks(string label, string body)
    {
        string xml = Wrap("<xs:element name='unused'><xs:complexType>" + body + "</xs:complexType></xs:element>");
        Assert.Throws<System.Xml.Schema.XmlSchemaException>(() => Oracle(xml));
        var error = Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(xml));
        Assert.DoesNotContain(error.Diagnostics, d => d.Code == "SCHEMA.Limit" || d.Code == "SCHEMA.Syntax");
        Assert.NotEmpty(label);
    }
    [Fact]
    public void CompiledSnapshotDoesNotRetainMutableSource()
    {
        string xml = Wrap(Group + Root); var context = new SchemaCompileContext();
        var document = context.Parse(xml).Document; var schema = XmlSchema.Compile(document);
        document.RootElement.SetAttribute("targetNamespace", "urn:changed");
        Result(schema, "<root><node><node/></node></root>", true);
        Result(schema, "<root xmlns='urn:changed'><node/></root>", false);
    }
}
