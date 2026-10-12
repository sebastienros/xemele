using Xunit;
using Xemele.Validation;
using static Xemele.Tests.M8Facets.FacetTests;
namespace Xemele.Tests.M8Facets;

public sealed class CompositeTests
{
    private static XmlDatatypeDefinition List(string name, SchemaName item, params XmlFacet[] facets) => new(new("urn:test", name), XmlDatatypeVariety.List, null, item, [], facets, [], false, default);
    private static XmlDatatypeDefinition Union(string name, SchemaName[] members, params XmlFacet[] facets) => new(new("urn:test", name), XmlDatatypeVariety.Union, null, null, members, facets, [], false, default);
    [Theory]
    [InlineData("", 0)][InlineData(" \t\r\n", 0)][InlineData("1", 1)][InlineData("1 2", 2)][InlineData(" 1\t2\n3 ", 3)]
    public void ListLengthsCountItemsAndEmptyUserListsAreValid(string input, int length)
    {
        var type = List("list", DatatypeTypes.Name("integer"), F(XmlFacetKind.Length, length.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        var value = Valid(type, input); Assert.Equal(XmlDatatypeVariety.List, value.Variety); Assert.Equal(length, value.Items!.Count);
        Assert.All(value.Items, i => Assert.Equal(DatatypeTypes.Name("integer"), i.TypeName));
    }
    [Fact]
    public void ListItemsAreValidatedAgainstTheirOwnRestrictedType()
    {
        var item = Restrict("item", "integer", F(XmlFacetKind.MinInclusive, "2"));
        var type = List("list", item.Name, F(XmlFacetKind.MinLength, "2"), F(XmlFacetKind.MaxLength, "3"));
        Valid(type, "2 3", null, item); Invalid(type, "2", item); Invalid(type, "2 3 4 5", item); Invalid(type, "2 1", item);
        Bad(type with { Facets = [F(XmlFacetKind.WhiteSpace, "replace")] }, item);
    }
    [Fact]
    public void ListRestrictionsPreserveItemsAndIntrinsicNonemptyBuiltinLists()
    {
        var parent = List("parent", DatatypeTypes.Name("integer"));
        var child = new XmlDatatypeDefinition(new("urn:test", "child"), XmlDatatypeVariety.List, parent.Name, null, [], [F(XmlFacetKind.Length, "2")], [], false, default);
        Assert.Equal(2, Valid(child, "1 2", null, parent).Items!.Count); Invalid(child, "1", parent);
        var builtin = child with { Name = new("urn:test", "builtin"), BaseType = DatatypeTypes.Name("IDREFS"), Facets = [] };
        Valid(builtin, "a b"); Invalid(builtin, "");
        Bad(child with { ItemType = DatatypeTypes.Name("string") }, parent);
    }
    [Fact]
    public void ListEnumerationUsesTypedItemEqualityAndOwnSnapshots()
    {
        var type = List("enum", DatatypeTypes.Name("decimal"), F(XmlFacetKind.Enumeration, "1.0 2.00"));
        var value = Valid(type, "1 2"); Assert.Equal(2, value.Items!.Count); Invalid(type, "2 1"); Invalid(type, "1 2 3");
        Assert.Throws<NotSupportedException>(() => ((IList<XmlDatatypeValue>)value.Items)[0] = value.Items[1]);
    }
    [Fact]
    public void UnionTriesDeclaredMembersInOrderAndPreservesTheSelection()
    {
        var type = Union("u", [DatatypeTypes.Name("integer"), DatatypeTypes.Name("boolean"), DatatypeTypes.Name("string")]);
        Assert.Equal(DatatypeTypes.Name("integer"), Valid(type, "1").SelectedMember!.TypeName);
        Assert.Equal(DatatypeTypes.Name("boolean"), Valid(type, "true").SelectedMember!.TypeName);
        var (e, r) = Ready(type); Assert.True(e.TryValidate(type, " a\tb ", Context, r, Op(), out var text, out _)); Assert.Equal(DatatypeTypes.Name("string"), text!.SelectedMember!.TypeName);
        var lib = r.Find(DatatypeTypes.XsdNamespace)!;
        Assert.True(lib.TryParse(DatatypeTypes.Name("string"), " a\tb ", Context, Op(), out var expected, out _));
        Assert.True(lib.ValueEquals(text!, expected!, Op()));
    }
    [Fact]
    public void UnionRestrictionCanSelectLaterMemberWhenEarlierTypedValueFailsEnumeration()
    {
        var parent = Union("parent", [DatatypeTypes.Name("integer"), DatatypeTypes.Name("string")]);
        var child = new XmlDatatypeDefinition(new("urn:test", "child"), XmlDatatypeVariety.Union, parent.Name, null, [], [F(XmlFacetKind.Enumeration, "a")], [], false, default);
        Assert.Equal(DatatypeTypes.Name("string"), Valid(child, "a", null, parent).SelectedMember!.TypeName); Invalid(child, "1", parent);
        Bad(child with { Facets = [F(XmlFacetKind.Length, "1")] }, parent);
    }
    [Fact]
    public void UnionCanSelectAListAndListCanSelectAnAtomicUnionItem()
    {
        var list = List("ints", DatatypeTypes.Name("integer"));
        var union = Union("u", [list.Name, DatatypeTypes.Name("boolean")]);
        var result = Valid(union, "1 2", null, list); Assert.Equal(list.Name, result.SelectedMember!.TypeName); Assert.Equal(2, result.SelectedMember.Items!.Count);
        var atomic = Union("atom", [DatatypeTypes.Name("integer"), DatatypeTypes.Name("boolean")]);
        var listUnion = List("unionlist", atomic.Name);
        var selected = Valid(listUnion, "1 true", null, atomic);
        Assert.Equal(DatatypeTypes.Name("integer"), selected.Items![0].SelectedMember!.TypeName);
        Assert.Equal(DatatypeTypes.Name("boolean"), selected.Items[1].SelectedMember!.TypeName);
        Bad(List("listOfLists", list.Name), list); Bad(List("listOfUnionLists", union.Name), union, list);
    }
    [Fact]
    public void NestedUnionPreservesBothMemberIdentityAndActualAtomicSelection()
    {
        var inner = Union("inner", [DatatypeTypes.Name("integer"), DatatypeTypes.Name("boolean")]);
        var outer = Union("outer", [inner.Name, DatatypeTypes.Name("string")]);
        var value = Valid(outer, "true", null, inner);
        Assert.Equal(inner.Name, value.SelectedMember!.TypeName); Assert.Equal(DatatypeTypes.Name("boolean"), value.SelectedMember.SelectedMember!.TypeName);
    }
    [Fact]
    public void FinalPreventsRestrictionListAndUnionDerivation()
    {
        var final = Restrict("final", "string") with { Final = [XmlDatatypeDerivation.Restriction, XmlDatatypeDerivation.List, XmlDatatypeDerivation.Union] };
        Bad(Restrict("child", "string") with { BaseType = final.Name }, final);
        Bad(List("list", final.Name), final); Bad(Union("union", [final.Name]), final);
        var onlyRestriction = final with { Final = [XmlDatatypeDerivation.Restriction] };
        Valid(List("list", onlyRestriction.Name), "a b", null, onlyRestriction);
    }
    [Fact]
    public void UnknownTypesCyclesAndMalformedVarietyGraphsReject()
    {
        var a = Restrict("a", "string") with { BaseType = new("urn:test", "b") };
        var b = Restrict("b", "string") with { BaseType = a.Name }; Bad(a, b);
        Bad(a); Bad(a with { BaseType = a.Name });
        Bad(List("bad", new("urn:test", "unknown"))); Bad(Union("empty", []));
        Bad(Restrict("bad", "anySimpleType")); Bad(Restrict("bad", "string") with { ItemType = DatatypeTypes.Name("string") });
        Bad(Union("bad", [DatatypeTypes.Name("anySimpleType")]));
    }
    [Fact]
    public void NoNamespaceDefinitionsOverrideAliasesAtExecution()
    {
        var parent = Restrict("string", "decimal") with { Name = new("", "string") };
        var child = Restrict("child", "string", F(XmlFacetKind.MinInclusive, "2")) with { Name = new("", "child"), BaseType = parent.Name };
        Assert.Equal(child.Name, Valid(child, "2.0", null, parent).TypeName); Invalid(child, "text", parent); Invalid(child, "1", parent);
    }
    [Theory]
    [MemberData(nameof(Xemele.Tests.M8Datatypes.CompositionTests.Builtins), MemberType = typeof(Xemele.Tests.M8Datatypes.CompositionTests))]
    public void All45VerifiedBuiltinsExecuteThroughTheFrozenEvaluator(string name, string lexical)
    {
        var registry = new XmlDatatypeRegistry(); var definition = registry.FindType(DatatypeTypes.Name(name))!; var e = new XmlFacetEvaluator();
        var op = Op(); Assert.True(e.ValidateDefinition(definition, registry, op), string.Join(";", op.Diagnostics.Select(d => d.Message)));
        Assert.True(e.TryValidate(definition, lexical, Context, registry, Op(), out var value, out var error), error?.Message);
        Assert.Equal(definition.Name, value!.TypeName);
    }
    [Fact]
    public void RngPatternsAreConjunctiveAndOtherRepeatedParametersReject()
    {
        var type = Restrict("params", "string", F(XmlFacetKind.Pattern, "a.*"), F(XmlFacetKind.Pattern, ".*z")) with { Name = new(DatatypeTypes.RngNamespace, "string") };
        Valid(type, "az"); Invalid(type, "ax"); Invalid(type, "bz");
        Bad(type with { Facets = [F(XmlFacetKind.MinLength, "1"), F(XmlFacetKind.MinLength, "1")] });
        Bad(type with { Facets = [F(XmlFacetKind.WhiteSpace, "collapse")] });
        Bad(type with { Facets = [F(XmlFacetKind.Enumeration, "a")] });
    }
}
