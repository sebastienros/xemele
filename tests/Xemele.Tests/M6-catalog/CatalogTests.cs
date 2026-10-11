// These controls deliberately exercise default and explicitly cancelled operation tokens.
#pragma warning disable xUnit1051
using System.Text;
using Xunit;
namespace Xemele.Tests;
public sealed class CatalogTests
{
    private const string Ns = "urn:oasis:names:tc:entity:xmlns:xml:catalog";
    private static string Catalog(string entries, string attrs = "") => $"<catalog xmlns='{Ns}' {attrs}>{entries}</catalog>";
    private static XmlResourceRequest Entity(string? system = null, string? pub = null) => new(new("https://request/entity"), null, XmlResourceKind.ExternalEntity) { SystemId = system, PublicId = pub };
    [Fact] public void LexicalRoutingAndKinds()
    {
        var c = XmlCatalog.Parse(Catalog("<system systemId='lexical' uri='https://mapped/system'/><public publicId='A B' uri='https://mapped/public'/><uri name='https://request/entity' uri='https://mapped/uri'/>", "prefer='system'"));
        Assert.Equal("https://mapped/system", c.Resolve(Entity("lexical", "A B"))!.AbsoluteUri);
        Assert.Equal("https://mapped/public", c.Resolve(Entity(null, " A\t B "))!.AbsoluteUri);
        Assert.Null(c.Resolve(Entity("missing", "A B")));
        Assert.Null(c.Resolve(Entity()));
        Assert.Equal("https://mapped/uri", c.Resolve(Entity() with { Kind = XmlResourceKind.XIncludeXml })!.AbsoluteUri);
    }
    [Fact] public void ExactRewriteScopeAndDocumentOrder()
    {
        var c = XmlCatalog.Parse(Catalog("<system systemId='x' uri='one'/><system systemId='x' uri='two'/><rewriteSystem systemIdStartString='abc' rewritePrefix='short/'/><rewriteSystem systemIdStartString='abcdef' rewritePrefix='long/'/><group prefer='public' xml:base='nested/'><public publicId='P' uri='pub'/></group><public publicId='Q' uri='q'/>", "xml:base='https://base/' prefer='system'"));
        Assert.Equal("https://base/one", c.Resolve(Entity("x"))!.AbsoluteUri);
        Assert.Equal("https://base/long/tail", c.Resolve(Entity("abcdeftail"))!.AbsoluteUri);
        Assert.Equal("https://base/nested/pub", c.Resolve(Entity("miss", "P"))!.AbsoluteUri);
        Assert.Null(c.Resolve(Entity("miss", "Q")));
    }
    [Theory] [InlineData("urn:publicid:-:OASIS:DTD+DocBook+XML+V4.1.2:EN", "-//OASIS//DTD DocBook XML V4.1.2//EN")]
    [InlineData("urn:publicid:A%2BB%3AC%3BD", "A+B:C;D")]
    public void PublicUrns(string urn, string publicId)
    {
        var c = XmlCatalog.Parse(Catalog($"<public publicId='{publicId}' uri='https://mapped/'/>"));
        Assert.NotNull(c.Resolve(Entity(urn)));
        Assert.NotNull(c.Resolve(Entity(null, urn)));
    }
    [Fact] public void PublicUrnMismatchRejectsEvenExactSystemMapping()
    {
        var catalog = XmlCatalog.Parse(Catalog("<system systemId='urn:publicid:A' uri='https://wrong/'/><public publicId='A' uri='https://correct/'/>"));
        AssertCode("Syntax", () => catalog.Resolve(Entity("urn:publicid:A", "B")));
        Assert.Equal("https://correct/", catalog.Resolve(Entity("urn:publicid:A", " A "))!.AbsoluteUri);
        Assert.Equal("https://correct/", catalog.Resolve(Entity("urn:publicid:A", "urn:publicid:A"))!.AbsoluteUri);
        Assert.Equal("https://correct/", catalog.Resolve(Entity("urn:publicid:A"))!.AbsoluteUri);
        Assert.Null(catalog.Resolve(Entity("urn:publicid:A", "B") with { Kind = XmlResourceKind.XIncludeXml }));
    }
    [Fact] public void EagerDelegationTerminalAndNext()
    {
        var resolver = new Resolver(r => Resource(Catalog(r.Uri.AbsolutePath == "/long" ? "" : "<system systemId='abc' uri='https://hit/'/>"), r.Uri));
        var c = XmlCatalog.Parse(Catalog("<delegateSystem systemIdStartString='ab' catalog='https://child/long'/><nextCatalog catalog='https://child/next'/>"), new() { Resolver = resolver });
        Assert.Equal(2, resolver.Calls); Assert.Null(c.Resolve(Entity("abc"))); Assert.Equal(2, resolver.Calls);
        var next = XmlCatalog.Parse(Catalog("<nextCatalog catalog='https://child/next'/>"), new() { Resolver = resolver });
        Assert.NotNull(next.Resolve(Entity("abc")));
    }
    [Fact] public void LongestDelegateThenEqualOrderAndDiscardPublic()
    {
        var visits = new List<string>();
        var resolver = new Resolver(r => { visits.Add(r.Uri.AbsolutePath); return Resource(Catalog(r.Uri.AbsolutePath == "/first" ? "<public publicId='P' uri='https://wrong/'/>" : r.Uri.AbsolutePath == "/short" ? "<system systemId='abc' uri='https://short/'/>" : "<system systemId='abc' uri='https://correct/'/>"), r.Uri); });
        var c = XmlCatalog.Parse(Catalog("<delegateSystem systemIdStartString='a' catalog='https://child/short'/><delegateSystem systemIdStartString='ab' catalog='https://child/first'/><delegateSystem systemIdStartString='ab' catalog='https://child/second'/>"), new() { Resolver = resolver });
        Assert.Equal("https://correct/", c.Resolve(Entity("abc", "P"))!.AbsoluteUri);
    }
    [Fact] public void PublicAndUriDelegationRemainSeparateAndTerminal()
    {
        var resolver = new Resolver(r => Resource(Catalog(r.Uri.AbsolutePath == "/public" ? "<public publicId='A B' uri='https://public/'/>" : r.Uri.AbsolutePath == "/next" ? "<uri name='https://request/entity' uri='https://next/'/>" : "", "prefer='system'"), r.Uri));
        var catalog = XmlCatalog.Parse(Catalog("<delegatePublic publicIdStartString=' A  ' catalog='https://child/public'/><delegateURI uriStartString='https://request/' catalog='https://child/uri'/><nextCatalog catalog='https://child/next'/>", "prefer='system'"), new() { Resolver = resolver });
        Assert.Equal("https://public/", catalog.Resolve(Entity(null, "A B"))!.AbsoluteUri);
        Assert.Null(catalog.Resolve(Entity("missing", "A B")));
        Assert.Null(catalog.Resolve(Entity() with { Kind = XmlResourceKind.XIncludeText }));
        Assert.Equal(3, resolver.Calls);
    }
    [Fact] public void OnlyCatalogAndGroupScopePreference()
    {
        var catalog = XmlCatalog.Parse(Catalog("<public publicId='P' prefer='public' uri='https://ignored-scope/'/><group prefer='public'><public publicId='Q' uri='https://scoped/'/></group>", "prefer='system'"));
        Assert.Null(catalog.Resolve(Entity("missing", "P")));
        Assert.Equal("https://scoped/", catalog.Resolve(Entity("missing", "Q"))!.AbsoluteUri);
        Assert.NotNull(catalog.Resolve(Entity(null, "P")));
    }
    [Theory] [InlineData("systemSuffix", "Unsupported")] [InlineData("system", "Syntax")]
    public void RejectUnsupportedAndMissingAttributes(string entry, string code) => AssertCode(code, () => XmlCatalog.Parse(Catalog($"<{entry}/>")));
    [Fact] public void DenialCycleAndQuotas()
    {
        string xml = Catalog("<nextCatalog catalog='https://child/'/>");
        AssertCode("ResourceDenied", () => XmlCatalog.Parse(xml));
        var resolver = new Resolver(r => Resource(xml, new("https://primary/")));
        AssertCode("Cycle", () => XmlCatalog.Parse(xml, new() { BaseUri = new("https://primary/"), Resolver = resolver }));
        AssertCode("Limit", () => XmlCatalog.Parse(xml, new() { Resolver = new Resolver(r => Resource(Catalog(""), r.Uri)), MaxCatalogDepth = 1 }));
        string empty = Catalog("");
        XmlCatalog.Parse(empty, new() { Limits = new() { DocumentCharacters = empty.Length } });
        AssertCode("Limit", () => XmlCatalog.Parse(empty, new() { Limits = new() { DocumentCharacters = empty.Length - 1 } }));
        AssertCode("Limit", () => XmlCatalog.Parse(xml, new() { Limits = new() { ExternalBytes = 1 }, Resolver = new Resolver(r => Resource(empty, r.Uri)) }));
    }
    [Fact] public async Task ResolverMapsExactlyOnceAndTransfersOwnership()
    {
        var c = XmlCatalog.Parse(Catalog("<system systemId='x' uri='https://mapped/'/>"));
        XmlResourceRequest? received = null; var stream = new MemoryStream(); var resource = new XmlResolvedResource(stream, new("https://final/"));
        var fallback = new Resolver(r => { received = r; return resource; }); var composed = new CatalogResolver(c, fallback);
        var request = Entity("x", "P") with { Referrer = new("https://referrer/") };
        Assert.Same(resource, composed.Resolve(request, default)); Assert.Equal(request with { Uri = new("https://mapped/") }, received);
        Assert.Same(resource, await composed.ResolveAsync(request, default)); Assert.Equal(1, fallback.AsyncCalls); Assert.True(stream.CanRead);
        resource.Dispose(); Assert.False(stream.CanRead);
        var denied = new Resolver(_ => null); Assert.Null(new CatalogResolver(c, denied).Resolve(request, default)); Assert.Equal(1, denied.Calls);
    }
    [Fact] public void ExceptionsAndCancellationRemainUnchanged()
    {
        var exception = new IOException("caller"); var resolver = new Resolver(_ => throw exception);
        Assert.Same(exception, Assert.Throws<IOException>(() => XmlCatalog.Parse(Catalog("<nextCatalog catalog='https://child/'/>"), new() { Resolver = resolver })));
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        Assert.Throws<OperationCanceledException>(() => XmlCatalog.Parse(Catalog(""), cancellationToken: cancel.Token));
        Assert.Throws<OperationCanceledException>(() => XmlCatalog.Parse(Catalog("")).Resolve(Entity(), cancel.Token));
    }
    [Fact] public void ScalarAndExpansionQuotaUnits()
    {
        string xml = Catalog("<!--😀 &ignored;--><public publicId='😀' uri='https://mapped/'/>");
        int scalars = xml.Length - 2;
        XmlCatalog.Parse(xml, new() { Limits = new() { DocumentCharacters = scalars, ExpandedCharacters = 1 } });
        AssertCode("Limit", () => XmlCatalog.Parse(xml, new() { Limits = new() { DocumentCharacters = scalars - 1 } }));
        string references = Catalog("<system systemId='x&amp;y' uri='https://mapped/'/><public publicId='A&#32;B' uri='https://mapped/'/>");
        XmlCatalog.Parse(references, new() { Limits = new() { ExpandedCharacters = 2 } });
        AssertCode("Limit", () => XmlCatalog.Parse(references, new() { Limits = new() { ExpandedCharacters = 1 } }));
    }
    [Fact] public void FragmentAliasCyclesAndBoundedByteProbe()
    {
        string xml = Catalog("<nextCatalog catalog='https://primary/#other'/>");
        var alias = new Resolver(r => Resource(xml, new("https://primary/#redirect")));
        AssertCode("Cycle", () => XmlCatalog.Parse(xml, new() { BaseUri = new("https://primary/#root"), Resolver = alias }));
        string child = Catalog(""); byte[] bytes = Encoding.UTF8.GetBytes(child); var exact = new TrackingStream(bytes);
        XmlCatalog.Parse(Catalog("<nextCatalog catalog='https://child/'/>"), new() { Resolver = new Resolver(_ => new(exact, new("https://child/"))), Limits = new() { ExternalBytes = bytes.Length } });
        Assert.Equal(bytes.Length, exact.BytesRead); Assert.Equal(1, exact.Disposals);
        var tooLong = new TrackingStream(Encoding.UTF8.GetBytes(child + " "));
        AssertCode("Limit", () => XmlCatalog.Parse(Catalog("<nextCatalog catalog='https://child/'/>"), new() { Resolver = new Resolver(_ => new(tooLong, new("https://child/"))), Limits = new() { ExternalBytes = bytes.Length } }));
        Assert.Equal(bytes.Length + 1, tooLong.BytesRead); Assert.Equal(1, tooLong.Disposals);
    }
    [Fact] public void OfflineStandardExampleAndUriNormalization()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))) directory = directory.Parent;
        string xml = File.ReadAllText(Path.Combine(directory!.FullName, "tests/fixtures/M6-catalog/oasis-example.xml"));
        var catalog = XmlCatalog.Parse(xml);
        Assert.Equal("https://offline/docbook/docbookx.dtd", catalog.Resolve(Entity(null, "-//OASIS//DTD DocBook XML V4.1.2//EN"))!.AbsoluteUri);
        Assert.Equal("https://offline/docbook/docbookx.dtd", catalog.Resolve(Entity("http://www.oasis-open.org/docbook/xml/4.1.2/docbookx.dtd"))!.AbsoluteUri);
        Assert.Equal("https://offline/docbook/a", catalog.Resolve(new(new("http://www.oasis-open.org/docbook/a"), null, XmlResourceKind.XIncludeText))!.AbsoluteUri);
        var escaped = XmlCatalog.Parse(Catalog("<uri name='https://source/尾巴' uri='https://mapped/'/>"));
        Assert.NotNull(escaped.Resolve(new(new("https://source/尾巴"), null, XmlResourceKind.Catalog)));
    }
    [Fact] public void CleanupPreservesCallerExceptionAndAsyncCancellation()
    {
        var original = new IOException("caller stream"); var stream = new ThrowingStream(original);
        var resolver = new Resolver(_ => new(stream, new("https://child/")));
        Assert.Same(original, Assert.Throws<IOException>(() => XmlCatalog.Parse(Catalog("<nextCatalog catalog='https://child/'/>"), new() { Resolver = resolver })));
        Assert.Equal(1, stream.Disposals);
    }
    [Fact] public void InvalidOptionsAndCallerParseExceptions()
    {
        var caller = new XmlParseException([new("CALLER.Failure", XmlDiagnosticSeverity.Fatal, "caller", default)]);
        var resolver = new Resolver(_ => throw caller);
        string xml = Catalog("<nextCatalog catalog='https://child/'/>");
        Assert.Throws<ArgumentException>(() => XmlCatalog.Parse(xml, new() { Resolver = resolver, BaseUri = new("relative", UriKind.Relative) }));
        Assert.Throws<ArgumentOutOfRangeException>(() => XmlCatalog.Parse(xml, new() { Resolver = resolver, Limits = new() { ExternalResources = 0 } }));
        Assert.Equal(0, resolver.Calls);
        Assert.Same(caller, Assert.Throws<XmlParseException>(() => XmlCatalog.Parse(xml, new() { Resolver = resolver })));
        var catalog = XmlCatalog.Parse(Catalog(""));
        Assert.Throws<ArgumentException>(() => catalog.Resolve(Entity() with { Kind = (XmlResourceKind)99 }));
        Assert.Throws<ArgumentException>(() => catalog.Resolve(Entity() with { Uri = new("relative", UriKind.Relative) }));
    }
    private sealed class ThrowingStream(Exception original) : MemoryStream
    {
        internal int Disposals;
        public override int Read(Span<byte> buffer) => throw original;
        protected override void Dispose(bool disposing) { Disposals++; base.Dispose(disposing); throw new InvalidOperationException("cleanup"); }
    }
    [Theory] [InlineData("iso-8859-1")] [InlineData("windows-1252")] [InlineData("utf-16")]
    public void ChildDeclaredEncodingAndFinalUriBase(string label)
    {
        string child = "<?xml version='1.0' encoding='" + label + "'?>" + Catalog("<public publicId='café' uri='mapped.dtd'/>");
        byte[] bytes;
        if (label == "utf-16") bytes = Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(child)).ToArray();
        else bytes = Encoding.Latin1.GetBytes(child);
        var stream = new TrackingStream(bytes);
        var catalog = XmlCatalog.Parse(Catalog("<nextCatalog catalog='https://requested/catalog.xml'/>"), new() { Resolver = new Resolver(_ => new(stream, new("https://final/directory/catalog.xml"))) });
        Assert.Equal("https://final/directory/mapped.dtd", catalog.Resolve(Entity(null, "café"))!.AbsoluteUri);
        Assert.Equal(1, stream.Disposals);
    }
    [Fact] public void NativeScalarAndExpansionCountersSpanEntireGraph()
    {
        string child = Catalog("<public publicId='😀&amp;A' uri='https://mapped/'/>");
        string root = Catalog("<public publicId='B&amp;C' uri='https://root/'/><nextCatalog catalog='https://child/'/>");
        long exactScalars = root.Length + child.Length - 1;
        var resolver = new Resolver(r => Resource(child, r.Uri));
        var options = new XmlCatalogOptions { Resolver = resolver, Limits = new() { DocumentCharacters = exactScalars, ExpandedCharacters = 2 } };
        Assert.NotNull(XmlCatalog.Parse(root, options).Resolve(Entity(null, "😀&A")));
        AssertCode("Limit", () => XmlCatalog.Parse(root, options with { Limits = options.Limits with { DocumentCharacters = exactScalars - 1 } }));
        AssertCode("Limit", () => XmlCatalog.Parse(root, options with { Limits = options.Limits with { ExpandedCharacters = 1 } }));
    }
    [Fact] public void RepeatedCompletedFinalIdentityCountsAttemptsAndCharacters()
    {
        string child = Catalog(""); string root = Catalog(string.Concat(Enumerable.Repeat("<nextCatalog catalog='https://requested/'/>", 3)));
        var resolver = new Resolver(_ => Resource(child, new("https://final/catalog#alias")));
        var options = new XmlCatalogOptions { Resolver = resolver, MaxCatalogDocuments = 2, Limits = new() { ExternalResources = 3, ExternalBytes = Encoding.UTF8.GetByteCount(child) * 3, DocumentCharacters = root.Length + child.Length * 3 } };
        XmlCatalog.Parse(root, options); Assert.Equal(3, resolver.Calls);
        AssertCode("Limit", () => XmlCatalog.Parse(root, options with { MaxCatalogDocuments = 1 }));
        AssertCode("Limit", () => XmlCatalog.Parse(root, options with { Limits = options.Limits with { ExternalResources = 2 } }));
        AssertCode("Limit", () => XmlCatalog.Parse(root, options with { Limits = options.Limits with { DocumentCharacters = options.Limits.DocumentCharacters - 1 } }));
    }
    [Fact] public void DelayedSyntaxAndQuotaDiagnosticsKeepFinalSource()
    {
        string root = Catalog("<nextCatalog catalog='https://requested/'/>");
        string child = "<catalog xmlns='" + Ns + "'>" + new string(' ', 5000);
        var final = new Uri("https://final/malformed.xml"); var stream = new TrackingStream(Encoding.UTF8.GetBytes(child));
        var exception = Assert.Throws<XmlParseException>(() => XmlCatalog.Parse(root, new() { Resolver = new Resolver(_ => new(stream, final)) }));
        Assert.Equal("CATALOG.Syntax", exception.Diagnostics[0].Code); Assert.Equal(final, exception.Diagnostics[0].Location.SourceUri); Assert.True(exception.Diagnostics[0].Location.LinePosition > 0); Assert.Equal(1, stream.Disposals);
        var quotaStream = new TrackingStream(Encoding.UTF8.GetBytes(child));
        var quota = Assert.Throws<XmlParseException>(() => XmlCatalog.Parse(root, new() { Resolver = new Resolver(_ => new(quotaStream, final)), Limits = new() { DocumentCharacters = root.Length + 100 } }));
        Assert.Equal("CATALOG.Limit", quota.Diagnostics[0].Code); Assert.Equal(final, quota.Diagnostics[0].Location.SourceUri); Assert.Equal(1, quotaStream.Disposals);
    }
    [Fact] public void DeepGroupsUseIterativeCatalogTraversal()
    {
        const int depth = 12000;
        string xml = Catalog(string.Concat(Enumerable.Repeat("<group>", depth)) + "<system systemId='x' uri='https://mapped/'/>" + string.Concat(Enumerable.Repeat("</group>", depth)));
        var catalog = XmlCatalog.Parse(xml, new() { Limits = new() { Depth = depth + 2 } });
        Assert.NotNull(catalog.Resolve(Entity("x")));
        AssertCode("Limit", () => XmlCatalog.Parse(xml, new() { Limits = new() { Depth = depth } }));
    }
    [Theory] [InlineData("nextCatalog")] [InlineData("delegateSystem")] [InlineData("delegateURI")] [InlineData("delegatePublic")]
    public void DeepAcyclicGraphsLoadAndResolveWithoutRecursion(string route)
    {
        const int children = 4000;
        string entry = ChainEntry(route, 0);
        var resolver = new Resolver(request =>
        {
            int index = int.Parse(request.Uri.AbsolutePath.AsSpan(1), System.Globalization.CultureInfo.InvariantCulture);
            string body = index == children - 1 ? "<system systemId='x' uri='https://mapped/deep'/><public publicId='P' uri='https://mapped/deep'/><uri name='https://request/entity' uri='https://mapped/deep'/>" : ChainEntry(route, index + 1);
            return Resource(Catalog(body), request.Uri);
        });
        var limits = new XmlResourceLimits { DocumentCharacters = null, ExpandedCharacters = null, Depth = null, AttributesPerElement = null, TokenCharacters = null, InternedNames = null, EntityDepth = null, ExternalBytes = null, ExternalResources = null, InclusionDepth = null, Diagnostics = null, EvaluationWork = null };
        var catalog = XmlCatalog.Parse(Catalog(entry), new() { Resolver = resolver, Limits = limits, MaxCatalogDepth = children + 1, MaxCatalogDocuments = children + 1 });
        var request = route == "delegateURI" ? Entity() with { Kind = XmlResourceKind.XIncludeXml } : route == "delegatePublic" ? Entity(null, "P") : Entity("x");
        Assert.Equal("https://mapped/deep", catalog.Resolve(request)!.AbsoluteUri);
        Assert.Null(catalog.Resolve(request with { SystemId = "absent", PublicId = "absent", Uri = new("https://miss/") }));
        Assert.Equal(children, resolver.Calls);
        AssertCode("Limit", () => XmlCatalog.Parse(Catalog(entry), new() { Resolver = resolver, Limits = limits, MaxCatalogDepth = children, MaxCatalogDocuments = children + 1 }));
    }
    private static string ChainEntry(string route, int index) => route switch
    {
        "delegateSystem" => "<delegateSystem systemIdStartString='x' catalog='https://chain/" + index + "'/>",
        "delegatePublic" => "<delegatePublic publicIdStartString='P' catalog='https://chain/" + index + "'/>",
        "delegateURI" => "<delegateURI uriStartString='https://request/' catalog='https://chain/" + index + "'/>",
        _ => "<nextCatalog catalog='https://chain/" + index + "'/>"
    };
    [Fact] public void GraphCancellationAfterLastAcquisitionAndLargeScopedSiblingControl()
    {
        using var cancel = new CancellationTokenSource();
        var resolver = new Resolver(request => { cancel.Cancel(); return Resource(Catalog(""), request.Uri); });
        Assert.Throws<OperationCanceledException>(() => XmlCatalog.Parse(Catalog("<delegateSystem systemIdStartString='a' catalog='https://child/'/>"), new() { Resolver = resolver }, cancel.Token));
        Assert.Equal(1, resolver.Calls);
        // Unequal and equal prefix lengths preserve first document-order ties after bounded merge sorting.
        string entries = "<group xml:base='https://child/'>" + string.Concat(Enumerable.Range(0, 2048).Select(i => "<delegateSystem systemIdStartString='" + new string('x', i % 17 + 1) + "' catalog='" + i + "'/>")) + "</group>";
        var ordered = new Resolver(request => Resource(Catalog("<system systemId='xxxxxxxxxxxxxxxxxtail' uri='https://mapped/" + request.Uri.AbsolutePath.AsSpan(1).ToString() + "'/>"), request.Uri));
        var catalog = XmlCatalog.Parse(Catalog(entries), new() { Resolver = ordered, Limits = new() { ExternalResources = null, ExternalBytes = null, InternedNames = null }, MaxCatalogDocuments = 2049 });
        Assert.Equal("https://mapped/16", catalog.Resolve(Entity("xxxxxxxxxxxxxxxxxtail"))!.AbsoluteUri);
    }
    [Fact] public void DelegateSortCancelsDuringComparisonAndPreservesTies()
    {
        var values = Enumerable.Range(0, 2048).Select(i => (Key: i % 17, Order: i)).ToArray();
        var sorted = CatalogSort.StableSort(values, static (a, b) => b.Key.CompareTo(a.Key), default);
        Assert.Equal(Enumerable.Range(0, 2048).Select(i => (Key: i % 17, Order: i)).OrderByDescending(x => x.Key), sorted);
        using var cancel = new CancellationTokenSource(); int comparisons = 0;
        var exception = Assert.Throws<OperationCanceledException>(() => CatalogSort.StableSort(values, (a, b) => { if (++comparisons == 100) cancel.Cancel(); return b.Key.CompareTo(a.Key); }, cancel.Token));
        Assert.Equal(cancel.Token, exception.CancellationToken); Assert.Equal(100, comparisons);
    }
    private sealed class TrackingStream(byte[] bytes) : MemoryStream(bytes)
    {
        internal int BytesRead, Disposals;
        public override int Read(Span<byte> buffer) { int count = base.Read(buffer); BytesRead += count; return count; }
        protected override void Dispose(bool disposing) { if (disposing) Disposals++; base.Dispose(disposing); }
    }
    private static XmlResolvedResource Resource(string xml, Uri source) => new(new MemoryStream(Encoding.UTF8.GetBytes(xml)), source);
    private static void AssertCode(string code, Action action) => Assert.Equal("CATALOG." + code, Assert.Throws<XmlParseException>(action).Diagnostics[0].Code);
    private sealed class Resolver(Func<XmlResourceRequest, XmlResolvedResource?> resolve) : XmlResourceResolver
    {
        internal int Calls, AsyncCalls;
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken) { Calls++; return resolve(request); }
        public override ValueTask<XmlResolvedResource?> ResolveAsync(XmlResourceRequest request, CancellationToken cancellationToken) { AsyncCalls++; return ValueTask.FromResult(resolve(request)); }
    }
}
