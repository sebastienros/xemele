using System.Security.Cryptography;
using System.Xml;
using System.Xml.Linq;
using Xunit;

namespace Xemele.Tests.Entities;

public sealed class PinnedDocumentTests
{
    [Theory]
    [InlineData("008", "5c62d26d2c0988e8a91c127b189cbbf520144a94d3f41061c3480891d7f163c4", "doc", "&<>\"'")]
    [InlineData("023", "588b9c8c22a7ec4e57df0c33038741c0840188abd12908598a9f2bca04c3a0c1", "doc", "")]
    [InlineData("024", "c534544b78396cd6295c3f07c60f514d3d38300765d97e5d6f0696d99333cf3c", "doc", "")]
    [InlineData("040", "0bbbbc77aac8efeb9bc220b566ae1b1bf1c1c40ae7e75adc90a56eb17762fb1b", "doc", "")]
    [InlineData("049", "64abd7e7d104031c2ef8c975f3240661557bde04f46108b66ea2e25d17c9d358", "doc", "£")]
    [InlineData("051", "9d943b6b0c3b9f945c45529cd6b65c0faa2772e1b42ed34de399d0e4c70dea29", "เจมส์", "")]
    [InlineData("060", "8cd9b925091421aa3541699474f23dd0f93261abf570592d5584b8940fd6d620", "doc", "X\nY")]
    [InlineData("115", "738337bb46c142f6950825850fe9c93c54fbb7fe06772181ce118f3a321de01c", "doc", "v")]
    public void PinnedWholeDocumentWithIndependentRootStructureOracle(string id, string sha256, string rootName, string text)
    {
        var cache = Environment.GetEnvironmentVariable("XEMELE_CORPUS_RESOURCES") ?? "/private/tmp/xemele-corpus-cache/w3c-xml-20130923/resources";
        var bytes = File.ReadAllBytes(Path.Combine(cache, "xmlconf", "xmltest", "valid", "sa", id + ".xml"));
        Assert.Equal(sha256, Convert.ToHexStringLower(SHA256.HashData(bytes)));
        using var source = new MemoryStream(bytes, false);
        using var reader = XmlReader.Create(source, new XmlReaderSettings { DtdProcessing = DtdProcessing.Parse, XmlResolver = null });
        var oracle = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
        var options = new XmlParseOptions { DtdProcessing = XmlDtdProcessing.Parse };
        var actual = XmlDocument.Parse(bytes.AsSpan(), options, TestContext.Current.CancellationToken);
        Assert.Equal(rootName, actual.RootElement.LocalName);
        Assert.Equal(text, actual.RootElement.GetTextContent());
        Assert.Equal(oracle.DocumentType!.Name, actual.DocumentType!.Name);
        Compare(actual.RootElement, oracle.Root!);
        for (var size = 1; size <= 32; size++)
        {
            using var chunked = new ProductionTests.BoundedStream(bytes, size);
            Compare(XmlDocument.Load(chunked, options, TestContext.Current.CancellationToken).RootElement, oracle.Root!);
            Assert.Equal(0, chunked.Disposals);
        }
    }

    private static void Compare(XmlNode node, XElement oracle)
    {
        Assert.Equal(oracle.Name.LocalName, node.LocalName);
        Assert.Equal(oracle.Name.NamespaceName, node.NamespaceUri);
        Assert.Equal(oracle.Value, node.GetTextContent());
        Assert.Equal(oracle.Attributes().Count(), node.Attributes.Count);
        foreach (var attribute in oracle.Attributes())
        {
            Assert.Equal(attribute.Value, node.GetAttribute(attribute.Name.LocalName, attribute.Name.NamespaceName));
        }

        var elements = oracle.Elements().ToArray();
        var index = 0;
        foreach (var child in node.Children)
        {
            if (child.Kind == XmlNodeKind.Element)
            {
                Compare(child, elements[index++]);
            }
        }

        Assert.Equal(elements.Length, index);
        Assert.Equal(oracle.Nodes().Count(), node.Children.Count);
    }
}
