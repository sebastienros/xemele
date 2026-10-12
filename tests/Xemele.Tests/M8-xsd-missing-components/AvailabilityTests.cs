using Xemele.Validation;
using Xunit;
using static Xemele.Tests.M8XsdMissingComponents.Support;
namespace Xemele.Tests.M8XsdMissingComponents;

public sealed class AvailabilityTests
{
    [Theory]
    [InlineData("type='absent'", "")]
    [InlineData("substitutionGroup='absent'", "")]
    [InlineData("type='L'", "<xs:simpleType name='L'><xs:list itemType='absent'/></xs:simpleType>")]
    public void PublishedAbsentTypesStopBeforeReplacementNilDefaultFixedAndIdentity(string selection, string definition)
    {
        var schema = XmlSchema.Compile(Wrap(definition + $"<xs:element name='bad' {selection} nillable='true' fixed='not-an-int'><xs:key name='k'><xs:selector xpath='.'/><xs:field xpath='.'/></xs:key></xs:element><xs:element name='good' type='xs:int'/>"));
        Assert.True(schema.Validate("<good>7</good>").IsValid);
        foreach (string instance in new[] { "<bad/>", "<bad>7</bad>", "<bad><good>7</good></bad>", $"<bad xmlns:xsi='http://www.w3.org/2001/XMLSchema-instance' xmlns:xs='{Xs}' xsi:type='xs:int'/>", "<bad xmlns:xsi='http://www.w3.org/2001/XMLSchema-instance' xsi:nil='true'/>" }) Missing(schema.Validate(instance));
    }
    [Fact]
    public void DenseTypesPreserveExplicitQNameNullInferenceAndRealDefault()
    {
        var (schema, builder, op) = Compile("<xs:element name='explicit' type='absent'/><xs:element name='infer' substitutionGroup='head'/><xs:element name='ordinary'/><xs:element name='affiliation' type='xs:int' substitutionGroup='head'/>");
        Assert.Equal(Enumerable.Range(0, 4), schema.Elements.Select(e => e.Id));
        Assert.Equal(new SchemaName("", "absent"), schema.Elements[0].Type);
        Assert.Null(schema.Elements[1].Type); Assert.Equal(new SchemaName(Xs, "anyType"), schema.Elements[2].Type);
        Assert.Equal(new SchemaName(Xs, "int"), schema.Elements[3].Type);
        Assert.DoesNotContain(schema.Types, t => t.Name.LocalName is "absent" or "head");
        var availability = builder.Seal();
        Assert.NotNull(availability.Missing(ComponentAvailability.Element(schema.Elements[0]), DependencyUse.TypeAssessment, op, default));
        Assert.NotNull(availability.Missing(ComponentAvailability.Element(schema.Elements[1]), DependencyUse.TypeAssessment, op, default));
        Assert.Null(availability.Missing(ComponentAvailability.Element(schema.Elements[3]), DependencyUse.TypeAssessment, op, default));
        Assert.NotNull(availability.Missing(ComponentAvailability.Element(schema.Elements[3]), DependencyUse.SubstitutionSelection, op, default));
        Assert.True(XmlSchema.Compile(Wrap("<xs:element name='r' type='xs:int' substitutionGroup='absent'/>")).Validate("<r>7</r>").IsValid);
    }
    [Theory]
    [InlineData("strict", false)] [InlineData("lax", false)] [InlineData("skip", true)]
    public void PresentUnavailableWildcardDeclarationIsDistinctFromAbsenceAndSkip(string policy, bool skipped)
    {
        var schema = XmlSchema.Compile(Wrap($"<xs:element name='root'><xs:complexType><xs:sequence><xs:any processContents='{policy}'/></xs:sequence></xs:complexType></xs:element><xs:element name='bad' type='absent'/>"));
        var known = schema.Validate("<root><bad/></root>");
        if (skipped) Assert.True(known.IsValid); else Missing(known);
        var absent = schema.Validate("<root><unlisted/></root>"); Assert.True(absent.IsComplete); Assert.Equal(policy != "strict", absent.IsValid);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void LocalAndReferenceConsumptionStop(bool reference)
    {
        string term = reference ? "<xs:element ref='bad'/>" : "<xs:element name='bad' type='absent'/>";
        var schema = XmlSchema.Compile(Wrap("<xs:element name='bad' type='absent'/><xs:element name='root'><xs:complexType><xs:sequence>" + term + "</xs:sequence></xs:complexType></xs:element>"));
        Missing(schema.Validate("<root><bad/></root>"));
    }
    [Fact]
    public void XsiOnlySelectedProgramStopsBeforeDeferredSubstitutionContent()
    {
        var schema = XmlSchema.Compile(Wrap("<xs:element name='h'/><xs:element name='m' substitutionGroup='absent'/><xs:complexType name='C'><xs:choice><xs:element ref='h'/><xs:element ref='m'/></xs:choice></xs:complexType><xs:element name='good' type='xs:int'/>"));
        Assert.True(schema.Validate("<good>7</good>").IsValid);
        Missing(schema.Validate("<unlisted xmlns:xsi='http://www.w3.org/2001/XMLSchema-instance' xsi:type='C'><h/></unlisted>"));
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void LaterDefinitionOrderResolvesBeforePublication(bool reverse)
    {
        const string declaration = "<xs:element name='r' substitutionGroup='head'/>";
        const string definition = "<xs:element name='head' type='T'/><xs:simpleType name='T'><xs:restriction base='xs:int'/></xs:simpleType>";
        Assert.True(XmlSchema.Compile(Wrap(reverse ? definition + declaration : declaration + definition)).Validate("<r>7</r>").IsValid);
    }
    [Fact]
    public void LaterPermittedIncludeDischargesReferencesAndNeverResolvesDuringValidation()
    {
        var stream = new OwnedStream(Wrap("<xs:simpleType name='T'><xs:restriction base='xs:int'/></xs:simpleType>"));
        var resolver = new Resolver(r => XmlResourceResolution.FromResource(new(stream, r.Uri)));
        var schema = XmlSchema.Compile(Wrap("<xs:include schemaLocation='child'/><xs:element name='r' type='T'/>"), new() { BaseUri = Basis, Resolver = resolver });
        Assert.Equal(1, stream.Disposals); Assert.True(schema.Validate("<r>7</r>").IsValid); Assert.False(schema.Validate("<r>bad</r>").IsValid); Assert.Equal(1, resolver.Calls);
    }
    [Fact]
    public void CompleteFrozenCompilerStillRejectsMissingTypes()
    {
        IXsdCompiler compiler = new XmlSchemaCompiler();
        Assert.Throws<SchemaCompileException>(() => compiler.Compile(XmlDocument.Parse(Wrap("<xs:element name='r' type='absent'/>"), cancellationToken: TestContext.Current.CancellationToken), new(), new SchemaCompileOperation()));
    }
    [Fact]
    public void ConsumedPartialAttributeDefaultAndWildcardStopBeforeValueHooks()
    {
        const string list = "<xs:simpleType name='L'><xs:list itemType='absent'/></xs:simpleType>";
        var defaulted = XmlSchema.Compile(Wrap(list + "<xs:element name='r'><xs:complexType><xs:attribute name='a' type='L' default='7'/></xs:complexType></xs:element>"));
        Missing(defaulted.Validate("<r/>"));
        var wildcard = XmlSchema.Compile(Wrap(list + "<xs:attribute name='a' type='L'/><xs:element name='r'><xs:complexType><xs:anyAttribute processContents='lax'/></xs:complexType></xs:element>"));
        Assert.True(wildcard.Validate("<r/>").IsValid); Missing(wildcard.Validate("<r a='7'/>"));
    }
}
