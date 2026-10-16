using System.Globalization;
using Xunit;
using Xemele.Validation;
namespace Xemele.Tests.M8DatatypesCore;

public sealed class CoreTests
{
    private static SchemaName Name(string local = "declared") => new("urn:test", local);
    private static SchemaCompileOperation Operation(long? work = null, int? components = null) =>
        new(new()
        {
            MaxOperations = work,
            MaxComponents = components,
            CancellationToken = TestContext.Current.CancellationToken,
            Limits = new()
            {
                EvaluationWork = null
            }
        });

    private static SchemaCompileOperation OperationWithToken(CancellationToken token) => new(new() { MaxOperations = null, MaxComponents = null, CancellationToken = token, Limits = new() { EvaluationWork = null } });

    [Fact]
    public void ExactMetadataHasAllNamesImmediateBasesAndFixedWhitespace()
    {
        string[] names = ["anySimpleType", "string", "normalizedString", "token", "language", "Name", "NMTOKEN", "NCName", "ID", "IDREF", "ENTITY", "NMTOKENS", "IDREFS", "ENTITIES", "boolean", "decimal", "float", "double", "integer", "nonPositiveInteger", "negativeInteger", "long", "int", "short", "byte", "nonNegativeInteger", "unsignedLong", "unsignedInt", "unsignedShort", "unsignedByte", "positiveInteger", "duration", "dateTime", "time", "date", "gYearMonth", "gYear", "gMonthDay", "gDay", "gMonth", "hexBinary", "base64Binary", "anyURI", "QName", "NOTATION"];
        Assert.Equal(names.Order(), DatatypeTypes.All.Keys.Order());
        foreach (var name in names)
        {
            Assert.True(DatatypeTypes.TryGet(DatatypeTypes.Name(name), out var metadata));
            Assert.Equal(name, metadata.LocalName);
            if (metadata.BaseType is string parent)
            {
                Assert.Contains(parent, names);
            }

            Assert.Equal(name is "string" or "anySimpleType" ? XmlWhitespaceRule.Preserve : name == "normalizedString" ? XmlWhitespaceRule.Replace : XmlWhitespaceRule.Collapse, metadata.Whitespace);
            Assert.Equal(metadata.Family, metadata.Primitive is { } primitive ? DatatypeValues.Family(primitive) : DatatypeFamily.TextBinary);
        }
        Assert.Equal("long", DatatypeTypes.All["int"].BaseType);
        Assert.Equal(DatatypePrimitive.Decimal, DatatypeTypes.All["unsignedByte"].Primitive);
        Assert.Null(DatatypeTypes.All["anySimpleType"].Variety);
        Assert.False(DatatypeTypes.All["token"].WhitespaceFixed);
        Assert.True(DatatypeTypes.All["NMTOKENS"].WhitespaceFixed);
        Assert.Equal("NMTOKEN", DatatypeTypes.All["NMTOKENS"].ItemType);
        Assert.False(DatatypeTypes.TryGet(new(DatatypeTypes.RngNamespace, "string"), out _));
        Assert.False(DatatypeTypes.TryGet(DatatypeTypes.Name("String"), out _));
        Assert.False(DatatypeTypes.TryGet(DatatypeTypes.Name("dateTimeStamp"), out _));
    }
    [Theory]
    [InlineData(0, " \tA\r\n根😀\u00a0\u0085\u2003 ")]
    [InlineData(1, "  A  根😀\u00a0\u0085\u2003 ")]
    [InlineData(2, "A 根😀\u00a0\u0085\u2003")]
    public void NormalizationUsesOnlyXmlWhitespaceAndUnicodeScalars(int ruleValue, string expected)
    {
        var rule = (XmlWhitespaceRule)ruleValue;
        const string input = " \tA\r\n根😀\u00a0\u0085\u2003 ";
        var operation = Operation();
        Assert.True(DatatypeNormalization.TryMeasure(input, rule, operation, out var length, out var scalars, out var error));
        Assert.Null(error);
        Assert.Equal(expected.Length, length);
        Assert.Equal(11, scalars);
        var buffer = new char[length];
        Assert.True(DatatypeNormalization.TryNormalize(input, rule, buffer, operation, out var written, out error));
        Assert.Equal(expected, new string(buffer, 0, written));
        Assert.Null(error);
        Assert.Equal(expected, DatatypeNormalization.Normalize(input, rule, operation));
    }
    [Fact]
    public void ExactWorkAndComponentBoundariesPollEntryAndExit()
    {
        const string input = "\t根😀 "; // Four scalars, five UTF-16 units; two-pass cost eight.
        var exact = Operation(8, 1);
        Assert.Equal("根😀", DatatypeNormalization.Normalize(input, XmlWhitespaceRule.Collapse, exact));
        Assert.Equal(0, exact.RemainingWork);
        Assert.True(exact.IsComplete);
        Assert.False(exact.ChargeComponents(1, default));
        var shortWork = Operation(7);
        Assert.Throws<SchemaCompileException>(() => DatatypeNormalization.Normalize(input, XmlWhitespaceRule.Collapse, shortWork));
        Assert.False(shortWork.IsComplete);
        Assert.Equal(0, shortWork.RemainingWork);
        var fullComponents = Operation();
        fullComponents.ChargeComponents(int.MaxValue, default);
        Assert.Throws<SchemaCompileException>(() => DatatypeNormalization.Normalize("", XmlWhitespaceRule.Preserve, fullComponents));
        var buffer = new char[5];
        var borrowed = Operation(8, 1);
        Assert.True(DatatypeNormalization.TryNormalize(input, XmlWhitespaceRule.Collapse, buffer, borrowed, out var written, out var error));
        Assert.Equal(3, written);
        Assert.Null(error);
        Assert.True(borrowed.ChargeComponents(1, default));
        var stopped = Operation(1);
        Assert.True(stopped.Charge(1, default));
        Assert.False(stopped.Charge(1, default));
        Assert.False(DatatypeNormalization.TryMeasure("", XmlWhitespaceRule.Preserve, stopped, out _, out _, out error));
        Assert.Same(DatatypeErrors.Stopped, error);
    }
    [Theory]
    [InlineData(0xd800, false)]
    [InlineData(0xdfff, false)]
    [InlineData(0xd800, true)]
    public void MalformedScalarsFailWithoutOwnedOutput(int codeUnit, bool embedded)
    {
        var input = embedded ? "ok" + new string((char)codeUnit, 1) + "X" : new string((char)codeUnit, 1);
        Assert.False(DatatypeNormalization.TryMeasure(input, XmlWhitespaceRule.Collapse, Operation(), out _, out _, out var error));
        Assert.Same(DatatypeErrors.InvalidUnicode, error);
        Assert.Throws<FormatException>(() => DatatypeNormalization.Normalize(input, XmlWhitespaceRule.Preserve, Operation()));
    }
    [Fact]
    public void DestinationArgumentsAndEmptyWhitespaceBoundariesAreExplicit()
    {
        Assert.Equal("", DatatypeNormalization.Normalize(" \t\r\n", XmlWhitespaceRule.Collapse, Operation()));
        Assert.Equal("", DatatypeNormalization.Normalize("", XmlWhitespaceRule.Replace, Operation()));
        Assert.Throws<ArgumentException>(() => DatatypeNormalization.TryNormalize("a", XmlWhitespaceRule.Preserve, [], Operation(), out _, out _));
        char[] input = ['a', 'b'];
        Assert.Throws<ArgumentException>(() => DatatypeNormalization.TryNormalize(input, XmlWhitespaceRule.Preserve, input, Operation(), out _, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => DatatypeNormalization.Normalize("", (XmlWhitespaceRule)3, Operation()));
        var shortRun = Operation(3);
        var destination = new char[2];
        Assert.False(DatatypeNormalization.TryNormalize("ab", XmlWhitespaceRule.Preserve, destination, shortRun, out var written, out var error));
        Assert.Equal(0, written);
        Assert.Same(DatatypeErrors.Stopped, error);
        Assert.Equal('a', destination[0]);
    }
    [Fact]
    public void OwnedNormalizationDetachesCallerAndReturnsPoolOnSuccessAndRefusal()
    {
        var input = "\t根😀  x ".ToCharArray();
        var snapshot = DatatypeNormalization.Normalize(input, XmlWhitespaceRule.Collapse, Operation());
        Array.Fill(input, 'z');
        Assert.Equal("根😀 x", snapshot);
        string longInput = new('a', 1024);
        Assert.Equal(longInput, DatatypeNormalization.Normalize(longInput, XmlWhitespaceRule.Preserve, Operation()));
        Assert.Throws<SchemaCompileException>(() => DatatypeNormalization.Normalize(longInput, XmlWhitespaceRule.Preserve, Operation(1025)));
        Assert.Equal(longInput, DatatypeNormalization.Normalize(longInput, XmlWhitespaceRule.Preserve, Operation()));
    }
    [Fact]
    public void CompositesSnapshotItemsPreserveSelectedAndDeclaredTypes()
    {
        var operation = Operation();
        var atomic = new Atom(Name("member"), DatatypePrimitive.Decimal, 7);
        var union = DatatypeValues.CreateUnion(Name("union"), atomic, operation);
        XmlDatatypeValue[] caller = [atomic, union];
        var list = DatatypeValues.CreateList(Name("list"), caller, operation);
        caller[0] = new Atom(Name(), DatatypePrimitive.Decimal, 8);
        Assert.Same(atomic, list.Items![0]);
        Assert.Same(union, list.Items[1]);
        Assert.Throws<NotSupportedException>(() => ((IList<XmlDatatypeValue>)list.Items).Clear());
        var derived = DatatypeValues.Retype(Name("derived"), union, operation);
        Assert.Equal(Name("derived"), derived.TypeName);
        Assert.Same(atomic, derived.SelectedMember);
        Assert.Equal(Name("member"), derived.SelectedMember!.TypeName);
        var retypedList = DatatypeValues.Retype(Name("derivedList"), list, operation);
        Assert.Same(list.Items, retypedList.Items);
        var retypedAtomic = DatatypeValues.Retype(Name("integerRestriction"), atomic, operation);
        Assert.Equal(XmlDatatypeVariety.Atomic, retypedAtomic.Variety);
        Assert.Null(retypedAtomic.Items);
        Assert.Null(retypedAtomic.SelectedMember);
        Assert.True(DatatypeValues.ValueEquals(atomic, retypedAtomic, operation));
        Assert.Equal(DatatypeValues.ValueHashCode(atomic, operation), DatatypeValues.ValueHashCode(retypedAtomic, operation));
        Assert.Same(atomic, DatatypeValues.Unwrap(DatatypeValues.Retype(Name(), retypedAtomic, operation), operation));
        Assert.Throws<ArgumentException>(() => DatatypeValues.CreateList(Name(), [list], operation));
        Assert.Throws<ArgumentException>(() => DatatypeValues.CreateList(Name(), [DatatypeValues.CreateUnion(Name(), list, operation)], operation));
    }
    [Fact]
    public void ListUnionAndRetypeBoundaryCostsAreChargedBeforePublication()
    {
        var atomic = new Atom(Name(), DatatypePrimitive.Decimal, 1);
        var exact = Operation(2, 2);
        var list = DatatypeValues.CreateList(Name(), [atomic], exact);
        Assert.Equal(0, exact.RemainingWork);
        Assert.False(exact.ChargeComponents(1, default));
        Assert.Throws<SchemaCompileException>(() => DatatypeValues.CreateList(Name(), [atomic], Operation(1, 2)));
        Assert.Throws<SchemaCompileException>(() => DatatypeValues.CreateList(Name(), [atomic], Operation(2, 1)));
        var unionOp = Operation(1, 1);
        DatatypeValues.CreateUnion(Name(), atomic, unionOp);
        Assert.Equal(0, unionOp.RemainingWork);
        var retype = Operation(1, 1);
        DatatypeValues.Retype(Name(), atomic, retype);
        Assert.Equal(0, retype.RemainingWork);
        Assert.Equal(1, DatatypeValues.ValueLength(list, Operation(1)));
        Assert.Equal(XmlValueOrder.Equal, DatatypeValues.Compare(list, list, Operation()));
        Assert.Null(DatatypeValues.TotalDigits(list, Operation()));
        Assert.Null(DatatypeValues.FractionDigits(list, Operation()));
    }
    [Fact]
    public void PrimitiveDomainsAndUnionListHashesFollowValueIdentity()
    {
        var op = Operation();
        var a = new Atom(Name("decimal"), DatatypePrimitive.Decimal, 1);
        var b = new Atom(Name("integer"), DatatypePrimitive.Decimal, 1);
        var other = new Atom(Name("double"), DatatypePrimitive.Double, 1);
        Assert.True(DatatypeValues.ValueEquals(a, b, op));
        Assert.Equal(XmlValueOrder.Equal, DatatypeValues.Compare(a, b, op));
        Assert.Equal(DatatypeValues.ValueHashCode(a, op), DatatypeValues.ValueHashCode(b, op));
        Assert.False(DatatypeValues.ValueEquals(a, other, op));
        Assert.Equal(XmlValueOrder.Indeterminate, DatatypeValues.Compare(a, other, op));
        var union = DatatypeValues.CreateUnion(Name(), b, op);
        Assert.Equal(DatatypeValues.ValueHashCode(a, op), DatatypeValues.ValueHashCode(union, op));
        var list = DatatypeValues.CreateList(Name("one"), [a, union], op);
        var list2 = DatatypeValues.CreateList(Name("two"), [b, a], op);
        Assert.True(DatatypeValues.ValueEquals(list, list2, op));
        Assert.Equal(DatatypeValues.ValueHashCode(list, op), DatatypeValues.ValueHashCode(list2, op));
        Assert.False(DatatypeValues.ValueEquals(list, a, op));
        Assert.Equal(XmlValueOrder.Indeterminate, DatatypeValues.Compare(list, a, op));
        Assert.Equal(1, DatatypeValues.TotalDigits(union, op));
        Assert.Equal(0, DatatypeValues.FractionDigits(union, op));
        Assert.Null(DatatypeValues.ValueLength(a, op));
        Assert.Throws<ArgumentException>(() => DatatypeValues.ValueEquals(a, new Atom(Name(), DatatypePrimitive.Decimal, 1, new Provider()), op));
    }
    [Fact]
    public void CancellationIncludesEmptyInputsCompositesAndProviderReturn()
    {
        using var canceled = new CancellationTokenSource();
        var op = OperationWithToken(canceled.Token);
        canceled.Cancel();
        Assert.Throws<OperationCanceledException>(() => DatatypeNormalization.Normalize("", XmlWhitespaceRule.Preserve, op));
        Assert.Throws<OperationCanceledException>(() => DatatypeValues.CreateList(Name(), [], op));
        var cancelProvider = new Provider { Callback = canceled.Cancel };
        using var middle = new CancellationTokenSource();
        cancelProvider.Callback = middle.Cancel;
        var mid = OperationWithToken(middle.Token);
        var atom = new Atom(Name(), DatatypePrimitive.Decimal, 1, cancelProvider);
        Assert.Throws<OperationCanceledException>(() => DatatypeValues.ValueEquals(atom, atom, mid));
    }
    [Fact]
    public void NestedUnionsUseIterativeTraversalAndOneBudget()
    {
        var op = Operation();
        XmlDatatypeValue value = new Atom(Name(), DatatypePrimitive.Decimal, 1);
        // Construction traverses existing members; no recursive stack walk.
        for (var i = 0; i < 128; i++)
        {
            value = DatatypeValues.CreateUnion(Name(), value, op);
        }

        var exact = Operation(129);
        Assert.Equal(1, DatatypeValues.TotalDigits(value, exact));
        Assert.Equal(0, exact.RemainingWork);
        Assert.Throws<SchemaCompileException>(() => DatatypeValues.TotalDigits(value, Operation(128)));
    }
    [Fact]
    public void BooleanDispatchBelongsToTextBinaryProvider()
    {
        Assert.Equal(DatatypeFamily.TextBinary, DatatypeTypes.All["boolean"].Family);
        var provider = new Provider(DatatypeFamily.TextBinary);
        var atom = new Atom(Name("boolean"), DatatypePrimitive.Boolean, 1, provider);
        var operation = Operation();
        var union = DatatypeValues.CreateUnion(Name(), atom, operation);
        Assert.True(DatatypeValues.ValueEquals(union, atom, operation));
        Assert.Equal(DatatypeValues.ValueHashCode(atom, operation), DatatypeValues.ValueHashCode(union, operation));
        Assert.Throws<ArgumentException>(() => new Atom(Name(), DatatypePrimitive.Boolean, 1));
    }
    [Fact]
    public void DirectUnwrapPollsCanceledStoppedAndExactEdgesWithoutDispatchWork()
    {
        var operation = Operation();
        var atom = new Atom(Name(), DatatypePrimitive.Decimal, 1);
        var list = DatatypeValues.CreateList(Name(), [], operation);
        var zeroEdge = Operation(1);
        Assert.Same(atom, DatatypeValues.Unwrap(atom, zeroEdge));
        Assert.Same(list, DatatypeValues.Unwrap(list, zeroEdge));
        Assert.Equal(1, zeroEdge.RemainingWork);
        using var canceled = new CancellationTokenSource();
        var cancelOperation = OperationWithToken(canceled.Token);
        canceled.Cancel();
        Assert.Throws<OperationCanceledException>(() => DatatypeValues.Unwrap(atom, cancelOperation));
        Assert.Throws<OperationCanceledException>(() => DatatypeValues.Unwrap(list, cancelOperation));
        var stopped = Operation(1);
        stopped.Charge(1, default);
        stopped.Charge(1, default);
        Assert.Throws<SchemaCompileException>(() => DatatypeValues.Unwrap(atom, stopped));
        Assert.Throws<SchemaCompileException>(() => DatatypeValues.Unwrap(list, stopped));
        var inner = DatatypeValues.CreateUnion(Name(), atom, operation);
        var outer = DatatypeValues.CreateUnion(Name(), inner, operation);
        var exact = Operation(2);
        Assert.Same(atom, DatatypeValues.Unwrap(outer, exact));
        Assert.Equal(0, exact.RemainingWork);
        Assert.Throws<SchemaCompileException>(() => DatatypeValues.Unwrap(outer, Operation(1)));
    }
    [Fact]
    public void NormalizationIsCultureIndependent()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new("tr-TR");
            Assert.Equal("I İ 根😀", DatatypeNormalization.Normalize("\tI  İ 根😀\r", XmlWhitespaceRule.Collapse, Operation()));
        }
        finally { CultureInfo.CurrentCulture = original; }
    }
    private sealed class Atom(SchemaName type, DatatypePrimitive primitive, int payload, Provider? provider = null) : DatatypeAtomicValue(type, primitive, provider ?? CoreTests.Provider.Shared)
    {
        internal int Payload => payload;
    }
    // A test-only immutable payload oracle; no lexical parser is supplied or invoked.
    private sealed class Provider(DatatypeFamily family = DatatypeFamily.Numeric) : IDatatypeFamilyProvider
    {
        internal static readonly Provider Shared = new(); internal Action? Callback
        {
            get; set;
        }
        public DatatypeFamily Family => family;
        public bool TryParse(DatatypeMetadata type, ReadOnlySpan<char> normalized, SchemaNamespaceContext context, ISchemaOperation operation, out DatatypeAtomicValue? value, out XmlDatatypeError? error) => throw new InvalidOperationException("Test has no parser.");
        public bool ValueEquals(DatatypeAtomicValue left, DatatypeAtomicValue right, ISchemaOperation operation)
        {
            Callback?.Invoke();
            return ((Atom)left).Payload == ((Atom)right).Payload;
        }
        public XmlValueOrder Compare(DatatypeAtomicValue left, DatatypeAtomicValue right, ISchemaOperation operation) => (XmlValueOrder)Math.Sign(((Atom)left).Payload - ((Atom)right).Payload);
        public int ValueHashCode(DatatypeAtomicValue value, ISchemaOperation operation) => ((Atom)value).Payload;
        public long? ValueLength(DatatypeAtomicValue value, ISchemaOperation operation) => null;
        public int? TotalDigits(DatatypeAtomicValue value, ISchemaOperation operation) => 1;
        public int? FractionDigits(DatatypeAtomicValue value, ISchemaOperation operation) => 0;
    }
}
