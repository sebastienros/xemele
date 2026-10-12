using Xemele.Validation;
using Xunit;
namespace Xemele.Tests.M8RngValidate;

public sealed class BudgetTests
{
    internal static RngProgram Program(string source)
    { var options = new SchemaCompileOptions(); var op = new SchemaCompileOperation(options); return RngProgram.Finalize(new RelaxNgCompiler().Compile(XmlDocument.Parse(source), options, op), op); }
    internal sealed class Observed(ValidationOptions? options = null, Action<int>? callback = null) : ISchemaOperation
    {
        private readonly SchemaValidationOperation inner = new(options); internal long Work; private int calls;
        public XmlResourceLimits Limits => inner.Limits; public CancellationToken CancellationToken => inner.CancellationToken;
        public bool IsComplete => inner.IsComplete; public long? RemainingWork => inner.RemainingWork;
        public bool Charge(long work, XmlLocation at) { callback?.Invoke(++calls); bool okay = inner.Charge(work, at); if (okay) Work += work; return okay; }
        public bool ChargeComponents(int count, XmlLocation at) => inner.ChargeComponents(count, at);
        public bool AddDiagnostic(XmlDiagnostic d) => inner.AddDiagnostic(d);
        public void Refuse(string code, string message, XmlLocation at) => inner.Refuse(code, message, at);
        internal ValidationResult Result() => inner.Complete();
    }
    [Fact]
    public void ActualSharedWorkHasExactBoundaryWithoutResetAcrossDatatypeAndRegex()
    {
        var program = Program(EvaluationTests.Element("<data type='string'><param name='pattern'>a+</param></data>", "datatypeLibrary='" + EvaluationTests.Xsd + "'"));
        var doc = XmlDocument.Parse("<r>aaa</r>", cancellationToken: TestContext.Current.CancellationToken);
        var measured = new Observed(); RelaxNgEvaluator.EvaluateCore(program, doc, measured); Assert.True(measured.Result().IsValid);
        Assert.True(measured.Work > 20);
        var exact = new Observed(new() { MaxOperations = measured.Work }); RelaxNgEvaluator.EvaluateCore(program, doc, exact); Assert.True(exact.Result().IsValid);
        foreach (var limits in new[] { new ValidationOptions { MaxOperations = measured.Work - 1 }, new ValidationOptions { Limits = new() { EvaluationWork = measured.Work - 1 } } })
        { var shortOp = new Observed(limits); RelaxNgEvaluator.EvaluateCore(program, doc, shortOp); Assert.False(shortOp.Result().IsComplete); Assert.Equal("VALIDATION.Limit", Assert.Single(shortOp.Result().Diagnostics).Code); }
    }
    [Fact]
    public void CompileBudgetIncludesParsingRegistryFinalizationAndEveryOccurrence()
    {
        string source = EvaluationTests.Element("<data type='int'><param name='minInclusive'>1</param></data>", "datatypeLibrary='" + EvaluationTests.Xsd + "'");
        long low = 1, high = 100_000;
        while (low < high)
        {
            long mid = (low + high) / 2;
            try { RelaxNgSchema.Compile(source, new() { MaxOperations = mid }); high = mid; }
            catch (SchemaCompileException e) { Assert.Equal("SCHEMA.Limit", e.Diagnostics[^1].Code); low = mid + 1; }
        }
        Assert.True(RelaxNgSchema.Compile(source, new() { MaxOperations = low }).Validate("<r>1</r>").IsValid);
        Assert.Equal("SCHEMA.Limit", Assert.Throws<SchemaCompileException>(() => RelaxNgSchema.Compile(source, new() { MaxOperations = low - 1 })).Diagnostics[^1].Code);
        Assert.Equal("SCHEMA.Limit", Assert.Throws<SchemaCompileException>(() => RelaxNgSchema.Compile(source, new() { MaxComponents = 2 })).Diagnostics[^1].Code);
    }
    [Fact]
    public void MutationAndCancellationDuringTraversalFailDeterministically()
    {
        var program = Program(EvaluationTests.Element("<text/>"));
        var doc = XmlDocument.Parse("<r>text</r>", cancellationToken: TestContext.Current.CancellationToken);
        var mutation = new Observed(callback: count => { if (count == 5) doc.RootElement.SetAttribute("a", "changed"); });
        Assert.Throws<InvalidOperationException>(() => RelaxNgEvaluator.EvaluateCore(program, doc, mutation));
        using var cancel = new CancellationTokenSource();
        var cancellation = new Observed(new() { CancellationToken = cancel.Token }, count => { if (count == 5) cancel.Cancel(); });
        Assert.ThrowsAny<OperationCanceledException>(() => RelaxNgEvaluator.EvaluateCore(program, doc, cancellation));
    }
    [Fact]
    public void UnlimitedInputQuotasStillStopAtFiniteMatcherRepresentation()
    {
        var recursive = RelaxNgSchema.Compile("<grammar xmlns='" + EvaluationTests.Ns + "'><start><ref name='n'/></start><define name='n'><element name='n'><optional><ref name='n'/></optional></element></define></grammar>");
        string deep = string.Concat(Enumerable.Repeat("<n>", 400)) + string.Concat(Enumerable.Repeat("</n>", 400));
        var document = XmlDocument.Parse(deep, new() { Limits = new() { Depth = null } }, TestContext.Current.CancellationToken);
        var result = recursive.Validate(document, new() { MaxOperations = null, Limits = new() { Depth = null, EvaluationWork = null } });
        Assert.False(result.IsComplete); Assert.Equal("VALIDATION.Limit", Assert.Single(result.Diagnostics).Code);
        string accepted = string.Concat(Enumerable.Repeat("<n>", 256)) + string.Concat(Enumerable.Repeat("</n>", 256));
        Assert.True(recursive.Validate(accepted).IsValid);
    }
    [Fact]
    public void InputDepthAttributesTokensCoalescingAndFiniteStackAreBounded()
    {
        var schema = EvaluationTests.Schema("<text/>");
        var doc = XmlDocument.Parse("<r>abc</r>", cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(schema.Validate(doc, new() { Limits = new() { TokenCharacters = 3 } }).IsValid);
        Assert.False(schema.Validate(doc, new() { Limits = new() { TokenCharacters = 2 } }).IsComplete);
        Assert.False(schema.Validate("<r>a<![CDATA[bc]]></r>", new() { Limits = new() { TokenCharacters = 2 } }).IsComplete);
        var attributes = EvaluationTests.Schema("<zeroOrMore><attribute><anyName/><text/></attribute></zeroOrMore><empty/>");
        Assert.False(attributes.Validate(XmlDocument.Parse("<r a='1' b='2'/>", cancellationToken: TestContext.Current.CancellationToken), new() { Limits = new() { AttributesPerElement = 1 } }).IsComplete);
        var recursive = RelaxNgSchema.Compile("<grammar xmlns='" + EvaluationTests.Ns + "'><start><ref name='n'/></start><define name='n'><element name='n'><optional><ref name='n'/></optional></element></define></grammar>");
        Assert.True(recursive.Validate("<n><n/></n>", new() { Limits = new() { Depth = 2 } }).IsValid);
        Assert.False(recursive.Validate(XmlDocument.Parse("<n><n/></n>", cancellationToken: TestContext.Current.CancellationToken), new() { Limits = new() { Depth = 1 } }).IsComplete);
        Assert.False(recursive.Validate("<n><n><n/></n></n>", new() { MaxOperations = 5, Limits = new() { Diagnostics = 1 } }).IsComplete);
    }
}
