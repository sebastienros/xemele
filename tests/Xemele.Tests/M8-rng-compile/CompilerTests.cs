using System.Text;
using Xemele.Validation;
using Xunit;

namespace Xemele.Tests.M8RngCompile;

public sealed class CompilerTests
{
    private const string Ns = "http://relaxng.org/ns/structure/1.0";
    private const string Library = "http://www.w3.org/2001/XMLSchema-datatypes";
    internal static RngCompiledSchema Compile(string xml, SchemaCompileOptions? options = null)
    {
        var context = new SchemaCompileContext(options);
        var source = context.Parse(xml);
        return new RelaxNgCompiler().Compile(source.Document, context.Options, context.Operation);
    }
    private static string Root(string pattern) => "<element xmlns='" + Ns + "' name='root'>" + pattern + "</element>";
    private static string Grammar(string content) => "<grammar xmlns='" + Ns + "'>" + content + "</grammar>";
    private static SchemaCompileException Reject(string pattern) => Assert.Throws<SchemaCompileException>(() => Compile(Root(pattern)));

    [Fact]
    public void FullSyntaxNormalizesOwnedDenseGraphAndPreservesOriginalContext()
    {
        string xml = "<element xmlns='" + Ns + "' xmlns:p='urn:outer' ns='urn:elements' name='root' datatypeLibrary='" + Library + "'>" +
            "<a:doc xmlns:a='urn:annotation'/><optional><attribute name='a'/></optional>" +
            "<mixed><zeroOrMore><element><choice><name>p:one</name><nsName ns='urn:other'/></choice>" +
            "<choice><value type='QName' xmlns:p='urn:inner' xml:base='child/'> p:item </value><list><oneOrMore><data type='integer'/></oneOrMore></list></choice>" +
            "</element></zeroOrMore></mixed></element>";
        var document = XmlDocument.Parse(xml, cancellationToken: TestContext.Current.CancellationToken);
        var options = new SchemaCompileOptions { BaseUri = new("https://example.test/schema/") };
        var graph = new RelaxNgCompiler().Compile(document, options, new SchemaCompileOperation(options));
        Assert.Equal(RngPatternKind.Ref, graph.Patterns[graph.Start].Kind);
        for (int i = 0; i < graph.Patterns.Count; i++)
        { Assert.Equal(i, graph.Patterns[i].Id); Assert.All(graph.Patterns[i].Children, edge => Assert.InRange(edge, 0, graph.Patterns.Count - 1)); }
        var value = Assert.Single(graph.Patterns, p => p.Kind == RngPatternKind.Value);
        Assert.Equal(" p:item ", value.Value); Assert.Equal("urn:inner", value.Context.Prefixes["p"]);
        Assert.Equal("urn:elements", value.Context.DefaultNamespace); Assert.Equal(new Uri("https://example.test/schema/child/"), value.Context.BaseUri);
        Assert.True(value.Location.LinePosition > 0); Assert.Null(value.Location.ByteOffset);
        var attribute = Assert.Single(graph.Patterns, p => p.Kind == RngPatternKind.Attribute);
        Assert.Equal(new SchemaName("", "a"), Assert.IsType<RngName>(attribute.NameClass).Name);
        document.RootElement.SetAttribute("ns", "changed");
        Assert.Equal("urn:elements", value.Context.DefaultNamespace);
        Assert.Null(graph.Datatypes.Find(Library)); // definition-only; no executable placeholder
    }
    [Theory]
    [InlineData("<group><empty/><text/></group>", (int)RngPatternKind.Text)]
    [InlineData("<interleave><text/><empty/></interleave>", (int)RngPatternKind.Text)]
    [InlineData("<oneOrMore><empty/></oneOrMore>", (int)RngPatternKind.Empty)]
    [InlineData("<choice><notAllowed/><text/></choice>", (int)RngPatternKind.Text)]
    [InlineData("<group><notAllowed/><text/></group>", (int)RngPatternKind.NotAllowed)]
    [InlineData("<list><notAllowed/></list>", (int)RngPatternKind.NotAllowed)]
    [InlineData("<attribute name='a'><notAllowed/></attribute>", (int)RngPatternKind.NotAllowed)]
    [InlineData("<choice><empty/><empty/></choice>", (int)RngPatternKind.Empty)]
    public void EmptyAndNotAllowedFixedPoint(string pattern, int expected)
    {
        var graph = Compile(Root(pattern)); var element = graph.Patterns[graph.Patterns[graph.Start].Children[0]];
        Assert.Equal((RngPatternKind)expected, graph.Patterns[element.Children[0]].Kind);
    }
    [Fact]
    public void GrammarScopesParentRefsCombineAndGuardedRecursion()
    {
        var graph = Compile(Grammar("<start><ref name='node'/></start><define name='node'><element name='n'><optional><grammar>" +
            "<start><parentRef name='node'/></start><define name='node'><element name='local'><empty/></element></define>" +
            "</grammar></optional></element></define>"));
        Assert.Single(graph.Patterns, p => p.Kind == RngPatternKind.Element);
        Assert.Contains(graph.Patterns, p => p.Kind == RngPatternKind.Choice);
        var combined = Compile(Grammar("<start combine='choice'><element name='a'><empty/></element></start><start><ref name='x'/></start>" +
            "<define name='x' combine='choice'><element name='b'><empty/></element></define><div><define name='x'><element name='c'><empty/></element></define></div>"));
        Assert.Equal(3, combined.Patterns.Count(p => p.Kind == RngPatternKind.Element));
    }
    [Theory]
    [InlineData("<start><ref name='absent'/></start>")]
    [InlineData("<start><parentRef name='x'/></start><define name='x'><empty/></define>")]
    [InlineData("<start><ref name='x'/></start><define name='x'><ref name='y'/></define><define name='y'><ref name='x'/></define>")]
    [InlineData("<start><element name='a'><empty/></element></start><start><element name='b'><empty/></element></start>")]
    [InlineData("<start><ref name='x'/></start><define name='x' combine='choice'><empty/></define><define name='x' combine='interleave'><empty/></define>")]
    [InlineData("<define name='x'><empty/></define>")]
    [InlineData("<start><element name='a'><empty/></element></start><define name='unused'><ref name='absent'/></define>")]
    public void ScopeReferenceAndCombinationRestrictions(string content)
    { Assert.Equal("RNG.SchemaRestriction", Assert.Throws<SchemaCompileException>(() => Compile(Grammar(content))).Diagnostics[0].Code); }

    [Theory]
    [InlineData("<attribute name='a'><attribute name='b'/></attribute>")]
    [InlineData("<attribute name='a'><element name='b'><empty/></element></attribute>")]
    [InlineData("<list><list><empty/></list></list>")]
    [InlineData("<list><text/></list>")]
    [InlineData("<list><attribute name='a'/></list>")]
    [InlineData("<list><interleave><data type='token'/><data type='token'/></interleave></list>")]
    [InlineData("<data type='token'><except><empty/></except></data>")]
    [InlineData("<data type='token'><except><list><empty/></list></except></data>")]
    [InlineData("<oneOrMore><group><attribute name='a'/><attribute name='b'/></group></oneOrMore>")]
    [InlineData("<group><data type='token'/><text/></group>")]
    [InlineData("<oneOrMore><value>x</value></oneOrMore>")]
    [InlineData("<attribute><anyName/></attribute>")]
    [InlineData("<attribute><nsName ns='urn:a'/></attribute>")]
    [InlineData("<group><attribute name='a'/><attribute name='a'/></group>")]
    [InlineData("<interleave><element name='a'><empty/></element><element name='a'><empty/></element></interleave>")]
    [InlineData("<interleave><text/><text/></interleave>")]
    [InlineData("<element><anyName><except><anyName/></except></anyName><empty/></element>")]
    [InlineData("<element><nsName><except><nsName/></except></nsName><empty/></element>")]
    [InlineData("<attribute name='xmlns'/>")]
    [InlineData("<attribute><name ns='http://www.w3.org/2000/xmlns'>a</name></attribute>")]
    public void DatatypeIndependentRestrictionsAreCompileErrors(string pattern)
    { Assert.Equal("RNG.SchemaRestriction", Reject(pattern).Diagnostics[0].Code); }
    [Theory]
    [InlineData("<empty/>")]
    [InlineData("<text/>")]
    [InlineData("<data type='token'/>")]
    [InlineData("<optional><element name='e'><empty/></element></optional>")]
    [InlineData("<group><element name='a'><empty/></element><element name='b'><empty/></element></group>")]
    public void StartRestrictions(string pattern)
    { Assert.Throws<SchemaCompileException>(() => Compile("<grammar xmlns='" + Ns + "'><start>" + pattern + "</start></grammar>")); }
    [Theory]
    [InlineData("<list><oneOrMore><data type='token'/></oneOrMore></list>")]
    [InlineData("<oneOrMore><attribute><anyName><except><name>a</name></except></anyName></attribute></oneOrMore>")]
    [InlineData("<choice><attribute name='a'/><attribute name='a'/></choice>")]
    [InlineData("<choice><data type='token'/><element name='a'><empty/></element></choice>")]
    [InlineData("<group><data type='token'/><attribute name='a'/></group>")]
    [InlineData("<data type='token'><except><choice><value>a</value><data type='string'/></choice></except></data>")]
    public void CloseValidAlternativesAreAccepted(string pattern) => Assert.NotEmpty(Compile(Root(pattern)).Patterns);

    [Theory]
    [InlineData("<group/>")]
    [InlineData("<empty><empty/></empty>")]
    [InlineData("<attribute name='a'><empty/><empty/></attribute>")]
    [InlineData("<value><a:note xmlns:a='urn:a'/></value>")]
    [InlineData("<group bad='yes'><empty/></group>")]
    [InlineData("<element name='bad:prefix'><empty/></element>")]
    [InlineData("<element name='two:colons:bad'><empty/></element>")]
    [InlineData("<unknown/>")]
    [InlineData("<text>not whitespace</text>")]
    public void FullXmlGrammarRejectsMalformedSchemas(string pattern) => Reject(pattern);

    [Theory]
    [InlineData("length", (int)XmlFacetKind.Length)] [InlineData("minLength", (int)XmlFacetKind.MinLength)]
    [InlineData("maxLength", (int)XmlFacetKind.MaxLength)] [InlineData("pattern", (int)XmlFacetKind.Pattern)]
    [InlineData("maxInclusive", (int)XmlFacetKind.MaxInclusive)] [InlineData("maxExclusive", (int)XmlFacetKind.MaxExclusive)]
    [InlineData("minInclusive", (int)XmlFacetKind.MinInclusive)] [InlineData("minExclusive", (int)XmlFacetKind.MinExclusive)]
    [InlineData("totalDigits", (int)XmlFacetKind.TotalDigits)] [InlineData("fractionDigits", (int)XmlFacetKind.FractionDigits)]
    public void ExactTenParameterMapping(string name, int expected)
    {
        var graph = Compile(Root("<data datatypeLibrary='" + Library + "' type='decimal'><param name='" + name + "' xmlns:p='urn:param' xml:base='urn:param:base'>  lexical\tvalue  </param></data>"));
        var param = Assert.Single(Assert.Single(graph.Patterns, p => p.Kind == RngPatternKind.Data).Parameters);
        Assert.Equal((XmlFacetKind)expected, param.Kind); Assert.Equal("  lexical\tvalue  ", param.LexicalValue); Assert.False(param.Fixed);
        Assert.Equal("urn:param", param.Context.Prefixes["p"]); Assert.Equal(new Uri("urn:param:base"), param.Context.BaseUri); Assert.True(param.Location.LinePosition > 1);
    }
    [Theory]
    [InlineData("whiteSpace")] [InlineData("enumeration")] [InlineData("Length")] [InlineData("unknown")]
    public void UnrepresentableOrExcludedParameterNamesAreDatatypeErrors(string name)
    { Assert.Equal("RNG.Datatype", Reject("<data datatypeLibrary='" + Library + "' type='string'><param name='" + name + "'>x</param></data>").Diagnostics[0].Code); }
    [Fact]
    public void BuiltinParametersRejectAndDatatypeChecksSurviveSimplification()
    {
        Assert.Equal("RNG.Datatype", Reject("<data type='token'><param name='length'>1</param></data>").Diagnostics[0].Code);
        var graph = Compile(Grammar("<start><element name='e'><group><notAllowed/><data datatypeLibrary='" + Library + "' type='string'><param name='pattern'>a</param><param name='pattern'>b</param></data></group></element></start>" +
            "<define name='unused'><value datatypeLibrary='" + Library + "' type='QName' xmlns:p='urn:unused'>p:item</value></define>"));
        var data = Assert.Single(graph.Patterns, p => p.Kind == RngPatternKind.Data); Assert.Equal(2, data.Parameters.Count);
        Assert.Equal("a", data.Parameters[0].LexicalValue); Assert.Equal("b", data.Parameters[1].LexicalValue);
        Assert.Equal("urn:unused", Assert.Single(graph.Patterns, p => p.Kind == RngPatternKind.Value).Context.Prefixes["p"]);
    }
    [Fact]
    public void ArbitraryNameClassesHaveExactOverlapPartition()
    {
        RngNameClass left = new RngAnyName(new RngNameChoice(new RngNsName("urn:a", null), new RngName(new("urn:b", "x"))));
        RngNameClass right = new RngNsName("urn:b", new RngName(new("urn:b", "y")));
        var op = new SchemaCompileOperation(); Assert.True(RngNameClasses.Overlaps(left, right, op, default));
        Assert.False(RngNameClasses.Overlaps(left, new RngNsName("urn:a", null), op, default));
        Assert.False(RngNameClasses.Contains(left, "urn:b", "x", op, default)); Assert.True(RngNameClasses.Contains(left, "urn:b", "z", op, default));
        var disjoint = Compile(Root("<interleave><element><anyName><except><nsName ns='urn:a'/></except></anyName><empty/></element>" +
            "<element><nsName ns='urn:a'/><empty/></element></interleave>")); Assert.NotEmpty(disjoint.Patterns);
    }
    [Fact]
    public void ShapeConstructorsRejectMalformedGraphsAndSnapshotCollections()
    {
        var context = new SchemaNamespaceContext(new Dictionary<string, string>(), "", null); var registry = new EmptyRegistry();
        Assert.Throws<ArgumentException>(() => new RngPattern(0, (int)RngPatternKind.Choice, [0], null, null, [], null, context, default));
        Assert.Throws<ArgumentException>(() => new RngPattern(0, (int)RngPatternKind.Text, [], new RngAnyName(null), null, [], null, context, default));
        Assert.Throws<ArgumentException>(() => new RngPattern(0, (int)RngPatternKind.Data, [], null, null, [], null, context, default));
        var leaf = new RngPattern(0, (int)RngPatternKind.Empty, [], null, null, [], null, context, default);
        Assert.Throws<ArgumentException>(() => new RngCompiledSchema(1, [leaf], registry));
        Assert.Throws<ArgumentException>(() => new RngCompiledSchema(0, [leaf with { Id = 2 }], registry));
        Assert.Throws<ArgumentException>(() => new RngCompiledSchema(0, [leaf with { Kind = RngPatternKind.Ref, Children = [1] }], registry));
        var list = new List<RngPattern> { leaf }; var graph = new RngCompiledSchema(0, list, registry); list.Clear(); Assert.Single(graph.Patterns);
        var children = new List<int> { 0 }; var repeated = new RngPattern(1, (int)RngPatternKind.OneOrMore, children, null, null, [], null, context, default); children.Clear(); Assert.Single(repeated.Children);
    }
    private sealed class EmptyRegistry : IXmlDatatypeRegistry
    { public IXmlDatatypeLibrary? Find(string ns) => null; public XmlDatatypeDefinition? FindType(SchemaName name) => null; }
}
