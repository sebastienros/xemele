using System.Text;
using Xunit;

namespace Xemele.Tests.ConformanceGrammar;

public sealed class StandaloneTransportTests
{
    [Theory]
    [InlineData("<![CDATA[ \t\r\n]]>&#32;<child>🙂</child>", true, " \t\n 🙂")]
    [InlineData("&stored;<child/>", false, "")]
    [InlineData("&reference;<child/>", true, " ")]
    [InlineData("&cdata;<child/>", true, " ")]
    [InlineData(" <child/>", false, "")]
    public void DtdProcessedOriginsAcrossByteEncodingsAndOneByteReads(string body, bool accepted, string text)
    {
        foreach (Encoding encoding in new Encoding[] { new UTF8Encoding(true, true), new UnicodeEncoding(false, true, true), new UnicodeEncoding(true, true, true) })
        {
            string label = encoding is UTF8Encoding ? "UTF-8" : "UTF-16";
            string input = "<?xml version='1.0' encoding='" + label + "' standalone='yes'?>" +
                "<!DOCTYPE r SYSTEM 'urn:standalone:subset' [<!ENTITY stored '&#32;'><!ENTITY reference '&#38;#32;'><!ENTITY cdata '<![CDATA[ ]]>'>]><r>" + body + "</r>";
            byte[] bytes = [.. encoding.GetPreamble(), .. encoding.GetBytes(input)];
            foreach (int size in new[] { 1, 2, 3, 7, bytes.Length })
            {
                var resolver = new Subset(); using var stream = new Bounded(bytes, size);
                var options = new XmlParseOptions { DtdProcessing = XmlDtdProcessing.Parse, Resolver = resolver };
                try
                {
                    var document = XmlDocument.Load(stream, options, TestContext.Current.CancellationToken);
                    Assert.True(accepted); Assert.Equal(text, document.RootElement.GetTextContent());
                    Assert.Empty(document.UnresolvedReferences);
                    Assert.Equal(" ", document.Dtd!.GeneralEntities[0].ReplacementText);
                    Assert.Equal("&#32;", document.Dtd.GeneralEntities[1].ReplacementText);
                }
                catch (XmlParseException error)
                {
                    Assert.False(accepted); Assert.Equal("XML.Standalone", Assert.Single(error.Diagnostics).Code);
                }
                Assert.Equal(1, resolver.Opens); Assert.Equal(1, resolver.Disposals);
                Assert.Equal(0, stream.Disposals); Assert.True(stream.CanRead);
            }
        }
    }
    private sealed class Subset : XmlResourceResolver
    {
        internal int Opens, Disposals;
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Equal(new Uri("urn:standalone:subset"), request.Uri); Opens++;
            return new(new Tracked(Encoding.UTF8.GetBytes("<!ELEMENT r (child)*><!ELEMENT child (#PCDATA)>"), this), new Uri("https://offline.invalid/subset.dtd"));
        }
        private sealed class Tracked(byte[] bytes, Subset owner) : MemoryStream(bytes, false)
        { protected override void Dispose(bool disposing) { if (disposing) owner.Disposals++; base.Dispose(disposing); } }
    }
    private sealed class Bounded(byte[] bytes, int size) : MemoryStream(bytes, false)
    {
        internal int Disposals;
        public override int Read(byte[] buffer, int offset, int count) => base.Read(buffer, offset, Math.Min(count, size));
        public override int Read(Span<byte> buffer) => base.Read(buffer[..Math.Min(buffer.Length, size)]);
        protected override void Dispose(bool disposing) { if (disposing) Disposals++; base.Dispose(disposing); }
    }
}
