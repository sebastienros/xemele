using Xemele.Validation;
using Xunit;
using static Xemele.Tests.M8XsdMissingComponents.Support;
namespace Xemele.Tests.M8XsdMissingComponents;

public sealed class ReviewControls
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public void UnavailableEdcOperandCannotHideKnownConflictInAnyOrder(int order)
    {
        string[] particles = ["<xs:element ref='m'/>", "<xs:element name='m' type='xs:int'/>", "<xs:element name='m' type='xs:string'/>"];
        int[][] permutations = [[0, 1, 2], [0, 2, 1], [1, 0, 2], [1, 2, 0], [2, 0, 1], [2, 1, 0]];
        string body = "<xs:element name='m' substitutionGroup='absent' block='substitution'/><xs:complexType name='C'><xs:sequence>";
        foreach (int i in permutations[order]) body += particles[i];
        body += "</xs:sequence></xs:complexType><xs:element name='r' type='C'/>";
        Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(Wrap(body)));
        var equal = XmlSchema.Compile(Wrap(body.Replace("xs:string", "xs:int", StringComparison.Ordinal)));
        Missing(equal.Validate("<r/>"));
    }
    [Fact]
    public void SoleOptionalMissingRefDoesNotCreateAnEdcSelfDependency()
    {
        var schema = XmlSchema.Compile(Wrap("<xs:element name='m' substitutionGroup='absent' block='substitution'/><xs:element name='r'><xs:complexType><xs:sequence><xs:element ref='m' minOccurs='0'/></xs:sequence></xs:complexType></xs:element>"));
        var empty = schema.Validate("<r/>"); Assert.True(empty.IsComplete); Assert.True(empty.IsValid);
        var used = schema.Validate("<r>\n<m/></r>"); Missing(used); Assert.Equal(2, used.Diagnostics[0].Location.LineNumber);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void MissingListMemberDoesNotHideInvalidKnownUnionMember(bool nested)
    {
        const string list = "<xs:simpleType name='L'><xs:list itemType='absent'/></xs:simpleType>";
        string union = "<xs:simpleType name='U'><xs:union memberTypes='xs:anySimpleType L'/></xs:simpleType>";
        if (nested) union += "<xs:simpleType name='V'><xs:union memberTypes='xs:int U'/></xs:simpleType>";
        Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(Wrap(list + union)));
        var valid = XmlSchema.Compile(Wrap(list + union.Replace("xs:anySimpleType", "xs:string", StringComparison.Ordinal) + "<xs:element name='r' type='" + (nested ? "V" : "U") + "'/><xs:element name='good' type='xs:int'/>"));
        Assert.True(valid.Validate("<good>7</good>").IsValid); Missing(valid.Validate("<r>a</r>"));
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void RestrictionMembershipCanBeIndeterminateWithoutBecomingFalse(bool affiliation)
    {
        string heads = affiliation ? "<xs:element name='h' type='xs:int'/><xs:element name='m' type='xs:int' substitutionGroup='absent'/>" : "<xs:element name='h'/><xs:element name='m' type='absent' substitutionGroup='h'/>";
        const string types = "<xs:complexType name='B'><xs:sequence><xs:element ref='h'/></xs:sequence></xs:complexType><xs:complexType name='D'><xs:complexContent><xs:restriction base='B'><xs:sequence><xs:element ref='m'/></xs:sequence></xs:restriction></xs:complexContent></xs:complexType><xs:element name='r' type='D'/><xs:element name='good' type='xs:int'/>";
        var partial = XmlSchema.Compile(Wrap(heads + types)); Assert.True(partial.Validate("<good>7</good>").IsValid); Missing(partial.Validate("<r><m>7</m></r>"));
        string completed = affiliation ? heads + "<xs:element name='absent' type='xs:int' substitutionGroup='h'/>" : heads + "<xs:simpleType name='absent'><xs:restriction base='xs:int'/></xs:simpleType>";
        Assert.True(XmlSchema.Compile(Wrap(completed + types)).Validate("<r><m>7</m></r>").IsValid);
        Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(Wrap(heads.Replace(affiliation ? " substitutionGroup='absent'" : " substitutionGroup='h'", "", StringComparison.Ordinal) + types)));
        Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(Wrap(heads + types.Replace("<xs:element ref='m'/>", "<xs:element ref='m' minOccurs='2' maxOccurs='2'/>", StringComparison.Ordinal))));
    }
    [Theory]
    [InlineData(false, "default", "<xs:pattern value='a'/>", "b", "a")]
    [InlineData(false, "fixed", "<xs:length value='2'/>", "a", "a b")]
    [InlineData(true, "default", "<xs:pattern value='a'/>", "b", "a")]
    [InlineData(true, "fixed", "<xs:length value='2'/>", "a", "a b")]
    [InlineData(false, "default", "<xs:minLength value='2'/>", "a", "a b")]
    [InlineData(true, "fixed", "<xs:maxLength value='1'/>", "a b", "a")]
    public void IndependentlyInvalidPartialConstraintsReject(bool attribute, string kind, string facets, string invalid, string matching)
    {
        string types = "<xs:simpleType name='L'><xs:list itemType='absent'/></xs:simpleType><xs:simpleType name='R'><xs:restriction base='L'>" + facets + "</xs:restriction></xs:simpleType>";
        string Declaration(string value) => attribute ? "<xs:element name='r'><xs:complexType><xs:attribute name='a' type='R' " + kind + "='" + value + "'/></xs:complexType></xs:element>" : "<xs:element name='r' type='R' " + kind + "='" + value + "'/>";
        Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(Wrap(types + Declaration(invalid))));
        var partial = XmlSchema.Compile(Wrap(types + Declaration(matching) + "<xs:element name='good' type='xs:int'/>"));
        Assert.True(partial.Validate("<good>7</good>").IsValid); Missing(partial.Validate("<r/>"));
    }
    [Fact]
    public void FrozenGraphOnlyLanesRejectExplicitlyTypedMissingAffiliation()
    {
        const string body = "<xs:element name='m' type='xs:int' substitutionGroup='absent'/><xs:element name='good' type='xs:int'/>";
        var (schema, availability, operation) = Compile(body);
        var compiler = new XsdContentCompiler(); var facets = new XmlFacetEvaluator();
        var programs = compiler.CompileAll(schema, operation, facets, availability);
        Assert.Throws<ArgumentException>(() => compiler.CompileAll(schema, new Counted(), facets));
        Assert.Throws<ArgumentException>(() => compiler.Compile(schema, schema.Types[0], new Counted()));
        Assert.Throws<ArgumentException>(() => ((IXsdEvaluator)new XmlSchemaValidator()).Validate(schema, XmlDocument.Parse("<good>7</good>", cancellationToken: TestContext.Current.CancellationToken), new(), programs, new XsdContentMatcher(), facets, null, null));
        Assert.True(XmlSchema.Compile(Wrap(body)).Validate("<m>7</m>").IsValid);
        Assert.True(XmlSchema.Compile(Wrap(body)).Validate("<good>7</good>").IsValid);
    }
}
