using System.Numerics;
using System.Text;
using Xemele.Validation;
using Xunit;
namespace Xemele.Tests.M8XsdContent;

public sealed class ReviewRegressionTests
{
    [Theory]
    [InlineData("", "", "false", "true", "restriction", false)]
    [InlineData("<xs:sequence><xs:element name='a' minOccurs='0'/></xs:sequence>", "", "false", "true", "restriction", false)]
    [InlineData("<xs:sequence/>", "<xs:sequence/>", "false", "true", "restriction", false)]
    [InlineData("", "", "true", "true", "extension", true)]
    [InlineData("<xs:sequence/>", "<xs:sequence/>", "true", "true", "extension", true)]
    [InlineData("", "", "true", "false", "restriction", true)]
    [InlineData("<xs:sequence/>", "<xs:sequence/>", "true", "false", "restriction", true)]
    public void KindTransitionsAreCheckedBeforeParticleNormalization(string basis, string content, string baseMixed, string mixed, string method, bool valid)
    {
        var graph = ContentTests.Schema($"<xs:complexType name='B' mixed='{baseMixed}'>{basis}</xs:complexType><xs:complexType name='T'><xs:complexContent mixed='{mixed}'><xs:{method} base='B'>{content}</xs:{method}></xs:complexContent></xs:complexType>");
        if (valid) Assert.NotNull(ContentTests.Program(graph)); else Assert.Throws<SchemaCompileException>(() => ContentTests.Program(graph));
    }
    [Theory]
    [InlineData("", "")]
    [InlineData("<xs:sequence/>", "<xs:sequence/>")]
    public void EffectiveEmptyExtensionInheritsMixedBaseContent(string basis, string content)
    {
        var graph = ContentTests.Schema($"<xs:complexType name='B' mixed='true'>{basis}</xs:complexType><xs:complexType name='T'><xs:complexContent mixed='false'><xs:extension base='B'>{content}</xs:extension></xs:complexContent></xs:complexType>");
        // Structures 2E §3.4.2, content type 3.2.1: effective-empty
        // extension inherits the base content type, even with mixed=false.
        Assert.Equal(XsdContentKind.Mixed, ContentTests.Type(graph).ContentKind);
        Assert.True(ContentTests.Accepts(ContentTests.Program(graph)));
    }
    [Theory]
    [InlineData(2, false)]
    [InlineData(0, true)]
    public void GroupWildcardRestrictionPreservesEachChildRange(int minimum, bool valid)
    {
        var graph = ContentTests.Schema($"<xs:complexType name='B'><xs:sequence><xs:any minOccurs='{minimum}' maxOccurs='2'/></xs:sequence></xs:complexType><xs:complexType name='T'><xs:complexContent><xs:restriction base='B'><xs:sequence><xs:element name='a'/><xs:element name='b'/></xs:sequence></xs:restriction></xs:complexContent></xs:complexType>");
        if (valid) Assert.NotNull(ContentTests.Program(graph)); else Assert.Throws<SchemaCompileException>(() => ContentTests.Program(graph));
    }
    [Theory]
    [InlineData("##local", false)]
    [InlineData("##local ##targetNamespace", true)]
    [InlineData("##any", true)]
    public void IntensionalAttributeWildcardUnionMustBeExpressible(string extension, bool valid)
    {
        var context = new SchemaCompileContext(); var document = context.Parse($"<xs:schema xmlns:xs='{ContentTests.Xs}' xmlns:t='urn:t' targetNamespace='urn:t'><xs:complexType name='B'><xs:anyAttribute namespace='##other'/></xs:complexType><xs:complexType name='T'><xs:complexContent><xs:extension base='t:B'><xs:anyAttribute namespace='{extension}'/></xs:extension></xs:complexContent></xs:complexType></xs:schema>").Document;
        void Compile()
        {
            var graph = new XmlSchemaCompiler().Compile(document, context.Options, context.Operation); var type = graph.Types.Single(t => t.Name == new SchemaName("urn:t", "T"));
            Assert.NotNull(new XsdContentCompiler().Compile(graph, type, new OperationTests.Ledger()));
        }
        if (valid) Compile(); else Assert.Throws<SchemaCompileException>(Compile);
    }
    [Fact]
    public void IntensionalNegatedSubsetDoesNotUseOrdinarySetContainment()
    {
        var op = new OperationTests.Ledger(); var notT = new XsdWildcard(true, ["urn:t", ""], XsdProcessContents.Strict); var notAbsent = new XsdWildcard(true, [""], XsdProcessContents.Strict);
        Assert.True(ContentNamespaces.SetSubset(notT, notAbsent, op, default)); Assert.False(ContentNamespaces.Subset(notT, notAbsent, op, default));
        Assert.True(ContentNamespaces.Subset(notT, notT, op, default)); Assert.True(ContentNamespaces.Subset(notT, new(true, [], XsdProcessContents.Strict), op, default));
    }
    [Fact]
    public void CounterOverflowReservesExactGrowthBeforePersistence()
    {
        var value = new ContentCounter(); value.Set(ulong.MaxValue);
        var refused = new OperationTests.Ledger(componentLimit: 0);
        Assert.Throws<SchemaCompileException>(() => value.Increment(refused, default)); Assert.False(value.AtLeast(BigInteger.One << 64, new OperationTests.Ledger(), default));
        var measured = new OperationTests.Ledger(); value.Increment(measured, default); Assert.True(measured.Components > 0);
        Assert.True(value.AtLeast(BigInteger.One << 64, new OperationTests.Ledger(), default)); Assert.False(value.AtLeast((BigInteger.One << 64) + 1, new OperationTests.Ledger(), default));
        var exact = new ContentCounter(); exact.Set(ulong.MaxValue); exact.Increment(new OperationTests.Ledger(measured.Work, measured.Components), default);
        var shortLimit = new ContentCounter(); shortLimit.Set(ulong.MaxValue); Assert.Throws<SchemaCompileException>(() => shortLimit.Increment(new OperationTests.Ledger(componentLimit: measured.Components - 1), default));
        Assert.False(shortLimit.AtLeast(BigInteger.One << 64, new OperationTests.Ledger(), default));
    }
    [Fact]
    public void ExpandedCounterArithmeticAndComparisonAreChargedAndPolled()
    {
        BigInteger seed = BigInteger.One << 4096; var counter = new ContentCounter(); counter.Set(seed);
        Assert.Throws<SchemaCompileException>(() => counter.Increment(new OperationTests.Ledger(componentLimit: 0), default));
        var measured = new OperationTests.Ledger(); counter.Increment(measured, default); Assert.True(measured.Components > 0);
        var exact = new ContentCounter(); exact.Set(seed); exact.Increment(new OperationTests.Ledger(measured.Work, measured.Components), default);
        Assert.True(exact.AtLeast(seed + 1, new OperationTests.Ledger(), default)); Assert.False(exact.AtLeast(seed + 2, new OperationTests.Ledger(), default));
        using var source = new CancellationTokenSource(); var cancel = new OperationTests.Ledger(token: source.Token) { OnCharge = n => { if (n == 20) source.Cancel(); } };
        var interrupted = new ContentCounter(); interrupted.Set(seed); Assert.Throws<OperationCanceledException>(() => interrupted.Increment(cancel, default)); Assert.Equal(20, cancel.Calls);
        Assert.False(interrupted.AtLeast(seed + 1, new OperationTests.Ledger(), default));
        using var compareSource = new CancellationTokenSource(); var compare = new OperationTests.Ledger(token: compareSource.Token) { OnCharge = n => { if (n == 20) compareSource.Cancel(); } };
        Assert.Throws<OperationCanceledException>(() => exact.Compare(exact, compare, default)); Assert.Equal(20, compare.Calls);
    }
    [Fact]
    public void NonExpressibleIntermediateMustBeRejectedBeforeCompilerDiscardsIt()
    {
        const string imported = "<xs:schema xmlns:xs='http://www.w3.org/2001/XMLSchema' targetNamespace='urn:u'><xs:attributeGroup name='foreign'><xs:anyAttribute namespace='##other'/></xs:attributeGroup></xs:schema>";
        const string principal = "<xs:schema xmlns:xs='http://www.w3.org/2001/XMLSchema' xmlns:t='urn:t' xmlns:u='urn:u' targetNamespace='urn:t'><xs:import namespace='urn:u' schemaLocation='urn:import'/><xs:attributeGroup name='local'><xs:anyAttribute namespace='##other'/></xs:attributeGroup><xs:complexType name='T'><xs:attributeGroup ref='t:local'/><xs:attributeGroup ref='u:foreign'/><xs:anyAttribute namespace='urn:v'/></xs:complexType></xs:schema>";
        Assert.Throws<SchemaCompileException>(() =>
        {
            var context = new SchemaCompileContext(new() { Resolver = new Resolver(imported) }); var document = context.Parse(principal).Document;
            var graph = new XmlSchemaCompiler().Compile(document, context.Options, context.Operation); var type = graph.Types.Single(t => t.Name == new SchemaName("urn:t", "T"));
            // On the current owning compiler, the illegal intermediate
            // complement {urn:t,urn:u,absent} is discarded by a final intersection.
            Assert.False(type.AnyAttribute!.Negated); Assert.Equal("urn:v", Assert.Single(type.AnyAttribute.Namespaces));
            new XsdContentCompiler().Compile(graph, type, new OperationTests.Ledger());
        });
    }
    private sealed class Resolver(string text) : XmlResourceResolver
    { public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken token) { token.ThrowIfCancellationRequested(); return new(new MemoryStream(Encoding.UTF8.GetBytes(text)), request.Uri); } }
}
