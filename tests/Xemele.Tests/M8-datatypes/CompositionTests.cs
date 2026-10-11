using Xunit;
using Xemele.Validation;
namespace Xemele.Tests.M8Datatypes;

public sealed class CompositionTests
{
    private static readonly XmlDatatypeRegistry Registry = new();
    private static readonly IXmlDatatypeLibrary Library = Registry.Find(DatatypeTypes.XsdNamespace)!;
    private static readonly SchemaNamespaceContext Context = new(new Dictionary<string, string> { ["p"] = "urn:values", ["q"] = "urn:values" }, "urn:default", null);
    private static SchemaCompileOperation Op(long? work = null, int? components = null) =>
        new(new() { MaxOperations = work, MaxComponents = components, CancellationToken = TestContext.Current.CancellationToken, Limits = new() { EvaluationWork = null } });
    private static XmlDatatypeValue Parse(string type, string input, IXmlDatatypeLibrary? library = null)
    {
        library ??= Library;
        Assert.True(library.TryParse(new(library.NamespaceUri, type), input, Context, Op(), out var value, out var error));
        Assert.Null(error); Assert.NotNull(value); return value;
    }
    public static TheoryData<string, string> Builtins => new()
    {
        { "anySimpleType", "arbitrary😀" }, { "string", "arbitrary😀" }, { "normalizedString", "a\tb" },
        { "token", " a\tb " }, { "language", " en-US " }, { "Name", "a:b" }, { "NMTOKEN", "1:a" },
        { "NCName", "根" }, { "ID", "a" }, { "IDREF", "a" }, { "ENTITY", "a" },
        { "NMTOKENS", "a 1:b" }, { "IDREFS", "a 根" }, { "ENTITIES", "a b" }, { "boolean", " true " },
        { "decimal", " .00100 " }, { "float", "NaN" }, { "double", "-INF" }, { "integer", "+12" },
        { "nonPositiveInteger", "0" }, { "negativeInteger", "-1" }, { "long", "9223372036854775807" },
        { "int", "-2147483648" }, { "short", "32767" }, { "byte", "-128" }, { "nonNegativeInteger", "0" },
        { "unsignedLong", "18446744073709551615" }, { "unsignedInt", "4294967295" }, { "unsignedShort", "65535" },
        { "unsignedByte", "255" }, { "positiveInteger", "1" }, { "duration", "P1Y2M3DT4H5M6.7S" },
        { "dateTime", "2000-02-29T24:00:00Z" }, { "time", "23:59:60Z" }, { "date", "2000-02-29+14:00" },
        { "gYearMonth", "2000-02Z" }, { "gYear", "-0001Z" }, { "gMonthDay", "--02-29Z" }, { "gDay", "---31Z" },
        { "gMonth", "--02Z" }, { "hexBinary", " 0aFF " }, { "base64Binary", " YQ== " },
        { "anyURI", "../根?q=1" }, { "QName", "p:根" }, { "NOTATION", "p:根" }
    };
    [Theory, MemberData(nameof(Builtins))]
    public void EveryAdvertisedBuiltinExecutes(string type, string lexical)
    {
        var name = DatatypeTypes.Name(type); var definition = Registry.FindType(name);
        Assert.NotNull(definition); Assert.True(Library.IsDefined(name));
        Assert.Equal(DatatypeTypes.All[type].Whitespace, Library.Whitespace(name));
        var value = Parse(type, lexical); Assert.Equal(name, value.TypeName);
        Assert.Equal(definition.Variety, value.Variety);
        Assert.True(Library.ValueEquals(value, value, Op())); Assert.Equal(XmlValueOrder.Equal, Library.Compare(value, value, Op()));
        Assert.Equal(Library.ValueHashCode(value, Op()), Library.ValueHashCode(Parse(type, lexical), Op()));
    }
    [Fact]
    public void ExactCompletenessAndNamespaceMapping()
    {
        Assert.Equal(45, Builtins.Count); Assert.Equal(45, DatatypeTypes.All.Count);
        foreach (string type in DatatypeTypes.All.Keys)
        {
            var rng = Registry.Find(DatatypeTypes.RngNamespace)!;
            Assert.True(rng.IsDefined(new(rng.NamespaceUri, type)));
            Assert.Same(Registry.FindType(DatatypeTypes.Name(type)), Registry.FindType(new(rng.NamespaceUri, type)));
        }
        Assert.Same(Library, Registry.Find(Library.NamespaceUri));
        Assert.Null(Registry.Find("urn:unknown")); Assert.Null(Registry.Find("HTTP://www.w3.org/2001/XMLSchema"));
        Assert.False(Library.IsDefined(new("urn:unknown", "string"))); Assert.False(Library.IsDefined(DatatypeTypes.Name("String")));
        var empty = Registry.Find("")!;
        Assert.True(empty.IsDefined(new("", "string"))); Assert.True(empty.IsDefined(new("", "token")));
        Assert.False(empty.IsDefined(new("", "decimal"))); Assert.Null(Registry.FindType(new("", "decimal")));
        Assert.False(empty.TryParse(new("", "decimal"), "1", Context, Op(), out var value, out var error));
        Assert.Null(value); Assert.Same(DatatypeErrors.UnknownType, error);
        Assert.Throws<ArgumentException>(() => Library.Whitespace(new("urn:wrong", "string")));
    }
    [Theory]
    [InlineData("decimal", "1.00")][InlineData("QName", "p:a")][InlineData("NMTOKENS", "a b")]
    public void RngAliasesPreserveDeclaredNamesAndSharedPrimitiveIdentity(string type, string input)
    {
        var rng = Registry.Find(DatatypeTypes.RngNamespace)!;
        var alias = Parse(type, input, rng); var canonical = Parse(type, input);
        Assert.Equal(new(rng.NamespaceUri, type), alias.TypeName);
        Assert.True(Library.ValueEquals(alias, canonical, Op()));
        Assert.Equal(Library.ValueHashCode(alias, Op()), rng.ValueHashCode(canonical, Op()));
        if (alias.Items is not null) Assert.All(alias.Items, item => Assert.Equal(DatatypeTypes.XsdNamespace, item.TypeName.NamespaceUri));
    }
    [Theory]
    [InlineData("NMTOKENS", "NMTOKEN")][InlineData("IDREFS", "IDREF")][InlineData("ENTITIES", "ENTITY")]
    public void NonemptyListsOwnOrderedAtomicItems(string type, string itemType)
    {
        char[] input = " \ta\r\n根  z \t".ToCharArray();
        Assert.True(Library.TryParse(DatatypeTypes.Name(type), input, Context, Op(), out var value, out var error));
        Array.Fill(input, '!'); Assert.Null(error); Assert.NotNull(value!.Items);
        Assert.Equal(3, Library.ValueLength(value, Op())); Assert.Equal(DatatypeTypes.Name(type), value.TypeName);
        for (int i = 0; i < 3; i++)
        {
            Assert.Equal(DatatypeTypes.Name(itemType), value.Items[i].TypeName);
            Assert.True(Library.ValueEquals(value.Items[i], Parse(itemType, new[] { "a", "根", "z" }[i]), Op()));
        }
        Assert.Throws<NotSupportedException>(() => ((IList<XmlDatatypeValue>)value.Items)[0] = Parse(itemType, "changed"));
        foreach (string empty in new[] { "", " \t\r\n " })
        { Assert.False(Library.TryParse(DatatypeTypes.Name(type), empty, Context, Op(), out value, out error)); Assert.Null(value); Assert.Same(DatatypeErrors.InvalidLexical, error); }
        Assert.Equal(XmlValueOrder.Indeterminate, Library.Compare(Parse(type, "a b"), Parse(type, "b a"), Op()));
    }
    [Theory]
    [InlineData("IDREFS", "valid bad:name valid")][InlineData("ENTITIES", "valid 1bad")]
    [InlineData("NMTOKENS", "a ! z")][InlineData("NMTOKENS", "a\u00a0b")]
    [InlineData("IDREFS", "a😀")][InlineData("ENTITIES", "a\ud800")]
    public void ListsFailAtomicallyAndOnlyXmlWhitespaceSplits(string type, string input)
    {
        var op = Op(); Assert.False(Library.TryParse(DatatypeTypes.Name(type), input, Context, op, out var value, out var error));
        Assert.Null(value); Assert.NotNull(error); Assert.True(op.IsComplete);
    }
    [Fact]
    public void CrossFamilyIdentitiesAndMeasuresPreserveTheFrozenContract()
    {
        foreach (var (a, x, b, y) in new[] { ("integer", "1", "decimal", "1.00"), ("float", "NaN", "float", "NaN"),
            ("double", "0", "double", "-0"), ("QName", "p:a", "QName", "q:a"), ("token", " a ", "string", "a") })
        {
            var left = Parse(a, x); var right = Parse(b, y); Assert.True(Library.ValueEquals(left, right, Op()));
            Assert.Equal(Library.ValueHashCode(left, Op()), Library.ValueHashCode(right, Op()));
        }
        foreach (var (a, b, input) in new[] { ("float", "double", "1"), ("decimal", "float", "1"), ("string", "anyURI", "a"),
            ("QName", "NOTATION", "p:a"), ("hexBinary", "base64Binary", "" ) })
        { Assert.False(Library.ValueEquals(Parse(a, input), Parse(b, input), Op())); Assert.Equal(XmlValueOrder.Indeterminate, Library.Compare(Parse(a, input), Parse(b, input), Op())); }
        Assert.Equal(3, Library.TotalDigits(Parse("decimal", ".001"), Op())); Assert.Equal(3, Library.FractionDigits(Parse("decimal", ".001"), Op()));
        Assert.Equal(1, Library.TotalDigits(Parse("decimal", "1.00"), Op())); Assert.Equal(4, Library.TotalDigits(Parse("decimal", "1000.00"), Op()));
        Assert.Equal(XmlValueOrder.Indeterminate, Library.Compare(Parse("dateTime", "2000-01-01T00:00:00"), Parse("dateTime", "2000-01-01T00:00:00Z"), Op()));
        Assert.Equal(XmlValueOrder.Indeterminate, Library.Compare(Parse("duration", "P1M"), Parse("duration", "P30D"), Op()));
        Assert.Equal(2, Library.ValueLength(Parse("string", "根😀"), Op())); Assert.Equal(1, Library.ValueLength(Parse("base64Binary", "YQ=="), Op()));
        Assert.Null(Library.ValueLength(Parse("QName", "p:a"), Op())); Assert.Null(Library.TotalDigits(Parse("float", "1"), Op()));
    }
    [Fact]
    public void CompositesKeepExactMemberAndSnapshots()
    {
        var atom = Parse("integer", "1"); XmlDatatypeValue[] caller = [atom];
        var list = Library.CreateList(new("urn:schema", "list"), caller, Op()); caller[0] = Parse("integer", "2");
        Assert.Same(atom, list.Items![0]);
        var union = Library.CreateUnion(new("urn:schema", "union"), list, Op());
        var retyped = Library.Retype(new("urn:schema", "restricted"), union, Op());
        Assert.Same(list, union.SelectedMember); Assert.Same(list, retyped.SelectedMember);
        Assert.Equal(1, Library.ValueLength(retyped, Op())); Assert.Null(Library.TotalDigits(retyped, Op()));
        Assert.True(Library.ValueEquals(list, retyped, Op())); Assert.Equal(Library.ValueHashCode(list, Op()), Library.ValueHashCode(retyped, Op()));
        Assert.Throws<ArgumentException>(() => Library.CreateList(new("urn:schema", "bad"), [list], Op()));
        Assert.Empty(Library.CreateList(new("urn:schema", "empty"), [], Op()).Items!);
    }
    [Fact]
    public void RegistrySnapshotsDefinitionsAndRejectsReservedOrDuplicateNames()
    {
        SchemaName name = new("urn:schema", "custom"); SchemaName[] members = [DatatypeTypes.Name("decimal")];
        var definition = new XmlDatatypeDefinition(name, XmlDatatypeVariety.Union, null, null, members, [], [], false, default);
        var caller = new List<XmlDatatypeDefinition> { definition }; var registry = new XmlDatatypeRegistry(caller, Op());
        caller.Clear(); members[0] = DatatypeTypes.Name("string");
        Assert.Same(definition, registry.FindType(name)); Assert.Equal(DatatypeTypes.Name("decimal"), registry.FindType(name)!.MemberTypes[0]);
        Assert.Null(registry.Find("urn:schema")); // Schema-defined types use later facet evaluation, never a fake library.
        Assert.Throws<ArgumentException>(() => new XmlDatatypeRegistry([definition, definition], Op()));
        Assert.Throws<ArgumentException>(() => new XmlDatatypeRegistry([definition with { Name = DatatypeTypes.Name("decimal") }], Op()));
        var noNamespace = definition with { Name = new("", "custom") };
        Assert.Same(noNamespace, new XmlDatatypeRegistry([noNamespace], Op()).FindType(noNamespace.Name));
        Assert.Contains(Registry.FindType(DatatypeTypes.Name("NMTOKENS"))!.Facets, f => f.Kind == XmlFacetKind.MinLength && f.LexicalValue == "1");
    }
    [Theory]
    [InlineData("boolean", "true")][InlineData("NMTOKENS", "a b")][InlineData("IDREFS", "根 long")]
    [InlineData("decimal", ".001")][InlineData("QName", "p:a")]
    public void ExactWorkAndComponentsReplayIndependently(string type, string input)
    {
        var ledger = new Ledger(); Assert.True(Library.TryParse(DatatypeTypes.Name(type), input, Context, ledger, out _, out _));
        Assert.True(ledger.Work > 1); Assert.True(ledger.Components > 0);
        foreach (var op in new[] { Op(ledger.Work, ledger.Components), Op(ledger.Work, null), Op(null, ledger.Components) })
        { Assert.True(Library.TryParse(DatatypeTypes.Name(type), input, Context, op, out var value, out var error)); Assert.NotNull(value); Assert.Null(error); }
        var componentRefusal = Op(null, Math.Max(1, ledger.Components - 1));
        if (ledger.Components == 1) Assert.True(componentRefusal.ChargeComponents(1, default));
        foreach (var op in new[] { Op(ledger.Work - 1, null), componentRefusal })
        { Assert.False(Library.TryParse(DatatypeTypes.Name(type), input, Context, op, out var value, out var error)); Assert.Null(value); Assert.Same(DatatypeErrors.Stopped, error); Assert.False(op.IsComplete); }
    }
    [Fact]
    public void IndependentlyCalculatedShortListAccounting()
    {
        // 'a b': dispatch1 + normalization9 + counting3 + array1 + item lookup1
        // + two atomic NMTOKEN parses (metadata1, scalar1, range probes, copy1)
        // + splitting3 + reference stores2 + core list dispatch1/items2.
        var atomic = new Ledger(); var provider = new TextBinaryDatatypeProvider();
        Assert.True(provider.TryParse(DatatypeTypes.All["NMTOKEN"], "a", Context, atomic, out _, out _));
        var second = new Ledger(); Assert.True(provider.TryParse(DatatypeTypes.All["NMTOKEN"], "b", Context, second, out _, out _));
        long expected = 1 + 9 + 3 + 1 + 1 + atomic.Work + second.Work + 3 + 2 + 1 + 1 + 2;
        int components = 3 + atomic.Components + second.Components + 3 + 1;
        var op = Op(expected, components);
        Assert.True(Library.TryParse(DatatypeTypes.Name("NMTOKENS"), "a b", Context, op, out _, out _)); Assert.Equal(0, op.RemainingWork);
        Assert.False(Library.TryParse(DatatypeTypes.Name("NMTOKENS"), "a b", Context, Op(expected - 1, components), out _, out _));
    }
    [Fact]
    public void LongScratchAndListsCancelInsideWorkWithUnlimitedCapsAndRecoverAfterRefusal()
    {
        string input = "根" + new string('a', 1024) + " b";
        using var cancellation = new CancellationTokenSource(); var ledger = new Ledger(cancellation, 500);
        Assert.Throws<OperationCanceledException>(() => Library.TryParse(DatatypeTypes.Name("IDREFS"), input, Context, ledger, out _, out _));
        var counts = new Ledger(); Assert.True(Library.TryParse(DatatypeTypes.Name("IDREFS"), input, Context, counts, out _, out _));
        Assert.False(Library.TryParse(DatatypeTypes.Name("IDREFS"), input, Context, Op(counts.Work - 1), out var failed, out var error));
        Assert.Null(failed); Assert.Same(DatatypeErrors.Stopped, error);
        Assert.Equal(2, Parse("IDREFS", input).Items!.Count);
        using var already = new CancellationTokenSource(); already.Cancel();
        Assert.Throws<OperationCanceledException>(() => Library.TryParse(DatatypeTypes.Name("unknown"), "", Context, new SchemaCompileOperation(new() { CancellationToken = already.Token }), out _, out _));
    }
    [Fact]
    public void NormalizationRulesAndInvalidDispatchAreExact()
    {
        Assert.True(Library.ValueEquals(Parse("normalizedString", "a\tb\rc\n"), Parse("string", "a b c "), Op()));
        Assert.True(Library.ValueEquals(Parse("token", " \ta\r\nb \n"), Parse("string", "a b"), Op()));
        Assert.True(Library.ValueEquals(Parse("token", "a\u00a0b"), Parse("string", "a\u00a0b"), Op()));
        Assert.False(Library.ValueEquals(Parse("string", "a\tb"), Parse("string", "a b"), Op()));
        foreach (var name in new[] { new SchemaName("urn:bad", "string"), DatatypeTypes.Name("String"), DatatypeTypes.Name("unknown") })
        { Assert.False(Library.TryParse(name, "a", Context, Op(), out var value, out var error)); Assert.Null(value); Assert.Same(DatatypeErrors.UnknownType, error); }
        Assert.False(Library.TryParse(DatatypeTypes.Name("string"), "\ud800", Context, Op(), out _, out var unicode)); Assert.Same(DatatypeErrors.InvalidUnicode, unicode);
    }
    [Theory]
    [InlineData(false)][InlineData(true)]
    public void CallerSchemaExceptionsAtCompositeBoundariesPropagate(bool alias)
    {
        var library = alias ? Registry.Find(DatatypeTypes.RngNamespace)! : Library;
        var type = new SchemaName(library.NamespaceUri, "NMTOKENS");
        var counts = new Ledger(); Assert.True(library.TryParse(type, "a b", Context, counts, out _, out _));
        var marker = new SchemaCompileException([new("TEST.Callback", XmlDiagnosticSeverity.Fatal, "Caller failure.", default)]);
        var caller = new Ledger(); caller.Callback = () => { if (caller.Work >= counts.Work - 1) { caller.Complete = false; throw marker; } };
        var actual = Assert.Throws<SchemaCompileException>(() => library.TryParse(type, "a b", Context, caller, out _, out _));
        Assert.Same(marker, actual);
    }
    [Fact]
    public void RegistrySetupHonorsExactQuotasAndCancellation()
    {
        var ledger = new Ledger(); _ = new XmlDatatypeRegistry([], ledger);
        _ = new XmlDatatypeRegistry([], Op(ledger.Work, ledger.Components));
        Assert.Throws<SchemaCompileException>(() => new XmlDatatypeRegistry([], Op(ledger.Work - 1, null)));
        Assert.Throws<SchemaCompileException>(() => new XmlDatatypeRegistry([], Op(null, ledger.Components - 1)));
        using var cancellation = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => new XmlDatatypeRegistry([], new Ledger(cancellation, 5)));
    }
    [Theory]
    [InlineData("custom")][InlineData("string")][InlineData("token")]
    public void NoTargetNamespaceDefinitionsTakePrecedenceWithoutChangingRngLibraries(string local)
    {
        var name = new SchemaName("", local);
        var definition = new XmlDatatypeDefinition(name, XmlDatatypeVariety.Atomic, DatatypeTypes.Name("decimal"), null, [], [], [], false, default);
        var registry = new XmlDatatypeRegistry([definition], Op());
        Assert.Same(definition, registry.FindType(name));
        var selected = registry.FindType(name)!.BaseType!.Value;
        var xsd = registry.Find(selected.NamespaceUri)!;
        Assert.True(xsd.TryParse(selected, "1.00", Context, Op(), out var parsed, out _));
        var declared = xsd.Retype(name, parsed!, Op());
        Assert.Equal(name, declared.TypeName); Assert.True(xsd.ValueEquals(parsed!, declared, Op()));
        var empty = registry.Find("")!;
        Assert.True(empty.IsDefined(new("", "string"))); Assert.True(empty.IsDefined(new("", "token")));
        Assert.True(empty.TryParse(new("", "token"), " a\tb ", Context, Op(), out var token, out _));
        // Compare in this registry's provider set, rather than the independent static set.
        Assert.True(xsd.TryParse(DatatypeTypes.Name("string"), "a b", Context, Op(), out var text, out _));
        Assert.True(xsd.ValueEquals(token!, text!, Op()));
        Assert.True(empty.TryParse(new("", "string"), " a\tb ", Context, Op(), out var preserved, out _));
        Assert.True(xsd.TryParse(DatatypeTypes.Name("string"), " a\tb ", Context, Op(), out var expected, out _));
        Assert.True(xsd.ValueEquals(preserved!, expected!, Op()));
        Assert.Same(registry.FindType(DatatypeTypes.Name("string")), registry.FindType(new(DatatypeTypes.RngNamespace, "string")));
        if (local != "string") Assert.Same(registry.FindType(DatatypeTypes.Name("string")), registry.FindType(new("", "string")));
        if (local != "token") Assert.Same(registry.FindType(DatatypeTypes.Name("token")), registry.FindType(new("", "token")));
    }
    [Fact]
    public void ManyDefinitionIngestionHasIndependentSetupCostsAndBoundedCancellation()
    {
        var definitions = new List<XmlDatatypeDefinition>();
        for (int i = 0; i < 128; i++) definitions.Add(new(new("urn:many", "t" + i), XmlDatatypeVariety.Atomic,
            DatatypeTypes.Name("string"), null, [], [], [], false, default));
        // Entry1 + builtin visits45 + caller visits128 + seal1. Setup6 and
        // eight components per builtin, one retained map entry per caller type.
        long work = 47 + definitions.Count; int components = 366 + definitions.Count;
        var op = Op(work, components); var registry = new XmlDatatypeRegistry(definitions, op);
        Assert.Equal(0, op.RemainingWork);
        var last = definitions[^1]; definitions.Clear(); Assert.Same(last, registry.FindType(last.Name));
        var array = Enumerable.Range(0, 128).Select(i => last with { Name = new("urn:many", "t" + i) }).ToArray();
        Assert.Throws<SchemaCompileException>(() => new XmlDatatypeRegistry(array, Op(work - 1, null)));
        Assert.Throws<SchemaCompileException>(() => new XmlDatatypeRegistry(array, Op(null, components - 1)));
        using var cancellation = new CancellationTokenSource();
        var cancelling = new Ledger(cancellation, 46 + 64);
        Assert.Throws<OperationCanceledException>(() => new XmlDatatypeRegistry(array, cancelling));
        Assert.Equal(46 + 64, cancelling.Work);
    }
    private sealed class Ledger(CancellationTokenSource? cancellation = null, long cancelAt = long.MaxValue) : ISchemaOperation
    {
        internal long Work; internal int Components;
        internal bool Complete = true; internal Action? Callback;
        public XmlResourceLimits Limits { get; } = new() { EvaluationWork = null };
        public CancellationToken CancellationToken => cancellation?.Token ?? default;
        public bool IsComplete => Complete; public long? RemainingWork => null;
        public bool Charge(long work, XmlLocation location) { Work += work; Callback?.Invoke(); if (Work >= cancelAt) cancellation?.Cancel(); CancellationToken.ThrowIfCancellationRequested(); return true; }
        public bool ChargeComponents(int count, XmlLocation location) { Components += count; CancellationToken.ThrowIfCancellationRequested(); return true; }
        public bool AddDiagnostic(XmlDiagnostic diagnostic) => throw new InvalidOperationException("Unexpected diagnostic.");
        public void Refuse(string code, string message, XmlLocation location) => throw new InvalidOperationException("Unexpected refusal.");
    }
}
