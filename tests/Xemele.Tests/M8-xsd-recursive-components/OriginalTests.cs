using Xemele.Validation;
using Xunit;
namespace Xemele.Tests.M8XsdRecursiveComponents;

public sealed class OriginalTests
{
    internal static string Fixture(string name)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "tests/fixtures/M8-xsd-recursive-components"))) directory = directory.Parent;
        return Path.Combine(directory!.FullName, "tests/fixtures/M8-xsd-recursive-components", name);
    }
    [Theory]
    [InlineData("addB077.xsd")]
    [InlineData("particlesZ010.xsd")]
    [InlineData("particlesEb041.xsd")]
    public void UnchangedOriginalSchemaPublishes(string file)
    {
        var oracle = new System.Xml.Schema.XmlSchemaSet { XmlResolver = null };
        using var reader = System.Xml.XmlReader.Create(Fixture(file));
        oracle.Add(null, reader); oracle.Compile();
        using var input = File.OpenRead(Fixture(file));
        var compiled = XmlSchema.Compile(input);
        Assert.NotNull(compiled); Assert.True(input.CanRead);
    }
    [Fact]
    public void UnchangedOriginalInstanceValidates()
    {
        var oracle = new System.Xml.Schema.XmlSchemaSet { XmlResolver = null };
        using var reader = System.Xml.XmlReader.Create(Fixture("particlesEb041.xsd"));
        oracle.Add(null, reader); oracle.Compile();
        var settings = new System.Xml.XmlReaderSettings { Schemas = oracle, ValidationType = System.Xml.ValidationType.Schema, XmlResolver = null };
        using var original = System.Xml.XmlReader.Create(Fixture("particlesEb041.xml"), settings);
        while (original.Read()) { }
        using var input = File.OpenRead(Fixture("particlesEb041.xsd"));
        var schema = XmlSchema.Compile(input);
        using var instance = File.OpenRead(Fixture("particlesEb041.xml"));
        var result = schema.Validate(instance);
        Assert.True(result.IsComplete); Assert.True(result.IsValid);
        Assert.True(instance.CanRead);
        var invalid = schema.Validate("<f:root xmlns:f='foo'><bar><foo/></bar></f:root>");
        Assert.True(invalid.IsComplete); Assert.False(invalid.IsValid);
        Assert.Contains(invalid.Diagnostics, d => d.Code == "XSD.Content");
    }
}
