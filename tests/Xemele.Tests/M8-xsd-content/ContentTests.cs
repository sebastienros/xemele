using System.Numerics;
using Xemele.Validation;
using Xunit;
namespace Xemele.Tests.M8XsdContent;

public sealed class ContentTests
{
    internal const string Xs = "http://www.w3.org/2001/XMLSchema";
    internal static XsdCompiledSchema Schema(string body)
    {
        var context = new SchemaCompileContext();
        var document = context.Parse($"<xs:schema xmlns:xs='{Xs}'>{body}</xs:schema>").Document;
        return new XmlSchemaCompiler().Compile(document, context.Options, context.Operation);
    }
    internal static XsdTypeDefinition Type(XsdCompiledSchema schema, string name = "T") => Assert.Single(schema.Types, x => x.Name == new SchemaName("", name));
    internal static XsdContentProgram Program(XsdCompiledSchema schema, string name = "T", ISchemaOperation? operation = null) => new XsdContentCompiler().Compile(schema, Type(schema, name), operation ?? new SchemaCompileContext().Operation);
    internal static bool Accepts(XsdContentProgram program, params string[] children)
    {
        var cursor = new XsdContentMatcher().CreateCursor(program); var op = new SchemaCompileContext().Operation;
        foreach (string child in children) if (!cursor.Accept(new("", child), op, out _, out _)) return false;
        return cursor.Complete(op);
    }
    private static XsdContentProgram Particle(string content) => Program(Schema($"<xs:complexType name='T'>{content}</xs:complexType>"));

    [Theory]
    [InlineData("<xs:sequence><xs:element name='a' minOccurs='2' maxOccurs='3'/><xs:element name='b' minOccurs='0'/></xs:sequence>", "a a", true)]
    [InlineData("<xs:sequence><xs:element name='a' minOccurs='2' maxOccurs='3'/><xs:element name='b' minOccurs='0'/></xs:sequence>", "a", false)]
    [InlineData("<xs:sequence><xs:element name='a' minOccurs='2' maxOccurs='3'/><xs:element name='b' minOccurs='0'/></xs:sequence>", "a a a b", true)]
    [InlineData("<xs:sequence><xs:element name='a' minOccurs='2' maxOccurs='3'/><xs:element name='b' minOccurs='0'/></xs:sequence>", "a a a a", false)]
    [InlineData("<xs:choice minOccurs='2' maxOccurs='3'><xs:element name='a'/><xs:element name='b'/></xs:choice>", "b a b", true)]
    [InlineData("<xs:choice minOccurs='2' maxOccurs='3'><xs:element name='a'/><xs:element name='b'/></xs:choice>", "b", false)]
    [InlineData("<xs:all><xs:element name='a'/><xs:element name='b' minOccurs='0'/></xs:all>", "b a", true)]
    [InlineData("<xs:all><xs:element name='a'/><xs:element name='b' minOccurs='0'/></xs:all>", "b", false)]
    [InlineData("<xs:all><xs:element name='a'/><xs:element name='b' minOccurs='0'/></xs:all>", "a a", false)]
    [InlineData("<xs:all minOccurs='0'><xs:element name='a'/><xs:element name='b'/></xs:all>", "", true)]
    [InlineData("<xs:all minOccurs='0'><xs:element name='a'/><xs:element name='b'/></xs:all>", "a", false)]
    [InlineData("<xs:all minOccurs='0'><xs:element name='a'/><xs:element name='b'/></xs:all>", "b a", true)]
    [InlineData("<xs:sequence/>", "", true)]
    [InlineData("<xs:choice/>", "", false)]
    [InlineData("<xs:choice minOccurs='0'/>", "", true)]
    [InlineData("", "a", false)]
    public void IndependentBoundaryExpectations(string particle, string children, bool expected)
        => Assert.Equal(expected, Accepts(Particle(particle), children.Length == 0 ? [] : children.Split(' ')));

    [Theory]
    [InlineData("<xs:sequence minOccurs='10000000000000000000000000000000000000000' maxOccurs='10000000000000000000000000000000000000000'><xs:element name='a' minOccurs='0'/></xs:sequence>", "", true)]
    [InlineData("<xs:sequence minOccurs='10000000000000000000000000000000000000000' maxOccurs='10000000000000000000000000000000000000000'><xs:element name='a' minOccurs='0'/></xs:sequence>", "a a a", true)]
    [InlineData("<xs:sequence minOccurs='2' maxOccurs='2'><xs:element name='a' minOccurs='0' maxOccurs='unbounded'/></xs:sequence>", "a a a a", true)]
    [InlineData("<xs:sequence minOccurs='2' maxOccurs='2'><xs:element name='a' minOccurs='0'/><xs:element name='b' minOccurs='0'/></xs:sequence>", "a b a b", true)]
    [InlineData("<xs:sequence minOccurs='2' maxOccurs='2'><xs:element name='a' minOccurs='0'/><xs:element name='b' minOccurs='0'/></xs:sequence>", "a b a b a", false)]
    [InlineData("<xs:sequence minOccurs='2' maxOccurs='2'><xs:element name='a' minOccurs='0'/><xs:element name='b' minOccurs='0'/></xs:sequence>", "b a b", true)]
    [InlineData("<xs:sequence><xs:element name='a' maxOccurs='10000000000000000000000000000000000000000'/></xs:sequence>", "a a a", true)]
    [InlineData("<xs:sequence><xs:element name='a' minOccurs='10000000000000000000000000000000000000000' maxOccurs='unbounded'/></xs:sequence>", "a a a", false)]
    public void NullableAndArbitraryPrecisionOccurrences(string particle, string children, bool expected)
        => Assert.Equal(expected, Accepts(Particle(particle), children.Length == 0 ? [] : children.Split(' ')));

    [Theory]
    [InlineData("<xs:sequence><xs:element name='a' minOccurs='0'/><xs:element name='a'/></xs:sequence>")]
    [InlineData("<xs:sequence><xs:element name='a' maxOccurs='2'/><xs:element name='a'/></xs:sequence>")]
    [InlineData("<xs:choice><xs:element name='a'/><xs:element name='a'/></xs:choice>")]
    [InlineData("<xs:choice><xs:element name='a'/><xs:any/></xs:choice>")]
    [InlineData("<xs:choice><xs:any namespace='##other'/><xs:any namespace='urn:x'/></xs:choice>")]
    [InlineData("<xs:sequence maxOccurs='2'><xs:element name='a'/><xs:element name='a' minOccurs='0'/></xs:sequence>")]
    [InlineData("<xs:sequence><xs:element name='a'/><xs:element name='a' type='xs:string'/></xs:sequence>")]
    public void UpAndDeclarationConsistencyReject(string particle)
    {
        var error = Assert.Throws<SchemaCompileException>(() => Particle(particle));
        Assert.Equal("XSD.SchemaRestriction", error.Diagnostics[0].Code);
    }
    [Fact]
    public void FixedRepetitionBoundaryHasDistinctDeclarations()
    {
        var schema = Schema("<xs:complexType name='T'><xs:sequence><xs:element name='a' minOccurs='2' maxOccurs='2'/><xs:element name='a'/></xs:sequence></xs:complexType>");
        var program = Program(schema); var op = new SchemaCompileContext().Operation; var cursor = new XsdContentMatcher().CreateCursor(program);
        var group = Assert.IsType<XsdGroupTerm>(Type(schema).Particle!.Term);
        int first = Assert.IsType<XsdElementTerm>(group.Particles[0].Term).DeclarationId, second = Assert.IsType<XsdElementTerm>(group.Particles[1].Term).DeclarationId;
        Assert.True(cursor.Accept(new("", "a"), op, out var id, out _)); Assert.Equal(first, id);
        Assert.True(cursor.Accept(new("", "a"), op, out id, out _)); Assert.Equal(first, id); Assert.False(cursor.Complete(op));
        Assert.True(cursor.Accept(new("", "a"), op, out id, out _)); Assert.Equal(second, id); Assert.True(cursor.Complete(op));
        Assert.False(cursor.Accept(new("", "a"), op, out id, out _)); Assert.Null(id); Assert.False(cursor.Complete(op));
    }
    [Theory]
    [InlineData("strict", 0)]
    [InlineData("lax", 1)]
    [InlineData("skip", 2)]
    public void WildcardPoliciesAndAbsentNamespaces(string policy, int expected)
    {
        var program = Particle($"<xs:sequence><xs:any namespace='##other' processContents='{policy}' minOccurs='0' maxOccurs='unbounded'/></xs:sequence>");
        var op = new SchemaCompileContext().Operation; var matcher = new XsdContentMatcher(); var cursor = matcher.CreateCursor(program);
        Assert.True(cursor.Accept(new("urn:foreign", "child"), op, out var id, out var actual)); Assert.Null(id); Assert.Equal((XsdProcessContents)expected, actual); Assert.True(cursor.Complete(op));
        Assert.False(matcher.CreateCursor(program).Accept(new("", "child"), op, out _, out _));
    }
    [Fact]
    public void OpenAnyTypeAndSimpleMixedEmptyPrograms()
    {
        var schema = Schema("<xs:complexType name='T' mixed='true'/>"); var op = new SchemaCompileContext().Operation;
        var any = Assert.Single(schema.Types, t => t.Name == new SchemaName(Xs, "anyType"));
        var open = new XsdContentCompiler().Compile(schema, any, op); var cursor = new XsdContentMatcher().CreateCursor(open);
        Assert.True(cursor.Complete(op)); Assert.True(cursor.Accept(new("", "unknown"), op, out var id, out var policy)); Assert.Null(id); Assert.Equal(XsdProcessContents.Lax, policy);
        Assert.True(cursor.Complete(op)); Assert.True(Accepts(Program(schema))); Assert.False(Accepts(Program(schema), "a"));
        var simple = Assert.Single(schema.Types, t => t.Name == new SchemaName(Xs, "string"));
        Assert.True(Accepts(new XsdContentCompiler().Compile(schema, simple, op)));
    }
    [Fact]
    public async Task IsolatedConcurrentCursorsAndSourceMutation()
    {
        var context = new SchemaCompileContext(); var document = context.Parse($"<xs:schema xmlns:xs='{Xs}'><xs:complexType name='T'><xs:sequence><xs:element name='a'/><xs:element name='b'/></xs:sequence></xs:complexType></xs:schema>").Document;
        var schema = new XmlSchemaCompiler().Compile(document, context.Options, context.Operation); var program = Program(schema);
        document.RootElement.SetAttribute("changed", "true");
        var matcher = new XsdContentMatcher(); var first = matcher.CreateCursor(program); var second = matcher.CreateCursor(program); var op = new SchemaCompileContext().Operation;
        Assert.True(first.Accept(new("", "a"), op, out _, out _)); Assert.False(second.Complete(op));
        Assert.False(second.Accept(new("", "bad"), op, out _, out _)); Assert.True(first.Accept(new("", "b"), op, out _, out _)); Assert.True(first.Complete(op));
        var results = await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(() => Accepts(program, "a", "b")))); Assert.All(results, Assert.True);
    }
    [Fact]
    public void ExhaustiveSmallNullableRepetitionHasIndependentLanguageOracle()
    {
        var program = Particle("<xs:sequence minOccurs='2' maxOccurs='2'><xs:element name='a' minOccurs='0'/><xs:element name='b' minOccurs='0'/></xs:sequence>");
        // Explicit language: concatenation of zero/one a then zero/one b twice.
        var language = new HashSet<string>(StringComparer.Ordinal) { "", "a", "b", "ab", "aa", "bb", "ba", "aab", "abb", "aba", "bab", "abab" };
        int cases = 0;
        for (int length = 0; length <= 5; length++) for (int bits = 0; bits < 1 << length; bits++)
        {
            var tokens = new string[length]; var word = new char[length];
            for (int i = 0; i < length; i++) { word[i] = (bits & 1 << i) == 0 ? 'a' : 'b'; tokens[i] = word[i].ToString(); }
            Assert.Equal(language.Contains(new string(word)), Accepts(program, tokens)); cases++;
        }
        Assert.Equal(63, cases);
        var repeated = Particle("<xs:sequence minOccurs='2' maxOccurs='2'><xs:element name='a' minOccurs='0' maxOccurs='unbounded'/><xs:element name='b' minOccurs='0'/></xs:sequence>");
        Assert.True(Accepts(repeated, "b", "a", "b")); Assert.False(Accepts(repeated, "b", "a", "b", "a"));
    }

}
