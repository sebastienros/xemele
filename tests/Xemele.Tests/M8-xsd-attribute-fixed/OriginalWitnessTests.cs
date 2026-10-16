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
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Xemele.slnx")))
        {
            root = root.Parent;
        }

        Assert.NotNull(root);
        var bytes = File.ReadAllBytes(Path.Combine(root.FullName, "tests/fixtures/M8-xsd-attribute-fixed/test113911.xsd"));
        Assert.Equal("ba5b1343bc8f015b7f5bcf951ffb64610a21fcd4741ffa3892408bb0d2a28bb6", Convert.ToHexStringLower(SHA256.HashData(bytes)));
        return bytes;
    }
    [Fact]
    public void IndependentBclAcceptsUnchangedOriginal()
    {
        var bytes = Original();
        using var stream = new MemoryStream(bytes);
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        var schemas = new System.Xml.Schema.XmlSchemaSet { XmlResolver = null };
        schemas.Add(null, reader);
        schemas.Compile();
        Assert.True(schemas.IsCompiled);
    }
    [Fact]
    public void PublicFacadeAcceptsUnchangedOriginal()
    {
        using var stream = new MemoryStream(Original());
        try
        {
            Assert.NotNull(ProductSchema.Compile(stream));
        }
        catch (Xemele.Validation.SchemaCompileException error)
        {
            Assert.Fail(string.Join("; ", error.Diagnostics.Select(d => $"{d.Code}: {d.Message}")));
        }
        Assert.True(stream.CanRead);
    }
}
