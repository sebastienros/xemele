using Xunit;
using Xemele.Validation;
using static Xemele.Tests.M8Facets.FacetTests;
namespace Xemele.Tests.M8Facets;

public sealed class OperationTests
{
    [Fact]
    public void DefinitionPreparationHonorsExactAndOneShortWorkAndComponents()
    {
        var type = Restrict("quota", "decimal", F(XmlFacetKind.MinInclusive, "1"), F(XmlFacetKind.MaxInclusive, "20"), F(XmlFacetKind.TotalDigits, "2"), F(XmlFacetKind.Pattern, "[0-9]+"));
        var registry = new XmlDatatypeRegistry([type], Op()); var counts = new Ledger();
        Assert.True(new XmlFacetEvaluator().ValidateDefinition(type, registry, counts)); Assert.True(counts.Work > 0); Assert.True(counts.Components > 0);
        Assert.True(new XmlFacetEvaluator().ValidateDefinition(type, registry, Op(counts.Work, counts.Components)));
        foreach (var shortOp in new[] { Op(counts.Work - 1, null), Op(null, counts.Components - 1) })
        { Assert.False(new XmlFacetEvaluator().ValidateDefinition(type, registry, shortOp)); Assert.False(shortOp.IsComplete); }
    }
    [Theory]
    [InlineData("integer", "12")][InlineData("string", "a根😀z")][InlineData("hexBinary", "aaff")]
    public void PreparedValidationHonorsExactAndOneShortBudgets(string parent, string input)
    {
        var type = Restrict("quota", parent, F(XmlFacetKind.Pattern, ".*")); var (e, r) = Ready(type);
        var count = new Ledger(); Assert.True(e.TryValidate(type, input, Context, r, count, out _, out _));
        Assert.True(e.TryValidate(type, input, Context, r, Op(count.Work, count.Components), out _, out _));
        foreach (var op in new[] { Op(count.Work - 1, null), Op(null, count.Components - 1) })
        {
            Assert.False(e.TryValidate(type, input, Context, r, op, out var value, out var error));
            Assert.False(op.IsComplete); Assert.Null(value); Assert.Same(DatatypeErrors.Stopped, error);
        }
    }
    [Fact]
    public void InvalidLexicalAndInvalidFacetAreCompleteWhileStoppedWorkIsIncomplete()
    {
        var type = Restrict("reject", "integer", F(XmlFacetKind.MinInclusive, "2")); var (e,r) = Ready(type);
        foreach (string lexical in new[] { "x", "1", "\ud800" })
        { var op = Op(); Assert.False(e.TryValidate(type, lexical, Context, r, op, out var v, out var error)); Assert.True(op.IsComplete); Assert.Null(v); Assert.NotNull(error); Assert.NotSame(DatatypeErrors.Stopped, error); }
        var tiny = Op(1); Assert.False(e.TryValidate(type, "2", Context, r, tiny, out _, out var stopped)); Assert.Same(DatatypeErrors.Stopped, stopped); Assert.False(tiny.IsComplete);
        Assert.True(e.TryValidate(type, "2", Context, r, Op(), out _, out _));
    }
    [Fact]
    public void RegexLimitCannotBeReportedAsAnOrdinaryMismatch()
    {
        var type = Restrict("regex", "string", F(XmlFacetKind.Pattern, ".*")); var (e,r) = Ready(type);
        var op = new SchemaCompileOperation(new() { Limits = new() { TokenCharacters = 1 }, MaxOperations = null, MaxComponents = null });
        Assert.False(e.TryValidate(type, "ab", Context, r, op, out var value, out var error));
        Assert.Null(value); Assert.Same(DatatypeErrors.Stopped, error); Assert.False(op.IsComplete);
    }
    [Fact]
    public void RegexAndDatatypeWorkSharesTheSameLedgerAcrossRepeatedValues()
    {
        var type = Restrict("shared", "integer", F(XmlFacetKind.Pattern, "[0-9]+")); var (e,r) = Ready(type);
        var count = new Ledger(); Assert.True(e.TryValidate(type, "12", Context, r, count, out _, out _));
        var shared = Op(count.Work * 2 - 1);
        Assert.True(e.TryValidate(type, "12", Context, r, shared, out _, out _));
        Assert.False(e.TryValidate(type, "12", Context, r, shared, out _, out var error)); Assert.Same(DatatypeErrors.Stopped, error);
    }
    [Fact]
    public void CancellationIsPolledDuringDefinitionAndValidationWithUnlimitedQuotas()
    {
        var type = Restrict("cancel", "string", F(XmlFacetKind.Pattern, "a.*")); var (e,r) = Ready(type);
        using var cancellation = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => e.TryValidate(type, "a" + new string('x', 4096), Context, r, new Ledger(cancellation, 100), out _, out _));
        using var compile = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => new XmlFacetEvaluator().ValidateDefinition(type, r, new Ledger(compile, 25)));
        using var entry = new CancellationTokenSource();
        var cancelled = new SchemaCompileOperation(new() { CancellationToken = entry.Token }); entry.Cancel();
        Assert.Throws<OperationCanceledException>(() => e.TryValidate(type, "a", Context, r, cancelled, out _, out _));
        Assert.True(e.TryValidate(type, "a", Context, r, Op(), out _, out _));
    }
    [Theory]
    [InlineData(false)][InlineData(true)]
    public void CallerExceptionsIncludingSchemaExceptionsPropagateUnchanged(bool incomplete)
    {
        var type = Restrict("caller", "integer"); var (e,r) = Ready(type);
        var marker = new SchemaCompileException([new("TEST.Caller", XmlDiagnosticSeverity.Fatal, "Caller marker.", default)]);
        var ledger = new Ledger(); ledger.Callback = () => { if (ledger.Work >= 10) { if (incomplete) ledger.Complete = false; throw marker; } };
        Assert.Same(marker, Assert.Throws<SchemaCompileException>(() => e.TryValidate(type, "123", Context, r, ledger, out _, out _)));
        var compile = new Ledger(); compile.Callback = () => { if (compile.Work >= 10) throw marker; };
        Assert.Same(marker, Assert.Throws<SchemaCompileException>(() => e.ValidateDefinition(type, r, compile)));
    }
    [Fact]
    public async Task PreparedEvaluatorSupportsConcurrentRunsAndOwnedSnapshots()
    {
        var prefix = new Dictionary<string,string> { ["p"] = "urn:retained" };
        var context = new SchemaNamespaceContext(prefix, "", null);
        XmlFacet[] facets = [F(XmlFacetKind.Enumeration, "p:a", context: context)];
        var type = Restrict("snapshot", "QName", facets); var (e,r) = Ready(type);
        prefix["p"] = "urn:changed"; facets[0] = F(XmlFacetKind.Enumeration, "p:b");
        var instance = new SchemaNamespaceContext(new Dictionary<string,string> { ["q"] = "urn:retained" }, "", null);
        await Task.WhenAll(Enumerable.Range(0, 16).Select(worker => Task.Run(() =>
        {
            for (int i = 0; i < 16; i++)
            {
                char[] input = "q:a".ToCharArray(); Assert.True(e.TryValidate(type, input, instance, r, Op(), out var value, out _)); Array.Fill(input, '!');
                Assert.Equal(type.Name, value!.TypeName);
            }
        }, TestContext.Current.CancellationToken)));
    }
    [Fact]
    public async Task ConcurrentPreparationPublishesOnlyCompletePlans()
    {
        var type = Restrict("prepare", "string", F(XmlFacetKind.Pattern, "a+")); var r = new XmlDatatypeRegistry([type], Op()); var e = new XmlFacetEvaluator();
        await Task.WhenAll(Enumerable.Range(0, 8).Select(worker => Task.Run(() => Assert.True(e.ValidateDefinition(type, r, Op())), TestContext.Current.CancellationToken)));
        Assert.True(e.TryValidate(type, "aa", Context, r, Op(), out _, out _));
        Assert.False(e.TryValidate(type, "b", Context, r, Op(), out _, out _));
    }
    [Fact]
    public void CacheIdentityIncludesBothRegistryAndDefinition()
    {
        var parent = Restrict("parent", "integer", F(XmlFacetKind.MinInclusive, "2"));
        var child = Restrict("child", "integer") with { BaseType = parent.Name };
        var r = new XmlDatatypeRegistry([parent, child], Op()); var other = new XmlDatatypeRegistry([parent with { Facets = [F(XmlFacetKind.MinInclusive, "3")] }, child], Op());
        var e = new XmlFacetEvaluator(); Assert.True(e.ValidateDefinition(child, r, Op())); Assert.True(e.ValidateDefinition(child, other, Op()));
        Assert.True(e.TryValidate(child, "2", Context, r, Op(), out _, out _)); Assert.False(e.TryValidate(child, "2", Context, other, Op(), out _, out _));
    }
    [Fact]
    public void GraphDepthIsBoundedBeforeStackExhaustion()
    {
        var definitions = new List<XmlDatatypeDefinition>(); SchemaName previous = DatatypeTypes.Name("string");
        for (int i = 0; i < 300; i++) { var type = Restrict("t" + i, "string") with { BaseType = previous }; definitions.Add(type); previous = type.Name; }
        var registry = new XmlDatatypeRegistry(definitions, Op()); var op = Op();
        Assert.False(new XmlFacetEvaluator().ValidateDefinition(definitions[^1], registry, op)); Assert.False(op.IsComplete);
    }
    [Fact]
    public void ExplicitRngAdapterSharesExactPreparationQuotasAndReturnsNoPartialDefinition()
    {
        var registry=new XmlDatatypeRegistry();var name=new SchemaName(DatatypeTypes.RngNamespace,"decimal");XmlFacet[] facets=[F(XmlFacetKind.MinInclusive,"1")];
        var count=new Ledger();Assert.True(new RngDatatypeFacetAdapter(new XmlFacetEvaluator()).ValidateParameters(name,facets,default,registry,count,out _));
        Assert.True(new RngDatatypeFacetAdapter(new XmlFacetEvaluator()).ValidateParameters(name,facets,default,registry,Op(count.Work,count.Components),out _));
        foreach(var op in new[] {Op(count.Work-1,null),Op(null,count.Components-1)})
        {Assert.False(new RngDatatypeFacetAdapter(new XmlFacetEvaluator()).ValidateParameters(name,facets,default,registry,op,out var definition));Assert.Null(definition);Assert.False(op.IsComplete);}
    }
    internal sealed class Ledger(CancellationTokenSource? cancellation = null, long cancelAt = long.MaxValue) : ISchemaOperation
    {
        internal long Work; internal int Components; internal Action? Callback; internal bool Complete = true;
        public XmlResourceLimits Limits { get; } = new() { EvaluationWork = null };
        public CancellationToken CancellationToken => cancellation?.Token ?? default;
        public bool IsComplete => Complete; public long? RemainingWork => null;
        public bool Charge(long work, XmlLocation location) { Work += work; Callback?.Invoke(); if (Work >= cancelAt) cancellation?.Cancel(); CancellationToken.ThrowIfCancellationRequested(); return true; }
        public bool ChargeComponents(int count, XmlLocation location) { Components += count; CancellationToken.ThrowIfCancellationRequested(); return true; }
        public bool AddDiagnostic(XmlDiagnostic diagnostic) => throw new InvalidOperationException("Unexpected diagnostic: " + diagnostic.Message);
        public void Refuse(string code, string message, XmlLocation location) => throw new InvalidOperationException("Unexpected refusal: " + message);
    }
}

public sealed class PrimitiveTests
{
    [Fact]
    public void PreparedLengthHasAnIndependentTenUnitWorkCalculation()
    {
        var type=Restrict("length","string",F(XmlFacetKind.Length,"2"));var(e,r)=Ready(type);
        Assert.True(e.TryValidate(type,"根😀",Context,r,Op(),out var value,out _));
        // Lookup1 + three level visits3 + two effective facet visits2 + length dispatch1
        // + one derived-value unwrap1 + precomputed text-family measure1 + native count comparison1 = 10.
        var exact=Op(10);Assert.True(e.CheckPreparedValue(type,"根😀",value!,r,exact));Assert.Equal(0,exact.RemainingWork);
        var shortOp=Op(9);Assert.False(e.CheckPreparedValue(type,"根😀",value!,r,shortOp));Assert.False(shortOp.IsComplete);
        Assert.Throws<ArgumentException>(()=>new XmlFacetEvaluator().CheckPreparedValue(type,"根😀",value!,r,Op()));
    }
    [Fact]
    public void MalformedFacetUnicodeRejectsAsSchemaRestrictions()
    {
        Bad(Restrict("length","string",F(XmlFacetKind.Length,"\ud800")));
        Bad(Restrict("ws","string",F(XmlFacetKind.WhiteSpace,"\ud800")));
        Bad(Restrict("enum","string",F(XmlFacetKind.Enumeration,"\ud800")));
        Bad(Restrict("bound","decimal",F(XmlFacetKind.MaxInclusive,"\ud800")));
    }
    [Theory]
    [InlineData("float","INF","1",true)][InlineData("float","INF","NaN",false)]
    [InlineData("double","-INF","-INF",true)][InlineData("double","-INF","0",false)]
    public void FloatingBoundSpecialValuesRetainTheVerifiedOrder(string type,string bound,string value,bool expected)
    {
        var definition=Restrict("floatbound",type,F(XmlFacetKind.MaxInclusive,bound));
        if(expected)Valid(definition,value);else Invalid(definition,value);
    }
    [Fact]
    public void DecimalPrecisionDoesNotFallThroughClrDecimalOrDouble()
    {
        const string huge="123456789012345678901234567890123456789012345678901234567890.12345";
        var definition=Restrict("precision","decimal",F(XmlFacetKind.TotalDigits,"65"),F(XmlFacetKind.FractionDigits,"5"),F(XmlFacetKind.Enumeration,huge));
        Valid(definition,huge+"00");Invalid(definition,huge+"1");
    }
}

public sealed class CallerEngineTests
{
    [Theory]
    [InlineData(false)][InlineData(true)]
    public void CallerRegexEngineSchemaExceptionsRetainTheirProvenance(bool matching)
    {
        var type=Restrict("engine","string",F(XmlFacetKind.Pattern,"a+"));var registry=new XmlDatatypeRegistry([type],Op());
        var marker=new SchemaCompileException([new("REGEX.Syntax",XmlDiagnosticSeverity.Error,"Caller schema marker.",default)]);
        var engine=new ThrowingEngine(marker,matching);var evaluator=new XmlFacetEvaluator(engine,engine);
        if(matching)
        {Assert.True(evaluator.ValidateDefinition(type,registry,Op()));Assert.Same(marker,Assert.Throws<SchemaCompileException>(()=>evaluator.TryValidate(type,"a",Context,registry,Op(),out _,out _)));}
        else Assert.Same(marker,Assert.Throws<SchemaCompileException>(()=>evaluator.ValidateDefinition(type,registry,Op())));
    }
    private sealed class ThrowingEngine(SchemaCompileException marker,bool matching):IXmlSchemaRegexCompiler,IXmlSchemaRegexMatcher
    {
        public XmlSchemaRegexProgram Compile(string pattern,XmlLocation at,ISchemaOperation op)=>matching ? new XmlSchemaRegexEngine().Compile(pattern,at,op):throw marker;
        public bool IsMatch(XmlSchemaRegexProgram program,ReadOnlySpan<char> input,ISchemaOperation op)=>throw marker;
    }
}
