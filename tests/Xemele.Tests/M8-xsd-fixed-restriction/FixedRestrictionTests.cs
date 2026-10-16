using System.Text;
using Xemele.Validation;
using Xunit;
namespace Xemele.Tests.M8XsdFixedRestriction;

public sealed class FixedRestrictionTests
{
    internal const string Xs = "http://www.w3.org/2001/XMLSchema";
    internal static string Restriction(string basis, string derived, string definitions = "") =>
        $"{definitions}<xs:complexType name='B'><xs:sequence>{basis}</xs:sequence></xs:complexType><xs:complexType name='D'><xs:complexContent><xs:restriction base='B'><xs:sequence>{derived}</xs:sequence></xs:restriction></xs:complexContent></xs:complexType><xs:complexType name='Unused'/>";
    internal static string Element(string fixedValue, string type = "xs:decimal", string context = "") =>
        $"<xs:element name='v' type='{type}' fixed='{fixedValue}' {context}/>";
    internal static XsdCompiledSchema Prepare(string body, XmlResourceResolver? resolver = null)
    {
        var context = new SchemaCompileContext(new()
        {
            Resolver = resolver
        });
        var compiled = new XmlSchemaCompiler().Compile(context.Parse($"<xs:schema xmlns:xs='{Xs}'>{body}</xs:schema>").Document, context.Options, context.Operation);
        return compiled with
        {
            Datatypes = new XmlDatatypeRegistry(compiled.SimpleTypes.Where(t => t.Name.NamespaceUri != Xs), context.Operation)
        };
    }
    internal static XsdContentProgram Complete(XsdCompiledSchema schema, ISchemaOperation? operation = null, IXmlFacetEvaluator? facets = null) =>
        new XsdContentCompiler().Compile(schema, schema.Types.Single(t => t.Name == new SchemaName("", "Unused")), operation ?? new SchemaCompileOperation(), facets ?? new XmlFacetEvaluator());

    [Theory]
    [InlineData("1", "2", false)]
    [InlineData("1", "01.0", true)]
    [InlineData("-0", "+0.000", true)]
    [InlineData("123456789012345678901234567890", "123456789012345678901234567891", false)]
    [InlineData("123456789012345678901234567890", "0123456789012345678901234567890.00", true)]
    public void DecimalWitnessesUseActualValueEquality(string basis, string derived, bool valid)
    {
        var graph = Prepare(Restriction(Element(basis), Element(derived)));
        if (valid)
        {
            Assert.NotNull(Complete(graph));
        }
        else
        {
            Assert.Contains(Assert.Throws<SchemaCompileException>(() => Complete(graph)).Diagnostics, d => d.Code == "XSD.SchemaRestriction");
        }
        // The independently composed structural lane remains compatible.
        IXsdContentCompiler frozen = new XsdContentCompiler();
        Assert.NotNull(frozen.Compile(graph, graph.Types.Single(t => t.Name.LocalName == "D"), new SchemaCompileOperation()));
    }
    [Theory]
    [InlineData("p:v", "xmlns:p='urn:one'", "p:v", "xmlns:p='urn:two'", false)]
    [InlineData("p:v", "xmlns:p='urn:one'", "q:v", "xmlns:q='urn:one'", true)]
    [InlineData("p:v", "xmlns:p='urn:one'", "q:w", "xmlns:q='urn:one'", false)]
    public void QNameUsesBothAuthoredDeclarationContexts(string basis, string baseContext, string derived, string derivedContext, bool valid)
    {
        var graph = Prepare(Restriction(Element(basis, "xs:QName", baseContext), Element(derived, "xs:QName", derivedContext)));
        if (valid)
        {
            Assert.NotNull(Complete(graph));
        }
        else
        {
            Assert.Throws<SchemaCompileException>(() => Complete(graph));
        }
    }
    [Theory]
    [InlineData("L", "1 2", "01.0 2.00", true)]
    [InlineData("L", "1 2", "1 3", false)]
    [InlineData("L", "1 2", "1", false)]
    [InlineData("U", "1", "01.0", true)]
    [InlineData("U", "1", "true", false)]
    [InlineData("UL", "1 2", "01.0 2.00", true)]
    [InlineData("UL", "1 2", "1 true", false)]
    public void UnionAndListValuesKeepTheirTypedIdentity(string type, string basis, string derived, bool valid)
    {
        const string definitions = "<xs:simpleType name='L'><xs:list itemType='xs:decimal'/></xs:simpleType><xs:simpleType name='U'><xs:union memberTypes='xs:decimal xs:boolean'/></xs:simpleType><xs:simpleType name='UL'><xs:list itemType='U'/></xs:simpleType>";
        var graph = Prepare(Restriction(Element(basis, type), Element(derived, type), definitions));
        if (valid)
        {
            Assert.NotNull(Complete(graph));
        }
        else
        {
            Assert.Throws<SchemaCompileException>(() => Complete(graph));
        }
    }
    [Theory]
    [InlineData("p:v p:w", "xmlns:p='urn:one'", "q:v q:w", "xmlns:q='urn:one'", true)]
    [InlineData("p:v p:w", "xmlns:p='urn:one'", "p:v p:w", "xmlns:p='urn:two'", false)]
    public void QNameListsPreserveEachContext(string basis, string baseContext, string derived, string derivedContext, bool valid)
    {
        const string definitions = "<xs:simpleType name='Q'><xs:list itemType='xs:QName'/></xs:simpleType>";
        var graph = Prepare(Restriction(Element(basis, "Q", baseContext), Element(derived, "Q", derivedContext), definitions));
        if (valid)
        {
            Assert.NotNull(Complete(graph));
        }
        else
        {
            Assert.Throws<SchemaCompileException>(() => Complete(graph));
        }
    }
    [Theory]
    [InlineData("C", "1", "01.0", true)]
    [InlineData("C", "1", "2", false)]
    [InlineData("M", "one", "one", true)]
    [InlineData("M", "one", "two", false)]
    public void EffectiveComplexSimpleAndMixedConstraintTypes(string type, string basis, string derived, bool valid)
    {
        const string definitions = "<xs:complexType name='C'><xs:simpleContent><xs:extension base='xs:decimal'/></xs:simpleContent></xs:complexType><xs:complexType name='M' mixed='true'/>";
        var graph = Prepare(Restriction(Element(basis, type), Element(derived, type), definitions));
        if (valid)
        {
            Assert.NotNull(Complete(graph));
        }
        else
        {
            Assert.Throws<SchemaCompileException>(() => Complete(graph));
        }
    }
    [Fact]
    public void UnequalUnselectedRelationDoesNotRejectValidOrderedMapping()
    {
        // Both base positions share a name, but only the first matches the
        // restricted position. The optional trailing position is skipped.
        var basis = Element("1") + "<xs:element name='v' type='xs:decimal' fixed='2' minOccurs='0'/>";
        Assert.NotNull(Complete(Prepare(Restriction(basis, Element("01.0")))));
        Assert.NotNull(Complete(Prepare(Restriction(basis, Element("01.0") + Element("2")))));
    }
    [Fact]
    public void UnrelatedSameNameDeclarationsAreNotCompared()
    {
        var graph = Prepare("<xs:complexType name='A'><xs:sequence>" + Element("1") + "</xs:sequence></xs:complexType><xs:complexType name='B'><xs:sequence>" + Element("2") + "</xs:sequence></xs:complexType><xs:complexType name='Unused'/>");
        Assert.NotNull(Complete(graph));
    }
    [Fact]
    public void MissingInheritedFixedConstraintStillFails()
    {
        var graph = Prepare(Restriction(Element("1"), "<xs:element name='v' type='xs:decimal'/>"));
        Assert.Throws<SchemaCompileException>(() => Complete(graph));
    }
    [Fact]
    public void NewFixedConstraintWithoutBaseFixedIsAllowed()
    {
        Assert.NotNull(Complete(Prepare(Restriction("<xs:element name='v' type='xs:decimal'/>", Element("2")))));
    }
    [Theory]
    [InlineData("01.0", true)]
    [InlineData("2", false)]
    public void UnusedRedefineGroupCarriersUseCompleteRelation(string replacement, bool valid)
    {
        var original = $"<xs:schema xmlns:xs='{Xs}'><xs:group name='g'><xs:sequence>{Element("1")}</xs:sequence></xs:group></xs:schema>";
        var graph = Prepare($"<xs:redefine schemaLocation='urn:base'><xs:group name='g'><xs:sequence>{Element(replacement)}</xs:sequence></xs:group></xs:redefine><xs:complexType name='Unused'/>", new Resolver(original));
        if (valid)
        {
            Assert.NotNull(Complete(graph));
        }
        else
        {
            Assert.Throws<SchemaCompileException>(() => Complete(graph));
        }
    }
    [Theory]
    [InlineData("01.0", true)]
    [InlineData("2", false)]
    public void UnusedRedefinedComplexTypeUsesCompleteRelation(string replacement, bool valid)
    {
        var original = $"<xs:schema xmlns:xs='{Xs}'><xs:complexType name='B'><xs:sequence>{Element("1")}</xs:sequence></xs:complexType></xs:schema>";
        var graph = Prepare($"<xs:redefine schemaLocation='urn:base'><xs:complexType name='B'><xs:complexContent><xs:restriction base='B'><xs:sequence>{Element(replacement)}</xs:sequence></xs:restriction></xs:complexContent></xs:complexType></xs:redefine><xs:complexType name='Unused'/>", new Resolver(original));
        if (valid)
        {
            Assert.NotNull(Complete(graph));
        }
        else
        {
            Assert.Throws<SchemaCompileException>(() => Complete(graph));
        }
    }
    [Fact]
    public void FacetsAreAppliedToTheAuthoredLexicalNotCanonicalText()
    {
        const string definitions = "<xs:simpleType name='P'><xs:restriction base='xs:decimal'><xs:pattern value='0[0-9]+[.]0'/></xs:restriction></xs:simpleType>";
        Assert.NotNull(Complete(Prepare(Restriction(Element("01.0", "P"), Element("001.0", "P"), definitions))));
        Assert.Throws<SchemaCompileException>(() => Complete(Prepare(Restriction(Element("01.0", "P"), Element("1", "P"), definitions))));
    }
    private sealed class Resolver(string text) : XmlResourceResolver
    {
        public override XmlResolvedResource? Resolve(XmlResourceRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new(new MemoryStream(Encoding.UTF8.GetBytes(text)), request.Uri);
        }
    }
}
