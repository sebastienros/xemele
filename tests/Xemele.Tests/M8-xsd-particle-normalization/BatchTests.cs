using Xemele.Validation;
using Xunit;
using Ledger = Xemele.Tests.M8XsdContent.OperationTests.Ledger;
using static Xemele.Tests.M8XsdParticleNormalization.NeighborTests;
using static Xemele.Tests.M8XsdParticleNormalization.BoundaryTests;
namespace Xemele.Tests.M8XsdParticleNormalization;

public sealed class BatchTests
{
    private static Dictionary<SchemaName, XsdContentProgram> Batch(XsdCompiledSchema graph, ISchemaOperation? operation = null) => new XsdContentCompiler().CompileAll(graph, operation ?? new SchemaCompileOperation(), new XmlFacetEvaluator());
    private static XsdCompiledSchema Typed(string source)
    {
        var graph = Graph(source);
        return graph with { Datatypes = new XmlDatatypeRegistry(graph.SimpleTypes.Where(t => t.Name.NamespaceUri != Xs), new SchemaCompileOperation()) };
    }
    [Fact]
    public void AllKeysAndIndependentlySpecifiedProgramsAreOwnedPerInvocation()
    {
        string source = Wrap("<xs:group name='unused'><xs:choice><xs:element name='x'/><xs:element name='x'/></xs:choice></xs:group><xs:complexType name='Empty'/><xs:complexType name='A'><xs:sequence><xs:element name='a'/></xs:sequence></xs:complexType><xs:complexType name='B'><xs:sequence><xs:element name='b'/></xs:sequence></xs:complexType>");
        var graph = Graph(source); var first = Batch(graph); var second = Batch(graph);
        Assert.Equal(graph.Types.Select(t => t.Name), first.Keys); Assert.Equal(graph.Types.Count, first.Count);
        Assert.Contains(new SchemaName(Xs, "anyType"), first.Keys); Assert.Contains(new SchemaName("", "#model-group:unused"), first.Keys);
        var matcher = new XsdContentMatcher();
        foreach (var type in graph.Types)
        {
            var program = Assert.IsType<CompiledContent>(first[type.Name]); Assert.NotSame(program, second[type.Name]); Assert.Equal(type.Location, program.Location);
            if (type.Particle is null) Assert.Null(program.Root); else Assert.Same(type.Particle, program.Root!.Particle);
        }
        bool Accepts(string type, params string[] children)
        {
            var cursor = matcher.CreateCursor(first[new("", type)]); var operation = new SchemaCompileOperation();
            foreach (string child in children) if (!cursor.Accept(new("", child), operation, out _, out _)) return false;
            return cursor.Complete(operation);
        }
        Assert.True(Accepts("Empty")); Assert.False(Accepts("Empty", "a"));
        Assert.True(Accepts("A", "a")); Assert.False(Accepts("A", "b")); Assert.False(Accepts("A"));
        Assert.True(Accepts("B", "b")); Assert.False(Accepts("B", "a")); Assert.False(Accepts("B"));
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void BothExistingOverloadsStillRejectOtherInvalidTypesAndForeignRequestedObjects(bool typed)
    {
        var graph = Typed(Wrap("<xs:complexType name='Unused'/><xs:complexType name='Invalid'><xs:choice><xs:element name='x'/><xs:element name='x'/></xs:choice></xs:complexType>"));
        var unused = graph.Types.Single(t => t.Name.LocalName == "Unused"); var compiler = new XsdContentCompiler();
        if (typed) Assert.Throws<SchemaCompileException>(() => compiler.Compile(graph, unused, new SchemaCompileOperation(), new XmlFacetEvaluator()));
        else Assert.Throws<SchemaCompileException>(() => compiler.Compile(graph, unused, new SchemaCompileOperation()));
        if (typed) Assert.Throws<ArgumentException>(() => compiler.Compile(graph, unused with { }, new SchemaCompileOperation(), new XmlFacetEvaluator()));
        else Assert.Throws<ArgumentException>(() => compiler.Compile(graph, unused with { }, new SchemaCompileOperation()));
        Assert.Throws<SchemaCompileException>(() => Batch(graph));
        Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(Wrap("<xs:complexType name='Unused'/><xs:complexType name='Invalid'><xs:choice><xs:element name='x'/><xs:element name='x'/></xs:choice></xs:complexType>")));
    }
    [Fact]
    public void EveryJoinValidatesAnUnrequestedAttributeWildcard()
    {
        var original = Typed(Wrap("<xs:complexType name='Unused'/><xs:complexType name='Other'/>"));
        var graph = original with { Types = original.Types.Select(t => t.Name.LocalName == "Other" ? t with { AnyAttribute = new(true, ["", "urn:a", "urn:b"], XsdProcessContents.Lax) } : t).ToArray() };
        var unused = graph.Types.Single(t => t.Name.LocalName == "Unused"); var compiler = new XsdContentCompiler();
        Assert.Throws<SchemaCompileException>(() => compiler.Compile(graph, unused, new SchemaCompileOperation()));
        Assert.Throws<SchemaCompileException>(() => compiler.Compile(graph, unused, new SchemaCompileOperation(), new XmlFacetEvaluator()));
        Assert.Throws<SchemaCompileException>(() => Batch(graph));
    }
    [Theory]
    [InlineData("element-decimal", false)] [InlineData("element-decimal", true)]
    [InlineData("element-qname", false)] [InlineData("element-qname", true)]
    [InlineData("attribute-qname", false)] [InlineData("attribute-qname", true)]
    [InlineData("unused-edc", false)] [InlineData("unused-edc", true)]
    public void TypedFixedQNameAndUnusedEdcChecksSurviveEveryJoin(string kind, bool valid)
    {
        string Element(string value, string type, string ns = "") => $"<xs:element name='v' type='{type}' fixed='{value}' {ns}/>";
        string Attribute(string value, string ns) => $"<xs:attribute name='a' type='xs:QName' fixed='{value}' {ns}/>";
        string body = kind switch
        {
            "element-decimal" => Restriction(Sequence(Element("1", "xs:decimal")), Sequence(Element(valid ? "01.0" : "2", "xs:decimal"))),
            "element-qname" => Restriction(Sequence(Element("p:v", "xs:QName", "xmlns:p='urn:one'")), Sequence(Element("p:v", "xs:QName", valid ? "xmlns:p='urn:one'" : "xmlns:p='urn:two'"))),
            "attribute-qname" => Restriction(Attribute("p:v", "xmlns:p='urn:one'"), Attribute("p:v", valid ? "xmlns:p='urn:one'" : "xmlns:p='urn:two'")),
            _ => Wrap($"<xs:group name='g'><xs:choice><xs:element name='x' type='xs:int'/><xs:element name='x' type='xs:{(valid ? "int" : "string")}'/></xs:choice></xs:group>")
        };
        string source = body.Replace("</xs:schema>", "<xs:complexType name='Unused'/></xs:schema>", StringComparison.Ordinal);
        var graph = Typed(source); var unused = graph.Types.Single(t => t.Name.LocalName == "Unused"); var compiler = new XsdContentCompiler();
        void OldTyped() => compiler.Compile(graph, unused, new SchemaCompileOperation(), new XmlFacetEvaluator());
        if (valid) { OldTyped(); Assert.NotEmpty(Batch(graph)); Assert.NotNull(XmlSchema.Compile(source)); }
        else { Assert.Throws<SchemaCompileException>(OldTyped); Assert.Throws<SchemaCompileException>(() => Batch(graph)); Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(source)); }
        // The frozen structural lane keeps lexical-only attribute comparison
        // and structural element comparison: the equal QName text here passes
        // even when separately authored namespace bindings denote unequal values.
        if (kind != "unused-edc") Assert.NotNull(compiler.Compile(graph, unused, new SchemaCompileOperation()));
        else if (valid) Assert.NotNull(compiler.Compile(graph, unused, new SchemaCompileOperation()));
        else Assert.Throws<SchemaCompileException>(() => compiler.Compile(graph, unused, new SchemaCompileOperation()));
    }
    [Theory]
    [InlineData(false, false)] [InlineData(false, true)] [InlineData(true, false)] [InlineData(true, true)]
    public void UnusedOldAndNewRedefineProgramsReachEveryJoin(bool complex, bool valid)
    {
        string element = "<xs:element name='x' type='xs:decimal' fixed='1'/>";
        string child = Wrap(complex ? $"<xs:complexType name='B'><xs:sequence>{element}</xs:sequence></xs:complexType>" : $"<xs:group name='g'><xs:sequence>{element}</xs:sequence></xs:group>");
        string replacement = element.Replace("fixed='1'", valid ? "fixed='01.0'" : "fixed='2'", StringComparison.Ordinal);
        string declaration = complex ? $"<xs:complexType name='B'><xs:complexContent><xs:restriction base='B'><xs:sequence>{replacement}</xs:sequence></xs:restriction></xs:complexContent></xs:complexType>" : $"<xs:group name='g'><xs:sequence>{replacement}</xs:sequence></xs:group>";
        string source = Wrap($"<xs:redefine schemaLocation='child.xsd'>{declaration}</xs:redefine><xs:complexType name='Unused'/>");
        var options = new SchemaCompileOptions { BaseUri = new Uri("https://batch.test/main.xsd"), Resolver = new Resolver(child) };
        var context = new SchemaCompileContext(options); var graph = new XmlSchemaCompiler().Compile(context.Parse(source).Document, context);
        graph = graph with { Datatypes = new XmlDatatypeRegistry(graph.SimpleTypes.Where(t => t.Name.NamespaceUri != Xs), context.Operation) };
        Assert.Contains(graph.Types, t => t.Name.LocalName.Contains("#redefined", StringComparison.Ordinal));
        var unused = graph.Types.Single(t => t.Name.LocalName == "Unused"); var compiler = new XsdContentCompiler();
        if (valid) { Assert.NotNull(compiler.Compile(graph, unused, new SchemaCompileOperation(), new XmlFacetEvaluator())); Assert.NotEmpty(Batch(graph)); Assert.NotNull(XmlSchema.Compile(source, options)); }
        else { Assert.Throws<SchemaCompileException>(() => compiler.Compile(graph, unused, new SchemaCompileOperation(), new XmlFacetEvaluator())); Assert.Throws<SchemaCompileException>(() => Batch(graph)); Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(source, options)); }
        Assert.NotNull(compiler.Compile(graph, unused, new SchemaCompileOperation())); // structural element fixed comparison unchanged
    }
    private static XsdCompiledSchema SmallGraph()
    {
        var basis = Graph(Wrap(""));
        XsdTypeDefinition Empty(string name, int line) => new(new("urn:batch", name), null, XsdDerivation.Restriction, false, false, [], [], XsdContentKind.Empty, null, null, [], null, new(new Uri("https://batch.test/small.xsd"), line, 1, null));
        return basis with { Types = [Empty("first", 1), Empty("second", 2)] };
    }
    [Fact]
    public void SmallGraphQuotasAreDerivedIndependentlyAndEntriesChargeBeforeGrowth()
    {
        // Per type: type-map entry1, visit1, empty node-list1, immutable program1,
        // output entry1 =>work5/components4. Output dictionary adds1/1; final
        // poll adds0/0. Two empty nonderived types therefore cost11work/9components.
        var graph = SmallGraph(); var exact = new Ledger(11, 9); Assert.Equal(2, Batch(graph, exact).Count); Assert.Equal(11, exact.Work); Assert.Equal(9, exact.Components);
        Assert.Throws<SchemaCompileException>(() => Batch(graph, new Ledger(10, 9)));
        Assert.Throws<SchemaCompileException>(() => Batch(graph, new Ledger(11, 8)));
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void LaterTypeCancellationAndRefusalCannotPublishWithUnlimitedCaps(bool cancel)
    {
        var graph = SmallGraph(); using var source = new CancellationTokenSource(); int secondVisits = 0;
        var ledger = new BatchLedger(source.Token);
        ledger.Before = (work, at) =>
        {
            if (at.LineNumber == 2 && ++secondVisits == 2)
            { if (cancel) source.Cancel(); else ledger.Refuse("SCHEMA.Limit", "independent later-type refusal", at); }
        };
        if (cancel) Assert.Throws<OperationCanceledException>(() => Batch(graph, ledger)); else Assert.Throws<SchemaCompileException>(() => Batch(graph, ledger));
        Assert.Equal(2, secondVisits); Assert.Equal(graph.Types[1].Location, ledger.Last); Assert.Null(ledger.RemainingWork);
    }
    [Fact]
    public void FinalZeroWorkPollChecksCancellationAndNoOperationCachesSuccess()
    {
        var graph = SmallGraph(); Assert.Equal(2, Batch(graph).Count); using var source = new CancellationTokenSource();
        var ledger = new BatchLedger(source.Token) { Before = (work, _) => { if (work == 0) source.Cancel(); } };
        Assert.Throws<OperationCanceledException>(() => Batch(graph, ledger)); Assert.True(source.IsCancellationRequested);
        Assert.Equal(2, Batch(graph).Count);
    }
    private sealed class BatchLedger(CancellationToken token) : ISchemaOperation
    {
        private readonly SchemaCompileOperation inner = new(new() { MaxOperations = null, MaxComponents = null, Limits = new() { EvaluationWork = null }, CancellationToken = token });
        internal Action<long, XmlLocation>? Before; internal XmlLocation Last;
        public XmlResourceLimits Limits => inner.Limits; public CancellationToken CancellationToken => inner.CancellationToken;
        public bool IsComplete => inner.IsComplete; public long? RemainingWork => inner.RemainingWork;
        public bool Charge(long work, XmlLocation at) { Last = at; Before?.Invoke(work, at); return inner.Charge(work, at); }
        public bool ChargeComponents(int count, XmlLocation at) => inner.ChargeComponents(count, at);
        public bool AddDiagnostic(XmlDiagnostic diagnostic) => inner.AddDiagnostic(diagnostic);
        public void Refuse(string code, string message, XmlLocation at) => inner.Refuse(code, message, at);
    }
    private sealed class Resolver(string child) : XmlResourceResolver
    {
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken token)
        { token.ThrowIfCancellationRequested(); return request.Uri == new Uri("https://batch.test/child.xsd") ? new(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(child)), request.Uri) : null; }
    }
}
