using System.Text;
using Xemele.Validation;
using Xunit;
using static Xemele.Tests.M8XsdImportVisibility.Support;
namespace Xemele.Tests.M8XsdImportVisibility;

public sealed class BoundaryTests
{
    private static string Principal(bool permission = true) => Wrap((permission ? "<xs:import namespace='urn:b'/>" : "") + "<xs:include schemaLocation='child'/><xs:element name='root' type='b:T'/>", "targetNamespace='urn:a' xmlns:b='urn:b'");
    private static string Child() => Wrap("<xs:import namespace='urn:b' schemaLocation='foreign'/><xs:element name='included' type='b:T'/>", "xmlns:b='urn:b'");
    private static Resolver Resources() => Map(new() { ["child"] = Child(), ["foreign"] = Foreign });
    [Theory]
    [InlineData("bytes")]
    [InlineData("scalars")]
    [InlineData("resources")]
    [InlineData("depth")]
    public void ResourceLimitsAggregateAcrossIncludedAuthorAndForeignImport(string unit)
    {
        long bytes = Encoding.UTF8.GetByteCount(Child()) + Encoding.UTF8.GetByteCount(Foreign);
        long scalars = Principal().Length + Child().Length + Foreign.Length;
        foreach (var exact in new[] { true, false })
        {
            var resolver = Resources();
            var limits = new XmlResourceLimits
            {
                ExternalBytes = unit == "bytes" ? bytes - (exact ? 0 : 1) : 100_000,
                DocumentCharacters = unit == "scalars" ? scalars - (exact ? 0 : 1) : 100_000,
                ExternalResources = unit == "resources" && !exact ? 1 : 2,
                InclusionDepth = unit == "depth" && !exact ? 1 : 2
            };
            var options = new SchemaCompileOptions { BaseUri = Basis, Resolver = resolver, Limits = limits };
            if (exact)
            {
                Assert.NotNull(XmlSchema.Compile(Principal(), options));
            }
            else
            {
                Assert.Contains(Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(Principal(), options)).Diagnostics, d => d.Code == "SCHEMA.Limit");
            }

            resolver.Disposed();
            Assert.NotEmpty(resolver.Streams);
        }
    }
    internal sealed class Ledger(long? work = null, int? components = null, CancellationToken token = default) : ISchemaOperation
    {
        public XmlResourceLimits Limits { get; } = new();
        public CancellationToken CancellationToken => token;
        public bool IsComplete { get; private set; } = true;
        public long? RemainingWork => work - Work;
        internal long Work; internal int Components;
        internal readonly List<XmlDiagnostic> Diagnostics = [];
        public bool Charge(long amount, XmlLocation at)
        {
            token.ThrowIfCancellationRequested();
            if (!IsComplete)
            {
                return false;
            }

            if (work is { } cap && amount > cap - Work)
            {
                Refuse("SCHEMA.Limit", "work", at);
                return false;
            }
            Work += amount;
            return true;
        }
        public bool ChargeComponents(int amount, XmlLocation at)
        {
            token.ThrowIfCancellationRequested();
            if (components is { } cap && amount > cap - Components)
            {
                Refuse("SCHEMA.Limit", "components", at);
                return false;
            }
            Components += amount;
            return IsComplete;
        }
        public bool AddDiagnostic(XmlDiagnostic diagnostic)
        {
            Diagnostics.Add(diagnostic);
            return IsComplete;
        }
        public void Refuse(string code, string message, XmlLocation at)
        {
            Diagnostics.Add(new(code, XmlDiagnosticSeverity.Fatal, message, at));
            IsComplete = false;
        }
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LookupWorkAndComponentsHaveExactBoundariesOnSharedOperation(bool publicParsing)
    {
        if (publicParsing)
        {
            const long cap = 1_000_000;
            SchemaCompileContext Context(long work) => new(new()
            {
                BaseUri = Basis,
                Resolver = Resources(),
                MaxOperations = work
            });
            void Run(SchemaCompileContext context) => new XmlSchemaCompiler().Compile(context.Parse(Principal()).Document, context);
            var measured = Context(cap);
            Run(measured);
            var used = cap - measured.Operation.RemainingWork!.Value;
            Assert.True(used > Principal().Length + Child().Length + Foreign.Length);
            var exact = Context(used);
            Run(exact);
            Assert.Equal(0, exact.Operation.RemainingWork);
            Assert.Contains(Assert.Throws<SchemaCompileException>(() => Run(Context(used - 1))).Diagnostics, d => d.Code == "SCHEMA.Limit");
        }
        else
        {
            XsdCompiledSchema Run(Ledger ledger)
            {
                var resolver = Resources();
                var graph = new XmlSchemaCompiler().Compile(XmlDocument.Parse(Principal()), new()
                {
                    BaseUri = Basis,
                    Resolver = resolver
                }, ledger);
                resolver.Disposed();
                return graph;
            }
            var measured = new Ledger();
            Run(measured);
            Assert.True(measured.Work > 0);
            Assert.True(measured.Components > 0);
            Assert.NotNull(Run(new(measured.Work, measured.Components)));
            Assert.Contains(Assert.Throws<SchemaCompileException>(() => Run(new(measured.Work - 1))).Diagnostics, d => d.Code == "SCHEMA.Limit");
            Assert.Contains(Assert.Throws<SchemaCompileException>(() => Run(new(components: measured.Components - 1))).Diagnostics, d => d.Code == "SCHEMA.Limit");
        }
    }
    [Theory]
    [InlineData("denied")]
    [InlineData("legacy")]
    [InlineData("cycle")]
    public void PermissionNeverBypassesResourceRefusalOrActiveCycles(string mode)
    {
        Resolver? resolver = null;
        resolver = new((r, _) => mode == "cycle" ? resolver!.Resolved(Wrap("<xs:include schemaLocation='main.xsd'/>"), r.Uri) : XmlResourceResolution.Denied);
        XmlResourceResolver selected = mode == "legacy" ? new Legacy() : resolver;
        var text = mode == "cycle" ? Principal() : Wrap("<xs:import namespace='urn:b' schemaLocation='foreign'/><xs:element name='root' type='b:T'/>", "xmlns:b='urn:b'");
        var error = Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(text, new() { BaseUri = Basis, Resolver = selected }));
        Assert.Equal(mode == "cycle" ? "SCHEMA.ResourceCycle" : "SCHEMA.ResourceDenied", Assert.Single(error.Diagnostics).Code);
        resolver.Disposed();
    }
    private sealed class Legacy : XmlResourceResolver
    {
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken token) => null;
    }
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void CancellationAtEntryAcquisitionAndReadPreservesDisposal(int when)
    {
        using var cancel = new CancellationTokenSource();
        var stream = new OwnedStream(Child());
        if (when == 2)
        {
            stream.AfterRead = cancel.Cancel;
        }

        var resolver = new Resolver((r, token) => { Assert.Equal(cancel.Token, token); if (when == 1) { cancel.Cancel(); } return XmlResourceResolution.FromResource(new(stream, r.Uri)); });
        if (when == 0)
        {
            cancel.Cancel();
        }

        var error = Assert.Throws<OperationCanceledException>(() => XmlSchema.Compile(Principal(), new() { BaseUri = Basis, Resolver = resolver, CancellationToken = cancel.Token }));
        Assert.Equal(cancel.Token, error.CancellationToken);
        Assert.Equal(when == 0 ? 0 : 1, stream.Disposals);
        Assert.Equal(when == 0 ? 0 : 1, resolver.Requests.Count);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnauthorizedIncludedReferenceRetainsFinalByteSourceIdentity(bool principalBytes)
    {
        Uri final = new("https://redirect.test/final-child.xsd");
        var resolver = new Resolver((_, _) => XmlResourceResolution.Unavailable);
        resolver = new((r, _) => r.SystemId == "foreign" ? resolver.Resolved(Foreign, r.Uri) : resolver.Resolved(Wrap("\n<xs:element name='included' type='b:T'/>", "xmlns:b='urn:b'"), final));
        var text = Wrap("<xs:import namespace='urn:b' schemaLocation='foreign'/><xs:include schemaLocation='child'/>", "targetNamespace='urn:a'");
        SchemaCompileException error;
        if (principalBytes)
        {
            using var input = new MemoryStream(Encoding.UTF8.GetBytes(text));
            error = Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(input, new() { BaseUri = Basis, Resolver = resolver }));
            Assert.True(input.CanRead);
        }
        else
        {
            error = Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(text, new() { BaseUri = Basis, Resolver = resolver }));
        }

        var at = Assert.Single(error.Diagnostics).Location;
        Assert.Equal(final, at.SourceUri);
        Assert.Equal(2, at.LineNumber);
        Assert.NotNull(at.ByteOffset);
        resolver.Disposed();
    }
    [Fact]
    public void SourceMutationDuringAcquisitionStillRefusesCompilation()
    {
        var document = XmlDocument.Parse(Principal(), cancellationToken: TestContext.Current.CancellationToken);
        var resolver = new Resolver((_, _) => { document.RootElement.SetAttribute("id", "mutated"); return XmlResourceResolution.Unavailable; });
        Assert.Throws<InvalidOperationException>(() => new XmlSchemaCompiler().Compile(document, new() { BaseUri = Basis, Resolver = resolver }, new SchemaCompileOperation()));
    }
    [Fact]
    public void ComponentAvailableUnderDifferentNamespaceCannotSatisfyPermission()
    {
        var resolver = Resources();
        var error = Reject(Principal().Replace("type='b:T'", "type='c:T'", StringComparison.Ordinal).Replace("xmlns:b='urn:b'", "xmlns:b='urn:b' xmlns:c='urn:c'", StringComparison.Ordinal), resolver);
        Assert.Contains(error.Diagnostics, d => d.Message.Contains("without an import", StringComparison.Ordinal));
    }
}
