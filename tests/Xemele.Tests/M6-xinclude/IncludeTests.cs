using System.Text;
using Xunit;
namespace Xemele.Tests.M6XInclude;

public sealed class IncludeTests
{
    private static XmlDocument Parse(string text, XmlParseOptions? options = null) => XmlDocument.Parse(text, options, cancellationToken: TestContext.Current.CancellationToken);
    private static XIncludeResult Process(XmlDocument document, XIncludeOptions options, CancellationToken? token = null) => XIncludeProcessor.Process(document, options, token ?? TestContext.Current.CancellationToken);
    private static Task<XIncludeResult> ProcessAsync(XmlDocument document, XIncludeOptions options) => XIncludeProcessor.ProcessAsync(document, options, TestContext.Current.CancellationToken);
    private const string Xi = "http://www.w3.org/2001/XInclude";
    private static XmlDocument Input(string body) => Parse($"<r xmlns:xi='{Xi}'>{body}</r>");
    private static XIncludeOptions Options(XmlResourceResolver? resolver = null) => new() { BaseUri = new("https://test/main.xml"), Resolver = resolver };
    private sealed class Resolver(Dictionary<string, string> items) : XmlResourceResolver
    {
        public readonly List<XmlResourceRequest> Requests = [];
        public Exception? Error;
        public string? Redirect { get; set; }
        public bool AsyncOnly;
        public int Disposals;
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken)
        {
            if (AsyncOnly) throw new InvalidOperationException("sync resolver called");
            return Get(request);
        }
        private XmlResolvedResource? Get(XmlResourceRequest request)
        {
            Requests.Add(request);
            if (Error is not null) throw Error;
            if (!items.TryGetValue(request.Uri.AbsolutePath, out var value)) return null;
            return new(new Content(Encoding.UTF8.GetBytes(value), this), new Uri(Redirect ?? request.Uri.AbsoluteUri));
        }
        public override async ValueTask<XmlResolvedResource?> ResolveAsync(XmlResourceRequest request, CancellationToken cancellationToken)
        {
            await Task.Yield(); cancellationToken.ThrowIfCancellationRequested(); return Get(request);
        }
        private sealed class Content : MemoryStream
        {
            private readonly byte[] data;
            private readonly Resolver owner;
            internal Content(byte[] data, Resolver owner) : base(data) { this.data = data; this.owner = owner; }
            public override int Read(Span<byte> buffer)
            {
                if (owner.AsyncOnly) throw new InvalidOperationException("sync read called");
                return base.Read(buffer);
            }
            public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                await Task.Yield(); cancellationToken.ThrowIfCancellationRequested();
                // MemoryStream's implementation may delegate to sync overrides.
                int available = (int)Math.Min(buffer.Length, Length - Position);
                data.AsMemory((int)Position, available).CopyTo(buffer); Position += available; return available;
            }
            protected override void Dispose(bool disposing)
            {
                if (owner.AsyncOnly && disposing) throw new InvalidOperationException("sync disposal called");
                if (disposing) owner.Disposals++;
                base.Dispose(disposing);
            }
            public override ValueTask DisposeAsync() { owner.Disposals++; base.Dispose(false); return default; }
        }
    }
    [Fact] public void XmlIncludesArePrivateAndFixSourceBase()
    {
        var input = Input("<xi:include href='a.xml'/>");
        var revision = input.ContentRevision;
        var resolver = new Resolver(new() { ["/a.xml"] = "<a>value</a>" });
        var result = Process(input, Options(resolver));
        Assert.True(result.Success); Assert.NotSame(input, result.Document);
        Assert.Equal("value", result.Document!.RootElement.GetTextContent());
        Assert.Equal("https://test/a.xml", result.Document.RootElement.FirstChild.GetAttribute("base", IncludeOperationXml));
        Assert.Equal("include", input.RootElement.FirstChild.LocalName); Assert.Equal(revision, input.ContentRevision);
        Assert.False(result.Document.SourceInputComplete); Assert.Null(result.Document.ValidationSourceContentRevision);
        Assert.Equal(1, result.IncludedResources); Assert.Equal(1, resolver.Disposals);
        Assert.Equal(XmlResourceKind.XIncludeXml, resolver.Requests[0].Kind);
    }
    private const string IncludeOperationXml = "http://www.w3.org/XML/1998/namespace";
    [Fact] public void NestedIncludeUsesFinalRedirectBase()
    {
        var resolver = new Resolver(new() { ["/a"] = "<a xmlns:xi='" + Xi + "'><xi:include href='next'/></a>", ["/next"] = "<next/>" });
        var result = Process(Input("<xi:include href='a'/>"), Options(resolver));
        Assert.True(result.Success); Assert.Equal("next", result.Document!.RootElement.FirstChild.FirstChild.LocalName);
        Assert.Equal(2, result.IncludedResources); Assert.Equal(2, resolver.Disposals);
    }
    [Theory] [InlineData("text")] [InlineData("xml")]
    public void NullableDenialUsesOwnedFallback(string parse)
    {
        var input = Input($"<xi:include href='missing' parse='{parse}'><xi:fallback><ok/></xi:fallback></xi:include>");
        var result = Process(input, Options());
        Assert.True(result.Success); Assert.Equal("ok", result.Document!.RootElement.FirstChild.LocalName);
        Assert.Equal("XINCLUDE.Fallback", Assert.Single(result.Diagnostics).Code);
        Assert.Equal(0, result.IncludedResources);
        Assert.Throws<NotSupportedException>(() => ((IList<XmlDiagnostic>)result.Diagnostics).Clear());
    }
    [Fact] public void LaterFailureDoesNotExposeEarlierOutput()
    {
        var input = Input("<xi:include href='a'/><xi:include href='missing'/>");
        var result = Process(input, Options(new Resolver(new() { ["/a"] = "<a/>" })));
        Assert.False(result.Success); Assert.Null(result.Document); Assert.Equal(1, result.IncludedResources);
        Assert.Equal(2, input.RootElement.Children.Count);
    }
    [Theory] [InlineData("x", "b")] [InlineData("element(x)", "b")] [InlineData("element(/1/2)", "b")] [InlineData("element(/1/2/1)", "c")]
    public void PointerSubsetCountsOnlyElements(string pointer, string expected)
    {
        var resolver = new Resolver(new() { ["/a"] = "<a><!--c--><first/><b xml:id='x'><c/></b></a>" });
        var result = Process(Input($"<xi:include href='a' xpointer='{pointer}'/>"), Options(resolver));
        Assert.True(result.Success); Assert.Equal(expected, result.Document!.RootElement.FirstChild.LocalName);
    }
    [Fact] public void SameDocumentSelectionUsesCapturedSourceAndNoIo()
    {
        var input = Input("<a xml:id='x'><v/></a><xi:include xpointer='x'/><xi:include xpointer='element(/1/1)'/>");
        var resolver = new Resolver(new()) { Error = new Exception("must not acquire") };
        var result = Process(input, Options(resolver));
        Assert.True(result.Success); Assert.Equal(3, result.Document!.RootElement.Children.Count); Assert.Empty(resolver.Requests);
    }
    [Theory] [InlineData("element(/1)")] [InlineData("root")]
    public void SameDocumentAncestorSelectionIsCycle(string pointer)
    {
        var input = Parse($"<r xml:id='root' xmlns:xi='{Xi}'><xi:include xpointer='{pointer}'/></r>");
        var result = Process(input, Options()); Assert.False(result.Success);
        Assert.Equal("XINCLUDE.Cycle", result.Diagnostics[^1].Code);
    }
    [Theory] [InlineData("xpointer(/)", "Unsupported")] [InlineData("xmlns(p=u)", "Unsupported")] [InlineData("element(/0)", "Syntax")] [InlineData("element(/1/)", "Syntax")] [InlineData("element(x/1) element(/1)", "Unsupported")]
    public void UnsupportedOrMalformedPointerDoesNotUseFallback(string pointer, string code)
    {
        var result = Process(Input($"<xi:include href='missing' xpointer='{pointer}'><xi:fallback><ok/></xi:fallback></xi:include>"), Options());
        Assert.False(result.Success); Assert.Equal("XINCLUDE." + code, result.Diagnostics[^1].Code);
    }
    [Fact] public void UntypedIdMissesWhileXmlIdMatches()
    {
        var resolver = new Resolver(new() { ["/a"] = "<a id='x'/>" });
        var result = Process(Input("<xi:include href='a' xpointer='x'/>"), Options(resolver));
        Assert.False(result.Success); Assert.Equal("XINCLUDE.Selection", result.Diagnostics[^1].Code);
    }
    [Fact] public void XmlSyntaxUsesFallbackButCallbackParseExceptionPropagates()
    {
        var input = Input("<xi:include href='a'><xi:fallback><ok/></xi:fallback></xi:include>");
        Assert.True(Process(input, Options(new Resolver(new() { ["/a"] = "<broken>" }))).Success);
        var error = new XmlParseException([new("CALLBACK", XmlDiagnosticSeverity.Error, "original", default)]);
        var resolver = new Resolver(new()) { Error = error };
        Assert.Same(error, Assert.Throws<XmlParseException>(() => Process(input, Options(resolver))));
    }
    [Fact] public void IOExceptionAndCancellationNeverUseFallback()
    {
        var input = Input("<xi:include href='a'><xi:fallback><ok/></xi:fallback></xi:include>");
        var error = new IOException("original");
        Assert.Same(error, Assert.Throws<IOException>(() => Process(input, Options(new Resolver(new()) { Error = error }))));
        var source = new CancellationTokenSource(); source.Cancel();
        Assert.Throws<OperationCanceledException>(() => Process(input, Options(), source.Token));
        Assert.Equal("include", input.RootElement.FirstChild.LocalName);
    }
    [Theory] [InlineData(8, true)] [InlineData(7, false)]
    public void AggregateBytesExactBoundary(int bytes, bool success)
    {
        var input = Input("<xi:include href='a'/><xi:include href='a'/>");
        var options = Options(new Resolver(new() { ["/a"] = "<a/>" })) with { Limits = new() { ExternalBytes = bytes }, ParseOptions = new() { Limits = new() { ExternalBytes = null } } };
        var result = Process(input, options);
        Assert.Equal(success, result.Success);
        if (!success) Assert.Equal("XINCLUDE.Limit", result.Diagnostics[^1].Code);
    }
    [Fact] public void SmallerParserLimitWinsAndQuotaDoesNotFallback()
    {
        var options = Options(new Resolver(new() { ["/a"] = "<a/>" })) with { Limits = new() { ExternalBytes = 8 }, ParseOptions = new() { Limits = new() { ExternalBytes = 3 } } };
        var result = Process(Input("<xi:include href='a'><xi:fallback><ok/></xi:fallback></xi:include>"), options);
        Assert.False(result.Success); Assert.Equal("XINCLUDE.Limit", result.Diagnostics[^1].Code);
    }
    [Fact] public void RepeatedDenialsChargeBeforeFallback()
    {
        var input = Input("<xi:include href='a'><xi:fallback/></xi:include><xi:include href='b'><xi:fallback/></xi:include>");
        var result = Process(input, Options() with { Limits = new() { ExternalResources = 1 } });
        Assert.False(result.Success); Assert.Equal("XINCLUDE.Limit", result.Diagnostics[^1].Code);
    }
    [Fact] public async Task AsyncReallyAwaitsResolverReadAndDisposal()
    {
        var resolver = new Resolver(new() { ["/a"] = "<a/>" }) { AsyncOnly = true };
        var input = Input("<xi:include href='a'/>"); long revision = input.ContentRevision;
        var task = ProcessAsync(input, Options(resolver));
        var result = await task;
        Assert.True(result.Success); Assert.Equal(1, resolver.Disposals); Assert.Equal(revision, input.ContentRevision);
    }
    [Fact] public void TextNeverParsesMarkupAndNormalizesLines()
    {
        var resolver = new Resolver(new() { ["/a"] = "<literal/>\r\n&x;" });
        var result = Process(Input("<xi:include href='a' parse='text'/>"), Options(resolver));
        Assert.True(result.Success); Assert.Equal(XmlNodeKind.Text, result.Document!.RootElement.FirstChild.Kind);
        Assert.Equal("<literal/>\n&x;", result.Document.RootElement.GetTextContent());
        Assert.Equal(XmlResourceKind.XIncludeText, resolver.Requests[0].Kind);
    }
    [Theory] [InlineData("unsupported")] [InlineData("ascii")]
    public void DeclaredEncodingFailureCanUseFallback(string encoding)
    {
        var resolver = new Resolver(new() { ["/a"] = "é" });
        var result = Process(Input($"<xi:include href='a' parse='text' encoding='{encoding}'><xi:fallback><ok/></xi:fallback></xi:include>"), Options(resolver));
        Assert.True(result.Success); Assert.Equal("ok", result.Document!.RootElement.FirstChild.LocalName);
    }
    [Fact] public void NamespaceAndLanguageFixupPreserveSelection()
    {
        var resolver = new Resolver(new() { ["/a"] = "<a xmlns:p='urn:p' xml:lang='fr'><p:b xml:id='x'/></a>" });
        var input = Parse($"<r xmlns:xi='{Xi}' xml:lang='en'><xi:include href='a' xpointer='x'/></r>");
        var result = Process(input, Options(resolver));
        Assert.True(result.Success); var b = result.Document!.RootElement.FirstChild;
        Assert.Equal("urn:p", b.NamespaceUri); Assert.Equal("fr", b.GetAttribute("lang", IncludeOperationXml));
        Assert.Contains(b.NamespaceDeclarations.ToListForTest(), d => d.Prefix == "p" && d.Value == "urn:p");
    }
    [Fact] public void MultipleRootsRejectAtomically()
    {
        var input = Parse($"<xi:include xmlns:xi='{Xi}' href='missing'><xi:fallback><a/><b/></xi:fallback></xi:include>");
        var result = Process(input, Options()); Assert.False(result.Success); Assert.Null(result.Document);
        Assert.Equal("XINCLUDE.Structure", result.Diagnostics[^1].Code); Assert.Equal("include", input.RootElement.LocalName);
    }
    [Fact] public void IncompleteInputIsRejectedWithoutReadingItsAttributes()
    {
        var input = Input("<a/>"); input.MarkSourceIncomplete();
        var result = Process(input, Options()); Assert.False(result.Success);
        Assert.Equal("XINCLUDE.Incomplete", result.Diagnostics[^1].Code);
    }
    [Fact] public void RedirectAliasCycleRejectsWithoutFallback()
    {
        var resolver = new Resolver(new() { ["/a"] = "<r xmlns:xi='" + Xi + "'><xi:include href='a'><xi:fallback><ok/></xi:fallback></xi:include></r>" }) { Redirect = "https://test/main.xml" };
        var result = Process(Input("<xi:include href='a'/>"), Options(resolver));
        Assert.False(result.Success); Assert.Equal("XINCLUDE.Cycle", result.Diagnostics[^1].Code); Assert.Equal(1, resolver.Disposals);
    }
    [Fact] public void DisjointSelectionsOfSameResourceAreLegal()
    {
        var xml = "<r xmlns:xi='" + Xi + "'><a xml:id='a'><xi:include href='same' xpointer='b'/></a><b xml:id='b'>ok</b></r>";
        var resolver = new Resolver(new() { ["/same"] = xml });
        var result = Process(Input("<xi:include href='same' xpointer='a'/>"), Options(resolver));
        Assert.True(result.Success); Assert.Equal("ok", result.Document!.RootElement.GetTextContent()); Assert.Equal(2, result.IncludedResources);
    }
    [Fact] public void CanonicalIdAndPathCycleIdentityAgree()
    {
        var xml = "<r xmlns:xi='" + Xi + "'><a xml:id='a'><xi:include href='same' xpointer='element(/1/1)'/></a></r>";
        var result = Process(Input("<xi:include href='same' xpointer='a'/>"), Options(new Resolver(new() { ["/same"] = xml })));
        Assert.False(result.Success); Assert.Equal("XINCLUDE.Cycle", result.Diagnostics[^1].Code);
    }
    [Fact] public void AmbiguousIdsAreFatalRatherThanFallback()
    {
        var resolver = new Resolver(new() { ["/a"] = "<r><a xml:id='x'/><b xml:id='x'/></r>" });
        var result = Process(Input("<xi:include href='a' xpointer='x'><xi:fallback><ok/></xi:fallback></xi:include>"), Options(resolver));
        Assert.False(result.Success); Assert.Equal("XINCLUDE.Syntax", result.Diagnostics[^1].Code);
    }
    [Theory] [InlineData("<xi:fallback/>")] [InlineData("<xi:include href='a'><xi:fallback/><xi:fallback/></xi:include>")] [InlineData("<xi:include href='a#f'/>")] [InlineData("<xi:include href='a' parse='bad'/>")] [InlineData("<xi:include parse='text'/>")]
    public void FatalSyntaxIsAtomic(string xml)
    {
        var input = Input(xml); var revision = input.ContentRevision;
        var result = Process(input, Options());
        Assert.False(result.Success); Assert.Equal("XINCLUDE.Syntax", result.Diagnostics[^1].Code); Assert.Equal(revision, input.ContentRevision);
    }
    [Theory] [InlineData("accept")] [InlineData("accept-language")]
    public void NegotiationIsExplicitlyUnsupported(string name)
    {
        var result = Process(Input($"<xi:include href='a' {name}='x'><xi:fallback><ok/></xi:fallback></xi:include>"), Options());
        Assert.False(result.Success); Assert.Equal("XINCLUDE.Unsupported", result.Diagnostics[^1].Code);
    }
    [Fact] public void UnselectedFallbackDoesNotTraverseMalformedDescendants()
    {
        var result = Process(Input("<xi:include href='a'><xi:fallback><xi:include parse='bad'/></xi:fallback></xi:include>"), Options(new Resolver(new() { ["/a"] = "<a/>" })));
        Assert.True(result.Success);
    }
    [Fact] public void FallbackDescendantsExpandNormallyWithoutCounterReset()
    {
        var resolver = new Resolver(new() { ["/b"] = "<b/>" });
        var input = Input("<xi:include href='missing'><xi:fallback><xi:include href='b'/></xi:fallback></xi:include>");
        Assert.True(Process(input, Options(resolver) with { Limits = new() { ExternalResources = 2 } }).Success);
        var rejected = Process(input, Options(resolver) with { Limits = new() { ExternalResources = 1 } });
        Assert.False(rejected.Success); Assert.Equal("XINCLUDE.Limit", rejected.Diagnostics[^1].Code);
    }
    [Fact] public void EmptySelectionCountsLoadedResourceAndMayFallback()
    {
        var resolver = new Resolver(new() { ["/a"] = "<a/>" });
        var result = Process(Input("<xi:include href='a' xpointer='missing'><xi:fallback><ok/></xi:fallback></xi:include>"), Options(resolver));
        Assert.True(result.Success); Assert.Equal(1, result.IncludedResources);
    }
    [Fact] public void SourceIdMetadataCapturePreservesInputAndRejectsStaleTyping()
    {
        var input = Parse("<!DOCTYPE r [<!ELEMENT r ANY><!ELEMENT a EMPTY><!ATTLIST a ident ID #IMPLIED>]><r xmlns:xi='" + Xi + "'><a ident='x'/><xi:include xpointer='x'/></r>", new() { DtdProcessing = XmlDtdProcessing.Parse });
        var revision = input.ContentRevision; var dtd = input.DocumentType;
        Assert.True(Process(input, Options()).Success); Assert.Equal(revision, input.ContentRevision); Assert.Same(dtd, input.DocumentType);
        input.RootElement.FirstChild.SetAttribute("ident", "y");
        var rejected = Process(input, Options()); Assert.False(rejected.Success); Assert.Equal("XINCLUDE.Selection", rejected.Diagnostics[^1].Code);
    }
    [Fact] public void FixupsMayBeDisabledAndLanguageMayBeCleared()
    {
        var resolver = new Resolver(new() { ["/a"] = "<a/>" });
        var input = Parse("<r xml:lang='en' xmlns:xi='" + Xi + "'><xi:include href='a'/></r>");
        var fixedResult = Process(input, Options(resolver));
        Assert.Equal("", fixedResult.Document!.RootElement.FirstChild.GetAttribute("lang", IncludeOperationXml));
        var omitted = Process(input, Options(resolver) with { FixupBaseUris = false, FixupLanguage = false });
        Assert.Null(omitted.Document!.RootElement.FirstChild.GetAttribute("lang", IncludeOperationXml));
        Assert.Null(omitted.Document.RootElement.FirstChild.GetAttribute("base", IncludeOperationXml));
    }
    [Fact] public async Task AsyncOriginalCallbackExceptionAndInputRemainUnchanged()
    {
        var input = Input("<xi:include href='a'><xi:fallback><ok/></xi:fallback></xi:include>");
        var revision = input.ContentRevision; var error = new IOException("original async");
        var resolver = new Resolver(new()) { Error = error, AsyncOnly = true };
        Assert.Same(error, await Assert.ThrowsAsync<IOException>(() => ProcessAsync(input, Options(resolver))));
        Assert.Equal(revision, input.ContentRevision);
    }
    [Fact] public void OptionsValidatedBeforeAcquisition()
    {
        var resolver = new Resolver(new()) { Error = new Exception("must not acquire") };
        Assert.Throws<ArgumentOutOfRangeException>(() => Process(Input("<xi:include href='a'/>"), Options(resolver) with { Limits = new() { InclusionDepth = 0 } }));
        Assert.Throws<ArgumentException>(() => Process(Input(""), Options() with { ParseOptions = new() { Recover = true } }));
        Assert.Empty(resolver.Requests);
    }
}
internal static class TestNamespaceExtensions
{
    internal static List<XmlNamespaceDeclaration> ToListForTest(this XmlNamespaceDeclarations declarations)
    {
        var result = new List<XmlNamespaceDeclaration>(); foreach (var item in declarations) result.Add(item); return result;
    }
}
