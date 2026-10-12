using System.Text;
using Xemele.Validation;
using Xunit;
namespace Xemele.Tests.M8XsdFacade;
// Public witnesses for coordinator-owned compiler/content repairs. Expected
// failures remain executable and are not skipped or relaxed before integration.
public sealed class PendingJoinTests
{
    [Theory]
    [InlineData("<xs:attribute name='a' type='xs:QName' fixed='p:v'/><xs:attributeGroup name='Unused'><xs:attribute ref='a' fixed='p:v' xmlns:p='urn:other'/></xs:attributeGroup>")]
    [InlineData("<xs:attributeGroup name='Unused'><xs:attribute name='a' type='xs:ID'/><xs:attribute name='b' type='xs:ID'/></xs:attributeGroup>")]
    [InlineData("<xs:group name='Unused'><xs:sequence><xs:element name='same' type='xs:int'/><xs:element name='same' type='xs:string'/></xs:sequence></xs:group>")]
    public void UnusedGroupsRetainPublicationObligations(string body)
    { Assert.Throws<SchemaCompileException>(() => PublicTests.Compile(body)); }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void PrincipalAndExternalDocumentCharactersShareOneCap(bool bytes)
    {
        string main = PublicTests.Wrap("<xs:include schemaLocation='child.xsd'/>");
        string child = PublicTests.Wrap("<xs:element name='root'/>");
        var options = new SchemaCompileOptions { BaseUri = new("https://source.test/main.xsd"),
            Resolver = new Resolver(child), Limits = new() { DocumentCharacters = Math.Max(main.Length, child.Length) } };
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(main));
        var error = Assert.Throws<SchemaCompileException>(() => { if (bytes) XmlSchema.Compile(stream, options); else XmlSchema.Compile(main, options); });
        Assert.Contains(error.Diagnostics, d => d.Code == "SCHEMA.Limit"); Assert.True(stream.CanRead);
        var exact = options with { Limits = options.Limits with { DocumentCharacters = main.Length + child.Length } };
        using var exactStream = new MemoryStream(Encoding.UTF8.GetBytes(main));
        Assert.NotNull(bytes ? XmlSchema.Compile(exactStream, exact) : XmlSchema.Compile(main, exact));
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void PrincipalAndExternalExpansionsShareOneCap(bool bytes)
    {
        string annotation = "<xs:annotation><xs:documentation>&amp;</xs:documentation></xs:annotation>";
        string main = PublicTests.Wrap(annotation + "<xs:include schemaLocation='child.xsd'/>");
        string child = PublicTests.Wrap(annotation + "<xs:element name='root'/>");
        var options = new SchemaCompileOptions { BaseUri = new("https://source.test/main.xsd"),
            Resolver = new Resolver(child), Limits = new() { ExpandedCharacters = 1 } };
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(main));
        Assert.Throws<SchemaCompileException>(() => { if (bytes) XmlSchema.Compile(stream, options); else XmlSchema.Compile(main, options); });
        var exact = options with { Limits = options.Limits with { ExpandedCharacters = 2 } };
        using var exactStream = new MemoryStream(Encoding.UTF8.GetBytes(main));
        Assert.NotNull(bytes ? XmlSchema.Compile(exactStream, exact) : XmlSchema.Compile(main, exact));
    }
    [Theory]
    [InlineData("1", "2", false)] [InlineData("1", "01.0", true)]
    [InlineData("p:v", "q:v", true)] [InlineData("p:v", "q:other", false)]
    public void ParticleRestrictionUsesTypedFixedEquality(string basis, string derived, bool valid)
    {
        string type = basis.Contains(':', StringComparison.Ordinal) ? "xs:QName" : "xs:decimal";
        string body = $"<xs:complexType name='B'><xs:sequence><xs:element name='a' type='{type}' fixed='{basis}'/></xs:sequence></xs:complexType><xs:complexType name='D'><xs:complexContent><xs:restriction base='B'><xs:sequence><xs:element name='a' type='{type}' fixed='{derived}' xmlns:q='urn:p'/></xs:sequence></xs:restriction></xs:complexContent></xs:complexType><xs:element name='root' type='D'/>";
        if (valid) Assert.NotNull(PublicTests.Compile(body));
        else Assert.Throws<SchemaCompileException>(() => PublicTests.Compile(body));
    }
    private sealed class Resolver(string child) : XmlResourceResolver
    { public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken) { cancellationToken.ThrowIfCancellationRequested(); return new(new MemoryStream(Encoding.UTF8.GetBytes(child)), request.Uri); } }
}
