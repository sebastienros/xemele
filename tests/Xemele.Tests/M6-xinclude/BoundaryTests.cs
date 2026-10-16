using System.Text;
using Xunit;
namespace Xemele.Tests.M6XInclude;

public sealed class BoundaryTests
{
    private const string Xi = "http://www.w3.org/2001/XInclude";
    private static XmlDocument Input(string attributes = "", string fallback = "") => XmlDocument.Parse($"<r xmlns:xi='{Xi}'><xi:include href='child' {attributes}>{fallback}</xi:include></r>", cancellationToken: TestContext.Current.CancellationToken);
    private static XIncludeOptions Options(XmlResourceResolver resolver) => new() { BaseUri = new("https://test/main"), Resolver = resolver };
    private sealed class BytesResolver(byte[] bytes) : XmlResourceResolver
    {
        public Exception? ReadError
        {
            get; init;
        }
        public Exception? DisposeError
        {
            get; init;
        }
        public int Disposals;
        public int ReadBytes;
        public int MaxRequest;
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken) => new(new Content(bytes, this), request.Uri);
        private sealed class Content(byte[] bytes, BytesResolver owner) : MemoryStream(bytes)
        {
            public override int Read(Span<byte> buffer)
            {
                owner.MaxRequest = Math.Max(owner.MaxRequest, buffer.Length);
                if (owner.ReadError is not null)
                {
                    throw owner.ReadError;
                }

                var count = base.Read(buffer);
                owner.ReadBytes += count;
                return count;
            }
            public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return ValueTask.FromResult(Read(buffer.Span));
            }
            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    owner.Disposals++;
                    if (owner.DisposeError is not null)
                    {
                        throw owner.DisposeError;
                    }
                }
                base.Dispose(disposing);
            }
        }
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StreamExceptionPreservedWhenCleanupAlsoThrows(bool asynchronous)
    {
        var original = new IOException("read");
        var cleanup = new IOException("cleanup");
        var resolver = new BytesResolver("<a/>"u8.ToArray()) { ReadError = original, DisposeError = cleanup };
        var input = Input(fallback: "<xi:fallback><ok/></xi:fallback>");
        var revision = input.ContentRevision;
        if (asynchronous)
        {
            Assert.Same(original, await Assert.ThrowsAsync<IOException>(() => XIncludeProcessor.ProcessAsync(input, Options(resolver), TestContext.Current.CancellationToken)));
        }
        else
        {
            Assert.Same(original, Assert.Throws<IOException>(() => XIncludeProcessor.Process(input, Options(resolver), TestContext.Current.CancellationToken)));
        }

        Assert.Equal(1, resolver.Disposals);
        Assert.Equal(revision, input.ContentRevision);
    }
    [Fact]
    public void SuccessfulAcquisitionCleanupExceptionPropagates()
    {
        var original = new IOException("cleanup");
        var resolver = new BytesResolver("<a/>"u8.ToArray()) { DisposeError = original };
        Assert.Same(original, Assert.Throws<IOException>(() => XIncludeProcessor.Process(Input(), Options(resolver), TestContext.Current.CancellationToken)));
        Assert.Equal(1, resolver.Disposals);
    }
    [Theory]
    [InlineData("UTF-8", "é")]
    [InlineData("UTF-16LE", "é")]
    [InlineData("UTF-16BE", "é")]
    [InlineData("ISO-8859-1", "é")]
    [InlineData("WINDOWS-1252", "€")]
    public void SupportedTextEncodingLabelsDecodeStrictly(string label, string expected)
    {
        var bytes = label switch
        {
            "UTF-8" => Encoding.UTF8.GetBytes(expected),
            "UTF-16LE" => Encoding.Unicode.GetBytes(expected),
            "UTF-16BE" => Encoding.BigEndianUnicode.GetBytes(expected),
            "ISO-8859-1" => [0xE9],
            _ => [0x80]
        };
        var result = XIncludeProcessor.Process(Input($"parse='text' encoding='{label}'"), Options(new BytesResolver(bytes)), TestContext.Current.CancellationToken);
        Assert.True(result.Success);
        Assert.Equal(expected, result.Document!.RootElement.GetTextContent());
    }
    [Theory]
    [InlineData("UTF-8", new byte[] { 0xFF, 0xFE, 0x61, 0 })]
    [InlineData("UTF-16BE", new byte[] { 0xFF, 0xFE, 0x61, 0 })]
    [InlineData("UTF-8", new byte[] { 0xC0, 0xAF })]
    [InlineData("UTF-8", new byte[] { 0 })]
    [InlineData("WINDOWS-1252", new byte[] { 0x81 })]
    [InlineData("UTF-16", new byte[] { 0x61, 0 })]
    public void InvalidEncodingOrXmlCharacterRejects(string label, byte[] bytes)
    {
        var result = XIncludeProcessor.Process(Input($"parse='text' encoding='{label}'"), Options(new BytesResolver(bytes)), TestContext.Current.CancellationToken);
        Assert.False(result.Success);
        Assert.Equal("XINCLUDE.ResourceError", result.Diagnostics[^1].Code);
    }
    [Fact]
    public void GenericUtf16BomSelectsEndian()
    {
        var result = XIncludeProcessor.Process(Input("parse='text' encoding='UTF-16'"), Options(new BytesResolver([0xFE, 0xFF, 0, 0x61])), TestContext.Current.CancellationToken);
        Assert.True(result.Success);
        Assert.Equal("a", result.Document!.RootElement.GetTextContent());
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ByteLimitProbesAtMostOneBeyondRemaining(bool asynchronous)
    {
        var resolver = new BytesResolver("123456789012345"u8.ToArray());
        var options = Options(resolver) with
        {
            Limits = new()
            {
                ExternalBytes = 4
            }
        };
        var result = asynchronous ? await XIncludeProcessor.ProcessAsync(Input("parse='text'"), options, TestContext.Current.CancellationToken) : XIncludeProcessor.Process(Input("parse='text'"), options, TestContext.Current.CancellationToken);
        Assert.False(result.Success);
        Assert.Equal("XINCLUDE.Limit", result.Diagnostics[^1].Code);
        Assert.Equal(5, resolver.ReadBytes);
        Assert.Equal(5, resolver.MaxRequest);
        Assert.Equal(1, resolver.Disposals);
    }
    [Theory]
    [InlineData(" x ", true)]
    [InlineData(" x ", false)]
    public void IdWhitespaceNormalizationIsXmlOnly(string value, bool success)
    {
        var input = XmlDocument.Parse($"<r xmlns:xi='{Xi}'><a xml:id='{value}'/><xi:include xpointer='x'/></r>", cancellationToken: TestContext.Current.CancellationToken);
        var result = XIncludeProcessor.Process(input, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(success, result.Success);
    }
    [Fact]
    public void DiagnosticsQuotaIncludesTerminalLimitEntry()
    {
        var input = XmlDocument.Parse($"<r xmlns:xi='{Xi}'><xi:include href='a'><xi:fallback/></xi:include><xi:include href='b'><xi:fallback/></xi:include></r>", cancellationToken: TestContext.Current.CancellationToken);
        var result = XIncludeProcessor.Process(input, new()
        {
            BaseUri = new("https://test/main"),
            Limits = new()
            {
                Diagnostics = 1
            }
        }, TestContext.Current.CancellationToken);
        Assert.False(result.Success);
        Assert.Equal("XINCLUDE.Limit", Assert.Single(result.Diagnostics).Code);
    }
}
