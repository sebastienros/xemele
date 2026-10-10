using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;
namespace Xemele.Tests.IncompleteAttributes;

public sealed class TransportAndGuardsTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    [Fact]
    public void SevenExactAuthoredInputsAndExternalResource()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "global.json"))) directory = directory.Parent;
        string folder = Path.Combine(directory!.FullName, "tests/Contracts/Fixtures/M2/UndeclaredAttributes");
        using var identities = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(folder, "authored-identities.json")));
        string body = identities.RootElement.GetProperty("resource").GetProperty("utf8").GetString()!;
        Assert.Equal(identities.RootElement.GetProperty("resource").GetProperty("sha256").GetString(), Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(body))));
        int index = 0;
        foreach (var row in identities.RootElement.GetProperty("observations").EnumerateArray())
        {
            byte[] bytes = File.ReadAllBytes(Path.Combine(folder, row.GetProperty("fixture").GetString()!));
            Assert.Equal(row.GetProperty("inputSha256").GetString(), Convert.ToHexStringLower(SHA256.HashData(bytes)));
            var resolver = new Resolver("urn:subset", body, "urn:redirected");
            bool accepted = XmlDocument.TryParse(bytes, out var doc, out var diagnostics, RuntimeTests.Options with { Resolver = resolver }, Ct);
            Assert.Equal(index is 2 or 4 or 5 or 6, accepted);
            if (accepted)
            {
                Assert.Empty(diagnostics); var value = Assert.Single(doc!.IncompleteAttributeValues);
                var gap = Assert.Single(value.Parts.OfType<XmlUnknownAttributeReference>());
                Assert.Equal("missing", gap.Name); Assert.Equal("&missing;", gap.LexicalReference);
                Assert.Equal("a", value.Attribute.LocalName); Assert.Equal(doc.RootElement, value.Parent);
                Assert.NotNull(gap.Location.ByteOffset);
                Assert.Equal(index == 6 ? new Uri("urn:redirected") : null, gap.Location.SourceUri);
                Assert.Equal(index is 4 or 5 or 6 ? 1 : 0, doc.IncompleteAttributeDefaults.Count);
            }
            else Assert.Equal("XML.UndeclaredEntity", Assert.Single(diagnostics).Code);
            Assert.Equal(index >= 5 ? 1 : 0, resolver.Opens); Assert.Equal(resolver.Opens, resolver.Disposals); index++;
        }
        Assert.Equal(7, index);
    }
    [Theory]
    [InlineData("UTF-8")]
    [InlineData("UTF-16LE")]
    [InlineData("UTF-16BE")]
    [InlineData("ISO-8859-1")]
    public void EveryShortReadByteBoundaryAndMappedScalar(string label)
    {
        const string tail = RuntimeTests.Prefix + "<r a='A\r\n&x;B&#x9;'/>";
        string xml = "<?xml version='1.0' encoding='" + label + "'?>" + tail;
        Encoding encoding = label.StartsWith("UTF-16", StringComparison.Ordinal) ? new UnicodeEncoding(label.EndsWith("BE", StringComparison.Ordinal), false, true) : label == "ISO-8859-1" ? Encoding.Latin1 : new UTF8Encoding(false, true);
        byte[] bytes = encoding.GetBytes(xml);
        var expected = XmlDocument.Parse(bytes, RuntimeTests.Options, Ct);
        var value = Assert.Single(expected.IncompleteAttributeValues); var gap = Assert.Single(value.Parts.OfType<XmlUnknownAttributeReference>());
        Assert.Equal((long)encoding.GetByteCount(xml.AsSpan(0, xml.IndexOf("&x;", StringComparison.Ordinal))), gap.Location.ByteOffset);
        Assert.Equal(2, gap.Location.LineNumber); Assert.Equal(1, gap.Location.LinePosition);
        Assert.Equal("A\r\n&x;B&#x9;", value.LexicalValue);
        for (int boundary = 1; boundary < bytes.Length; boundary++)
        {
            using var source = new ShortStream(bytes, boundary);
            var doc = XmlDocument.Load(source, RuntimeTests.Options, Ct);
            Assert.True(source.CanRead);
            var actual = Assert.Single(doc.IncompleteAttributeValues);
            Assert.Equal(value.LexicalValue, actual.LexicalValue); Assert.Equal(value.ValueLocation, actual.ValueLocation); Assert.Equal(value.Parts, actual.Parts);
        }
    }
    [Fact]
    public void AllActualOutputCopyAndTextEntrypointsRefuseBeforeEffectsEvenForUnusedDefaults()
    {
        foreach (string xml in new[] { RuntimeTests.Prefix + "<r a='&x;'><leaf>known</leaf></r>",
            "<!DOCTYPE r [<!ENTITY % p ''>%p;<!ATTLIST unused a CDATA '&x;'>]><r><leaf>known</leaf></r>" })
        {
            var source = XmlDocument.Parse(xml, RuntimeTests.Options, Ct); var root = source.RootElement; var leaf = root.FirstChild;
            var destination = XmlDocument.Parse("<d/>", cancellationToken: Ct);
            int nodes = destination.Nodes.Count; long version = destination.MutationVersion;
            Refuses(() => source.ToXmlString()); Refuses(() => XmlSerializer.Serialize(source));
            foreach (bool deep in new[] { false, true })
            foreach (var node in new[] { source.DocumentNode, root, leaf })
            {
                Refuses(() => node.Clone(deep)); Refuses(() => node.GetTextContent());
                if (node.Kind != XmlNodeKind.Document) Refuses(() => destination.ImportNode(node, deep));
                Assert.Equal(nodes, destination.Nodes.Count); Assert.Equal(version, destination.MutationVersion);
            }
            Assert.Equal("known", leaf.FirstChild.Value);
            using var stream = new WriteProbe();
            foreach (bool bom in new[] { false, true })
            foreach (Encoding encoding in new Encoding[] { new UTF8Encoding(false, true), new UnicodeEncoding(false, true, true), new UnicodeEncoding(true, true, true) })
            {
                var options = new XmlSaveOptions { EmitByteOrderMark = bom, Encoding = encoding };
                Refuses(() => source.Save(stream, options)); Refuses(() => XmlSerializer.Serialize(source, stream, options));
                Assert.Equal(0, stream.Writes); Assert.Equal(0, stream.Length);
            }
            using var canceled = new CancellationTokenSource(); canceled.Cancel();
            Assert.Throws<OperationCanceledException>(() => source.ToXmlString(cancellationToken: canceled.Token));
            Assert.Throws<ArgumentNullException>(() => source.Save(null!, cancellationToken: Ct));
            Assert.Throws<ArgumentException>(() => source.ToXmlString(new() { Encoding = Encoding.ASCII }, Ct));
        }
    }
    [Fact]
    public void HandlesRefuseAfterMutationDetachmentAndReplacementWhileProvenanceSurvives()
    {
        var doc = XmlDocument.Parse(RuntimeTests.Prefix + "<r a='&x;' b='ok'/>", RuntimeTests.Options, Ct);
        var original = Assert.Single(doc.IncompleteAttributeValues); var attr = original.Attribute;
        Assert.Equal(doc.RootElement, attr.OwnerElement); Assert.True(attr.IsAttached);
        attr.Value = "new"; Assert.Throws<InvalidOperationException>(() => attr.Value);
        attr.Remove(); Assert.False(attr.IsAttached); Assert.Throws<InvalidOperationException>(() => attr.Value);
        doc.RootElement.SetAttribute("a", "replacement"); Assert.Equal("replacement", doc.RootElement.GetAttribute("a"));
        original.Parent.Remove(); Assert.True(original.Parent.Parent.IsDefault);
        Assert.Same(original, Assert.Single(doc.IncompleteAttributeValues)); Refuses(() => doc.ToXmlString());
        var clean = XmlDocument.Parse("<clean>ok</clean>", cancellationToken: Ct); Assert.Empty(clean.IncompleteAttributeValues); Assert.Empty(clean.IncompleteAttributeDefaults);
        var copied = doc.ImportNode(clean.RootElement); Assert.Equal("ok", copied.FirstChild.Value); Refuses(() => copied.Clone());
        Assert.Throws<NotSupportedException>(() => ((IList<XmlIncompleteAttributeValue>)doc.IncompleteAttributeValues).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<XmlAttributeValuePart>)original.Parts).Clear());
    }
    private static void Refuses(Action action) => Assert.Equal("Document contains unresolved entity references.", Assert.Throws<InvalidOperationException>(action).Message);
    internal sealed class Resolver(string requested, string body, string? redirected = null, int split = int.MaxValue) : XmlResourceResolver
    {
        internal int Opens, Disposals;
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken)
        { cancellationToken.ThrowIfCancellationRequested(); Assert.Equal(requested, request.Uri.AbsoluteUri); Opens++; return new(new ResourceStream(this, Encoding.UTF8.GetBytes(body), split), new Uri(redirected ?? requested)); }
        private sealed class ResourceStream(Resolver owner, byte[] bytes, int split) : ShortStream(bytes, split)
        { protected override void Dispose(bool disposing) { if (disposing) owner.Disposals++; base.Dispose(disposing); } }
    }
    private class ShortStream(byte[] bytes, int split) : MemoryStream(bytes, false)
    {
        public override int Read(Span<byte> buffer) => base.Read(buffer[..Math.Min(buffer.Length, Position < split ? split - (int)Position : 1)]);
        public override int Read(byte[] buffer, int offset, int count) => base.Read(buffer, offset, Math.Min(count, Position < split ? split - (int)Position : 1));
    }
    private sealed class WriteProbe : MemoryStream
    {
        internal int Writes;
        public override void Write(ReadOnlySpan<byte> buffer) { Writes++; base.Write(buffer); }
        public override void Write(byte[] buffer, int offset, int count) { Writes++; base.Write(buffer, offset, count); }
    }
}
