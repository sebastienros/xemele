using Xemele.Validation;
using Xunit;
namespace Xemele.Tests.M8XsdComponentMapping;

public sealed class WildcardNeighborTests
{
    private static readonly Uri Source = new("https://mapping.test/wildcard.xsd");
    private static string Schema(string kind, string? lexical, bool optional = true)
    {
        var ns = lexical is null ? "" : $" namespace='{lexical}'";
        var body = kind == "any" ? $"<xs:sequence><xs:any{ns} minOccurs='{(optional ? 0 : 1)}' processContents='skip'/></xs:sequence>" : $"<xs:anyAttribute{ns} processContents='skip'/>";
        return NeighborTests.Wrap($"<xs:complexType name='T'>\n {body}\n</xs:complexType><xs:element name='r' type='t:T'/>", "targetNamespace='urn:t' xmlns:t='urn:t'");
    }
    private static XsdCompiledSchema Graph(string xml, ISchemaOperation? operation = null)
    {
        var c = new SchemaCompileContext(new()
        {
            BaseUri = Source
        });
        return new XmlSchemaCompiler().Compile(c.Parse(xml).Document, c.Options, operation ?? c.Operation);
    }
    private static XsdWildcard Wildcard(XsdCompiledSchema graph, string kind)
    {
        var t = graph.Types.Single(t => t.Name == new SchemaName("urn:t", "T"));
        return kind == "anyAttribute" ? t.AnyAttribute! : ((XsdWildcardTerm)((XsdGroupTerm)t.Particle!.Term).Particles[0].Term).Wildcard;
    }
    [Theory]
    [InlineData("any", "")]
    [InlineData("any", " \t\r\n ")]
    [InlineData("any", "&#x9;&#xA;&#xD;")]
    [InlineData("anyAttribute", "")]
    [InlineData("anyAttribute", " \t\r\n ")]
    [InlineData("anyAttribute", "&#x9;&#xA;&#xD;")]
    public void ExplicitEmptyListsMapToPositiveEmptySet(string kind, string lexical)
    {
        var xml = Schema(kind, lexical);
        var wildcard = Wildcard(Graph(xml), kind);
        Assert.False(wildcard.Negated);
        Assert.Empty(wildcard.Namespaces);
        Assert.Equal(XsdProcessContents.Skip, wildcard.ProcessContents);
        var facade = XmlSchema.Compile(xml);
        Assert.True(facade.Validate("<r xmlns='urn:t'/>").IsValid);
        var forbidden = kind == "any" ? "<r xmlns='urn:t'><a xmlns=''/></r>" : "<r xmlns='urn:t' a='value'/>";
        var result = facade.Validate(forbidden);
        Assert.True(result.IsComplete);
        Assert.False(result.IsValid);
    }
    [Theory]
    [InlineData("any", null)]
    [InlineData("any", "##any")]
    [InlineData("any", "##local")]
    [InlineData("any", "##targetNamespace")]
    [InlineData("any", "##other")]
    [InlineData("anyAttribute", null)]
    [InlineData("anyAttribute", "##any")]
    [InlineData("anyAttribute", "##local")]
    [InlineData("anyAttribute", "##targetNamespace")]
    [InlineData("anyAttribute", "##other")]
    public void EmptyListIsDistinctFromExistingNamespacePolicies(string kind, string? lexical)
    {
        var wildcard = Wildcard(Graph(Schema(kind, lexical)), kind);
        if (lexical is null or "##any")
        {
            Assert.True(wildcard.Negated);
            Assert.Empty(wildcard.Namespaces);
        }
        else if (lexical == "##other")
        {
            Assert.True(wildcard.Negated);
            Assert.Equal(["urn:t", ""], wildcard.Namespaces);
        }
        else
        {
            Assert.False(wildcard.Negated);
            Assert.Equal([lexical == "##local" ? "" : "urn:t"], wildcard.Namespaces);
        }
        var schema = XmlSchema.Compile(Schema(kind, lexical));
        foreach (var uri in new[] { "", "urn:t", "urn:other" })
        {
            var xml = kind == "any" ? $"<r xmlns='urn:t'><a xmlns='{uri}'/></r>" : uri == "" ? "<r xmlns='urn:t' a='value'/>" : $"<r xmlns='urn:t' xmlns:p='{uri}' p:a='value'/>";
            var expected = lexical is null or "##any" || lexical == "##local" && uri == "" || lexical == "##targetNamespace" && uri == "urn:t" || lexical == "##other" && uri == "urn:other";
            var result = schema.Validate(xml);
            Assert.True(result.IsComplete);
            Assert.Equal(expected, result.IsValid);
        }
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RequiredEmptySetParticleRemainsUnsatisfiable(bool optional)
    {
        var schema = XmlSchema.Compile(Schema("any", "", optional));
        var result = schema.Validate("<r xmlns='urn:t'/>");
        Assert.True(result.IsComplete);
        Assert.Equal(optional, result.IsValid);
        Assert.False(schema.Validate("<r xmlns='urn:t'><a/></r>").IsValid);
    }
    [Theory]
    [InlineData("any", "##unknown")]
    [InlineData("any", "##any ##local")]
    [InlineData("any", "##other urn:other")]
    [InlineData("anyAttribute", "##unknown")]
    [InlineData("anyAttribute", "##any ##local")]
    [InlineData("anyAttribute", "##other urn:other")]
    public void EmptyMappingDoesNotRelaxInvalidNamespaceTokens(string kind, string lexical)
    {
        var e = Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(Schema(kind, lexical)));
        Assert.Equal("Invalid wildcard namespace token.", Assert.Single(e.Diagnostics).Message);
    }
    [Theory]
    [InlineData("any")]
    [InlineData("anyAttribute")]
    public void EmptyMappingDoesNotRelaxProcessContents(string kind)
    {
        var e = Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(Schema(kind, "").Replace("processContents='skip'", "processContents='unknown'", StringComparison.Ordinal)));
        Assert.Equal("Invalid wildcard processContents.", Assert.Single(e.Diagnostics).Message);
    }
    [Theory]
    [InlineData("any")]
    [InlineData("anyAttribute")]
    public void WhitespaceMappingChargesWorkAtSourceAndPollsCancellation(string kind)
    {
        var xml = Schema(kind, new string(' ', 4096));
        var normal = new Ledger();
        Graph(xml, normal);
        Assert.True(normal.LocatedWork >= 4096);
        var actual = new SchemaCompileOperation(new()
        {
            MaxOperations = normal.Work,
            MaxComponents = normal.Components
        });
        Assert.NotNull(Graph(xml, actual));
        var shortWork = new SchemaCompileOperation(new()
        {
            MaxOperations = normal.Work - 1
        });
        Assert.Throws<SchemaCompileException>(() => Graph(xml, shortWork));
        Assert.False(shortWork.IsComplete);
        var shortComponents = new SchemaCompileOperation(new()
        {
            MaxComponents = normal.Components - 1
        });
        Assert.Throws<SchemaCompileException>(() => Graph(xml, shortComponents));
        Assert.False(shortComponents.IsComplete);
        using var c = new CancellationTokenSource();
        var cancel = new Ledger(c.Token) { Cancel = c };
        Assert.Throws<OperationCanceledException>(() => Graph(xml, cancel));
        Assert.Equal(1024, cancel.LocatedWork);
        // Real operation terminal retains the wildcard node location on refusal.
        var stopped = new SchemaCompileOperation(new()
        {
            MaxOperations = cancel.Work
        });
        var e = Assert.Throws<SchemaCompileException>(() => Graph(xml, stopped));
        var at = e.Diagnostics[^1].Location;
        Assert.Equal(Source, at.SourceUri);
        Assert.Equal(3, at.LineNumber);
        Assert.True(at.LinePosition > 0);
    }
    private sealed class Ledger(CancellationToken token = default) : ISchemaOperation
    {
        private readonly SchemaCompileOperation inner = new(new() { CancellationToken = token });
        internal long Work, LocatedWork; internal int Components; internal CancellationTokenSource? Cancel;
        public XmlResourceLimits Limits => inner.Limits; public CancellationToken CancellationToken => inner.CancellationToken;
        public bool IsComplete => inner.IsComplete; public long? RemainingWork => inner.RemainingWork;
        public bool Charge(long n, XmlLocation at)
        {
            if (at.LineNumber == 3)
            {
                LocatedWork += n;
                if (LocatedWork == 1024)
                {
                    Cancel?.Cancel();
                }
            }
            var ok = inner.Charge(n, at);
            if (ok)
            {
                Work += n;
            }

            return ok;
        }
        public bool ChargeComponents(int n, XmlLocation at)
        {
            var ok = inner.ChargeComponents(n, at);
            if (ok)
            {
                Components += n;
            }

            return ok;
        }
        public bool AddDiagnostic(XmlDiagnostic d) => inner.AddDiagnostic(d); public void Refuse(string c, string m, XmlLocation at) => inner.Refuse(c, m, at);
    }
}
