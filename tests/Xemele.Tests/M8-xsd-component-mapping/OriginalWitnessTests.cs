using Xemele.Validation;
using Xunit;
namespace Xemele.Tests.M8XsdComponentMapping;

public sealed class OriginalWitnessTests
{
    internal static string Fixture(string name)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "tests/fixtures/M8-xsd-component-mapping")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory!.FullName, "tests/fixtures/M8-xsd-component-mapping", name);
    }
    [Fact]
    public void OriginalGroupN007AcceptsMixedCharacters()
    {
        // Independent BCL instance oracle consumes the unchanged original bytes.
        var schemas = new System.Xml.Schema.XmlSchemaSet { XmlResolver = null };
        using var schemaReader = System.Xml.XmlReader.Create(Fixture("groupN007.xsd"));
        schemas.Add(null, schemaReader);
        schemas.Compile();
        var settings = new System.Xml.XmlReaderSettings { Schemas = schemas, ValidationType = System.Xml.ValidationType.Schema, XmlResolver = null };
        using var originalReader = System.Xml.XmlReader.Create(Fixture("groupN007.xml"), settings);
        while (originalReader.Read())
        {
        }
        using var schema = File.OpenRead(Fixture("groupN007.xsd"));
        var compiled = XmlSchema.Compile(schema);
        using var instance = File.OpenRead(Fixture("groupN007.xml"));
        var result = compiled.Validate(instance);
        Assert.True(result.IsComplete);
        Assert.True(result.IsValid, string.Join("; ", result.Diagnostics.Select(d => d.Code + ": " + d.Message)));
        Assert.True(schema.CanRead);
        Assert.True(instance.CanRead);
        var child = compiled.Validate("<doc><elem><a1/></elem></doc>");
        Assert.True(child.IsComplete);
        Assert.False(child.IsValid);
        Assert.Contains(child.Diagnostics, d => d.Code == "XSD.Content");
    }
    [Fact]
    public void OriginalGroupH007RejectsMixedRestrictionOfElementOnlyBase()
    {
        var bcl = new System.Xml.Schema.XmlSchemaSet { XmlResolver = null };
        using var reader = System.Xml.XmlReader.Create(Fixture("groupH007.xsd"));
        Assert.Throws<System.Xml.Schema.XmlSchemaException>(() => { bcl.Add(null, reader); bcl.Compile(); });
        using var original = File.OpenRead(Fixture("groupH007.xsd"));
        var error = Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(original));
        Assert.Contains(error.Diagnostics, d => d.Code == "XSD.SchemaRestriction");
    }
    [Fact]
    public void OriginalGroupN008RejectsForbiddenChildren()
    {
        using var schema = File.OpenRead(Fixture("groupN008.xsd"));
        using var instance = File.OpenRead(Fixture("groupN008.xml"));
        var result = XmlSchema.Compile(schema).Validate(instance);
        Assert.True(result.IsComplete);
        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, d => d.Code == "XSD.Content");
    }
    [Theory]
    [InlineData("xv001.xsd")]
    [InlineData("xv004.xsd")]
    public void OriginalSaxonComponentCompilerRejectsNames(string file)
    {
        var context = new SchemaCompileContext();
        using var input = File.OpenRead(Fixture(file));
        var document = context.Parse(input).Document;
        var error = Assert.Throws<SchemaCompileException>(() => new XmlSchemaCompiler().Compile(document, context.Options, context.Operation));
        Assert.Contains(error.Diagnostics, d => d.Message == "Invalid NCName.");
    }
    [Theory]
    [InlineData("xv001.xsd")]
    [InlineData("xv004.xsd")]
    public void OriginalSaxonSchemasRejectUnderApprovedSecondEditionProfile(string file)
    {
        // Original upstream expectation remains VALID. Approved profile is INVALID:
        // U+0133 is outside Second Edition BaseChar/Ideographic; supplementary
        // component names likewise have no membership in its Appendix B productions.
        using var input = File.OpenRead(Fixture(file));
        var error = Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(input));
        Assert.Contains(error.Diagnostics, d => d.Code == "XSD.SchemaRestriction" && d.Message == "Invalid NCName.");
        Assert.DoesNotContain(error.Diagnostics, d => d.Code == "SCHEMA.Syntax");
        Assert.True(input.CanRead);
    }
}
