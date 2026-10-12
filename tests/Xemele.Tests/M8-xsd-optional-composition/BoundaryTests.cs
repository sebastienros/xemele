using System.Text;
using Xemele.Validation;
using Xunit;
using static Xemele.Tests.M8XsdOptionalComposition.NeighborTests;
namespace Xemele.Tests.M8XsdOptionalComposition;

public sealed class BoundaryTests
{
    private const string Directives = "<xs:include schemaLocation='missing'/><xs:include schemaLocation='missing'/><xs:include schemaLocation='child'/><xs:element name='root' type='T'/>";
    private static string Child() => Wrap("<xs:simpleType name='T'><xs:restriction base='xs:int'/></xs:simpleType>");
    private static Explicit Resolver(List<OwnedStream> streams) => new((r, _) =>
    {
        if (r.SystemId == "missing") return XmlResourceResolution.Unavailable;
        var stream = new OwnedStream(Child()); streams.Add(stream); return XmlResourceResolution.FromResource(new(stream, r.Uri));
    });
    [Theory]
    [InlineData("bytes")] [InlineData("scalars")] [InlineData("resources")] [InlineData("depth")]
    public void AbsenceUsesNoResourceOrParsingAllowanceAndBoundariesAggregate(string limit)
    {
        string source = Wrap(Directives); long bytes = Encoding.UTF8.GetByteCount(Child()), scalars = source.Length + Child().Length;
        foreach (bool exact in new[] { true, false })
        {
            var streams = new List<OwnedStream>(); var resolver = Resolver(streams);
            var limits = new XmlResourceLimits
            {
                ExternalBytes = limit == "bytes" ? bytes - (exact ? 0 : 1) : 100_000,
                DocumentCharacters = limit == "scalars" ? scalars - (exact ? 0 : 1) : 100_000,
                ExternalResources = 1, InclusionDepth = limit == "depth" ? (exact ? 1 : 1) : 64
            };
            // Positive ExternalResources cannot represent zero; consume the one slot
            // and prove the existing guard also stops a later unavailable callback.
            if (limit == "resources" && !exact) source = Wrap(Directives.Replace("<xs:element", "<xs:include schemaLocation='missing'/><xs:element", StringComparison.Ordinal));
            // A loaded child holds a traversal scope; its own absent dependency adds
            // a second depth. Keep the exact depth boundary on that distinct route.
            if (limit == "depth")
            {
                string nested = Wrap("<xs:include schemaLocation='missing'/>");
                resolver = new Explicit((r, _) => r.SystemId == "missing" ? XmlResourceResolution.Unavailable : XmlResourceResolution.FromResource(new(new OwnedStream(nested), r.Uri)));
                source = Wrap("<xs:include schemaLocation='child'/>"); limits = limits with { InclusionDepth = exact ? 2 : 1, ExternalResources = 2 };
            }
            var options = new SchemaCompileOptions { BaseUri = Basis, Resolver = resolver, Limits = limits };
            if (exact) Assert.NotNull(XmlSchema.Compile(source, options));
            else Assert.Contains(Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(source, options)).Diagnostics, d => d.Code == "SCHEMA.Limit");
            Assert.All(streams, s => Assert.Equal(1, s.Disposals)); Assert.Equal(limit == "depth" ? (exact ? 2 : 1) : 3, resolver.Calls);
        }
    }
    internal sealed class Ledger(long? work = null, int? components = null) : ISchemaOperation
    {
        public XmlResourceLimits Limits { get; } = new();
        public CancellationToken CancellationToken => default;
        public bool IsComplete { get; private set; } = true;
        public long? RemainingWork => work - Work;
        internal long Work; internal int Components;
        internal readonly List<XmlDiagnostic> Diagnostics = [];
        public bool Charge(long amount, XmlLocation at) { if (!IsComplete) return false; if (work is { } cap && amount > cap - Work) { Refuse("SCHEMA.Limit", "work", at); return false; } Work += amount; return true; }
        public bool ChargeComponents(int amount, XmlLocation at) { if (components is { } cap && amount > cap - Components) { Refuse("SCHEMA.Limit", "components", at); return false; } Components += amount; return IsComplete; }
        public bool AddDiagnostic(XmlDiagnostic diagnostic) { Diagnostics.Add(diagnostic); return IsComplete; }
        public void Refuse(string code, string message, XmlLocation at) { Diagnostics.Add(new(code, XmlDiagnosticSeverity.Fatal, message, at)); IsComplete = false; }
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void OptionalAttemptsAndRedefineClassificationHaveExactWorkAndComponents(bool annotation)
    {
        string source = Wrap(annotation ? "<xs:redefine schemaLocation='missing'><xs:annotation/></xs:redefine>" + Directives : Directives);
        XsdCompiledSchema Run(Ledger ledger)
        {
            var streams = new List<OwnedStream>(); var result = new XmlSchemaCompiler().Compile(XmlDocument.Parse(source), new() { BaseUri = Basis, Resolver = Resolver(streams) }, ledger);
            Assert.All(streams, s => Assert.Equal(1, s.Disposals)); return result;
        }
        var measured = new Ledger(); Run(measured); Assert.True(measured.Work > 0); Assert.True(measured.Components > 0);
        Assert.NotNull(Run(new(measured.Work, measured.Components)));
        Assert.Contains(Assert.Throws<SchemaCompileException>(() => Run(new(measured.Work - 1))).Diagnostics, d => d.Code == "SCHEMA.Limit");
        Assert.Contains(Assert.Throws<SchemaCompileException>(() => Run(new(components: measured.Components - 1))).Diagnostics, d => d.Code == "SCHEMA.Limit");
        TestContext.Current.TestOutputHelper!.WriteLine($"annotation={annotation}, work={measured.Work}, components={measured.Components}");
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void SharedPublicWorkCapIncludesPrincipalParsingAndAvailableFollowup(bool evaluation)
    {
        const long cap = 1_000_000; string source = Wrap(Directives);
        SchemaCompileContext Context(long work) => new(new() { BaseUri = Basis, Resolver = Resolver([]), MaxOperations = evaluation ? cap : work, Limits = new() { EvaluationWork = evaluation ? work : cap } });
        void Run(SchemaCompileContext c) => new XmlSchemaCompiler().Compile(c.Parse(source).Document, c);
        var measured = Context(cap); Run(measured); long used = cap - measured.Operation.RemainingWork!.Value; Assert.True(used > source.Length + Child().Length);
        var exact = Context(used); Run(exact); Assert.Equal(0, exact.Operation.RemainingWork);
        var shortCap = Context(used - 1); Assert.Contains(Assert.Throws<SchemaCompileException>(() => Run(shortCap)).Diagnostics, d => d.Code == "SCHEMA.Limit");
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void CancellationAtEntryAfterAbsenceAcquisitionOrParsingKeepsOwnership(int when)
    {
        using var cancel = new CancellationTokenSource();
        var stream = new OwnedStream(Child()) { AfterRead = when == 3 ? cancel.Cancel : null };
        var resolver = new Explicit((r, token) => { Assert.Equal(cancel.Token, token); if (when != 3) cancel.Cancel(); return when == 1 ? XmlResourceResolution.Unavailable : XmlResourceResolution.FromResource(new(stream, r.Uri)); });
        if (when == 0) cancel.Cancel();
        var error = Assert.Throws<OperationCanceledException>(() => XmlSchema.Compile(Wrap("<xs:include schemaLocation='child'/>"), new() { BaseUri = Basis, Resolver = resolver, CancellationToken = cancel.Token }));
        Assert.Equal(cancel.Token, error.CancellationToken); Assert.Equal(when == 0 ? 0 : 1, resolver.Calls); Assert.Equal(when > 1 ? 1 : 0, stream.Disposals);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CallerIoAndParseExceptionsAreNeverOptionalAbsence(bool parse)
    {
        Exception sentinel = parse ? new XmlParseException([new("CALLER", XmlDiagnosticSeverity.Error, "caller", default)]) : new IOException("caller");
        var resolver = new Explicit((_, _) => throw sentinel);
        Assert.Same(sentinel, Assert.ThrowsAny<Exception>(() => XmlSchema.Compile(Wrap("<xs:include schemaLocation='child'/>"), new() { BaseUri = Basis, Resolver = resolver })));
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void PrimaryFailureSurvivesCleanupFailureAndSuccessfulCleanupFailurePropagates(int mode)
    {
        var cleanup = new IOException("dispose"); var read = new XmlParseException([new("CALLER.Read", XmlDiagnosticSeverity.Error, "read", default)]);
        var stream = new OwnedStream(mode == 0 ? "<broken>" : Child()) { DisposeFailure = cleanup, ReadFailure = mode == 1 ? read : null };
        var resource = new XmlResolvedResource(stream, new Uri(Basis, "child")); var resolver = new Explicit((_, _) => XmlResourceResolution.FromResource(resource));
        var error = Assert.ThrowsAny<Exception>(() => XmlSchema.Compile(Wrap("<xs:include schemaLocation='child'/>"), new() { BaseUri = Basis, Resolver = resolver }));
        if (mode == 0) Assert.Contains(Assert.IsType<SchemaCompileException>(error).Diagnostics, d => d.Code == "SCHEMA.Syntax");
        else Assert.Same(mode == 1 ? read : cleanup, error);
        Assert.Equal(1, stream.Disposals); resource.Dispose(); Assert.Equal(1, stream.Disposals);
    }
    [Fact]
    public void RedirectedFinalSourceAndAuthoredXmlBaseControlNestedRequest()
    {
        Uri redirected = new("https://redirect.test/dir/final.xsd"); var streams = new List<OwnedStream>();
        var resolver = new Explicit((r, _) =>
        {
            Assert.Equal(XmlResourceKind.Schema, r.Kind);
            if (r.SystemId == "child") { Assert.Equal(new Uri("https://base.test/path/child"), r.Uri); Assert.Equal(Basis, r.Referrer); var s = new OwnedStream(Wrap("<xs:include xml:base='nested/' schemaLocation='grand'/>", "targetNamespace='urn:a'")); streams.Add(s); return XmlResourceResolution.FromResource(new(s, redirected)); }
            Assert.Equal("grand", r.SystemId); Assert.Equal(new Uri("https://redirect.test/dir/nested/grand"), r.Uri); Assert.Equal(redirected, r.Referrer);
            var stream = new OwnedStream(Wrap("<xs:element name='root' type='xs:int'/>", "targetNamespace='urn:a'")); streams.Add(stream); return XmlResourceResolution.FromResource(new(stream, r.Uri));
        });
        var schema = XmlSchema.Compile(Wrap("<xs:include xml:base='https://base.test/path/' schemaLocation='child'/>", "targetNamespace='urn:a'"), new() { BaseUri = Basis, Resolver = resolver });
        Assert.True(schema.Validate("<root xmlns='urn:a'>2</root>").IsValid); Assert.Equal(2, resolver.Calls); Assert.All(streams, s => Assert.Equal(1, s.Disposals));
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ByteAndCharacterProvenanceForResolvedComponentFailure(bool bytes)
    {
        Uri final = new("https://redirect.test/final.xsd"); string child = Wrap("\n<xs:element name='root' type='xs:int' default='bad'/>"); var stream = new OwnedStream(child);
        var resolver = new Explicit((_, _) => XmlResourceResolution.FromResource(new(stream, final))); string source = Wrap("<xs:include schemaLocation='child'/>");
        SchemaCompileException error;
        if (bytes) { using var input = new MemoryStream(Encoding.UTF8.GetBytes(source)); error = Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(input, new() { BaseUri = Basis, Resolver = resolver })); Assert.True(input.CanRead); }
        else error = Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(source, new() { BaseUri = Basis, Resolver = resolver }));
        var at = Assert.Single(error.Diagnostics).Location; Assert.Equal(final, at.SourceUri); Assert.Equal(2, at.LineNumber); Assert.True(at.LinePosition > 0); Assert.NotNull(at.ByteOffset); Assert.Equal(1, stream.Disposals);
    }
    [Fact]
    public void ResolverMutationAndOperationRefusalCannotTurnIntoAbsence()
    {
        var context = new SchemaCompileContext(new() { BaseUri = Basis });
        var document = context.Parse(Wrap("<xs:include schemaLocation='child'/>")).Document;
        var resolver = new Explicit((_, _) => { document.RootElement.SetAttribute("id", "changed"); return XmlResourceResolution.Unavailable; });
        Assert.Throws<InvalidOperationException>(() => new XmlSchemaCompiler().Compile(document, new() { BaseUri = Basis, Resolver = resolver }, new SchemaCompileOperation()));
        SchemaCompileContext? stopped = null;
        var stop = new Explicit((_, _) => { stopped!.Operation.Refuse("CALLER.Stop", "stop", default); return XmlResourceResolution.Unavailable; });
        stopped = new(new() { BaseUri = Basis, Resolver = stop });
        Assert.Equal("CALLER.Stop", Assert.Single(Assert.Throws<SchemaCompileException>(() => new XmlSchemaCompiler().Compile(stopped.Parse(Wrap("<xs:include schemaLocation='child'/>")).Document, stopped)).Diagnostics).Code);
    }
}
