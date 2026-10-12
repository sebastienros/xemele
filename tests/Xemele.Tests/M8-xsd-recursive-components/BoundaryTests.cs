using System.Text;
using Xemele.Validation;
using Xunit;
namespace Xemele.Tests.M8XsdRecursiveComponents;

public sealed class BoundaryTests
{
    private static readonly Uri Source = new("https://recursive.test/main.xsd"), Final = new("https://recursive.test/redirect/final.xsd");
    private const string Body = "<xs:element name='root'>\n<xs:complexType>\n<xs:sequence>\n<xs:element name='child'>\n<xs:complexType>\n<xs:group ref='g' minOccurs='0'/>\n</xs:complexType>\n</xs:element>\n</xs:sequence>\n</xs:complexType>\n</xs:element>\n";
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CompletionHasExactAggregateWorkAndComponents(bool imported)
    {
        string body = NeighborTests.Wrap(Body + NeighborTests.Group);
        string root = imported ? NeighborTests.Wrap("<xs:include schemaLocation='child.xsd'/>") : body;
        var resolver = new Resolver(body);
        var options = new SchemaCompileOptions { BaseUri = Source, Resolver = resolver, MaxOperations = null, MaxComponents = null, Limits = new() { EvaluationWork = null } };
        var context = new SchemaCompileContext(options); var document = context.Parse(root).Document;
        var compiler = new XmlSchemaCompiler(); var ledger = new Ledger();
        var graph = compiler.Compile(document, context.Options, ledger);
        Assert.Equal(3, graph.Elements.Count); Assert.True(ledger.Work > 0); Assert.True(ledger.Components > 0);
        var exact = new SchemaCompileOperation(options with { MaxOperations = ledger.Work, MaxComponents = ledger.Components });
        Assert.NotNull(compiler.Compile(document, context.Options, exact)); Assert.True(exact.IsComplete);
        var shortWork = new SchemaCompileOperation(options with { MaxOperations = ledger.Work - 1 });
        var workError = Assert.Throws<SchemaCompileException>(() => compiler.Compile(document, context.Options, shortWork));
        Assert.False(shortWork.IsComplete); Assert.Equal("SCHEMA.Limit", workError.Diagnostics[^1].Code);
        var shortComponents = new SchemaCompileOperation(options with { MaxComponents = ledger.Components - 1 });
        var componentsError = Assert.Throws<SchemaCompileException>(() => compiler.Compile(document, context.Options, shortComponents));
        Assert.False(shortComponents.IsComplete); Assert.Equal("SCHEMA.Limit", componentsError.Diagnostics[^1].Code);
        if (imported) { Assert.Equal(4, resolver.Calls); Assert.Equal(4, resolver.Disposals); Assert.Contains(ledger.Locations, at => at.SourceUri == Final); }
    }
    [Fact]
    public void CancellationAndWorkRefusalOccurInsideDeferredBodyWithNullCaps()
    {
        string xml = NeighborTests.Wrap(Body + NeighborTests.Group);
        var options = new SchemaCompileOptions { BaseUri = Source, MaxOperations = null, MaxComponents = null, Limits = new() { EvaluationWork = null } };
        var context = new SchemaCompileContext(options); var document = context.Parse(xml).Document;
        var compiler = new XmlSchemaCompiler(); var observed = new Ledger();
        compiler.Compile(document, context.Options, observed);
        // Line 7 belongs to the nested deferred type's group reference. Earlier
        // syntax/registration visits are included; target the last actual charge,
        // during completion after the containing element declaration exists.
        int visits = observed.Locations.Count(at => at.SourceUri == Source && at.LineNumber == 7);
        Assert.True(visits > 1);
        using var cancellation = new CancellationTokenSource();
        var cancel = new Ledger(cancellation, Source, 7, visits);
        Assert.Throws<OperationCanceledException>(() => compiler.Compile(document, context.Options, cancel));
        Assert.Equal(visits, cancel.TargetVisits); Assert.Equal(7, cancel.Last.LineNumber); Assert.Equal(Source, cancel.Last.SourceUri);
        var stopped = new SchemaCompileOperation(options with { MaxOperations = cancel.Work });
        var failure = Assert.Throws<SchemaCompileException>(() => compiler.Compile(document, context.Options, stopped));
        Assert.Equal("SCHEMA.Limit", failure.Diagnostics[^1].Code); Assert.Equal(cancel.Last, failure.Diagnostics[^1].Location);
    }
    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public void DeferredFailurePreservesCharacterOrByteProvenance(bool bytes, bool occurrence)
    {
        string xml = "<xs:schema xmlns:xs='http://www.w3.org/2001/XMLSchema'>\n<xs:element name='r'>\n<xs:complexType>\n<xs:group ref='missing'" + (occurrence ? " minOccurs='2' maxOccurs='1'" : "") + "/>\n</xs:complexType></xs:element></xs:schema>";
        SchemaCompileException failure;
        if (bytes)
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
            failure = Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(stream, new() { BaseUri = Source }));
            Assert.True(stream.CanRead);
        }
        else failure = Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(xml, new() { BaseUri = Source }));
        var diagnostic = Assert.Single(failure.Diagnostics);
        Assert.Equal("XSD.SchemaRestriction", diagnostic.Code); Assert.Equal(Source, diagnostic.Location.SourceUri);
        // Existing component lookup diagnostics name the owning complex type;
        // particle occurrence diagnostics name the authored group reference.
        Assert.Equal(occurrence ? 4 : 3, diagnostic.Location.LineNumber); Assert.Equal(1, diagnostic.Location.LinePosition);
        string locatedNode = occurrence ? "<xs:group" : "<xs:complexType";
        Assert.Equal(bytes ? (long?)Encoding.UTF8.GetByteCount(xml.AsSpan(0, xml.IndexOf(locatedNode, StringComparison.Ordinal))) : null, diagnostic.Location.ByteOffset);
    }
    [Fact]
    public void DeferredImportedFailureUsesFinalResourceUriAndDisposesOnce()
    {
        const string imported = "<xs:schema xmlns:xs='http://www.w3.org/2001/XMLSchema'>\n<xs:element name='r'>\n<xs:complexType>\n<xs:group ref='missing'/>\n</xs:complexType></xs:element></xs:schema>";
        var resolver = new Resolver(imported);
        var failure = Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(NeighborTests.Wrap("<xs:include schemaLocation='child.xsd'/>"), new() { BaseUri = Source, Resolver = resolver }));
        var diagnostic = Assert.Single(failure.Diagnostics);
        Assert.Equal(Final, diagnostic.Location.SourceUri); Assert.Equal(3, diagnostic.Location.LineNumber); Assert.NotNull(diagnostic.Location.ByteOffset);
        Assert.Equal(1, resolver.Calls); Assert.Equal(1, resolver.Disposals);
    }
    [Fact]
    public void LateSourceMutationRefusesPublication()
    {
        var context = new SchemaCompileContext(new() { BaseUri = Source });
        var document = context.Parse(NeighborTests.Wrap(Body + NeighborTests.Group)).Document;
        var compiler = new XmlSchemaCompiler(); var observed = new Ledger(); compiler.Compile(document, context.Options, observed);
        int visits = observed.Locations.Count(at => at.LineNumber == 7);
        var mutate = new Ledger(target: Source, line: 7, occurrence: visits) { OnTarget = () => document.RootElement.SetAttribute("changed", "yes") };
        Assert.Throws<InvalidOperationException>(() => compiler.Compile(document, context.Options, mutate));
        Assert.Equal(visits, mutate.TargetVisits);
    }
    private sealed class Resolver(string text) : XmlResourceResolver
    {
        internal int Calls, Disposals;
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken token)
        { token.ThrowIfCancellationRequested(); Calls++; Assert.Equal(XmlResourceKind.Schema, request.Kind); return new(new OwnedStream(text, () => Disposals++), Final); }
    }
    private sealed class OwnedStream(string text, Action dispose) : MemoryStream(Encoding.UTF8.GetBytes(text))
    { protected override void Dispose(bool disposing) { if (disposing) dispose(); base.Dispose(disposing); } }
    private sealed class Ledger(CancellationTokenSource? cancellation = null, Uri? target = null, int line = -1, int occurrence = -1) : ISchemaOperation
    {
        private readonly SchemaCompileOperation inner = new(new() { MaxOperations = null, MaxComponents = null, Limits = new() { EvaluationWork = null }, CancellationToken = cancellation?.Token ?? default });
        internal long Work; internal int Components, TargetVisits; internal XmlLocation Last; internal Action? OnTarget;
        internal readonly List<XmlLocation> Locations = new();
        public XmlResourceLimits Limits => inner.Limits; public CancellationToken CancellationToken => inner.CancellationToken;
        public bool IsComplete => inner.IsComplete; public long? RemainingWork => inner.RemainingWork;
        public bool Charge(long work, XmlLocation at)
        {
            if (work > 0)
            {
                Locations.Add(at);
                if (at.SourceUri == target && at.LineNumber == line && ++TargetVisits == occurrence)
                { Last = at; OnTarget?.Invoke(); cancellation?.Cancel(); }
            }
            bool ok = inner.Charge(work, at); if (ok) Work += work; return ok;
        }
        public bool ChargeComponents(int count, XmlLocation at) { bool ok = inner.ChargeComponents(count, at); if (ok) Components += count; return ok; }
        public bool AddDiagnostic(XmlDiagnostic d) => inner.AddDiagnostic(d); public void Refuse(string c, string m, XmlLocation at) => inner.Refuse(c, m, at);
    }
}
