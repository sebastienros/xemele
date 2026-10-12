using System.Xml;
using Xemele.Validation;
using Xunit;
using Ledger = Xemele.Tests.M8XsdContent.OperationTests.Ledger;
using ProductSchema = Xemele.Validation.XmlSchema;
namespace Xemele.Tests.M8XsdAttributeFixed;

public sealed class AttributeFixedTests
{
    internal const string Xs = "http://www.w3.org/2001/XMLSchema";
    internal const string Definitions = "<xs:simpleType name='L'><xs:list itemType='xs:decimal'/></xs:simpleType><xs:simpleType name='U'><xs:union memberTypes='xs:decimal xs:boolean'/></xs:simpleType><xs:simpleType name='Q'><xs:list itemType='xs:QName'/></xs:simpleType>";
    internal static string Wrap(string body) => $"<xs:schema xmlns:xs='{Xs}'>{body}</xs:schema>";
    internal static string Attribute(string value, string type = "xs:decimal", string context = "") => $"<xs:attribute name='a' type='{type}' fixed='{value}' {context}/>";
    internal static string Derivation(string basis, string derived, string kind = "restriction", string definitions = "") =>
        Wrap($"{definitions}<xs:complexType name='B'>{basis}</xs:complexType><xs:complexType name='D'><xs:complexContent><xs:{kind} base='B'>{derived}</xs:{kind}></xs:complexContent></xs:complexType><xs:element name='root' type='D'/>");
    internal static XsdCompiledSchema Prepare(string source)
    {
        var context = new SchemaCompileContext(new());
        var graph = new XmlSchemaCompiler().Compile(context.Parse(source).Document, context.Options, context.Operation);
        return graph with { Datatypes = new XmlDatatypeRegistry(graph.SimpleTypes.Where(t => t.Name.NamespaceUri != Xs), context.Operation) };
    }
    internal static XsdContentProgram Complete(XsdCompiledSchema graph, ISchemaOperation? operation = null, IXmlFacetEvaluator? facets = null) =>
        new XsdContentCompiler().Compile(graph, graph.Types.Single(t => t.Name == new SchemaName("", "D")), operation ?? new SchemaCompileOperation(), facets ?? new XmlFacetEvaluator());
    private static bool BclAccepts(string source)
    {
        try
        {
            using var reader = XmlReader.Create(new StringReader(source), new XmlReaderSettings { XmlResolver = null, DtdProcessing = DtdProcessing.Prohibit });
            var schemas = new System.Xml.Schema.XmlSchemaSet { XmlResolver = null };
            schemas.Add(null, reader); schemas.Compile(); return schemas.IsCompiled;
        }
        catch (System.Xml.Schema.XmlSchemaException) { return false; }
    }
    [Theory]
    [InlineData("L", "1  2 3", "01.0 2.00 03", true)]
    [InlineData("L", "1 2", "1 3", false)]
    [InlineData("L", "1 2", "1", false)]
    [InlineData("xs:token", "  a   b  ", "a b", true)]
    [InlineData("xs:token", "  a   b  ", "a c", false)]
    [InlineData("xs:token", "  café   東京  ", "café 東京", true)]
    [InlineData("xs:token", "café 東京", "café 京都", false)]
    [InlineData("xs:string", " a ", "a", false)]
    [InlineData("xs:int", "  -1 ", "-01", true)]
    [InlineData("xs:int", "-1", "1", false)]
    [InlineData("xs:decimal", "1", "01.00", true)]
    [InlineData("xs:decimal", "-0", "+0.000", true)]
    [InlineData("U", "1", "01.00", true)]
    [InlineData("U", "1", "true", false)]
    [InlineData("U", "true", "1", false)]
    public void RestrictionUsesActualValuesWithIndependentOracle(string type, string basis, string derived, bool valid)
    {
        string source = Derivation(Attribute(basis, type), Attribute(derived, type), definitions: Definitions);
        Assert.Equal(valid, BclAccepts(source));
        if (valid) Assert.NotNull(ProductSchema.Compile(source));
        else Assert.Contains(Assert.Throws<SchemaCompileException>(() => ProductSchema.Compile(source)).Diagnostics, d => d.Code == "XSD.SchemaRestriction");
    }
    [Theory]
    [InlineData("0123456789012345678901234567890.00", true)]
    [InlineData("123456789012345678901234567891", false)]
    public void UnboundedDecimalsUseExplicitMathematicalOracle(string derived, bool valid)
    {
        // 100*A = B for the zero-padded scale-2 coefficient, whereas A+1 != A.
        // This exceeds System.Decimal.MaxValue; BCL range rejection cannot
        // independently establish equality or inequality in the XSD value space.
        const string basis = "123456789012345678901234567890";
        string digits = derived.Replace(".", "", StringComparison.Ordinal);
        var coefficient = System.Numerics.BigInteger.Parse(digits, System.Globalization.CultureInfo.InvariantCulture);
        int scale = derived.Contains('.') ? 2 : 0;
        var integer = System.Numerics.BigInteger.Parse(basis, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(valid, coefficient == integer * System.Numerics.BigInteger.Pow(10, scale));
        string source = Derivation(Attribute(basis), Attribute(derived));
        if (valid) Assert.NotNull(ProductSchema.Compile(source));
        else Assert.Contains(Assert.Throws<SchemaCompileException>(() => ProductSchema.Compile(source)).Diagnostics, d => d.Code == "XSD.SchemaRestriction");
    }
    [Theory]
    [InlineData("restriction", "p:v", "xmlns:p='urn:one'", "q:v", "xmlns:q='urn:one'", true)]
    [InlineData("restriction", "p:v", "xmlns:p='urn:one'", "p:v", "xmlns:p='urn:two'", false)]
    [InlineData("restriction", "p:v", "xmlns:p='urn:one'", "q:w", "xmlns:q='urn:one'", false)]
    [InlineData("extension", "p:v", "xmlns:p='urn:one'", "q:v", "xmlns:q='urn:one'", true)]
    [InlineData("extension", "p:v", "xmlns:p='urn:one'", "p:v", "xmlns:p='urn:two'", false)]
    public void QNameContextBelongsToEachLocalUse(string kind, string basis, string bc, string derived, string dc, bool valid)
    {
        string source = Derivation(Attribute(basis, "xs:QName", bc), Attribute(derived, "xs:QName", dc), kind);
        if (kind == "restriction") Assert.Equal(valid, BclAccepts(source));
        if (valid) Assert.NotNull(ProductSchema.Compile(source)); else Assert.Throws<SchemaCompileException>(() => ProductSchema.Compile(source));
    }
    [Theory]
    [InlineData("Q", "p:v p:w", "q:v q:w", true)]
    [InlineData("Q", "p:v p:w", "q:v q:x", false)]
    public void QNameListsRetainAuthoredContexts(string type, string basis, string derived, bool valid)
    {
        string source = Derivation(Attribute(basis, type, "xmlns:p='urn:one'"), Attribute(derived, type, "xmlns:q='urn:one'"), definitions: Definitions);
        Assert.Equal(valid, BclAccepts(source));
        if (valid) Assert.NotNull(ProductSchema.Compile(source)); else Assert.Throws<SchemaCompileException>(() => ProductSchema.Compile(source));
    }
    [Theory]
    [InlineData("restriction", "01.00", true)] [InlineData("restriction", "2", false)]
    [InlineData("extension", "01.00", true)] [InlineData("extension", "2", false)]
    public void BothDerivationKindsCheckInheritedFixed(string kind, string value, bool valid)
    {
        string source = Derivation(Attribute("1"), Attribute(value), kind);
        if (valid) Assert.NotNull(ProductSchema.Compile(source)); else Assert.Throws<SchemaCompileException>(() => ProductSchema.Compile(source));
    }
    [Theory]
    [InlineData("fixed='1'", "default='1'", false)]
    [InlineData("fixed='1'", "", false)]
    [InlineData("default='1'", "fixed='2'", true)]
    [InlineData("", "fixed='2'", true)]
    [InlineData("fixed='1'", "use='prohibited'", true)]
    public void FixedDefaultAndAbsentRemainDistinct(string basis, string derived, bool valid)
    {
        string source = Derivation($"<xs:attribute name='a' type='xs:int' {basis}/>", $"<xs:attribute name='a' type='xs:int' {derived}/>");
        Assert.Equal(valid, BclAccepts(source));
        if (valid) Assert.NotNull(ProductSchema.Compile(source)); else Assert.Throws<SchemaCompileException>(() => ProductSchema.Compile(source));
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ReferencedDeclarationContextSurvivesDifferentlyBoundUse(bool overrideBase)
    {
        string global = "<xs:attribute name='a' type='xs:QName' fixed='p:v' xmlns:p='urn:one'/>";
        string basis = overrideBase ? "<xs:attribute ref='a' fixed='q:v' xmlns:q='urn:one' xmlns:p='urn:two'/>" : "<xs:attribute ref='a' xmlns:p='urn:two'/>";
        string source = Derivation(basis, "<xs:attribute ref='a' fixed='q:v' xmlns:q='urn:one' xmlns:p='urn:three'/>", definitions: global);
        Assert.True(BclAccepts(source)); Assert.NotNull(ProductSchema.Compile(source));
        var graph = Prepare(source); var observed = new RecordingFacets(); Complete(graph, facets: observed);
        Assert.Contains(observed.Seen, x => x.Lexical == "q:v" && x.Context.Prefixes["q"] == "urn:one");
        if (!overrideBase) Assert.Contains(observed.Seen, x => x.Lexical == "p:v" && x.Context.Prefixes["p"] == "urn:one");
    }
    [Fact]
    public void InvalidLocalOverrideCannotBorrowDeclarationContext()
    {
        string source = Derivation("<xs:attribute ref='a'/>", "<xs:attribute ref='a' fixed='p:v' xmlns:p='urn:two'/>", definitions: "<xs:attribute name='a' type='xs:QName' fixed='p:v' xmlns:p='urn:one'/>");
        Assert.False(BclAccepts(source)); Assert.Throws<SchemaCompileException>(() => ProductSchema.Compile(source));
    }
    [Fact]
    public void FacetsReceiveAuthoredLexicalAndSharedOperation()
    {
        string source = Derivation(Attribute("01.0", "P"), Attribute("001.0", "P"), definitions: "<xs:simpleType name='P'><xs:restriction base='xs:decimal'><xs:pattern value='0[0-9]+[.]0'/></xs:restriction></xs:simpleType>");
        var graph = Prepare(source); var facets = new RecordingFacets(); var op = new Ledger(); Complete(graph, op, facets);
        Assert.Equal(new[] { "001.0", "01.0" }, facets.Seen.Select(x => x.Lexical)); Assert.All(facets.Seen, x => Assert.Same(op, x.Operation));
        Assert.NotNull(ProductSchema.Compile(source));
        Assert.Throws<SchemaCompileException>(() => ProductSchema.Compile(source.Replace("001.0", "1", StringComparison.Ordinal)));
    }
    [Theory]
    [InlineData("1", "01.0", false)] [InlineData("1", "1", true)] [InlineData("1", "2", false)]
    public void FrozenStructuralLanePreservesLexicalComparison(string basis, string derived, bool valid)
    {
        var graph = Prepare(Derivation(Attribute(basis), Attribute(derived)));
        IXsdContentCompiler frozen = new XsdContentCompiler();
        void Run() => frozen.Compile(graph, graph.Types.Single(t => t.Name.LocalName == "D"), new SchemaCompileOperation());
        if (valid) Run(); else Assert.Throws<SchemaCompileException>(Run);
    }
    [Fact]
    public void ExactSharedBudgetAndCancellationBoundaries()
    {
        var graph = Prepare(Derivation(Attribute("1 2 3", "L"), Attribute("01 02 03", "L"), definitions: Definitions));
        var measured = new Ledger(); Complete(graph, measured);
        Complete(graph, new Ledger(measured.Work, measured.Components));
        var stopped = new Ledger(measured.Work - 1);
        Assert.Throws<SchemaCompileException>(() => Complete(graph, stopped)); Assert.False(stopped.IsComplete); Assert.Contains(stopped.Diagnostics, d => d.Code == "SCHEMA.Limit");
        Assert.Throws<SchemaCompileException>(() => Complete(graph, new Ledger(componentLimit: measured.Components - 1)));
        using var cancel = new CancellationTokenSource();
        var facets = new RecordingFacets { Before = (_, _) => cancel.Cancel() };
        Assert.ThrowsAny<OperationCanceledException>(() => Complete(graph, new Ledger(token: cancel.Token), facets));
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void SemanticMismatchAndOperationRefusalHaveDifferentDiagnostics(bool refuse)
    {
        var graph = Prepare(Derivation(Attribute("1"), Attribute("01.0")));
        var op = new Ledger(); var facets = new RecordingFacets { Reject = true, Before = (_, operation) => { if (refuse) operation.Refuse("SCHEMA.Limit", "independent stop", default); } };
        var error = Assert.Throws<SchemaCompileException>(() => Complete(graph, op, facets));
        Assert.Equal(!refuse, op.IsComplete);
        Assert.Contains(error.Diagnostics, d => d.Code == (refuse ? "SCHEMA.Limit" : "XSD.SchemaRestriction"));
        if (refuse) Assert.DoesNotContain(op.Diagnostics, d => d.Code == "XSD.SchemaRestriction");
    }
    private sealed class RecordingFacets : IXmlFacetEvaluator
    {
        internal readonly List<(string Lexical, SchemaNamespaceContext Context, ISchemaOperation Operation)> Seen = [];
        internal Action<ReadOnlyMemory<char>, ISchemaOperation>? Before;
        internal bool Reject;
        private readonly XmlFacetEvaluator inner = new();
        public bool ValidateDefinition(XmlDatatypeDefinition type, IXmlDatatypeRegistry registry, ISchemaOperation operation) => inner.ValidateDefinition(type, registry, operation);
        public bool TryValidate(XmlDatatypeDefinition type, ReadOnlySpan<char> lexical, SchemaNamespaceContext context, IXmlDatatypeRegistry registry, ISchemaOperation operation, out XmlDatatypeValue? value, out XmlDatatypeError? error)
        {
            string owned = lexical.ToString(); Seen.Add((owned, context, operation)); Before?.Invoke(owned.AsMemory(), operation);
            if (Reject) { value = null; error = null; return false; }
            return inner.TryValidate(type, lexical, context, registry, operation, out value, out error);
        }
    }
}
