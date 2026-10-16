using Xemele.Streaming;
using Xunit;

namespace Xemele.Tests.DomDecoderTerminal;

public sealed class DecoderTerminalTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    public static TheoryData<string> Reproducers =>
    [
        "PHKvPg==", "PHK+zrHwn5iAPCFbQ0RBVEFbeF1dPjwvcj4=",
        "PCHET0NUWVBFIHIgWzwhQVRUTElTVCByIGEgQ0RBVEEgJ3YnPl0+PHIvPg==",
        "PHKgYT0nMScgYT0nMicvPg==", "PHK+"
    ];

    [Theory, MemberData(nameof(Reproducers))]
    public async Task InvalidDetectionPrefixHasStructuredDiagnosticOnEveryDomTransport(string base64)
    {
        var bytes = Convert.FromBase64String(base64);
        var options = new XmlParseOptions { DtdProcessing = XmlDtdProcessing.Parse };
        Assert.False(XmlDocument.TryParse(bytes, out var document, out var diagnostics, options, Ct));
        Assert.Null(document);
        var expected = Assert.Single(diagnostics);
        Assert.Equal("INPUT.InvalidEncoding", expected.Code);
        Assert.Equal(2, expected.Location.ByteOffset);
        Assert.Equal(expected, Assert.Single(Assert.Throws<XmlParseException>(() => XmlDocument.Parse(bytes, options, Ct)).Diagnostics));
        var sax = Assert.Throws<XmlParseException>(() => XmlSaxParser.Parse(bytes, new XmlSaxCallbacks(), options, Ct));
        Assert.Equal(expected, Assert.Single(sax.Diagnostics));
        foreach (var chunk in new[] { 1, 2, 3, 4096 })
        {
            using var strict = new ShortStream(bytes, chunk);
            Assert.Equal(expected, Assert.Single(Assert.Throws<XmlParseException>(() => XmlDocument.Load(strict, options, Ct)).Diagnostics));
            Assert.True(strict.CanRead);
            using var asyncStrict = new ShortStream(bytes, chunk) { AsyncOnly = true };
            Assert.Equal(expected, Assert.Single((await Assert.ThrowsAsync<XmlParseException>(() => XmlDocument.LoadAsync(asyncStrict, options, Ct))).Diagnostics));
            Assert.True(asyncStrict.CanRead);
            foreach (var recover in new[] { false, true })
            {
                var selected = options with
                {
                    Recover = recover
                };
                var direct = XmlDocument.ParseWithDiagnostics(bytes, selected, Ct);
                Check(direct, expected);
                using var input = new ShortStream(bytes, chunk);
                Check(XmlDocument.LoadWithDiagnostics(input, selected, Ct), expected);
                Assert.True(input.CanRead);
                using var asyncInput = new ShortStream(bytes, chunk) { AsyncOnly = true };
                Check(await XmlDocument.LoadWithDiagnosticsAsync(asyncInput, selected, Ct), expected);
                Assert.True(asyncInput.CanRead);
            }
        }
    }

    private static void Check(XmlParseResult<XmlDocument> result, XmlDiagnostic expected)
    {
        Assert.False(result.IsComplete);
        Assert.False(result.IsWellFormed);
        Assert.Null(result.Value);
        Assert.Equal(expected, Assert.Single(result.Diagnostics));
    }

    private sealed class ShortStream(byte[] bytes, int chunk) : MemoryStream(bytes, false)
    {
        public bool AsyncOnly;
        public override int Read(Span<byte> buffer)
        {
            if (AsyncOnly)
            {
                throw new InvalidOperationException("Synchronous input was used.");
            }

            return Fill(buffer);
        }
        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            return Fill(buffer.Span);
        }
        private int Fill(Span<byte> buffer)
        {
            var count = 0;
            while (count < Math.Min(chunk, buffer.Length))
            {
                var value = base.ReadByte();
                if (value < 0)
                {
                    break;
                }

                buffer[count++] = (byte)value;
            }
            return count;
        }
    }
}
