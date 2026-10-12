using System.Security.Cryptography;
using System.Xml;
using Xunit;
using ProductSchema = Xemele.Validation.XmlSchema;
namespace Xemele.Tests.M8XsdAttributeFixed;

public sealed class OriginalWitnessTests
{
    internal static byte[] Original()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "docs/tasks/M8-xsd-attribute-fixed.json"))) root = root.Parent;
        Assert.NotNull(root);
        return File.ReadAllBytes(Path.Combine(root.FullName, "tests/fixtures/M8-xsd-attribute-fixed/test113911.xsd"));
    }
    [Fact]
    public void IndependentBclAcceptsUnchangedOriginal()
    {
        byte[] bytes = Original();
        using var stream = new MemoryStream(bytes);
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        var schemas = new System.Xml.Schema.XmlSchemaSet { XmlResolver = null };
        schemas.Add(null, reader); schemas.Compile(); Assert.True(schemas.IsCompiled);
    }
    [Fact]
    public void PublicFacadeAcceptsUnchangedOriginal()
    {
        using var stream = new MemoryStream(Original());
        Assert.NotNull(ProductSchema.Compile(stream)); Assert.True(stream.CanRead);
    }
}
