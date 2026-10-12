using Xunit;
using Xemele.Validation;
using static Xemele.Tests.M8ConstraintLexical.ConstraintTests;
namespace Xemele.Tests.M8ConstraintLexical;

public sealed class OperationTests
{
    [Theory]
    [InlineData("int", "01")][InlineData("decimal", ".0001")]
    [InlineData("dateTime", "2000-03-01T00:00:00.001+01:00")][InlineData("date", "2000-03-01+14:00")]
    [InlineData("hexBinary", "abcd")][InlineData("QName", " p:a ")]
    public void ExactWorkAndComponentReservationsSucceedAndOneShortReturnsNoResult(string name, string input)
    {
        var r = new XmlDatatypeRegistry(); var e = new XmlFacetEvaluator(); var type = r.FindType(DatatypeTypes.Name(name))!;
        Assert.True(e.ValidateDefinition(type, r, Op())); var ledger = new Ledger();
        Assert.True(DatatypeConstraintRepresentation.TryCreate(type, input, Context, r, e, ledger, out _, out _));
        Assert.True(DatatypeConstraintRepresentation.TryCreate(type, input, Context, r, e, Op(ledger.Work, ledger.Components), out _, out _));
        foreach (var op in new[] { Op(ledger.Work - 1), Op(null, ledger.Components - 1) })
        {
            Assert.False(DatatypeConstraintRepresentation.TryCreate(type, input, Context, r, e, op, out var result, out var error));
            Assert.Null(result); Assert.Same(DatatypeErrors.Stopped, error); Assert.False(op.IsComplete);
        }
    }
    [Fact]
    public void WriterHasIndependentCapacityAndCopyWorkWitnesses()
    {
        var ledger = new Ledger(); Span<char> output = stackalloc char[3];
        var writer = new ConstraintWriter(output, true, ledger); writer.Append("abc"); Assert.Equal(3, ledger.Work); Assert.Equal(3, writer.Length); Assert.True(output.SequenceEqual("abc"));
        Assert.Equal((int.MaxValue - 2) / 2, ConstraintWriter.CheckedLength((int.MaxValue - 2) / 2, Op()));
        foreach (long invalid in new[] { -1L, (long)(int.MaxValue - 2) / 2 + 1, long.MaxValue })
        {
            var op = Op(); Assert.Throws<ConstraintOperation.Stopped>(() => ConstraintWriter.CheckedLength(invalid, op)); Assert.False(op.IsComplete);
        }
    }
    [Theory]
    [InlineData("string")][InlineData("decimal")][InlineData("dateTime")][InlineData("base64Binary")]
    public void CancellationIsPolledInsideFormattingWithUnlimitedQuotas(string name)
    {
        string input = name switch { "dateTime" => "2000-01-01T00:00:00." + new string('1', 8192) + "Z", "base64Binary" => new string('A', 8192), _ => new string('1', 8192) };
        var r = new XmlDatatypeRegistry(); var e = new XmlFacetEvaluator(); var type = r.FindType(DatatypeTypes.Name(name))!;
        Assert.True(e.ValidateDefinition(type, r, Op())); var validation = new Ledger();
        Assert.True(e.TryValidate(type, input, Context, r, validation, out _, out _));
        using var cancel = new CancellationTokenSource(); var op = new Ledger(cancel, validation.Work + 1024);
        Assert.Throws<OperationCanceledException>(() => DatatypeConstraintRepresentation.TryCreate(type, input, Context, r, e, op, out _, out _));
        Assert.True(op.Work > validation.Work); Assert.Null(op.RemainingWork);
    }
    [Fact]
    public void InvalidAuthoredInputRejectsCompletelyAndCancellationAtEntryPropagates()
    {
        var r = new XmlDatatypeRegistry(); var type = r.FindType(DatatypeTypes.Name("int"))!; var e = new XmlFacetEvaluator();
        foreach (string input in new[] { "x", "1.0", "\ud800" })
        { var op = Op(); Assert.False(DatatypeConstraintRepresentation.TryCreate(type, input, Context, r, e, op, out var rep, out var error)); Assert.Null(rep); Assert.NotNull(error); Assert.True(op.IsComplete); }
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        Assert.Throws<OperationCanceledException>(() => DatatypeConstraintRepresentation.TryCreate(type, "01", Context, r, e, new Ledger(cancel), out _, out _));
    }
    [Fact]
    public void CallerExceptionDuringFormattingRetainsProvenance()
    {
        var r = new XmlDatatypeRegistry(); var type = r.FindType(DatatypeTypes.Name("decimal"))!; var e = new XmlFacetEvaluator(); Assert.True(e.ValidateDefinition(type, r, Op()));
        var parse = new Ledger(); Assert.True(e.TryValidate(type, "123.45", Context, r, parse, out _, out _));
        var marker = new SchemaCompileException([new("TEST.Caller", XmlDiagnosticSeverity.Fatal, "Caller marker", default)]);
        var op = new Ledger { ThrowAt = parse.Work + 3, Marker = marker };
        Assert.Same(marker, Assert.Throws<SchemaCompileException>(() => DatatypeConstraintRepresentation.TryCreate(type, "123.45", Context, r, e, op, out _, out _)));
    }
    internal sealed class Ledger(CancellationTokenSource? cancellation = null, long cancelAt = long.MaxValue) : ISchemaOperation
    {
        internal long Work; internal int Components; internal long ThrowAt = long.MaxValue; internal SchemaCompileException? Marker;
        public XmlResourceLimits Limits { get; } = new() { EvaluationWork = null };
        public CancellationToken CancellationToken => cancellation?.Token ?? default;
        public bool IsComplete => true; public long? RemainingWork => null;
        public bool Charge(long work, XmlLocation location) { Work = checked(Work + work); if (Work >= ThrowAt && Marker is not null) throw Marker; if (Work >= cancelAt) cancellation?.Cancel(); CancellationToken.ThrowIfCancellationRequested(); return true; }
        public bool ChargeComponents(int count, XmlLocation location) { Components = checked(Components + count); CancellationToken.ThrowIfCancellationRequested(); return true; }
        public bool AddDiagnostic(XmlDiagnostic diagnostic) => throw new InvalidOperationException(diagnostic.Message);
        public void Refuse(string code, string message, XmlLocation location) => throw new InvalidOperationException(message);
    }
}
