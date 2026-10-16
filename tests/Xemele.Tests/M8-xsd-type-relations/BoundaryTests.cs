using Xemele.Validation;
using Xunit;
using static Xemele.Tests.M8XsdTypeRelations.NeighborTests;
namespace Xemele.Tests.M8XsdTypeRelations;

public sealed class BoundaryTests
{
    [Fact]
    public void UnionGraphAndContentCompilationHaveExactAggregateBoundaries()
    {
        var context = new SchemaCompileContext(new()
        {
            MaxOperations = null,
            MaxComponents = null,
            Limits = new()
            {
                EvaluationWork = null
            }
        });
        var doc = context.Parse(Substitution("xs:positiveInteger")).Document;
        var compiler = new XmlSchemaCompiler();
        var observed = new Ledger();
        var graph = compiler.Compile(doc, context.Options, observed);
        Assert.NotNull(compiler.Compile(doc, context.Options, new Ledger(observed.Work, observed.Components)));
        AssertRefuses(() => compiler.Compile(doc, context.Options, new Ledger(observed.Work - 1)));
        AssertRefuses(() => compiler.Compile(doc, context.Options, new Ledger(componentLimit: observed.Components - 1)));
        var type = graph.Types.Single(t => t.Particle is not null && t.Name.LocalName.StartsWith("#anonymous", StringComparison.Ordinal));
        var content = new XsdContentCompiler();
        observed = new Ledger();
        Assert.NotNull(content.Compile(graph, type, observed));
        Assert.NotNull(content.Compile(graph, type, new Ledger(observed.Work, observed.Components)));
        AssertRefuses(() => content.Compile(graph, type, new Ledger(observed.Work - 1)));
        AssertRefuses(() => content.Compile(graph, type, new Ledger(componentLimit: observed.Components - 1)));
    }
    [Fact]
    public void RelationChargesSharedWorkAndCancellationWithAuthoredLocation()
    {
        var graph = Graph(Substitution("xs:positiveInteger"));
        var types = graph.Types.ToDictionary(t => t.Name);
        var location = new XmlLocation(new Uri("https://types.test/schema.xsd"), 9, 3, 123);
        var observed = new Ledger();
        bool Relation(ISchemaOperation op) => XsdSubstitutionGroups.TypeRelation(types, null, graph.Datatypes, new(Xs, "positiveInteger"), new("", "U"), op, location, true, false, false, out _, out _);
        Assert.True(Relation(observed));
        Assert.True(observed.Work > 2);
        Assert.True(Relation(new Ledger(observed.Work)));
        var shortWork = new SchemaCompileOperation(new()
        {
            MaxOperations = observed.Work - 1
        });
        var failure = AssertRefuses(() => Relation(shortWork));
        Assert.Equal(location, failure.Diagnostics[^1].Location);
        using var source = new CancellationTokenSource();
        var cancelled = new Ledger(cancelAt: 3, source: source);
        Assert.Throws<OperationCanceledException>(() => Relation(cancelled));
        Assert.Equal(3, cancelled.Calls);
        Assert.Equal(location, cancelled.Last);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InvalidExtensionPreservesScalarAndByteLocation(bool bytes)
    {
        var xml = $"<xs:schema xmlns:xs='{Xs}'>\n<xs:complexType name='B'><xs:simpleContent><xs:extension base='xs:int'/></xs:simpleContent></xs:complexType>\n<xs:complexType name='D'><xs:complexContent><xs:extension base='B'>\n<xs:sequence><xs:element name='e'/></xs:sequence>\n</xs:extension></xs:complexContent></xs:complexType></xs:schema>";
        var options = new SchemaCompileOptions { BaseUri = new Uri("https://types.test/main.xsd") };
        SchemaCompileException error;
        if (bytes)
        {
            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml));
            error = Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(stream, options));
            Assert.True(stream.CanRead);
        }
        else
        {
            error = Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(xml, options));
        }

        var diagnostic = Assert.Single(error.Diagnostics);
        Assert.Equal("XSD.SchemaRestriction", diagnostic.Code);
        Assert.Equal(options.BaseUri, diagnostic.Location.SourceUri);
        Assert.Equal(4, diagnostic.Location.LineNumber);
        Assert.Equal(1, diagnostic.Location.LinePosition);
        Assert.Equal(bytes ? (long?)xml.IndexOf("<xs:sequence>", StringComparison.Ordinal) : null, diagnostic.Location.ByteOffset);
    }
    private static SchemaCompileException AssertRefuses(Action action)
    {
        var error = Assert.Throws<SchemaCompileException>(action);
        Assert.Contains(error.Diagnostics, d => d.Code == "SCHEMA.Limit");
        return error;
    }
    private sealed class Ledger(long? workLimit = null, int? componentLimit = null, int cancelAt = -1, CancellationTokenSource? source = null) : ISchemaOperation
    {
        private readonly SchemaCompileOperation inner = new(new() { MaxOperations = workLimit, MaxComponents = componentLimit, CancellationToken = source?.Token ?? default, Limits = new() { EvaluationWork = null } });
        internal long Work; internal int Components, Calls; internal XmlLocation Last; internal readonly List<XmlDiagnostic> Diagnostics = [];
        public XmlResourceLimits Limits => inner.Limits; public CancellationToken CancellationToken => inner.CancellationToken;
        public bool IsComplete => inner.IsComplete; public long? RemainingWork => inner.RemainingWork;
        public bool Charge(long work, XmlLocation at)
        {
            Last = at;
            if (++Calls == cancelAt)
            {
                source!.Cancel();
            }

            var ok = inner.Charge(work, at);
            if (ok)
            {
                Work += work;
            }

            return ok;
        }
        public bool ChargeComponents(int count, XmlLocation at)
        {
            var ok = inner.ChargeComponents(count, at);
            if (ok)
            {
                Components += count;
            }

            return ok;
        }
        public bool AddDiagnostic(XmlDiagnostic d)
        {
            Diagnostics.Add(d);
            return inner.AddDiagnostic(d);
        }
        public void Refuse(string code, string message, XmlLocation at)
        {
            Diagnostics.Add(new(code, XmlDiagnosticSeverity.Fatal, message, at));
            inner.Refuse(code, message, at);
        }
    }
}
