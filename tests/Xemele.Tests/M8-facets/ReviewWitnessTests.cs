using Xunit;
using Xemele.Validation;
using static Xemele.Tests.M8Facets.FacetTests;
namespace Xemele.Tests.M8Facets;

public sealed class ReviewWitnessTests
{
    [Fact]
    public void ActualCompilerRetainsListItemAndUnionMemberFieldsOnRestrictions()
    {
        var document=XmlDocument.Parse("""
        <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema" xmlns:t="urn:join" targetNamespace="urn:join">
          <xs:simpleType name="ints"><xs:list itemType="xs:integer"/></xs:simpleType>
          <xs:simpleType name="two"><xs:restriction base="t:ints"><xs:length value="2"/></xs:restriction></xs:simpleType>
          <xs:simpleType name="choices"><xs:union memberTypes="xs:integer xs:boolean"/></xs:simpleType>
          <xs:simpleType name="one"><xs:restriction base="t:choices"><xs:enumeration value="1"/></xs:restriction></xs:simpleType>
        </xs:schema>
        """, cancellationToken: TestContext.Current.CancellationToken);
        var schema=new XmlSchemaCompiler().Compile(document,new(),Op());
        var definitions=schema.SimpleTypes.Where(t=>t.Name.NamespaceUri!=DatatypeTypes.XsdNamespace).ToArray();
        var registry=new XmlDatatypeRegistry(definitions,Op());var evaluator=new XmlFacetEvaluator();
        var list=definitions.Single(t=>t.Name.LocalName=="two");var union=definitions.Single(t=>t.Name.LocalName=="one");
        Assert.Equal(DatatypeTypes.Name("integer"),list.ItemType);Assert.Equal(2,union.MemberTypes.Count);
        Assert.True(evaluator.ValidateDefinition(list,registry,Op()));Assert.True(evaluator.TryValidate(list,"1 2",Context,registry,Op(),out var l,out _));Assert.Equal(2,l!.Items!.Count);
        Assert.False(evaluator.TryValidate(list,"1",Context,registry,Op(),out _,out _));
        Assert.True(evaluator.ValidateDefinition(union,registry,Op()));Assert.True(evaluator.TryValidate(union,"1",Context,registry,Op(),out var u,out _));Assert.Equal(DatatypeTypes.Name("integer"),u!.SelectedMember!.TypeName);
        Assert.False(evaluator.TryValidate(union,"2",Context,registry,Op(),out _,out _));
        Assert.False(evaluator.ValidateDefinition(list with {ItemType=DatatypeTypes.Name("string")},registry,Op()));
        Assert.False(evaluator.ValidateDefinition(union with {MemberTypes=union.MemberTypes.Reverse().ToArray()},registry,Op()));
    }
    [Fact]
    public void DirectLengthMinMaxCannotInventTheirRequiredAncestorOrigin()
    { Bad(Restrict("direct","string",F(XmlFacetKind.Length,"2"),F(XmlFacetKind.MinLength,"1"),F(XmlFacetKind.MaxLength,"3"))); }
    [Fact]
    public void InheritedLengthBoundsAndExplicitRepeatsKeepTheirOrigin()
    {
        var a=Restrict("a","string",F(XmlFacetKind.MinLength,"1"),F(XmlFacetKind.MaxLength,"3"));
        var b=Restrict("b","string",F(XmlFacetKind.Length,"2")) with {BaseType=a.Name};
        var c=Restrict("c","string",F(XmlFacetKind.Length,"2"),F(XmlFacetKind.MinLength,"1"),F(XmlFacetKind.MaxLength,"3")) with {BaseType=b.Name};
        Valid(c,"ab",null,a,b);Invalid(c,"a",a,b);
        Bad(c with {Facets=[F(XmlFacetKind.Length,"2"),F(XmlFacetKind.MinLength,"2")]},a,b);
        var noOrigin=Restrict("a2","string",F(XmlFacetKind.Length,"2"));
        Bad(Restrict("child","string",F(XmlFacetKind.MinLength,"1")) with {BaseType=noOrigin.Name},noOrigin);
    }
    [Fact]
    public void EqualExclusiveEndpointsPrepareAnEmptyValueSpace()
    {
        var type=Restrict("empty","decimal",F(XmlFacetKind.MinExclusive,"2"),F(XmlFacetKind.MaxExclusive,"2"));
        foreach(string value in new[] {"1","2","3"})Invalid(type,value);
        Bad(Restrict("mixed","decimal",F(XmlFacetKind.MinInclusive,"2"),F(XmlFacetKind.MaxExclusive,"2")));
        Bad(Restrict("mixed2","decimal",F(XmlFacetKind.MinExclusive,"2"),F(XmlFacetKind.MaxInclusive,"2")));
    }
    [Fact]
    public void EffectiveFractionDigitsReplaceTheObsoleteAncestorBeforeCrossChecks()
    {
        var a=Restrict("a","decimal",F(XmlFacetKind.FractionDigits,"5"));
        var b=Restrict("b","decimal",F(XmlFacetKind.FractionDigits,"3")) with {BaseType=a.Name};
        var c=Restrict("c","decimal",F(XmlFacetKind.TotalDigits,"4")) with {BaseType=b.Name};
        Valid(c,"1.234",null,a,b);Invalid(c,"1.2345",a,b);
        Valid(Restrict("two","decimal",F(XmlFacetKind.FractionDigits,"3"),F(XmlFacetKind.TotalDigits,"4")) with {BaseType=a.Name},"1.234",null,a);
        Bad(c with {Facets=[F(XmlFacetKind.TotalDigits,"2")]},a,b);
    }
    [Fact]
    public void RetainedMembersAndEffectiveLengthOriginShareExactPreparationBudgets()
    {
        var list = new XmlDatatypeDefinition(new("urn:test", "list"), XmlDatatypeVariety.List, DatatypeTypes.Name("anySimpleType"), DatatypeTypes.Name("integer"), [], [], [], false, default);
        var parent = Restrict("parent", "string", F(XmlFacetKind.MinLength, "1"), F(XmlFacetKind.MaxLength, "3"));
        var child = Restrict("child", "string", F(XmlFacetKind.Length, "2")) with { BaseType = parent.Name };
        var union = new XmlDatatypeDefinition(new("urn:test", "union"), XmlDatatypeVariety.Union, DatatypeTypes.Name("anySimpleType"), null, [DatatypeTypes.Name("integer"), DatatypeTypes.Name("boolean")], [], [], false, default);
        var listChild = Restrict("listChild", "string", F(XmlFacetKind.Length, "2")) with { BaseType = list.Name, Variety = XmlDatatypeVariety.List, ItemType = list.ItemType };
        var unionChild = Restrict("unionChild", "string", F(XmlFacetKind.Enumeration, "1")) with { BaseType = union.Name, Variety = XmlDatatypeVariety.Union, MemberTypes = union.MemberTypes };
        var registry = new XmlDatatypeRegistry([list, parent, child, union, listChild, unionChild], Op());
        foreach (var type in new[] { child, listChild, unionChild })
        {
            var ledger = new OperationTests.Ledger(); Assert.True(new XmlFacetEvaluator().ValidateDefinition(type, registry, ledger));
            Assert.True(new XmlFacetEvaluator().ValidateDefinition(type, registry, Op(ledger.Work, ledger.Components)));
            foreach (var shortOp in new[] { Op(ledger.Work - 1), Op(null, ledger.Components - 1) })
            { Assert.False(new XmlFacetEvaluator().ValidateDefinition(type, registry, shortOp)); Assert.False(shortOp.IsComplete); }
            using var cancellation = new CancellationTokenSource();
            Assert.Throws<OperationCanceledException>(() => new XmlFacetEvaluator().ValidateDefinition(type, registry, new OperationTests.Ledger(cancellation, ledger.Work - 1)));
        }
    }
}
