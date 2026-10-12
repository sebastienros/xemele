using System.Numerics;
using System.Text;
using Xemele.Validation;
using Xunit;
namespace Xemele.Tests.M8XsdEffectiveContent;

public sealed class MappingTests
{
    private const string Xs = "http://www.w3.org/2001/XMLSchema";
    private static readonly Uri Source = new("https://schema.test/effective.xsd");
    private static string Wrap(string body) => $"<xs:schema xmlns:xs='{Xs}'>\n{body}</xs:schema>";
    private static XsdCompiledSchema Compile(string body, SchemaCompileOptions? options = null, ISchemaOperation? operation = null)
    {
        var context = new SchemaCompileContext(options); return new XmlSchemaCompiler().Compile(context.Parse(Wrap(body)).Document, context.Options, operation ?? context.Operation);
    }
    private static XsdTypeDefinition Type(XsdCompiledSchema graph, string name = "T") => graph.Types.Single(t => t.Name == new SchemaName("", name));

    [Theory]
    [InlineData("")]
    [InlineData("<xs:sequence/>")]
    [InlineData("<xs:sequence><xs:annotation><xs:documentation>ignored</xs:documentation></xs:annotation></xs:sequence>")]
    [InlineData("<xs:all/>")]
    [InlineData("<xs:choice minOccurs='0'/>")]
    public void EmptyEffectiveExtensionInheritsEntireBaseDespiteExplicitMixedFalse(string particle)
    {
        foreach (string basis in new[] { "", "<xs:sequence><xs:element name='a' minOccurs='0'/></xs:sequence>" })
        {
            var graph = Compile($"<xs:complexType name='B' mixed='true'>{basis}</xs:complexType><xs:complexType name='T'><xs:complexContent mixed='false'><xs:extension base='B'>{particle}</xs:extension></xs:complexContent></xs:complexType>");
            var b = Type(graph, "B"); var t = Type(graph);
            Assert.Equal(XsdContentKind.Mixed, t.ContentKind); Assert.Same(b.Particle, t.Particle);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("<xs:sequence/>")]
    [InlineData("<xs:all minOccurs='0'/>")]
    [InlineData("<xs:choice minOccurs='0' maxOccurs='5'/>")]
    public void EmptySyntaxMapsToEmptyOrOneEmptySequenceAccordingToMixed(string particle)
    {
        var empty = Type(Compile($"<xs:complexType name='T'>{particle}</xs:complexType>"));
        Assert.Equal(XsdContentKind.Empty, empty.ContentKind); Assert.Null(empty.Particle);
        var mixed = Type(Compile($"<xs:complexType name='T' mixed='true'>{particle}</xs:complexType>"));
        Assert.Equal(XsdContentKind.Mixed, mixed.ContentKind); Assert.NotNull(mixed.Particle);
        Assert.Equal(BigInteger.One, mixed.Particle.MinOccurs); Assert.Equal(BigInteger.One, mixed.Particle.MaxOccurs);
        var group = Assert.IsType<XsdGroupTerm>(mixed.Particle.Term); Assert.Equal(XsdCompositor.Sequence, group.Compositor); Assert.Empty(group.Particles);
    }

    [Theory]
    [InlineData("<xs:choice/>")]
    [InlineData("<xs:sequence minOccurs='0'><xs:element name='a'/></xs:sequence>")]
    [InlineData("<xs:choice minOccurs='0'><xs:element name='a'/></xs:choice>")]
    [InlineData("<xs:group ref='g'/>")]
    public void EmptyChoiceRequiredNullableNonemptyAndEmptyGroupReferenceRemainParticles(string particle)
    {
        var graph = Compile($"<xs:group name='g'><xs:sequence/></xs:group><xs:complexType name='T'>{particle}</xs:complexType>");
        var type = Type(graph); Assert.Equal(XsdContentKind.ElementOnly, type.ContentKind); Assert.NotNull(type.Particle);
    }

    [Theory]
    [InlineData("<xs:sequence minOccurs='0' maxOccurs='0'><xs:element name='a'/></xs:sequence>")]
    [InlineData("<xs:choice minOccurs='0' maxOccurs='0'><xs:element name='a'/></xs:choice>")]
    [InlineData("<xs:group ref='g' minOccurs='0' maxOccurs='0'/>")]
    public void ValidZeroOccurrenceAddsNoEffectiveParticle(string particle)
    {
        string prefix = "<xs:group name='g'><xs:sequence/></xs:group>";
        var restriction = Type(Compile(prefix + $"<xs:complexType name='T' mixed='true'>{particle}</xs:complexType>"));
        Assert.Equal(XsdContentKind.Empty, restriction.ContentKind); Assert.Null(restriction.Particle);
        var graph = Compile(prefix + $"<xs:complexType name='B' mixed='true'/><xs:complexType name='T'><xs:complexContent mixed='false'><xs:extension base='B'>{particle}</xs:extension></xs:complexContent></xs:complexType>");
        Assert.Equal(XsdContentKind.Mixed, Type(graph).ContentKind); Assert.Same(Type(graph, "B").Particle, Type(graph).Particle);
    }

    [Theory]
    [InlineData("mixed='true'", "mixed='false'", false)]
    [InlineData("mixed='false'", "mixed='true'", true)]
    [InlineData("mixed='true'", "", true)]
    [InlineData("", "", false)]
    public void EffectiveMixedPrecedenceIsPreservedForRestriction(string outer, string inner, bool mixed)
    {
        var type = Type(Compile($"<xs:complexType name='T' {outer}><xs:complexContent {inner}><xs:restriction base='xs:anyType'/></xs:complexContent></xs:complexType>"));
        Assert.Equal(mixed ? XsdContentKind.Mixed : XsdContentKind.Empty, type.ContentKind);
    }

    [Theory]
    [InlineData("<xs:sequence maxOccurs='0'/>")]
    [InlineData("<xs:all minOccurs='0' maxOccurs='0'/>")]
    [InlineData("<xs:sequence minOccurs='2' maxOccurs='1'/>")]
    [InlineData("<xs:sequence minOccurs='0' maxOccurs='0'/><xs:sequence/>")]
    [InlineData("<xs:all><xs:sequence/></xs:all>")]
    public void EmptyMappingDoesNotHideInvalidGrammarOccurrencesOrDuplicateBodies(string body)
    {
        var error = Assert.Throws<SchemaCompileException>(() => Compile($"<xs:complexType name='T'>{body}</xs:complexType>"));
        Assert.Equal("XSD.SchemaRestriction", Assert.Single(error.Diagnostics).Code);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SynthesizedParticleCopiesActualCharacterOrByteSourceLocation(bool bytes)
    {
        string xml = Wrap("<xs:complexType name='T' mixed='true'>\n <xs:all/>\n</xs:complexType>");
        var context = new SchemaCompileContext(new() { BaseUri = Source }); SchemaDocumentScope scope;
        if (bytes) { using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml)); scope = context.Parse(stream); } else scope = context.Parse(xml);
        var t = Type(new XmlSchemaCompiler().Compile(scope.Document, context.Options, context.Operation)); var at = t.Particle!.Location;
        Assert.Equal(Source, at.SourceUri); Assert.Equal(3, at.LineNumber); Assert.Equal(2, at.LinePosition); Assert.Equal(bytes, at.ByteOffset.HasValue);
        if (bytes) Assert.Equal(Encoding.UTF8.GetByteCount(xml.AsSpan(0, xml.IndexOf("<xs:all", StringComparison.Ordinal))), at.ByteOffset);
    }

    [Fact]
    public void NewSyntheticGroupReservesComponentsAndPollsAtItsSource()
    {
        const string body = "<xs:complexType name='T' mixed='true'>\n <xs:all/>\n</xs:complexType>";
        var context = new SchemaCompileContext(new() { BaseUri = Source }); var document = context.Parse(Wrap(body)).Document;
        var normal = new Ledger(); new XmlSchemaCompiler().Compile(document, context.Options, normal);
        Assert.True(normal.MappingCharges >= 4);
        var exact = new Ledger(normal.Work, normal.Components); new XmlSchemaCompiler().Compile(document, context.Options, exact);
        var shortComponents = new Ledger(componentLimit: normal.Components - 1); Assert.Throws<SchemaCompileException>(() => new XmlSchemaCompiler().Compile(document, context.Options, shortComponents));
        var shortWork = new Ledger(workLimit: normal.Work - 1); Assert.Throws<SchemaCompileException>(() => new XmlSchemaCompiler().Compile(document, context.Options, shortWork));
        using var cancel = new CancellationTokenSource(); var interrupted = new Ledger(token: cancel.Token) { OnMappingComponent = n => { if (n == 3) cancel.Cancel(); } };
        Assert.Throws<OperationCanceledException>(() => new XmlSchemaCompiler().Compile(document, context.Options, interrupted)); Assert.Equal(3, interrupted.MappingCharges);
    }

    private sealed class Ledger(long? workLimit = null, int? componentLimit = null, CancellationToken token = default) : ISchemaOperation
    {
        private readonly SchemaCompileOperation inner = new(new() { MaxOperations = workLimit, MaxComponents = componentLimit, CancellationToken = token });
        internal long Work; internal int Components, MappingCharges; internal Action<int>? OnMappingComponent;
        public XmlResourceLimits Limits => inner.Limits; public CancellationToken CancellationToken => inner.CancellationToken;
        public bool IsComplete => inner.IsComplete; public long? RemainingWork => inner.RemainingWork;
        public bool Charge(long n, XmlLocation at) { bool ok = inner.Charge(n, at); if (ok) Work += n; return ok; }
        public bool ChargeComponents(int n, XmlLocation at) { if (at.LineNumber == 3) { MappingCharges++; OnMappingComponent?.Invoke(MappingCharges); } bool ok = inner.ChargeComponents(n, at); if (ok) Components += n; return ok; }
        public bool AddDiagnostic(XmlDiagnostic d) => inner.AddDiagnostic(d); public void Refuse(string c, string m, XmlLocation at) => inner.Refuse(c, m, at);
    }
}
