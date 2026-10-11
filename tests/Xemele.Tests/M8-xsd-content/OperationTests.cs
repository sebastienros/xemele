using System.Numerics;
using Xemele.Validation;
using Xunit;
namespace Xemele.Tests.M8XsdContent;

public sealed class OperationTests
{
    internal sealed class Ledger(long? workLimit = null, int? componentLimit = null, CancellationToken token = default) : ISchemaOperation
    {
        public XmlResourceLimits Limits { get; } = new();
        public CancellationToken CancellationToken => token;
        public bool IsComplete { get; private set; } = true;
        public long? RemainingWork => workLimit - Work;
        internal long Work; internal int Components, Calls;
        internal readonly List<XmlDiagnostic> Diagnostics = [];
        internal Action<int>? OnCharge;
        public bool Charge(long work, XmlLocation at)
        {
            token.ThrowIfCancellationRequested(); OnCharge?.Invoke(++Calls); token.ThrowIfCancellationRequested();
            if (!IsComplete) return false;
            if (workLimit is { } cap && work > cap - Work) { Refuse("SCHEMA.Limit", "work", at); return false; }
            Work = checked(Work + work); return true;
        }
        public bool ChargeComponents(int count, XmlLocation at)
        {
            token.ThrowIfCancellationRequested();
            if (!IsComplete) return false;
            if (componentLimit is { } cap && count > cap - Components) { Refuse("SCHEMA.Limit", "components", at); return false; }
            Components = checked(Components + count); return true;
        }
        public bool AddDiagnostic(XmlDiagnostic d) { token.ThrowIfCancellationRequested(); Diagnostics.Add(d); return IsComplete; }
        public void Refuse(string code, string message, XmlLocation at) { Diagnostics.Add(new(code, XmlDiagnosticSeverity.Fatal, message, at)); IsComplete = false; }
    }
    private static XsdCompiledSchema Schema() => ContentTests.Schema("<xs:complexType name='T'><xs:sequence minOccurs='0' maxOccurs='unbounded'><xs:element name='a' minOccurs='0'/><xs:element name='b' minOccurs='0'/></xs:sequence></xs:complexType>");
    [Fact]
    public void ExactCompilerWorkAndComponentBoundaries()
    {
        var schema = Schema(); var measured = new Ledger(); ContentTests.Program(schema, operation: measured);
        Assert.True(measured.Work > 0); Assert.True(measured.Components > 0);
        Assert.NotNull(ContentTests.Program(schema, operation: new Ledger(measured.Work, measured.Components)));
        Assert.Throws<SchemaCompileException>(() => ContentTests.Program(schema, operation: new Ledger(measured.Work - 1)));
        Assert.Throws<SchemaCompileException>(() => ContentTests.Program(schema, operation: new Ledger(componentLimit: measured.Components - 1)));
    }
    [Fact]
    public void ExactCursorWorkAndComponentBoundaries()
    {
        var program = ContentTests.Program(Schema()); var matcher = new XsdContentMatcher(); var measured = new Ledger();
        void Run(ISchemaOperation op)
        { var cursor = matcher.CreateCursor(program); Assert.True(cursor.Accept(new("", "a"), op, out _, out _)); Assert.True(cursor.Accept(new("", "b"), op, out _, out _)); Assert.True(cursor.Complete(op)); }
        Run(measured); Assert.True(measured.Work > 0); Assert.True(measured.Components > 0);
        Run(new Ledger(measured.Work, measured.Components));
        Assert.Throws<SchemaCompileException>(() => Run(new Ledger(measured.Work - 1)));
        Assert.Throws<SchemaCompileException>(() => Run(new Ledger(componentLimit: measured.Components - 1)));
    }
    [Fact]
    public void CancellationWithoutWorkCapsAndCallerExceptionsArePreserved()
    {
        var schema = Schema(); using var canceled = new CancellationTokenSource(); canceled.Cancel();
        Assert.Throws<OperationCanceledException>(() => ContentTests.Program(schema, operation: new Ledger(token: canceled.Token)));
        using var during = new CancellationTokenSource(); var ledger = new Ledger(token: during.Token) { OnCharge = n => { if (n == 20) during.Cancel(); } };
        Assert.Throws<OperationCanceledException>(() => ContentTests.Program(schema, operation: ledger)); Assert.Equal(20, ledger.Calls);
        var caller = new InvalidOperationException("caller"); var exploding = new Ledger { OnCharge = _ => throw caller };
        Assert.Same(caller, Assert.Throws<InvalidOperationException>(() => ContentTests.Program(schema, operation: exploding)));
        var cursor = new XsdContentMatcher().CreateCursor(ContentTests.Program(schema));
        Assert.Same(caller, Assert.Throws<InvalidOperationException>(() => cursor.Accept(new("", "a"), exploding, out _, out _)));
    }
    [Fact]
    public void CancellationDuringCursorClosureIsBoundedAndCursorFaults()
    {
        var program = ContentTests.Program(Schema()); var cursor = new XsdContentMatcher().CreateCursor(program);
        using var source = new CancellationTokenSource(); var op = new Ledger(token: source.Token) { OnCharge = n => { if (n == 10) source.Cancel(); } };
        Assert.Throws<OperationCanceledException>(() => cursor.Complete(op)); Assert.Equal(10, op.Calls);
        Assert.False(cursor.Complete(new Ledger()));
    }
    [Fact]
    public void RestrictionDiagnosticRetainsActualParticleLocation()
    {
        var schema = ContentTests.Schema("\n<xs:complexType name='T'>\n<xs:choice>\n<xs:element name='a'/>\n<xs:element name='a'/>\n</xs:choice>\n</xs:complexType>");
        var error = Assert.Throws<SchemaCompileException>(() => ContentTests.Program(schema));
        Assert.Equal(ContentTests.Type(schema).Particle!.Location, error.Diagnostics[0].Location); Assert.True(error.Diagnostics[0].Location.LineNumber > 0);
    }
    [Fact]
    public void HugeBoundsUseConstantGraphAndCursorShape()
    {
        XsdCompiledSchema Build(string value) => ContentTests.Schema($"<xs:complexType name='T'><xs:sequence><xs:element name='a' minOccurs='0' maxOccurs='{value}'/></xs:sequence></xs:complexType>");
        var small = Build("100"); var huge = Build("1" + new string('0', 1000)); var a = new Ledger(); var b = new Ledger();
        var ps = Assert.IsType<CompiledContent>(ContentTests.Program(small, operation: a)); var ph = Assert.IsType<CompiledContent>(ContentTests.Program(huge, operation: b));
        Assert.Equal(ps.Nodes.Length, ph.Nodes.Length); Assert.Equal(a.Components, b.Components); Assert.True(b.Work > a.Work);
        var ca = new Ledger(); var cb = new Ledger(); var matcher = new XsdContentMatcher();
        Assert.True(matcher.CreateCursor(ps).Accept(new("", "a"), ca, out _, out _)); Assert.True(matcher.CreateCursor(ph).Accept(new("", "a"), cb, out _, out _)); Assert.Equal(ca.Components, cb.Components);
    }
    [Theory]
    [InlineData("<xs:sequence maxOccurs='0' minOccurs='0'><xs:choice><xs:element name='a'/><xs:element name='a' type='xs:string'/></xs:choice></xs:sequence>")]
    [InlineData("<xs:all><xs:element name='a' minOccurs='0' maxOccurs='0'/></xs:all>")]
    public void RemovedZeroOccurrenceParticlesDoNotMatchOrCauseAmbiguity(string content)
    {
        var program = ContentTests.Program(ContentTests.Schema($"<xs:complexType name='T'>{content}</xs:complexType>"));
        Assert.True(ContentTests.Accepts(program)); Assert.False(ContentTests.Accepts(program, "a"));
    }
    [Fact]
    public void LargeIntegerAlgebraHasIndependentResultsAndBoundedCancellation()
    {
        BigInteger a = BigInteger.Pow(10, 180) + 17, b = BigInteger.Pow(10, 160) + 31;
        var op = new Ledger(); Assert.Equal(a * b, ContentIntegers.Multiply(a, b, op, default)); Assert.Equal(a + b, ContentIntegers.Add(a, b, op, default));
        Assert.Equal(1, ContentIntegers.Compare(a, b, op, default)); Assert.Equal(0, ContentIntegers.Compare(a, a, op, default));
        using var source = new CancellationTokenSource(); var cancel = new Ledger(token: source.Token) { OnCharge = n => { if (n == 50) source.Cancel(); } };
        Assert.Throws<OperationCanceledException>(() => ContentIntegers.Multiply(a, b, cancel, default)); Assert.Equal(50, cancel.Calls);
    }
    [Fact]
    public void DeclarationGraphTraversalDoesNotUseTheClrCallStack()
    {
        var schema = ContentTests.Schema("<xs:complexType name='T'/>"); var old = ContentTests.Type(schema);
        XsdParticle p = new(new XsdWildcardTerm(new(true, [], XsdProcessContents.Lax)), 0, null, default);
        for (int i = 0; i < 2048; i++) p = new(new XsdGroupTerm(XsdCompositor.Sequence, [p]), 1, 1, default);
        var replacement = old with { Particle = p, ContentKind = XsdContentKind.ElementOnly };
        var types = schema.Types.Select(t => ReferenceEquals(t, old) ? replacement : t).ToArray();
        var graph = schema with { Types = types };
        Assert.NotNull(new XsdContentCompiler().Compile(graph, replacement, new Ledger()));
    }

}
