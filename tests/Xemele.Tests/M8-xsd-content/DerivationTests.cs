using System.Text;
using Xemele.Validation;
using Xunit;
namespace Xemele.Tests.M8XsdContent;

public sealed class DerivationTests
{
    [Theory]
    [InlineData("<xs:sequence><xs:element name='a' minOccurs='0'/><xs:element name='b'/></xs:sequence>", "<xs:sequence><xs:element name='b'/></xs:sequence>", true)]
    [InlineData("<xs:sequence><xs:element name='a'/><xs:element name='b'/></xs:sequence>", "<xs:sequence><xs:element name='b'/></xs:sequence>", false)]
    [InlineData("<xs:choice maxOccurs='3'><xs:element name='a'/><xs:element name='b'/></xs:choice>", "<xs:sequence><xs:element name='a'/><xs:element name='b'/></xs:sequence>", true)]
    [InlineData("<xs:choice><xs:element name='a'/><xs:element name='b'/></xs:choice>", "<xs:sequence><xs:element name='a'/><xs:element name='b'/></xs:sequence>", false)]
    [InlineData("<xs:all><xs:element name='a'/><xs:element name='b'/></xs:all>", "<xs:sequence><xs:element name='b'/><xs:element name='a'/></xs:sequence>", true)]
    [InlineData("<xs:all><xs:element name='a'/><xs:element name='b'/></xs:all>", "<xs:all><xs:element name='b'/><xs:element name='a'/></xs:all>", false)]
    [InlineData("<xs:choice><xs:element name='a'/><xs:element name='b'/></xs:choice>", "<xs:choice><xs:element name='b'/><xs:element name='a'/></xs:choice>", false)]
    [InlineData("<xs:sequence><xs:any namespace='urn:a urn:b' processContents='lax' minOccurs='0' maxOccurs='unbounded'/></xs:sequence>", "<xs:sequence><xs:any namespace='urn:a' processContents='strict'/></xs:sequence>", true)]
    [InlineData("<xs:sequence><xs:any namespace='urn:a' processContents='strict'/></xs:sequence>", "<xs:sequence><xs:any namespace='urn:a' processContents='skip'/></xs:sequence>", false)]
    [InlineData("<xs:sequence><xs:any namespace='##other' minOccurs='0' maxOccurs='unbounded'/></xs:sequence>", "<xs:sequence><xs:any namespace='##any'/></xs:sequence>", false)]
    [InlineData("<xs:sequence><xs:element name='a' nillable='true' type='xs:string'/></xs:sequence>", "<xs:sequence><xs:element name='a' type='xs:token'/></xs:sequence>", true)]
    [InlineData("<xs:sequence><xs:element name='a'/></xs:sequence>", "<xs:sequence><xs:element name='a' nillable='true'/></xs:sequence>", false)]
    [InlineData("<xs:sequence><xs:element name='a' block='restriction'/></xs:sequence>", "<xs:sequence><xs:element name='a'/></xs:sequence>", false)]
    [InlineData("<xs:sequence><xs:element name='a' minOccurs='0'/></xs:sequence>", "", true)]
    [InlineData("<xs:sequence><xs:element name='a'/></xs:sequence>", "", false)]
    public void NormativeParticleRestrictionTable(string basis, string restriction, bool valid)
    {
        var schema = ContentTests.Schema($"<xs:complexType name='B'>{basis}</xs:complexType><xs:complexType name='T'><xs:complexContent><xs:restriction base='B'>{restriction}</xs:restriction></xs:complexContent></xs:complexType>");
        if (valid) Assert.NotNull(ContentTests.Program(schema)); else Assert.Throws<SchemaCompileException>(() => ContentTests.Program(schema));
    }
    [Theory]
    [InlineData("<xs:attribute name='a' use='required'/>", "", true)]
    [InlineData("<xs:attribute name='a' use='required'/>", "<xs:attribute name='a'/>", false)]
    [InlineData("<xs:attribute name='a' use='required'/>", "<xs:attribute name='a' use='required'/>", true)]
    [InlineData("<xs:attribute name='a' type='xs:string'/>", "<xs:attribute name='a' type='xs:token'/>", true)]
    [InlineData("<xs:attribute name='a' type='xs:token'/>", "<xs:attribute name='a' type='xs:string'/>", false)]
    [InlineData("<xs:attribute name='a' fixed='x'/>", "<xs:attribute name='a' fixed='y'/>", false)]
    [InlineData("<xs:anyAttribute namespace='##any' processContents='lax'/>", "<xs:anyAttribute namespace='##local' processContents='strict'/>", true)]
    [InlineData("<xs:anyAttribute namespace='##local' processContents='strict'/>", "<xs:anyAttribute namespace='##local' processContents='lax'/>", false)]
    [InlineData("", "<xs:anyAttribute/>", false)]
    [InlineData("", "<xs:attribute name='new'/>", false)]
    public void EffectiveAttributeAndWildcardRestriction(string basis, string restriction, bool valid)
    {
        var schema = ContentTests.Schema($"<xs:complexType name='B'>{basis}</xs:complexType><xs:complexType name='T'><xs:complexContent><xs:restriction base='B'>{restriction}</xs:restriction></xs:complexContent></xs:complexType>");
        if (valid) Assert.NotNull(ContentTests.Program(schema)); else Assert.Throws<SchemaCompileException>(() => ContentTests.Program(schema));
    }
    [Fact]
    public void ExtensionHasEffectiveBasePrefixAndPreservesAttributeWildcards()
    {
        var schema = ContentTests.Schema("<xs:complexType name='B'><xs:sequence><xs:element name='a'/></xs:sequence><xs:attribute name='key' use='required'/><xs:anyAttribute namespace='urn:old'/></xs:complexType><xs:complexType name='T'><xs:complexContent><xs:extension base='B'><xs:sequence><xs:element name='b'/></xs:sequence><xs:anyAttribute namespace='urn:new'/></xs:extension></xs:complexContent></xs:complexType>");
        Assert.True(ContentTests.Accepts(ContentTests.Program(schema), "a", "b")); Assert.False(ContentTests.Accepts(ContentTests.Program(schema), "b", "a"));
    }
    [Fact]
    public void ExtensionCannotChangeMixednessAndHistoryUsesExtensionFirstIntermediate()
    {
        var mismatch = ContentTests.Schema("<xs:complexType name='B' mixed='true'><xs:sequence><xs:element name='a'/></xs:sequence></xs:complexType><xs:complexType name='T'><xs:complexContent mixed='false'><xs:extension base='B'><xs:sequence><xs:element name='b'/></xs:sequence></xs:extension></xs:complexContent></xs:complexType>");
        Assert.Throws<SchemaCompileException>(() => ContentTests.Program(mismatch));
        var resurrection = ContentTests.Schema("<xs:complexType name='B'><xs:attribute name='a'/></xs:complexType><xs:complexType name='R'><xs:complexContent><xs:restriction base='B'/></xs:complexContent></xs:complexType><xs:complexType name='T'><xs:complexContent><xs:extension base='R'><xs:attribute name='a'/></xs:extension></xs:complexContent></xs:complexType>");
        Assert.NotNull(ContentTests.Program(resurrection));
        Assert.Throws<SchemaCompileException>(() => ContentTests.Schema("<xs:complexType name='B'><xs:attribute name='a' type='xs:string'/></xs:complexType><xs:complexType name='R'><xs:complexContent><xs:restriction base='B'/></xs:complexContent></xs:complexType><xs:complexType name='T'><xs:complexContent><xs:extension base='R'><xs:attribute name='a' type='xs:integer'/></xs:extension></xs:complexContent></xs:complexType>"));
    }
    [Theory]
    [InlineData("", true)]
    [InlineData("substitution", false)]
    [InlineData("restriction", false)]
    public void SubstitutionClosureReturnsActualGlobalDeclaration(string block, bool expected)
    {
        var schema = ContentTests.Schema($"<xs:element name='head' type='xs:string' abstract='true' block='{block}'/><xs:element name='mid' type='xs:token' substitutionGroup='head' abstract='true'/><xs:element name='child' type='xs:token' substitutionGroup='mid'/><xs:complexType name='T'><xs:sequence><xs:element ref='head'/></xs:sequence></xs:complexType>");
        var cursor = new XsdContentMatcher().CreateCursor(ContentTests.Program(schema)); var op = new SchemaCompileContext().Operation;
        Assert.Equal(expected, cursor.Accept(new("", "child"), op, out var id, out _));
        if (expected) { Assert.Equal(schema.GlobalElements[new("", "child")], id); Assert.True(cursor.Complete(op)); }
        else Assert.Null(id);
        Assert.False(new XsdContentMatcher().CreateCursor(ContentTests.Program(schema)).Accept(new("", "head"), op, out _, out _));
    }
    [Fact]
    public void TypeBlockAndCandidateOwnBlockAreDistinct()
    {
        string bodies = "<xs:complexType name='B'/><xs:complexType name='D' block='restriction'><xs:complexContent><xs:restriction base='B'/></xs:complexContent></xs:complexType><xs:element name='h' type='B'/><xs:element name='c' type='D' substitutionGroup='h'/><xs:complexType name='T'><xs:sequence><xs:element ref='h'/></xs:sequence></xs:complexType>";
        var schema = ContentTests.Schema(bodies); Assert.True(ContentTests.Accepts(ContentTests.Program(schema), "c"));
        schema = ContentTests.Schema(bodies.Replace("name='B'/>", "name='B' block='restriction'/>", StringComparison.Ordinal));
        Assert.False(ContentTests.Accepts(ContentTests.Program(schema), "c"));
    }
    [Fact]
    public void SubstitutionNamesParticipateInUpaAndParticleRestriction()
    {
        string declarations = "<xs:element name='h'/><xs:element name='c' substitutionGroup='h'/>";
        var ambiguous = ContentTests.Schema(declarations + "<xs:complexType name='T'><xs:choice><xs:element ref='h'/><xs:element ref='c'/></xs:choice></xs:complexType>");
        Assert.Throws<SchemaCompileException>(() => ContentTests.Program(ambiguous));
        var restriction = ContentTests.Schema(declarations + "<xs:complexType name='B'><xs:sequence><xs:element ref='h'/></xs:sequence></xs:complexType><xs:complexType name='T'><xs:complexContent><xs:restriction base='B'><xs:sequence><xs:element ref='c'/></xs:sequence></xs:restriction></xs:complexContent></xs:complexType>");
        Assert.True(ContentTests.Accepts(ContentTests.Program(restriction), "c"));
    }
    [Theory]
    [InlineData("<xs:group name='g'><xs:sequence><xs:element name='not-allowed'/></xs:sequence></xs:group>", false)]
    [InlineData("<xs:group name='g'><xs:sequence><xs:element name='a'/></xs:sequence></xs:group>", true)]
    [InlineData("<xs:attributeGroup name='ag'><xs:attribute name='not-allowed'/></xs:attributeGroup>", false)]
    [InlineData("<xs:attributeGroup name='ag'><xs:attribute name='a'/></xs:attributeGroup>", true)]
    public void UnreferencedRedefinitionCarriersAreFinalized(string replacement, bool valid)
    {
        string original = $"<xs:schema xmlns:xs='{ContentTests.Xs}'><xs:group name='g'><xs:choice><xs:element name='a'/><xs:element name='b'/></xs:choice></xs:group><xs:attributeGroup name='ag'><xs:attribute name='a'/><xs:attribute name='b'/></xs:attributeGroup></xs:schema>";
        var context = new SchemaCompileContext(new() { Resolver = new Resolver(original) });
        var document = context.Parse($"<xs:schema xmlns:xs='{ContentTests.Xs}'><xs:redefine schemaLocation='urn:base'>{replacement}</xs:redefine><xs:complexType name='T'/></xs:schema>").Document;
        var schema = new XmlSchemaCompiler().Compile(document, context.Options, context.Operation);
        if (valid) Assert.NotNull(ContentTests.Program(schema)); else Assert.Throws<SchemaCompileException>(() => ContentTests.Program(schema));
    }
    private sealed class Resolver(string text) : XmlResourceResolver
    { public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken token) { token.ThrowIfCancellationRequested(); return new(new MemoryStream(Encoding.UTF8.GetBytes(text)), request.Uri); } }
}
