using Xemele.Validation;
using Xunit;
using ProductSchema = Xemele.Validation.XmlSchema;
namespace Xemele.Tests.M8XsdParticleNormalization;

public sealed class NeighborTests
{
    internal const string Xs = "http://www.w3.org/2001/XMLSchema";
    internal static string Wrap(string body) => $"<xs:schema xmlns:xs='{Xs}'>{body}</xs:schema>";
    internal static string Restriction(string basis, string derived, string declarations = "") => Wrap($"{declarations}<xs:complexType name='B'>{basis}</xs:complexType><xs:complexType name='D'><xs:complexContent><xs:restriction base='B'>{derived}</xs:restriction></xs:complexContent></xs:complexType><xs:element name='root' type='D'/>");
    internal static string Element(string name, string occurs = "") => $"<xs:element ref='{name}' {occurs}/>";
    internal static string Sequence(string body, string occurs = "") => $"<xs:sequence {occurs}>{body}</xs:sequence>";
    internal static string Singleton(string derived = "m", string basisOccurs = "", string derivedOccurs = "", string headBlock = "") => Restriction(Sequence(Element("h"), basisOccurs), Sequence(Element(derived), derivedOccurs), $"<xs:element name='h' type='xs:string' abstract='true' {headBlock}/><xs:element name='m' type='xs:string' substitutionGroup='h'/><xs:element name='other' type='xs:string'/>");
    internal static bool BclAccepts(string source)
    {
        try
        {
            using var reader = System.Xml.XmlReader.Create(new StringReader(source), new System.Xml.XmlReaderSettings { XmlResolver = null, DtdProcessing = System.Xml.DtdProcessing.Prohibit });
            var schemas = new System.Xml.Schema.XmlSchemaSet { XmlResolver = null }; schemas.Add(null, reader); schemas.Compile(); return schemas.IsCompiled;
        }
        catch (System.Xml.Schema.XmlSchemaException) { return false; }
    }
    internal static void Expect(string source, bool valid, bool oracle = true)
    {
        if (oracle) Assert.Equal(valid, BclAccepts(source));
        if (valid) Assert.NotNull(ProductSchema.Compile(source));
        else Assert.Contains(Assert.Throws<SchemaCompileException>(() => ProductSchema.Compile(source)).Diagnostics, d => d.Code == "XSD.SchemaRestriction");
    }
    public static IEnumerable<object[]> DeclarationOrders()
    {
        foreach (string a in new[] { "a", "b", "c", "d" }) foreach (string b in new[] { "a", "b", "c", "d" })
        foreach (string c in new[] { "a", "b", "c", "d" }) foreach (string d in new[] { "a", "b", "c", "d" })
            if (new HashSet<string> { a, b, c, d }.Count == 4) yield return [a + b + c + d];
    }
    internal static string Nested(string order, string derived = "c", string occurs = "")
    {
        var declarations = new System.Text.StringBuilder();
        foreach (char name in order)
            declarations.Append(name switch
            {
                'a' => "<xs:element name='a' type='xs:int' substitutionGroup='b'/>",
                'b' => "<xs:element name='b' type='xs:int' substitutionGroup='c'/>",
                'c' => "<xs:element name='c' substitutionGroup='d'/>",
                _ => "<xs:element name='d'/>"
            });
        declarations.Append("<xs:element name='other'/>");
        return Restriction(Sequence(Element("d", occurs)), Sequence(Element(derived, occurs)), declarations.ToString());
    }
    [Theory]
    [MemberData(nameof(DeclarationOrders))]
    public void AllDeclarationPermutationsPreserveNestedSubstitutionSets(string order)
    {
        string source = Nested(order); Expect(source, true);
        var schema = ProductSchema.Compile(source);
        Assert.True(schema.Validate("<root><a>1</a></root>").IsValid);
        Assert.True(schema.Validate("<root><b>2</b></root>").IsValid);
        Assert.True(schema.Validate("<root><c/></root>").IsValid);
        Assert.False(schema.Validate("<root><d/></root>").IsValid);
    }
    [Theory]
    [InlineData("minOccurs='0' maxOccurs='3'", true)]
    [InlineData("minOccurs='2' maxOccurs='4'", true)]
    [InlineData("maxOccurs='unbounded'", true)]
    public void NonUnitImplicitChoiceBoundsAndExpansionGuardsRemainFinite(string occurs, bool valid) => Expect(Nested("dcba", occurs: occurs), valid);
    [Theory]
    [InlineData("a b c", true)] [InlineData("c b a", false)] [InlineData("b a c", false)]
    public void AuthoredChoicesKeepRecurseLaxOrder(string order, bool valid)
    {
        string Choice(string names) => "<xs:choice>" + string.Concat(names.Split(' ').Select(name => $"<xs:element name='{name}' type='xs:string'/>")) + "</xs:choice>";
        Expect(Restriction(Choice("a b c d"), Choice(order)), valid);
    }
    [Theory]
    [InlineData("", "", true)]
    [InlineData("minOccurs='0' maxOccurs='3'", "minOccurs='1' maxOccurs='2'", true)]
    [InlineData("minOccurs='2' maxOccurs='3'", "minOccurs='2' maxOccurs='3'", true)]
    [InlineData("minOccurs='2' maxOccurs='3'", "minOccurs='1' maxOccurs='3'", false)]
    [InlineData("minOccurs='2' maxOccurs='3'", "minOccurs='2' maxOccurs='4'", false)]
    [InlineData("minOccurs='2' maxOccurs='unbounded'", "minOccurs='3' maxOccurs='8'", true)]
    public void SingletonExpansionPreservesOuterOccurrences(string basis, string derived, bool valid) => Expect(Singleton(basisOccurs: basis, derivedOccurs: derived), valid);
    [Theory]
    [InlineData("other", "", false)] [InlineData("m", "block='substitution'", false)]
    public void SingletonExpansionCannotAdmitIncompatibleOrBlockedNeighbor(string derived, string block, bool valid) => Expect(Singleton(derived, headBlock: block), valid);
    [Fact]
    public void InvalidMemberTypeStillRejectsBeforeParticleNormalization() => Expect(Singleton().Replace("name='m' type='xs:string'", "name='m' type='xs:int'", StringComparison.Ordinal), false);
    [Theory]
    [InlineData(false, false, true)] [InlineData(true, false, false)] [InlineData(true, true, false)]
    public void NamedGroupDefersUpaOnlyUntilActualNamedOrAnonymousContent(bool used, bool anonymous, bool valid)
    {
        string group = "<xs:group name='g'><xs:choice><xs:element name='x' type='xs:string'/><xs:element name='x' type='xs:string'/></xs:choice></xs:group>";
        string actual = !used ? "" : anonymous ? "<xs:element name='root'><xs:complexType><xs:group ref='g'/></xs:complexType></xs:element>" : "<xs:complexType name='T'><xs:group ref='g'/></xs:complexType>";
        Expect(Wrap(group + actual), valid);
    }
    [Theory]
    [InlineData("choice")] [InlineData("sequence")]
    public void UnusedElementDeclarationConsistencyStillRejects(string compositor)
    {
        string source = Wrap($"<xs:group name='g'><xs:{compositor}><xs:element name='x' type='xs:int'/><xs:element name='x' type='xs:string'/></xs:{compositor}></xs:group>");
        // BCL defers unused group finalization. XSD 1.0 Model Group Correct /
        // Element Declarations Consistent requires same-name declarations to
        // share a type, independent of whether a group is referenced.
        Assert.True(BclAccepts(source)); Expect(source, false, oracle: false);
    }
    [Fact]
    public void UnusedUndefinedDeclarationStillRejects()
    {
        string source = Wrap("<xs:group name='g'><xs:choice><xs:element ref='missing'/><xs:element name='x'/></xs:choice></xs:group>");
        // The compiler's established publication policy rejects unresolved
        // declaration references even in unused groups; BCL defers this check.
        Assert.True(BclAccepts(source)); Expect(source, false, oracle: false);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void UnusedRedefinitionStillChecksOriginalConsistencyAndRestriction(bool inconsistent)
    {
        string child = Wrap(inconsistent ? "<xs:group name='g'><xs:sequence><xs:element name='x' type='xs:int'/><xs:element name='x' type='xs:string'/></xs:sequence></xs:group>" : "<xs:group name='g'><xs:sequence><xs:element name='x'/></xs:sequence></xs:group>");
        string source = Wrap("<xs:redefine schemaLocation='child.xsd'><xs:group name='g'><xs:sequence><xs:element name='y'/></xs:sequence></xs:group></xs:redefine>");
        Assert.Throws<SchemaCompileException>(() => ProductSchema.Compile(source, new() { BaseUri = new Uri("https://particles.test/main.xsd"), Resolver = new Resolver(child) }));
    }
    private sealed class Resolver(string child) : XmlResourceResolver
    {
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken token)
        { token.ThrowIfCancellationRequested(); return request.Uri == new Uri("https://particles.test/child.xsd") ? new(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(child)), request.Uri) : null; }
    }
}
