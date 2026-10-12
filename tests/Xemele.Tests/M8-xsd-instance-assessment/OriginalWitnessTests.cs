using Xemele.Validation;
using Xunit;
namespace Xemele.Tests.M8XsdInstanceAssessment;

public sealed class OriginalWitnessTests
{
    internal static string Fixture(string name)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "tests/fixtures/M8-xsd-instance-assessment"))) directory = directory.Parent;
        return Path.Combine(directory!.FullName, "tests/fixtures/M8-xsd-instance-assessment", name);
    }
    internal static bool BclValid(byte[] schema, byte[] xml)
    {
        var schemas = new System.Xml.Schema.XmlSchemaSet { XmlResolver = null };
        using var schemaReader = System.Xml.XmlReader.Create(new MemoryStream(schema), new() { DtdProcessing = System.Xml.DtdProcessing.Prohibit, XmlResolver = null });
        schemas.Add(null, schemaReader); schemas.Compile();
        bool valid = true;
        var settings = new System.Xml.XmlReaderSettings { Schemas = schemas, ValidationType = System.Xml.ValidationType.Schema, XmlResolver = null, DtdProcessing = System.Xml.DtdProcessing.Prohibit };
        settings.ValidationEventHandler += (_, e) => { if (e.Severity == System.Xml.Schema.XmlSeverityType.Error) valid = false; };
        using var reader = System.Xml.XmlReader.Create(new MemoryStream(xml), settings);
        while (reader.Read()) { }
        return valid;
    }
    [Theory]
    [InlineData("attZ002", true)]
    [InlineData("test71818", true)]
    [InlineData("test84188", true)]
    [InlineData("idF018", false)]
    public void UnchangedOriginalHasIndependentBclAndPublicOutcome(string name, bool valid)
    {
        byte[] source = File.ReadAllBytes(Fixture(name + ".xsd")), instance = File.ReadAllBytes(Fixture(name + ".xml"));
        Assert.Equal(valid, BclValid(source, instance));
        using var input = new MemoryStream(source);
        var schema = XmlSchema.Compile(input, new() { CancellationToken = TestContext.Current.CancellationToken });
        using var xml = new MemoryStream(instance);
        var result = schema.Validate(xml, new() { CancellationToken = TestContext.Current.CancellationToken });
        Assert.True(result.IsComplete);
        Assert.Equal(valid, result.IsValid);
        if (!valid) Assert.Contains(result.Diagnostics, d => d.Code == "XSD.Identity" && d.Location.LineNumber == 4);
        Assert.True(input.CanRead); Assert.True(xml.CanRead);
    }
}
