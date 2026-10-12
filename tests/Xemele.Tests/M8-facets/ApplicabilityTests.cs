using Xunit;
using Xemele.Validation;
using static Xemele.Tests.M8Facets.FacetTests;
namespace Xemele.Tests.M8Facets;

public sealed class ApplicabilityTests
{
    [Theory]
    [InlineData("duration","P1D","P2D")][InlineData("dateTime","2000-01-01T00:00:00Z","2000-01-02T00:00:00Z")]
    [InlineData("time","12:00:00Z","13:00:00Z")][InlineData("date","2000-01-01Z","2000-01-02Z")]
    [InlineData("gYearMonth","2000-01Z","2000-02Z")][InlineData("gYear","2000Z","2001Z")]
    [InlineData("gMonthDay","--01-01Z","--01-02Z")][InlineData("gDay","---01Z","---02Z")]
    [InlineData("gMonth","--01Z","--02Z")]
    public void EveryTemporalPrimitiveSupportsOrderedFacets(string parent,string bound,string above)
    {
        Valid(Restrict("temporal",parent,F(XmlFacetKind.MinInclusive,bound)),bound);
        Valid(Restrict("temporal",parent,F(XmlFacetKind.MinExclusive,bound)),above);
        Invalid(Restrict("temporal",parent,F(XmlFacetKind.MinExclusive,bound)),bound);
        Invalid(Restrict("temporal",parent,F(XmlFacetKind.MaxInclusive,bound)),above);
        Valid(Restrict("temporal",parent,F(XmlFacetKind.MaxExclusive,above)),bound);
    }
    [Theory]
    [InlineData("string")][InlineData("hexBinary")][InlineData("base64Binary")][InlineData("anyURI")]
    [InlineData("QName")][InlineData("NOTATION")]
    public void LengthFamiliesRejectDigitsAndBounds(string parent)
    { Bad(Restrict("digits",parent,F(XmlFacetKind.TotalDigits,"1")));Bad(Restrict("bounds",parent,F(XmlFacetKind.MaxExclusive,"1"))); }
    [Theory]
    [InlineData("boolean","true")][InlineData("QName","p:a")][InlineData("NOTATION","p:a")]
    [InlineData("hexBinary","aaff")][InlineData("base64Binary","YQ==")][InlineData("anyURI","root")]
    public void EnumerationIsSupportedByEveryNonOrderedFamily(string parent,string lexical)
    { Valid(Restrict("enum",parent,F(XmlFacetKind.Enumeration,lexical)),lexical); }
    [Fact]
    public void MissingExecutableLibraryNeverProvidesDefinitionOnlySuccess()
    {
        var type=Restrict("type","string");var op=Op();var registry=new DefinitionsOnly(type);
        Assert.False(new XmlFacetEvaluator().ValidateDefinition(type,registry,op));Assert.True(op.IsComplete);
        Assert.False(new XmlFacetEvaluator().TryValidate(type,"a",Context,registry,Op(),out var value,out var error));Assert.Null(value);Assert.NotNull(error);
    }
    [Fact]
    public void EmptyRngLibraryParametersRejectWithoutBlockingRealNoNamespaceDefinitions()
    {
        var emptyParams=Restrict("string","string",F(XmlFacetKind.Length,"1")) with {Name=new("","string"),BaseType=DatatypeTypes.Name("string")};
        var registry=new XmlDatatypeRegistry();var op=Op();var adapter=new RngDatatypeFacetAdapter(new XmlFacetEvaluator());
        Assert.False(adapter.ValidateParameters(new("","string"),emptyParams.Facets,default,registry,op,out _));Assert.True(op.IsComplete);
        Assert.True(adapter.ValidateParameters(new("","token"),[],default,registry,Op(),out var direct));Assert.Null(direct);
        Assert.True(registry.Find("")!.TryParse(new("","token")," a\tb ",Context,Op(),out _,out _));
        var parent=Restrict("parent","string") with {Name=new("","parent")};
        var child=Restrict("child","string",F(XmlFacetKind.Length,"1")) with {Name=new("","child"),BaseType=parent.Name};
        Valid(child,"a",null,parent);Invalid(child,"ab",parent);
    }
    [Theory]
    [InlineData(XmlFacetKind.Length,"2","ab")][InlineData(XmlFacetKind.MinLength,"2","ab")]
    [InlineData(XmlFacetKind.MaxLength,"2","ab")][InlineData(XmlFacetKind.Pattern,"[a-z]+","ab")]
    [InlineData(XmlFacetKind.TotalDigits,"2","12")][InlineData(XmlFacetKind.FractionDigits,"1","1.2")]
    [InlineData(XmlFacetKind.MinInclusive,"1","1")][InlineData(XmlFacetKind.MinExclusive,"1","2")]
    [InlineData(XmlFacetKind.MaxInclusive,"2","2")][InlineData(XmlFacetKind.MaxExclusive,"2","1")]
    public void AllTenRngXsdParameterKindsExecute(object raw,string bound,string lexical)
    {
        var kind=(XmlFacetKind)raw;string parent=kind is XmlFacetKind.Length or XmlFacetKind.MinLength or XmlFacetKind.MaxLength or XmlFacetKind.Pattern ? "string":"decimal";
        var definition=Restrict(parent,parent,F(kind," \t"+bound+"\r\n ")) with {Name=new(DatatypeTypes.RngNamespace,parent)};
        // Pattern parameter values are retained as authored regex, not collapsed.
        if(kind==XmlFacetKind.Pattern)definition=definition with {Facets=[F(kind,bound)]};
        Valid(definition,lexical);
    }
    [Theory]
    [MemberData(nameof(Xemele.Tests.M8Datatypes.CompositionTests.Builtins),MemberType=typeof(Xemele.Tests.M8Datatypes.CompositionTests))]
    public void EveryRngBuiltinAliasExecutesWithoutParameters(string local,string lexical)
    {
        var registry=new XmlDatatypeRegistry();var builtin=registry.FindType(DatatypeTypes.Name(local))!;
        var definition=new XmlDatatypeDefinition(new(DatatypeTypes.RngNamespace,local),builtin.Variety,builtin.Name,null,[],[],[],false,default);
        var evaluator=new XmlFacetEvaluator();Assert.True(evaluator.ValidateDefinition(definition,registry,Op()));
        Assert.True(evaluator.TryValidate(definition,lexical,Context,registry,Op(),out var value,out var error),error?.Message);
        Assert.Equal(definition.Name,value!.TypeName);
    }
    [Fact]
    public void UnknownRngAliasesAndMismatchedCanonicalBasesNeverExecute()
    {
        var registry=new XmlDatatypeRegistry();var unknown=Restrict("unknown","string") with {Name=new(DatatypeTypes.RngNamespace,"unknown")};
        Assert.False(new XmlFacetEvaluator().ValidateDefinition(unknown,registry,Op()));
        var mismatch=Restrict("string","decimal") with {Name=new(DatatypeTypes.RngNamespace,"string")};
        Assert.False(new XmlFacetEvaluator().ValidateDefinition(mismatch,registry,Op()));
    }
    [Fact]
    public void DetachedNoNamespaceDefinitionsRemainXsdRestrictions()
    {
        var definition=Restrict("string","decimal",F(XmlFacetKind.MinInclusive,"2")) with {Name=new("","string")};
        var registry=new XmlDatatypeRegistry();var evaluator=new XmlFacetEvaluator();Assert.True(evaluator.ValidateDefinition(definition,registry,Op()));
        Assert.True(evaluator.TryValidate(definition,"2.0",Context,registry,Op(),out var value,out _));Assert.Equal(definition.Name,value!.TypeName);
        Assert.False(evaluator.TryValidate(definition,"text",Context,registry,Op(),out _,out _));
        var pattern=Restrict("detached","string",F(XmlFacetKind.Pattern,"a"),F(XmlFacetKind.Pattern,"b")) with {Name=new("","detached")};
        Assert.True(evaluator.ValidateDefinition(pattern,registry,Op()));Assert.True(evaluator.TryValidate(pattern,"b",Context,registry,Op(),out _,out _));
    }
    [Fact]
    public void RngAliasMarkersRequireMatchingVarietyAndRestrictionOnlyStructure()
    {
        var registry=new XmlDatatypeRegistry();var original=Restrict("string","string") with {Name=new(DatatypeTypes.RngNamespace,"string")};
        foreach(var bad in new[] {original with {Variety=XmlDatatypeVariety.List},original with {ItemType=DatatypeTypes.Name("integer")},original with {MemberTypes=[DatatypeTypes.Name("integer")]},original with {BaseType=null},original with {Facets=[F(XmlFacetKind.Length,"1",true)]}})
            Assert.False(new XmlFacetEvaluator().ValidateDefinition(bad,registry,Op()));
    }
    [Fact]
    public void RngAdapterSnapshotsEachOccurrenceWithoutRegisteringReservedNames()
    {
        var evaluator=new XmlFacetEvaluator();var adapter=new RngDatatypeFacetAdapter(evaluator);var registry=new XmlDatatypeRegistry();
        XmlFacet[] parameters=[F(XmlFacetKind.Pattern,"a.*"),F(XmlFacetKind.Pattern,".*z")];
        Assert.True(adapter.ValidateParameters(new(DatatypeTypes.RngNamespace,"string"),parameters,default,registry,Op(),out var definition));
        parameters[0]=F(XmlFacetKind.Pattern,"b.*");Assert.Equal("a.*",definition!.Facets[0].LexicalValue);
        Assert.True(evaluator.TryValidate(definition,"az",Context,registry,Op(),out _,out _));Assert.False(evaluator.TryValidate(definition,"ax",Context,registry,Op(),out _,out _));
        Assert.True(adapter.ValidateParameters(definition.Name,[F(XmlFacetKind.Pattern,"b.*")],default,registry,Op(),out var second));Assert.NotSame(definition,second);
        Assert.True(evaluator.TryValidate(second!,"bz",Context,registry,Op(),out _,out _));Assert.False(evaluator.TryValidate(definition,"bz",Context,registry,Op(),out _,out _));
        Assert.False(adapter.ValidateParameters(new("urn:external","string"),[],default,registry,Op(),out _));
        Assert.False(adapter.ValidateParameters(definition.Name,[F(XmlFacetKind.Length,"1",true)],default,registry,Op(),out _));
    }
    private sealed class DefinitionsOnly(XmlDatatypeDefinition definition):IXmlDatatypeRegistry
    {public IXmlDatatypeLibrary? Find(string ns)=>null;public XmlDatatypeDefinition? FindType(SchemaName name)=>name==definition.Name ? definition:null;}
}
