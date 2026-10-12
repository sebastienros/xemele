using Xemele.Validation;
using Xunit;
using static Xemele.Tests.M8XsdMissingComponents.Support;
namespace Xemele.Tests.M8XsdMissingComponents;

public sealed class ReviewControls
{
    [Fact]
    public void DiscardedRestrictionCandidatesCannotPublishMissingDependencies()
    {
        const string basis = "<xs:complexType name='B'><xs:sequence><xs:element name='x' type='absent' minOccurs='0'/><xs:element name='s' type='xs:int'/><xs:any processContents='lax' minOccurs='1' maxOccurs='1'/></xs:sequence></xs:complexType>";
        const string derived = "<xs:complexType name='D'><xs:complexContent><xs:restriction base='B'><xs:sequence><xs:element name='s' type='xs:int'/><xs:element name='x' type='xs:int'/></xs:sequence></xs:restriction></xs:complexContent></xs:complexType><xs:element name='r' type='D'/>";
        foreach (string definition in new[] { "", "<xs:simpleType name='absent'><xs:restriction base='xs:string'/></xs:simpleType>" })
        {
            var schema = XmlSchema.Compile(Wrap(definition + basis + derived));
            var result = schema.Validate("<r><s>7</s><x>8</x></r>");
            Assert.True(result.IsComplete); Assert.True(result.IsValid); Assert.Empty(result.Diagnostics);
            Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(Wrap(definition + basis + derived.Replace("name='x' type='xs:int'", "name='x' type='xs:int' minOccurs='2' maxOccurs='2'", StringComparison.Ordinal))));
        }
    }
    [Fact]
    public void ExpandedSubstitutionLeavesCannotRematchThroughBlockedAffiliation()
    {
        const string heads = "<xs:element name='h' type='xs:int' block='restriction'/><xs:element name='m' type='xs:int' substitutionGroup='h'/><xs:element name='s' type='xs:short' substitutionGroup='m'/>";
        const string types = "<xs:complexType name='B'><xs:sequence><xs:element ref='h'/></xs:sequence></xs:complexType><xs:complexType name='D'><xs:complexContent><xs:restriction base='B'><xs:sequence><xs:element ref='m'/></xs:sequence></xs:restriction></xs:complexContent></xs:complexType><xs:element name='r' type='D'/>";
        Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(Wrap(heads + types)));
        var allowed = XmlSchema.Compile(Wrap(heads.Replace(" block='restriction'", "", StringComparison.Ordinal) + types));
        Assert.True(allowed.Validate("<r><s>7</s></r>").IsValid);
        Assert.True(allowed.Validate("<r><m>7</m></r>").IsValid);
    }
    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public void PartialUnionKeepsMemberWhitespaceThroughEnclosingPatterns(bool attribute, bool pattern)
    {
        const string types = "<xs:simpleType name='S'><xs:restriction base='xs:string'><xs:length value='4'/></xs:restriction></xs:simpleType><xs:simpleType name='L'><xs:list itemType='absent'/></xs:simpleType><xs:simpleType name='R3'><xs:restriction base='L'><xs:length value='3'/></xs:restriction></xs:simpleType><xs:simpleType name='U'><xs:union memberTypes='S R3'/></xs:simpleType>";
        string Declaration(string lexical) => attribute ? "<xs:element name='r'><xs:complexType><xs:attribute name='a' type='V' fixed='" + lexical + "'/></xs:complexType></xs:element>" : "<xs:element name='r' type='V' default='" + lexical + "'/>";
        string Restriction(string regex) => "<xs:simpleType name='V'><xs:restriction base='U'>" + (pattern ? "<xs:pattern value='" + regex + "'/>" : "") + "</xs:restriction></xs:simpleType>";
        string body = types + Restriction("a  b") + Declaration("a  b") + "<xs:element name='good' type='xs:int'/>";
        var partial = XmlSchema.Compile(Wrap(body), new() { Limits = new() { Diagnostics = 1 } });
        var good = partial.Validate("<good>7</good>"); Assert.True(good.IsComplete); Assert.True(good.IsValid); Assert.Empty(good.Diagnostics);
        Missing(partial.Validate(attribute ? "<r a='a  b'/>" : "<r/>"));
        Assert.True(XmlSchema.Compile(Wrap(body.Replace("itemType='absent'", "itemType='xs:string'", StringComparison.Ordinal))).Validate(attribute ? "<r a='a  b'/>" : "<r/>").IsValid);
        Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(Wrap(types + Restriction("a  b") + Declaration("a b"))));
        if (pattern) Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(Wrap(types + Restriction("a b") + Declaration("a  b"))));
    }
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
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public void RestrictionMembershipCanBeIndeterminateWithoutBecomingFalse(bool affiliation, bool knownSibling)
    {
        string heads = affiliation ? "<xs:element name='h' type='xs:int'/><xs:element name='m' type='xs:int' substitutionGroup='absent'/>" : "<xs:element name='h'/><xs:element name='m' type='absent' substitutionGroup='h'/>";
        if (knownSibling) heads += "<xs:element name='k' type='xs:int' substitutionGroup='h'/>";
        const string types = "<xs:complexType name='B'><xs:sequence><xs:element ref='h'/></xs:sequence></xs:complexType><xs:complexType name='D'><xs:complexContent><xs:restriction base='B'><xs:sequence><xs:element ref='m'/></xs:sequence></xs:restriction></xs:complexContent></xs:complexType><xs:element name='r' type='D'/><xs:element name='good' type='xs:int'/>";
        var partial = XmlSchema.Compile(Wrap(heads + types)); Assert.True(partial.Validate("<good>7</good>").IsValid); Missing(partial.Validate("<r><m>7</m></r>"));
        string completed = affiliation ? heads + "<xs:element name='absent' type='xs:int' substitutionGroup='h'/>" : heads + "<xs:simpleType name='absent'><xs:restriction base='xs:int'/></xs:simpleType>";
        Assert.True(XmlSchema.Compile(Wrap(completed + types)).Validate("<r><m>7</m></r>").IsValid);
        Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(Wrap(heads.Replace(affiliation ? " substitutionGroup='absent'" : " substitutionGroup='h'", "", StringComparison.Ordinal) + types)));
        Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(Wrap(heads + types.Replace("<xs:element ref='m'/>", "<xs:element ref='m' minOccurs='2' maxOccurs='2'/>", StringComparison.Ordinal))));
    }
    [Fact]
    public void CompleteSubstitutionChoiceCannotRestrictAnUnrelatedLocalLeaf()
    {
        const string head = "<xs:element name='h' type='xs:int'/>";
        const string body = "<xs:complexType name='B'><xs:sequence><xs:element name='h' type='xs:int'/></xs:sequence></xs:complexType><xs:complexType name='D'><xs:complexContent><xs:restriction base='B'><xs:sequence><xs:element ref='h'/></xs:sequence></xs:restriction></xs:complexContent></xs:complexType><xs:element name='r' type='D'/>";
        Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(Wrap(head + "<xs:element name='m' type='xs:int' substitutionGroup='h'/>" + body)));
        Assert.True(XmlSchema.Compile(Wrap(head + body)).Validate("<r><h>7</h></r>").IsValid);
    }
    [Fact]
    public void ExpandedDerivedChoiceKeepsSpecificOmittedMembershipProofs()
    {
        const string heads = "<xs:element name='h' type='xs:int'/><xs:element name='m' type='xs:int' substitutionGroup='absent'/><xs:element name='k' type='xs:int' substitutionGroup='h'/><xs:element name='t' type='xs:short' substitutionGroup='m'/>";
        const string types = "<xs:complexType name='B'><xs:sequence><xs:element ref='h'/></xs:sequence></xs:complexType><xs:complexType name='D'><xs:complexContent><xs:restriction base='B'><xs:sequence><xs:element ref='m'/></xs:sequence></xs:restriction></xs:complexContent></xs:complexType><xs:element name='r' type='D'/><xs:element name='good' type='xs:int'/>";
        var partial = XmlSchema.Compile(Wrap(heads + types));
        var good = partial.Validate("<good>7</good>"); Assert.True(good.IsComplete); Assert.True(good.IsValid); Assert.Empty(good.Diagnostics);
        var complete = XmlSchema.Compile(Wrap(heads + "<xs:element name='absent' type='xs:int' substitutionGroup='h'/>" + types));
        foreach (string name in new[] { "m", "t" })
        {
            string instance = "<r><" + name + ">7</" + name + "></r>";
            Missing(partial.Validate(instance)); Assert.True(complete.Validate(instance).IsValid);
        }
        Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(Wrap(heads.Replace(" substitutionGroup='absent'", "", StringComparison.Ordinal) + types)));
        Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(Wrap(heads + types.Replace("<xs:element ref='m'/>", "<xs:element ref='m' minOccurs='2' maxOccurs='2'/>", StringComparison.Ordinal))));
        Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(Wrap(heads.Replace("name='h' type='xs:int'", "name='h' type='xs:int' block='restriction'", StringComparison.Ordinal) + types)));
    }
    [Fact]
    public void UnorderedRestrictionRetainsIndeterminateMembershipAndKnownRangeFailures()
    {
        const string heads = "<xs:element name='h' type='xs:int'/><xs:element name='m' type='xs:int' substitutionGroup='absent'/><xs:element name='n' type='xs:int'/>";
        const string body = "<xs:complexType name='B'><xs:all><xs:element ref='h'/><xs:element ref='n'/></xs:all></xs:complexType><xs:complexType name='D'><xs:complexContent><xs:restriction base='B'><xs:sequence><xs:element ref='n'/><xs:element ref='m'/></xs:sequence></xs:restriction></xs:complexContent></xs:complexType><xs:element name='r' type='D'/><xs:element name='good' type='xs:int'/>";
        var partial = XmlSchema.Compile(Wrap(heads + body)); Assert.True(partial.Validate("<good>7</good>").IsValid); Missing(partial.Validate("<r><n>7</n><m>7</m></r>"));
        Assert.True(XmlSchema.Compile(Wrap(heads + "<xs:element name='absent' type='xs:int' substitutionGroup='h'/>" + body)).Validate("<r><n>7</n><m>7</m></r>").IsValid);
        Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(Wrap(heads + body.Replace("<xs:element ref='n'/><xs:element ref='m'/>", "<xs:element ref='n' minOccurs='2' maxOccurs='2'/><xs:element ref='m'/>", StringComparison.Ordinal))));
    }
    [Fact]
    public void CompleteTwentyChildUnorderedRestrictionKeepsFiniteUniqueMappingWork()
    {
        string children = string.Concat(Enumerable.Range(0, 20).Select(i => "<xs:element name='n" + i + "' type='xs:int'/>"));
        var (schema, _, _) = Compile("<xs:complexType name='B'><xs:all>" + children + "</xs:all></xs:complexType><xs:complexType name='D'><xs:complexContent><xs:restriction base='B'><xs:sequence>" + children + "</xs:sequence></xs:restriction></xs:complexContent></xs:complexType>");
        var operation = new Counted { WorkCap = 100_000, ComponentCap = 20_000 };
        var programs = new XsdContentCompiler().CompileAll(schema, operation, new XmlFacetEvaluator());
        Assert.True(operation.IsComplete); Assert.True(programs.ContainsKey(new("", "D")));
        Assert.InRange(operation.Work, 1, 100_000); Assert.InRange(operation.Components, 1, 20_000);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void PartialUnionConstraintRejectsOnlyWhenEveryMemberIndependentlyRejects(bool attribute)
    {
        const string types = "<xs:simpleType name='L'><xs:list itemType='absent'/></xs:simpleType><xs:simpleType name='R2'><xs:restriction base='L'><xs:length value='2'/></xs:restriction></xs:simpleType><xs:simpleType name='R3'><xs:restriction base='L'><xs:length value='3'/></xs:restriction></xs:simpleType><xs:simpleType name='U'><xs:union memberTypes='R2 R3'/></xs:simpleType>";
        string Declaration(string lexical) => attribute ? "<xs:element name='r'><xs:complexType><xs:attribute name='a' type='U' fixed='" + lexical + "'/></xs:complexType></xs:element>" : "<xs:element name='r' type='U' default='" + lexical + "'/>";
        var failure = Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(Wrap(types + Declaration("a"))));
        Assert.Single(failure.Diagnostics);
        foreach (string lexical in new[] { "a b", "a b c" })
        {
            var partial = XmlSchema.Compile(Wrap(types + Declaration(lexical) + "<xs:element name='good' type='xs:int'/>"), new() { Limits = new() { Diagnostics = 1 } });
            var good = partial.Validate("<good>7</good>"); Assert.True(good.IsComplete); Assert.True(good.IsValid); Assert.Empty(good.Diagnostics);
            Missing(partial.Validate(attribute ? "<r a='" + lexical + "'/>" : "<r/>"));
        }
        // Complete members are checked against the actual authored namespace
        // context; no fake typed value or speculative branch diagnostic.
        var qname = XmlSchema.Compile(Wrap(types.Replace("R2 R3", "xs:QName R2", StringComparison.Ordinal) + Declaration("q:n"), "xmlns:q='urn:q'"));
        Missing(qname.Validate(attribute ? "<r a='q:n' xmlns:q='urn:q'/>" : "<r/>"));
        Assert.Throws<SchemaCompileException>(() => XmlSchema.Compile(Wrap(types.Replace("R2 R3", "xs:int R2", StringComparison.Ordinal) + Declaration("bad"))));
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
