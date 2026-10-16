using System.Security.Cryptography;
using System.Text;
using Xemele.Parsing.Dom;
using Xemele.Parsing.Syntax;
using Xunit;

namespace Xemele.Tests.ConformanceGrammar;

public sealed class OriginalStandaloneTests
{
    private static string Cache => Environment.GetEnvironmentVariable("XEMELE_CORPUS_RESOURCES")
        ?? "/private/tmp/xemele-corpus-cache/w3c-xml-20130923/resources";
    [Fact]
    public void OriginalInvNotSa14PreservesCdataAndMixedContentAtEverySplit()
    {
        const string path = "xmlconf/sun/invalid/not-sa14.xml";
        var bytes = Read(path, "2457653d2adc9b4dc7fc2636c570861e2dec37b0a9bbe00d9c64b047147160dc");
        var input = new UTF8Encoding(false, true).GetString(bytes);
        var source = new Uri("https://xemele.invalid/corpus/" + path);
        for (var split = 0; split <= input.Length; split++)
        {
            var resolver = new OfflineSubset();
            var options = new XmlParseOptions { DtdProcessing = XmlDtdProcessing.Parse, Resolver = resolver };
            using var core = new XmlParserCore(options, source);
            var sink = new DomSink(options);
            // This original is ASCII; original-byte mappings equal unit indices.
            long[] offsets = [.. Enumerable.Range(0, bytes.Length).Select(i => (long)i)];
            Assert.Equal(ScanStep.NeedInput, core.Append(input.AsSpan(0, split), offsets.AsSpan(0, split), false, sink, TestContext.Current.CancellationToken).Step);
            core.CompleteByteSource(bytes.Length);
            var result = core.Append(input.AsSpan(split), offsets.AsSpan(split), true, sink, TestContext.Current.CancellationToken);
            Assert.Null(result.Diagnostic);
            Assert.Equal(ScanStep.Completed, result.Step);
            var root = sink.Document.RootElement;
            Assert.Equal("root", root.LocalName);
            Assert.Equal("", root.NamespaceUri);
            Assert.False(root.Attributes.GetEnumerator().MoveNext());
            Assert.Equal(XmlNodeKind.CData, root.FirstChild.Kind);
            Assert.Equal("\n    ", root.FirstChild.Value);
            var child = root.FirstChild.NextSibling;
            Assert.Equal("child", child.LocalName);
            Assert.Equal("", child.NamespaceUri);
            Assert.False(child.Attributes.GetEnumerator().MoveNext());
            Assert.Equal("\n    The whitespace before and after this element keeps\n    this from being standalone.  (CDATA is just another\n    way to represent text...)\n    ", child.GetTextContent());
            Assert.Equal(XmlNodeKind.CData, child.NextSibling.Kind);
            Assert.Equal("\n", child.NextSibling.Value);
            Assert.True(child.NextSibling.NextSibling.IsDefault);
            Assert.Equal(1, resolver.Opens);
            Assert.Equal(1, resolver.Disposals);
        }
    }
    private static byte[] Read(string path, string expected)
    {
        var bytes = File.ReadAllBytes(Path.Combine(Cache, path));
        Assert.Equal(expected, Convert.ToHexStringLower(SHA256.HashData(bytes)));
        return bytes;
    }
    private sealed class OfflineSubset : XmlResourceResolver
    {
        internal int Opens, Disposals;
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Equal("https://xemele.invalid/corpus/xmlconf/sun/valid/sa.dtd", request.Uri.AbsoluteUri);
            var bytes = Read("xmlconf/sun/valid/sa.dtd", "e7e377a4d1cc47387984267490f2402fef5519f455101efb99594ef6db3e2c20");
            // Independent grant is the original resource, never an online fetch.
            Opens++;
            return new(new Tracked(bytes, this), request.Uri);
        }
        private sealed class Tracked(byte[] bytes, OfflineSubset owner) : MemoryStream(bytes, false)
        {
            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    owner.Disposals++;
                }

                base.Dispose(disposing);
            }
        }
    }
}
