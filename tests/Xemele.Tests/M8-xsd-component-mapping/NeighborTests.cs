using System.Numerics;
using System.Text;
using Xemele.Validation;
using Xunit;
namespace Xemele.Tests.M8XsdComponentMapping;

public sealed class NeighborTests
{
    private const string Xs = "http://www.w3.org/2001/XMLSchema";
    private static readonly Uri Source = new("https://mapping.test/schema.xsd");
    internal static string Wrap(string body, string attributes = "") => $"<xs:schema xmlns:xs='{Xs}' {attributes}>\n{body}</xs:schema>";
    private static string Zero(string form) => form == "group" ? "<xs:group ref='G' minOccurs='0' maxOccurs='0'/>" : $"<xs:{form} minOccurs='0' maxOccurs='0'><xs:element name='unused'/></xs:{form}>";
    private const string Group = "<xs:group name='G'><xs:sequence><xs:element name='unused'/></xs:sequence></xs:group>";
    private static XsdCompiledSchema Graph(string xml, ISchemaOperation? op = null)
    {
        var c = new SchemaCompileContext(new()
        {
            BaseUri = Source
        });
        return new XmlSchemaCompiler().Compile(c.Parse(xml).Document, c.Options, op ?? c.Operation);
    }
    private static XsdTypeDefinition Type(XsdCompiledSchema g, string name = "T") => g.Types.Single(t => t.Name == new SchemaName("", name));
    private static void EmptySequence(XsdParticle? p)
    {
        Assert.NotNull(p);
        Assert.Equal(BigInteger.One, p.MinOccurs);
        Assert.Equal(BigInteger.One, p.MaxOccurs);
        var group = Assert.IsType<XsdGroupTerm>(p.Term);
        Assert.Equal(XsdCompositor.Sequence, group.Compositor);
        Assert.Empty(group.Particles);
    }
    [Theory]
    [InlineData("sequence", false)]
    [InlineData("sequence", true)]
    [InlineData("choice", false)]
    [InlineData("choice", true)]
    [InlineData("group", false)]
    [InlineData("group", true)]
    public void ValidatedZeroOccurrenceMapsAndValidatesAccordingToMixed(string form, bool mixed)
    {
        var xml = Wrap(Group + $"<xs:complexType name='T' mixed='{mixed.ToString().ToLowerInvariant()}'>{Zero(form)}</xs:complexType><xs:element name='r' type='T'/>");
        var t = Type(Graph(xml));
        Assert.Equal(mixed ? XsdContentKind.Mixed : XsdContentKind.Empty, t.ContentKind);
        if (mixed)
        {
            EmptySequence(t.Particle);
        }
        else
        {
            Assert.Null(t.Particle);
        }

        var facade = XmlSchema.Compile(xml);
        Assert.True(facade.Validate("<r/>").IsValid);
        var text = facade.Validate("<r>characters</r>");
        Assert.True(text.IsComplete);
        Assert.Equal(mixed, text.IsValid);
        var child = facade.Validate("<r><unused/></r>");
        Assert.True(child.IsComplete);
        Assert.False(child.IsValid);
    }
    [Theory]
    [InlineData("sequence", true, false)]
    [InlineData("sequence", false, true)]
    [InlineData("choice", true, false)]
    [InlineData("choice", false, true)]
    [InlineData("group", true, false)]
    [InlineData("group", false, true)]
    public void ZeroOccurrenceUsesInnerEffectiveMixedForRestriction(string form, bool outer, bool inner)
    {
        var t = Type(Graph(Wrap(Group + $"<xs:complexType name='T' mixed='{outer.ToString().ToLowerInvariant()}'><xs:complexContent mixed='{inner.ToString().ToLowerInvariant()}'><xs:restriction base='xs:anyType'>{Zero(form)}</xs:restriction></xs:complexContent></xs:complexType>")));
        Assert.Equal(inner ? XsdContentKind.Mixed : XsdContentKind.Empty, t.ContentKind);
        if (inner)
        {
            EmptySequence(t.Particle);
        }
        else
        {
            Assert.Null(t.Particle);
        }
    }
    [Theory]
    [InlineData("sequence", false)]
    [InlineData("sequence", true)]
    [InlineData("choice", false)]
    [InlineData("choice", true)]
    [InlineData("group", false)]
    [InlineData("group", true)]
    public void EmptyExtensionInheritsWholeBaseAndMixedExtensionRetainsBase(string form, bool mixed)
    {
        var g = Graph(Wrap(Group + $"<xs:complexType name='B' mixed='true'><xs:sequence><xs:element name='a' minOccurs='0'/></xs:sequence></xs:complexType><xs:complexType name='T'><xs:complexContent mixed='{mixed.ToString().ToLowerInvariant()}'><xs:extension base='B'>{Zero(form)}</xs:extension></xs:complexContent></xs:complexType>"));
        var b = Type(g, "B");
        var t = Type(g);
        Assert.Equal(XsdContentKind.Mixed, t.ContentKind);
        if (!mixed)
        {
            Assert.Same(b.Particle, t.Particle);
        }
        else
        {
            var group = Assert.IsType<XsdGroupTerm>(t.Particle!.Term);
            Assert.Same(b.Particle, group.Particles[0]);
            EmptySequence(group.Particles[1]);
        }
    }
    [Theory]
    [InlineData("<xs:sequence maxOccurs='0'/>")]
    [InlineData("<xs:choice minOccurs='1' maxOccurs='0'/>")]
    [InlineData("<xs:group ref='G' minOccurs='2' maxOccurs='0'/>")]
    [InlineData("<xs:all minOccurs='0' maxOccurs='0'/>")]
    [InlineData("<xs:sequence minOccurs='0' maxOccurs='0'/><xs:choice/>")]
    [InlineData("<xs:sequence minOccurs='0' maxOccurs='0'><xs:all/></xs:sequence>")]
    public void MixedZeroMappingDoesNotHideInvalidAuthoredParticles(string particle)
    {
        var e = Assert.Throws<SchemaCompileException>(() => Graph(Wrap(Group + $"<xs:complexType name='T' mixed='true'>{particle}</xs:complexType>")));
        Assert.Contains(e.Diagnostics, d => d.Code == "XSD.SchemaRestriction");
    }
    [Theory]
    [InlineData("sequence", false)]
    [InlineData("sequence", true)]
    [InlineData("choice", false)]
    [InlineData("choice", true)]
    [InlineData("group", false)]
    [InlineData("group", true)]
    public void SynthesizedZeroParticlePreservesScalarAndByteLocations(string form, bool bytes)
    {
        var xml = Wrap(Group + "\n<xs:complexType name='T' mixed='true'>\n " + Zero(form) + "\n</xs:complexType>");
        var c = new SchemaCompileContext(new()
        {
            BaseUri = Source
        });
        SchemaDocumentScope s;
        if (bytes)
        {
            using var input = new MemoryStream(Encoding.UTF8.GetBytes(xml));
            s = c.Parse(input);
        }
        else
        {
            s = c.Parse(xml);
        }

        var at = Type(new XmlSchemaCompiler().Compile(s.Document, c.Options, c.Operation)).Particle!.Location;
        Assert.Equal(Source, at.SourceUri);
        Assert.Equal(4, at.LineNumber);
        Assert.Equal(2, at.LinePosition);
        Assert.Equal(bytes, at.ByteOffset.HasValue);
        if (bytes)
        {
            Assert.Equal(Encoding.UTF8.GetByteCount(xml.AsSpan(0, xml.IndexOf(Zero(form), StringComparison.Ordinal))), at.ByteOffset);
        }
    }
    [Theory]
    [InlineData("sequence")]
    [InlineData("choice")]
    [InlineData("group")]
    public void ZeroSynthesisHasExactWorkComponentsAndCancellation(string form)
    {
        var xml = Wrap(Group + "\n<xs:complexType name='T' mixed='true'>\n " + Zero(form) + "\n</xs:complexType>");
        var normal = new Ledger();
        Graph(xml, normal);
        Assert.True(normal.MappingComponents >= 4);
        Graph(xml, new Ledger(normal.Work, normal.Components));
        var shortWork = new Ledger(normal.Work - 1);
        Assert.Throws<SchemaCompileException>(() => Graph(xml, shortWork));
        Assert.False(shortWork.IsComplete);
        var shortComponents = new Ledger(componentLimit: normal.Components - 1);
        Assert.Throws<SchemaCompileException>(() => Graph(xml, shortComponents));
        Assert.False(shortComponents.IsComplete);
        using var c = new CancellationTokenSource();
        var cancel = new Ledger(token: c.Token) { OnComponent = (n, at) => { if (at.LineNumber == 4 && n == normal.MappingComponents - 1) { c.Cancel(); } } };
        Assert.Throws<OperationCanceledException>(() => Graph(xml, cancel));
    }
    [Theory]
    [InlineData("A")]
    [InlineData("_name-1.2")]
    [InlineData("名_1")]
    [InlineData("é")]
    [InlineData("A\u0301")]
    [InlineData("\u0134")]
    public void LegalSecondEditionNamesPublish(string name)
    {
        var schema = XmlSchema.Compile(Wrap($"<xs:element name='{name}' type='xs:string'/>"));
        Assert.True(schema.Validate($"<{name}>text</{name}>").IsValid);
    }
    [Theory]
    [InlineData("element")]
    [InlineData("attribute")]
    [InlineData("simpleType")]
    [InlineData("complexType")]
    [InlineData("group")]
    [InlineData("attributeGroup")]
    [InlineData("notation")]
    public void AllComponentNameKindsUseSecondEdition(string kind)
    {
        var body = kind switch
        {
            "simpleType" => "<xs:restriction base='xs:string'/>",
            "group" => "<xs:sequence/>",
            _ => ""
        };
        var attributes = kind == "notation" ? " public='pub'" : "";
        var error = Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(Wrap($"<xs:{kind} name='D&#x133;kstra'{attributes}>{body}</xs:{kind}>"), new() { BaseUri = Source }));
        Assert.Equal("Invalid NCName.", Assert.Single(error.Diagnostics).Message);
        Assert.Equal(2, error.Diagnostics[0].Location.LineNumber);
        Assert.Equal(Source, error.Diagnostics[0].Location.SourceUri);
    }
    [Theory]
    [InlineData("schema")]
    [InlineData("annotation")]
    [InlineData("element")]
    public void SchemaIdsUseSecondEdition(string place)
    {
        var body = place == "annotation" ? "<xs:annotation id='D&#x133;kstra'/>" : "<xs:element name='r'" + (place == "element" ? " id='D&#x133;kstra'" : "") + "/>";
        var error = Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(Wrap(body, place == "schema" ? "id='D&#x133;kstra'" : "")));
        Assert.Equal("Invalid NCName.", Assert.Single(error.Diagnostics).Message);
    }
    [Theory]
    [InlineData("xs:D&#x133;kstra", "")]
    [InlineData("p&#x133;:string", "xmlns:pĳ='http://www.w3.org/2001/XMLSchema'")]
    [InlineData("xs:D&#x15000;", "")]
    [InlineData("p&#x15000;:string", "xmlns:p𕀀='http://www.w3.org/2001/XMLSchema'")]
    [InlineData("xs:", "")]
    [InlineData(":string", "")]
    [InlineData("xs:a:b", "")]
    public void QNamePrefixAndLocalUseSecondEdition(string type, string namespaces)
    {
        var error = Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(Wrap($"<xs:element name='r' type='{type}'/>", namespaces)));
        Assert.Equal("Invalid NCName.", Assert.Single(error.Diagnostics).Message);
    }
    [Fact]
    public void LegalMultilingualDeclarationIdAndQNameComponentsPublish()
    {
        var schema = XmlSchema.Compile(Wrap("<xs:simpleType name='名' id='名1'><xs:restriction base='名:string'/></xs:simpleType><xs:element name='r' type='名'/>", "xmlns:名='http://www.w3.org/2001/XMLSchema'"));
        Assert.True(schema.Validate("<r>text</r>").IsValid);
    }
    [Theory]
    [InlineData("ĳ")]
    [InlineData("Dĳkstra")]
    [InlineData("𕀀")]
    [InlineData("名ĳ")]
    [InlineData("𰀀")]
    public void ParserAndTreeKeepFifthEditionNameGrammar(string name)
    {
        var document = XmlDocument.Parse($"<{name}/>", cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(name, document.RootElement.LocalName);
        Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(Wrap($"<xs:element name='{name}'/>")));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LongNamesScanAndRejectAtTheirOriginalLocation(bool invalid)
    {
        var name = new string('名', 4096) + (invalid ? "ĳ" : "1");
        var xml = Wrap($"<xs:element name='{name}'/>");
        if (!invalid)
        {
            Assert.NotNull(XmlSchema.Compile(xml));
            return;
        }
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        var e = Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(input, new() { BaseUri = Source }));
        var at = Assert.Single(e.Diagnostics).Location;
        Assert.Equal(Source, at.SourceUri);
        Assert.Equal(2, at.LineNumber);
        Assert.Equal(1, at.LinePosition);
        Assert.Equal(Encoding.UTF8.GetByteCount(xml.AsSpan(0, xml.IndexOf("<xs:element", StringComparison.Ordinal))), at.ByteOffset);
    }
    [Theory]
    [InlineData("ascii", "A")]
    [InlineData("multilingual", "名_1")]
    [InlineData("edition-gap", "xĳ")]
    [InlineData("empty", "")]
    [InlineData("digit-first", "1a")]
    [InlineData("colon", "a:b")]
    [InlineData("unpaired-high", "\ud800")]
    [InlineData("unpaired-low", "\udc00")]
    [InlineData("bad-pair", "\ud800A")]
    [InlineData("supplementary", "𕀀")]
    public void BorrowedScanMatchesIndependentBoundaryOracle(string caseId, string name)
    {
        Assert.NotEmpty(caseId);
        var expected = name is "A" or "名_1";
        Assert.Equal(expected, XsdCompilation.ComponentNCName(name, new SchemaCompileOperation()));
    }
    [Fact]
    public void NameScanChargesExactSharedBudgetAndCancelsInsideLongName()
    {
        var xml = Wrap("<xs:element name='" + new string('名', 2048) + "'/>");
        var normal = new Ledger();
        Graph(xml, normal);
        Assert.True(normal.NameWork > 2048);
        Graph(xml, new Ledger(normal.Work, normal.Components));
        var shortWork = new Ledger(normal.Work - 1);
        Assert.Throws<SchemaCompileException>(() => Graph(xml, shortWork));
        Assert.False(shortWork.IsComplete);
        // Stop inside the borrowed table lookup: refusal retains the authored location.
        var stop = new Ledger(nameWorkLimit: 1024);
        Assert.Throws<SchemaCompileException>(() => Graph(xml, stop));
        Assert.False(stop.IsComplete);
        var located = new SchemaCompileOperation(new()
        {
            MaxOperations = stop.Work
        });
        var e = Assert.Throws<SchemaCompileException>(() => Graph(xml, located));
        Assert.False(located.IsComplete);
        var at = e.Diagnostics[^1].Location;
        Assert.Equal(Source, at.SourceUri);
        Assert.Equal(2, at.LineNumber);
        using var c = new CancellationTokenSource();
        var cancel = new Ledger(token: c.Token) { OnWork = (n, at) => { if (at.LineNumber == 2 && n == 1024) { c.Cancel(); } } };
        Assert.Throws<OperationCanceledException>(() => Graph(xml, cancel));
    }
    private sealed class Ledger(long? workLimit = null, int? componentLimit = null, CancellationToken token = default, long? nameWorkLimit = null) : ISchemaOperation
    {
        private readonly SchemaCompileOperation inner = new(new() { MaxOperations = workLimit, MaxComponents = componentLimit, CancellationToken = token });
        internal long Work, NameWork; internal int Components, MappingComponents;
        internal Action<long, XmlLocation>? OnWork; internal Action<int, XmlLocation>? OnComponent;
        public XmlResourceLimits Limits => inner.Limits; public CancellationToken CancellationToken => inner.CancellationToken;
        public bool IsComplete => inner.IsComplete; public long? RemainingWork => inner.RemainingWork;
        public bool Charge(long n, XmlLocation at)
        {
            if (at.LineNumber == 2)
            {
                NameWork += n;
                OnWork?.Invoke(NameWork, at);
                if (nameWorkLimit is { } max && NameWork > max)
                {
                    inner.Refuse("SCHEMA.Limit", "Name work cap.", at);
                    return false;
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
            if (at.LineNumber == 4)
            {
                MappingComponents++;
                OnComponent?.Invoke(MappingComponents, at);
            }
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
