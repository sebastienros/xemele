using System.Text;
using Xemele.Validation;
using Xunit;
using static Xemele.Tests.M8SchCompile.CompilerTests;

namespace Xemele.Tests.M8SchCompile;

public sealed class ResourceTests
{
    private sealed class Resolver(Func<XmlResourceRequest, CancellationToken, XmlResolvedResource?> action) : XmlResourceResolver
    { public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken = default) => action(request, cancellationToken); }
    private sealed class CountStream(string text) : MemoryStream(Encoding.UTF8.GetBytes(text))
    { internal int Disposals; protected override void Dispose(bool disposing) { if (disposing) Disposals++; base.Dispose(disposing); } }
    private static SchematronCompileOptions Options(XmlResourceResolver resolver, XmlResourceLimits? limits = null) => new() { Schema = new() { Resolver = resolver, BaseUri = new("https://example.test/main.sch"), Limits = limits ?? new() } };
    private static string Include(string href) => "<include href='" + href + "'/>";

    [Fact]
    public void IncludesNestedRedirectsAndFragmentsPreserveLexicalRequestsLocationsAndOwnership()
    {
        var requests = new List<XmlResourceRequest>(); var streams = new List<CountStream>();
        var options = Options(new Resolver((request, _) =>
        {
            requests.Add(request);
            string xml = requests.Count == 1 ? "<pattern xmlns='" + Ns + "' id='imported' xml:base='sub/'>" + Include("rules.sch#r") + "</pattern>" :
                "<container xmlns='urn:container'><rule xmlns='" + Ns + "' id='r' context='item'><assert test='true()'>included</assert></rule></container>";
            var stream = new CountStream(xml); streams.Add(stream);
            return new(stream, new(requests.Count == 1 ? "https://example.test/redirect/pattern.sch" : "https://example.test/redirect/sub/rules.sch"));
        }));
        var graph = Compile(Schema(Include("pattern.sch")), options);
        Assert.Equal(2, requests.Count); Assert.All(requests, r => Assert.Equal(XmlResourceKind.Schema, r.Kind));
        Assert.Equal("rules.sch#r", requests[1].SystemId); Assert.Equal(new Uri("https://example.test/redirect/sub/rules.sch#r"), requests[1].Uri);
        Assert.Equal(new Uri("https://example.test/redirect/pattern.sch"), requests[1].Referrer);
        Assert.All(streams, s => Assert.Equal(1, s.Disposals));
        var check = graph.Patterns[0].Rules[0].Checks[0]; Assert.Equal(new Uri("https://example.test/redirect/sub/rules.sch"), check.Location.SourceUri);
        Assert.NotNull(check.Location.ByteOffset); Assert.True(check.Location.LinePosition > 1);
    }

    [Fact]
    public void ExternalRuleExtendsImportsOnlyBodyAndKeepsProvenance()
    {
        var stream = new CountStream("<rule xmlns='" + Ns + "' abstract='true' id='r'><let name='x' value='1'/><assert test='$x=1'>x</assert></rule>");
        var graph = Compile(Schema(Pattern("<rule context='root'><extends href='external.sch'/></rule>")), Options(new Resolver((r, _) => new(stream, r.Uri))));
        Assert.Single(graph.Patterns[0].Rules[0].Lets); Assert.Single(graph.Patterns[0].Rules[0].Checks);
        Assert.Equal(new Uri("https://example.test/external.sch"), graph.Patterns[0].Rules[0].Checks[0].Location.SourceUri); Assert.Equal(1, stream.Disposals);
    }

    [Fact]
    public void LocalFragmentIncludesDoNotFetchAndCyclesAreErrors()
    {
        int calls = 0;
        var options = Options(new Resolver((_, _) => { calls++; throw new InvalidOperationException(); }));
        var graph = Compile(Schema("<a:storage xmlns:a='urn:a'><pattern id='p'>" + Rule() + "</pattern></a:storage>" + Include("#p")), options);
        Assert.Single(graph.Patterns); Assert.Equal(0, calls);
        var error = Assert.Throws<SchemaCompileException>(() => Compile(Schema("<a:storage xmlns:a='urn:a'><pattern id='p'>" + Include("#p") + "</pattern></a:storage>" + Include("#p")), options));
        Assert.Equal("SCHEMA.ResourceCycle", error.Diagnostics[0].Code);
    }

    [Fact]
    public void FinalUriRedirectCyclesDisposeEveryStreamAndHaveFatalSource()
    {
        var streams = new List<CountStream>();
        var options = Options(new Resolver((_, _) => { var stream = new CountStream("<pattern xmlns='" + Ns + "'>" + Include("urn:alias") + "</pattern>"); streams.Add(stream); return new(stream, new("urn:final")); }));
        var error = Assert.Throws<SchemaCompileException>(() => Compile(Schema(Include("urn:first")), options));
        Assert.Equal("SCHEMA.ResourceCycle", error.Diagnostics[0].Code); Assert.Equal(XmlDiagnosticSeverity.Fatal, error.Diagnostics[0].Severity); Assert.Equal(new Uri("urn:final"), error.Diagnostics[0].Location.SourceUri);
        Assert.Equal(2, streams.Count); Assert.All(streams, s => Assert.Equal(1, s.Disposals));
    }

    [Fact]
    public void AggregateResourcesBytesDepthAndComponentsRespectExactCaps()
    {
        string imported = "<pattern xmlns='" + Ns + "'>" + Rule() + "</pattern>";
        var options = Options(new Resolver((r, _) => new(new CountStream(imported), r.Uri)), new() { ExternalResources = 1, InclusionDepth = 1, ExternalBytes = Encoding.UTF8.GetByteCount(imported) });
        Assert.Single(Compile(Schema(Include("urn:p")), options).Patterns);
        foreach (var limits in new[] { options.Schema.Limits with { ExternalBytes = Encoding.UTF8.GetByteCount(imported) - 1 }, options.Schema.Limits with { ExternalResources = 1 } })
        {
            string source = limits.ExternalBytes == options.Schema.Limits.ExternalBytes ? Schema(Include("urn:p") + Include("urn:q")) : Schema(Include("urn:p"));
            Assert.Equal("SCHEMA.Limit", Assert.Throws<SchemaCompileException>(() => Compile(source, options with { Schema = options.Schema with { Limits = limits } })).Diagnostics[^1].Code);
        }
        var nested = Options(new Resolver((r, _) => new(new CountStream(r.Uri.OriginalString == "urn:first" ? "<pattern xmlns='" + Ns + "'>" + Include("urn:second") + "</pattern>" : imported), r.Uri)), new() { InclusionDepth = 1 });
        Assert.Equal("SCHEMA.Limit", Assert.Throws<SchemaCompileException>(() => Compile(Schema(Include("urn:first")), nested)).Diagnostics[^1].Code);
        var document = XmlDocument.Parse(Schema(Pattern(Rule())), cancellationToken: TestContext.Current.CancellationToken);
        var counting = new Counting(new SchemaCompileOperation()); new SchematronCompiler().Compile(document, new(), counting);
        Assert.True(counting.Work > 0); Assert.True(counting.Components > 0);
        var exact = new SchematronCompileOptions { Schema = new() { MaxOperations = counting.Work, MaxComponents = counting.Components } };
        new SchematronCompiler().Compile(document, exact, new SchemaCompileOperation(exact.Schema));
        foreach (var cap in new[] { exact.Schema with { MaxOperations = counting.Work - 1 }, exact.Schema with { MaxComponents = counting.Components - 1 }, exact.Schema with { Limits = new() { EvaluationWork = counting.Work - 1 } } })
            Assert.Equal("SCHEMA.Limit", Assert.Throws<SchemaCompileException>(() => new SchematronCompiler().Compile(document, exact with { Schema = cap }, new SchemaCompileOperation(cap))).Diagnostics[^1].Code);
    }
    private sealed class Counting(ISchemaOperation inner) : ISchemaOperation
    {
        internal long Work; internal int Components;
        public XmlResourceLimits Limits => inner.Limits; public CancellationToken CancellationToken => inner.CancellationToken;
        public bool IsComplete => inner.IsComplete; public long? RemainingWork => inner.RemainingWork;
        public bool Charge(long work, XmlLocation location) { if (!inner.Charge(work, location)) return false; Work += work; return true; }
        public bool ChargeComponents(int count, XmlLocation location) { if (!inner.ChargeComponents(count, location)) return false; Components += count; return true; }
        public bool AddDiagnostic(XmlDiagnostic diagnostic) => inner.AddDiagnostic(diagnostic);
        public void Refuse(string code, string message, XmlLocation location) => inner.Refuse(code, message, location);
    }

    [Fact]
    public void ResolverCallerParseAndIoExceptionsPropagateByIdentityAndDisposeOnce()
    {
        var caller = new XmlParseException([new("CALLER", XmlDiagnosticSeverity.Error, "caller", default)]);
        Assert.Same(caller, Assert.Throws<XmlParseException>(() => Compile(Schema(Include("urn:p")), Options(new Resolver((_, _) => throw caller)))));
        var argument = new ArgumentException("caller"); Assert.Same(argument, Assert.Throws<ArgumentException>(() => Compile(Schema(Include("urn:p")), Options(new Resolver((_, _) => throw argument)))));
        var syntax = new CountStream("<bad>");
        Assert.Equal("SCHEMA.Syntax", Assert.Throws<SchemaCompileException>(() => Compile(Schema(Include("urn:p")), Options(new Resolver((r, _) => new(syntax, r.Uri))))).Diagnostics[0].Code); Assert.Equal(1, syntax.Disposals);
        Assert.Equal("SCHEMA.ResourceDenied", Assert.Throws<SchemaCompileException>(() => Compile(Schema(Include("relative")))).Diagnostics[0].Code);
        Assert.Equal("SCHEMA.ResourceDenied", Assert.Throws<SchemaCompileException>(() => Compile(Schema(Include("urn:p")), Options(new Resolver((_, _) => null)))).Diagnostics[0].Code);
        var io = new IOException("caller read"); var faulting = new FaultStream(io);
        Assert.Same(io, Assert.Throws<IOException>(() => Compile(Schema(Include("urn:p")), Options(new Resolver((r, _) => new(faulting, r.Uri)))))); Assert.Equal(1, faulting.Disposals);
    }
    private sealed class FaultStream(IOException error) : Stream
    {
        internal int Disposals; public override bool CanRead => true; public override bool CanWrite => false; public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException(); public override long Position { get => 0; set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => throw error;
        public override int Read(Span<byte> buffer) => throw error;
        public override void Flush() => throw new NotSupportedException(); public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException(); public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) Disposals++; base.Dispose(disposing); }
    }

    [Fact]
    public void CancellationAtEntryAfterResolverAndDuringWorkNeverPublishesSuccess()
    {
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => Compile(Schema(Pattern(Rule())), new() { Schema = new() { CancellationToken = cancellation.Token } }));
        using var later = new CancellationTokenSource(); var stream = new CountStream("<pattern xmlns='" + Ns + "'/>");
        var options = Options(new Resolver((r, _) => { later.Cancel(); return new(stream, r.Uri); })); options = options with { Schema = options.Schema with { CancellationToken = later.Token } };
        Assert.Throws<OperationCanceledException>(() => Compile(Schema(Include("urn:p")), options)); Assert.Equal(1, stream.Disposals);
        var document = XmlDocument.Parse(Schema(Pattern(Rule())), cancellationToken: TestContext.Current.CancellationToken);
        using var progress = new CancellationTokenSource(); var operation = new CancelDuringWork(new SchemaCompileOperation(new() { CancellationToken = progress.Token }), progress);
        Assert.Throws<OperationCanceledException>(() => new SchematronCompiler().Compile(document, new(), operation));
    }
    private sealed class CancelDuringWork(ISchemaOperation inner, CancellationTokenSource cancellation) : ISchemaOperation
    {
        private int count;
        public XmlResourceLimits Limits => inner.Limits; public CancellationToken CancellationToken => inner.CancellationToken;
        public bool IsComplete => inner.IsComplete; public long? RemainingWork => inner.RemainingWork;
        public bool Charge(long work, XmlLocation location) { if (++count == 50) cancellation.Cancel(); return inner.Charge(work, location); }
        public bool ChargeComponents(int count, XmlLocation location) => inner.ChargeComponents(count, location);
        public bool AddDiagnostic(XmlDiagnostic diagnostic) => inner.AddDiagnostic(diagnostic); public void Refuse(string code, string message, XmlLocation location) => inner.Refuse(code, message, location);
    }

    [Fact]
    public void PrincipalMutationDuringResolutionIsDetectedAndSourceRemainsCallerOwned()
    {
        var document = XmlDocument.Parse(Schema(Include("urn:p")), cancellationToken: TestContext.Current.CancellationToken); var stream = new CountStream("<pattern xmlns='" + Ns + "'/>");
        var options = Options(new Resolver((r, _) => { document.RootElement.SetAttribute("schemaVersion", "changed"); return new(stream, r.Uri); }));
        Assert.Throws<InvalidOperationException>(() => new SchematronCompiler().Compile(document, options, new SchemaCompileOperation(options.Schema))); Assert.Equal(1, stream.Disposals);
    }

    [Theory]
    [InlineData("<include href='urn:p' bad='x'/>")]
    [InlineData("<include/>")]
    [InlineData("<include href='urn:p'>text</include>")]
    [InlineData("<include href='urn:p'><pattern/></include>")]
    public void MalformedIncludeNeverInvokesResolver(string include)
    {
        int calls = 0; var options = Options(new Resolver((_, _) => { calls++; return null; }));
        Assert.Equal("SCH.SchemaRestriction", Assert.Throws<SchemaCompileException>(() => Compile(Schema(include), options)).Diagnostics[0].Code); Assert.Equal(0, calls);
    }

    [Fact]
    public void FiniteRepresentationAndXPathCapsSurviveNullGeneralQuotas()
    {
        string source = Schema(Pattern(Rule(content: "<assert test='true()'>" + new string('x', 8) + "</assert>")));
        Assert.Equal("SCHEMA.Limit", Assert.Throws<SchemaCompileException>(() => Compile(source, new() { XPathLimits = new() { MaxExpressionCharacters = 3 } })).Diagnostics[^1].Code);
        var text = new StringBuilder(Schema("", "")); text.Clear(); text.Append("<schema xmlns='").Append(Ns).Append("'><pattern><rule context='root'><assert test='true()'>");
        for (int i = 0; i < 2000; i++) text.Append("<a:n xmlns:a='urn:a'>");
        text.Append('x'); for (int i = 0; i < 2000; i++) text.Append("</a:n>"); text.Append("</assert></rule></pattern></schema>");
        Assert.Equal("SCHEMA.Limit", Assert.Throws<SchemaCompileException>(() => Compile(text.ToString(), new() { Schema = new() { MaxOperations = null, MaxComponents = null, Limits = new() { Depth = null, EvaluationWork = null } } })).Diagnostics[^1].Code);
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void EveryIndependentXPathQuotaProducesCompileLimitDiagnostics(int limit)
    {
        var limits = limit switch { 0 => new Xemele.XPath.XPathCompileLimits { MaxTokens = 1 }, 1 => new Xemele.XPath.XPathCompileLimits { MaxAstNodes = 1 }, _ => new Xemele.XPath.XPathCompileLimits { MaxDepth = 1 } };
        Assert.Equal("SCHEMA.Limit", Assert.Throws<SchemaCompileException>(() => Compile(Schema(Pattern(Rule(content: "<assert test='(1+2)*3=9'/>"))), new() { XPathLimits = limits })).Diagnostics[^1].Code);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ForeignAndFragmentTraversalChargeEveryCommentAndPiEdge(bool fragment)
    {
        string Source(int count)
        {
            string trivia = string.Concat(Enumerable.Repeat("<!--comment--><?p text?>", count));
            string annotated = "<note xmlns=''>" + trivia + "<nested>ignored</nested>" +
                (fragment ? "<pattern xmlns='" + Ns + "' id='p'>" + Rule() + "</pattern>" : "") + "</note>";
            return Schema(annotated + (fragment ? Include("#p") : Pattern(Rule())));
        }
        long Count(string source)
        {
            var document = XmlDocument.Parse(source, cancellationToken: TestContext.Current.CancellationToken);
            var operation = new Counting(new SchemaCompileOperation()); new SchematronCompiler().Compile(document, new(), operation); return operation.Work;
        }
        long first = Count(Source(0)), second = Count(Source(10));
        Assert.Equal(fragment ? 40 : 20, second - first);
        var options = new SchematronCompileOptions { Schema = new() { MaxOperations = second } };
        var doc = XmlDocument.Parse(Source(10), cancellationToken: TestContext.Current.CancellationToken);
        new SchematronCompiler().Compile(doc, options, new SchemaCompileOperation(options.Schema));
        var under = options with { Schema = options.Schema with { MaxOperations = second - 1 } };
        Assert.Equal("SCHEMA.Limit", Assert.Throws<SchemaCompileException>(() => new SchematronCompiler().Compile(doc, under, new SchemaCompileOperation(under.Schema))).Diagnostics[^1].Code);
        using var cancellation = new CancellationTokenSource();
        var cancel = new CancelDuringWork(new SchemaCompileOperation(new() { CancellationToken = cancellation.Token }), cancellation);
        var many = XmlDocument.Parse(Source(200), cancellationToken: TestContext.Current.CancellationToken);
        Assert.Throws<OperationCanceledException>(() => new SchematronCompiler().Compile(many, new(), cancel));
    }

    [Fact]
    public void IncludeTriviaAndStructuralWhitespaceHaveExactAggregateWorkCharges()
    {
        long Count(string trivia, string gap)
        {
            var doc = XmlDocument.Parse(Schema("<include href='urn:p'>" + trivia + "</include>" + gap), cancellationToken: TestContext.Current.CancellationToken);
            var options = Options(new Resolver((r, _) => new(new CountStream("<pattern xmlns='" + Ns + "'/>") , r.Uri)));
            var operation = new Counting(new SchemaCompileOperation(options.Schema)); new SchematronCompiler().Compile(doc, options, operation); return operation.Work;
        }
        long baseline = Count("", " ");
        Assert.Equal(2, Count("<!--c--><?p text?>", " ") - baseline);
        // The same existing text node adds four scalars to snapshot and to White.
        Assert.Equal(8, Count("", "     ") - baseline);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void PreparatoryTokenLimitsKeepFatalProvenanceForAllRoutes(int route)
    {
        string source = route switch {
            0 => Schema(Pattern(Rule(content: "<assert test='(1+2)*3=9'/>"))),
            1 => Schema(Pattern(Rule("$node"), "abstract='true' id='t'") + Pattern("<param name='node' value='root'/>", "is-a='t'")),
            _ => Schema(Pattern(Rule("item | @code"))) };
        int cap = route == 1 ? 1 : 2;
        var error = Assert.Throws<SchemaCompileException>(() => Compile(source, new() { XPathLimits = new() { MaxTokens = cap }, Schema = new() { BaseUri = new("urn:token:source") } }));
        Assert.Equal("SCHEMA.Limit", error.Diagnostics[^1].Code); Assert.Equal(XmlDiagnosticSeverity.Fatal, error.Diagnostics[^1].Severity);
        Assert.Equal(new Uri("urn:token:source"), error.Diagnostics[^1].Location.SourceUri); Assert.True(error.Diagnostics[^1].Location.LinePosition > 0);
        Assert.Equal("SCH.XPath", Assert.Throws<SchemaCompileException>(() => Compile(Schema(Pattern(Rule(content: "<assert test='1 + '/>"))))).Diagnostics[0].Code);
    }

}
