using System.Security.Cryptography;
using System.Text;
using Xemele.Internal.Operations;
using Xemele.Parsing.Dtd;
using Xemele.Parsing.Entities;
using Xemele.Resolution.Core;
using Xemele.Tests.Entities;
using Xunit;

namespace Xemele.Tests.ExternalDeclarationHook;

public sealed class ExternalDeclarationTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private static readonly Uri FinalUri = new("https://final.example/entity");
    // Exact inventory-pinned E38.ent bytes, including both original LF bytes.
    private static ReadOnlySpan<byte> E38 => "<?xml version=\"1.1\" encoding=\"utf-8\"?>\n<foo/>\n"u8;

    [Fact]
    public void OriginalE38MetadataSurvivesFramerDisposalAndBodyRefills()
    {
        var bytes = E38.ToArray();
        Assert.Equal(46, bytes.Length);
        Assert.Equal("028229ed9dda8481d7996ef03a1e17300255abef2bf082011312f09b28623eef", Convert.ToHexStringLower(SHA256.HashData(bytes)));
        for (var size = 1; size <= bytes.Length; size++)
        {
            using var fixture = new SourceFixture(bytes, size);
            Assert.True(fixture.Frame());
            var declaration = fixture.Source.TextDeclaration!.Value;
            Assert.Equal("1.1", declaration.Version);
            Assert.Equal("utf-8", declaration.EncodingName);
            Assert.Equal(new XmlLocation(FinalUri, 1, 1, 0), declaration.Location);
            Assert.Equal(new XmlLocation(FinalUri, 1, 39, null), fixture.Source.FirstLocation);
            Assert.Equal("\n<foo/>\n", fixture.ReadBody(out var offsets));
            Assert.Equal(Enumerable.Range(38, 8).Select(i => (long)i), offsets);
            fixture.Dispose();
            Assert.Equal(declaration, fixture.Source.TextDeclaration);
            Assert.Equal("1.1", declaration.Version);
            Assert.Equal(1, fixture.Resolver.Stream!.Disposals);
        }
        var resolver = new Resolver(bytes, 1);
        var document = XmlDocument.Parse("<!DOCTYPE r [<!ENTITY e SYSTEM 'urn:entity'>]><r>&e;</r>", new()
        {
            DtdProcessing = XmlDtdProcessing.Parse,
            Resolver = resolver
        }, Cancellation);
        Assert.Equal("foo", document.RootElement.FirstChild.NextSibling.LocalName);
        Assert.Equal(1, resolver.Stream!.Disposals); // Existing composition behavior; Core policy is a later task.
    }

    [Theory]
    [InlineData(null)]
    [InlineData("1.0")]
    [InlineData("1.1")]
    [InlineData("1.7")]
    [InlineData("1.00")]
    [InlineData("1.10")]
    public void OptionalExactVersionsAndOriginalBomMappingsSurviveEveryReadSize(string? version)
    {
        foreach (var encoding in new Encoding[] { new UTF8Encoding(true, true), new UnicodeEncoding(false, true, true), new UnicodeEncoding(true, true, true) })
        {
            var name = encoding is UTF8Encoding ? "UTF-8" : "UTF-16";
            var prolog = "<?xml\r\n" + (version is null ? "" : $"version='{version}' ") + $"encoding='{name}'?>";
            byte[] bytes = [.. encoding.GetPreamble(), .. encoding.GetBytes(prolog + "😀z")];
            for (var size = 1; size <= bytes.Length; size++)
            {
                using var fixture = new SourceFixture(bytes, size);
                Assert.True(fixture.Frame());
                var metadata = fixture.Source.TextDeclaration!.Value;
                Assert.Equal(version, metadata.Version);
                Assert.Equal(name, metadata.EncodingName);
                Assert.Equal(new XmlLocation(FinalUri, 1, 1, encoding.GetPreamble().Length), metadata.Location);
                Assert.Equal(2, fixture.Source.FirstLocation.LineNumber);
                Assert.Equal(prolog.Length - prolog.IndexOf('\n'), fixture.Source.FirstLocation.LinePosition);
                Assert.Equal("😀z", fixture.ReadBody(out var offsets));
                long start = encoding.GetPreamble().Length + encoding.GetByteCount(prolog);
                Assert.Equal(new[] { start, start, start + encoding.GetByteCount("😀") }, offsets);
                fixture.Dispose();
                Assert.Equal(metadata, fixture.Source.TextDeclaration);
                Assert.Equal(1, fixture.Resolver.Stream!.Disposals);
            }
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("tail")]
    [InlineData("<?xml-stylesheet?>")]
    public void AbsentDeclarationDoesNotInventMetadataOrLosePrefix(string body)
    {
        using var fixture = new SourceFixture(Encoding.UTF8.GetBytes(body), 1);
        Assert.Null(fixture.Source.TextDeclaration);
        Assert.True(fixture.Frame());
        Assert.Null(fixture.Source.TextDeclaration);
        Assert.Equal(body, fixture.ReadBody(out var offsets));
        Assert.Equal(Enumerable.Range(0, body.Length).Select(i => (long)i), offsets);
        fixture.Dispose();
        Assert.Equal(1, fixture.Resolver.Stream!.Disposals);
    }

    [Theory]
    [InlineData("<?xml version='2.0' encoding='UTF-8'?>", "XML.Syntax")]
    [InlineData("<?xml version='1.1' encoding='UTF-16'?>", "INPUT.EncodingMismatch")]
    [InlineData("<?xml version='1.1' encoding='unknown'?>", "INPUT.UnsupportedEncoding")]
    [InlineData("<?xml version='1.1'", "XML.Syntax")]
    public void RejectedSourcesNeverPublishUnvalidatedMetadata(string prolog, string code)
    {
        using var fixture = new SourceFixture(Encoding.UTF8.GetBytes(prolog), 1);
        Assert.False(fixture.Frame());
        Assert.Null(fixture.Source.TextDeclaration);
        Assert.Equal(code, fixture.Source.Diagnostic!.Code);
        Assert.Equal(FinalUri, fixture.Source.Diagnostic.Location.SourceUri);
        fixture.Dispose();
        Assert.Equal(1, fixture.Resolver.Stream!.Disposals);
    }

    [Fact]
    public void CancellationAtEntryAndDuringReadPreservesCleanupAndNoMetadata()
    {
        using (var fixture = new SourceFixture(E38.ToArray(), 1))
        {
            Assert.Throws<OperationCanceledException>(() => fixture.Source.Frame(fixture.Options.Limits, fixture.Budget, new(true)));
            Assert.Null(fixture.Source.TextDeclaration);
            fixture.Dispose();
            Assert.Equal(1, fixture.Resolver.Stream!.Disposals);
        }
        using var cancel = new CancellationTokenSource();
        using var during = new SourceFixture(E38.ToArray(), 1, cancel.Token);
        during.Resolver.Stream!.AfterRead = () => { if (during.Resolver.Stream.Position == 18) { cancel.Cancel(); } };
        Assert.Throws<OperationCanceledException>(() => during.Source.Frame(during.Options.Limits, during.Budget, cancel.Token));
        Assert.Null(during.Source.TextDeclaration);
        Assert.Equal(1, during.Resolver.Stream.Disposals);
        during.Dispose();
        Assert.Equal(1, during.Resolver.Stream.Disposals);
    }

    [Fact]
    public void LaterInvalidBodyByteRetainsSuccessfullyValidatedMetadata()
    {
        const string prolog = "<?xml version='1.10' encoding='UTF-8'?>";
        using var fixture = new SourceFixture([.. Encoding.UTF8.GetBytes(prolog + "body"), 0xFF], 4096);
        Assert.True(fixture.Frame());
        var metadata = fixture.Source.TextDeclaration!.Value;
        var body = new StringBuilder();
        while (fixture.Source.EnsureCharacters(Cancellation))
        {
            body.Append(fixture.Source.Characters);
            fixture.Source.Consume(fixture.Source.Characters.Length);
        }
        Assert.Equal("body", body.ToString());
        Assert.Equal("INPUT.InvalidEncoding", fixture.Source.Diagnostic!.Code);
        Assert.Equal(prolog.Length + 4, fixture.Source.Diagnostic.Location.ByteOffset);
        Assert.Equal("1.10", metadata.Version);
        Assert.Equal(metadata, fixture.Source.TextDeclaration);
        fixture.Dispose();
        Assert.Equal(1, fixture.Resolver.Stream!.Disposals);
    }

    [Fact]
    public void LongBoundedExactVersionSurvivesRefillsAndQuotaRejectionPublishesNothing()
    {
        var version = "1." + new string('0', 4096);
        var prolog = $"<?xml version='{version}' encoding='UTF-8'?>";
        var bytes = Encoding.UTF8.GetBytes(prolog + new string('x', 9000));
        using var exact = new SourceFixture(bytes, 4096, limits: new()
        {
            TokenCharacters = prolog.Length
        });
        Assert.True(exact.Frame());
        var metadata = exact.Source.TextDeclaration!.Value;
        Assert.Equal(version, metadata.Version);
        Assert.Equal(new string('x', 9000), exact.ReadBody(out _));
        Assert.Same(metadata.Version, exact.Source.TextDeclaration!.Value.Version);
        exact.Dispose();
        Assert.Same(metadata.Version, exact.Source.TextDeclaration!.Value.Version);
        using var limited = new SourceFixture(bytes, 1, limits: new()
        {
            TokenCharacters = prolog.Length - 1
        });
        Assert.False(limited.Frame());
        Assert.Equal("XML.ResourceLimit", limited.Source.Diagnostic!.Code);
        Assert.Null(limited.Source.TextDeclaration);
    }

    private sealed class SourceFixture : IDisposable
    {
        internal readonly Resolver Resolver;
        internal readonly XmlParseOptions Options;
        internal readonly XmlEntityBudget Budget;
        internal readonly ExternalXmlSource Source;
        private readonly XmlResolutionSession session;
        internal SourceFixture(byte[] bytes, int size, CancellationToken cancellation = default, XmlResourceLimits? limits = null)
        {
            Resolver = new(bytes, size);
            Options = new()
            {
                Limits = limits ?? new(),
                Resolver = Resolver,
                DtdProcessing = XmlDtdProcessing.Parse
            };
            var operation = new OperationBudget(Options, cancellation);
            Budget = new(operation, new(StringComparer.Ordinal));
            session = new(Resolver, operation, cancellation);
            var handle = session.Open(new(new Uri("urn:entity"), null, XmlResourceKind.ExternalEntity), new(null, 1, 1, null));
            Assert.Null(handle.Diagnostic);
            Source = new(session, handle.Resource!, Options);
        }
        internal bool Frame() => Source.Frame(Options.Limits, Budget, Cancellation);
        internal string ReadBody(out long[] offsets)
        {
            var text = new StringBuilder();
            var mappings = new List<long>();
            while (Source.EnsureCharacters(Cancellation))
            {
                text.Append(Source.Characters);
                foreach (var offset in Source.Mappings)
                {
                    mappings.Add(offset);
                }

                Source.Consume(Source.Characters.Length);
            }
            Assert.Null(Source.Diagnostic);
            Assert.True(Source.Completed);
            offsets = [.. mappings];
            return text.ToString();
        }
        public void Dispose()
        {
            Source.Dispose();
            session.Dispose();
        }
    }
    private sealed class Resolver(byte[] bytes, int size) : XmlResourceResolver
    {
        internal TrackingStream? Stream;
        public override XmlResolvedResource Resolve(XmlResourceRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Stream = new(bytes, size);
            return new(Stream, FinalUri);
        }
    }
    private sealed class TrackingStream(byte[] bytes, int size) : MemoryStream(bytes, false)
    {
        internal int Disposals; internal Action? AfterRead;
        public override int Read(Span<byte> destination)
        {
            var count = base.Read(destination[..Math.Min(size, destination.Length)]);
            AfterRead?.Invoke();
            return count;
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Disposals++;
            }

            base.Dispose(disposing);
        }
    }
}
