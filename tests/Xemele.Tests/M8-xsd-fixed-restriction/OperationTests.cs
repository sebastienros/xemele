using Xemele.Validation;
using Xunit;
namespace Xemele.Tests.M8XsdFixedRestriction;

public sealed class OperationTests
{
    internal sealed class Ledger(long? workLimit = null, int? componentLimit = null, CancellationToken token = default) : ISchemaOperation
    {
        public XmlResourceLimits Limits { get; } = new();
        public CancellationToken CancellationToken => token;
        public bool IsComplete { get; private set; } = true;
        public long? RemainingWork => workLimit - Work;
        internal long Work; internal int Components;
        internal readonly List<XmlDiagnostic> Diagnostics = [];
        public bool Charge(long work, XmlLocation location)
        {
            token.ThrowIfCancellationRequested();
            if (!IsComplete) return false;
            if (workLimit is { } cap && work > cap - Work) { Refuse("SCHEMA.Limit", "work", location); return false; }
            Work = checked(Work + work); return true;
        }
        public bool ChargeComponents(int count, XmlLocation location)
        {
            token.ThrowIfCancellationRequested();
            if (!IsComplete) return false;
            if (componentLimit is { } cap && count > cap - Components) { Refuse("SCHEMA.Limit", "components", location); return false; }
            Components = checked(Components + count); return true;
        }
        public bool AddDiagnostic(XmlDiagnostic diagnostic) { Diagnostics.Add(diagnostic); return IsComplete; }
        public void Refuse(string code, string message, XmlLocation location)
        { Diagnostics.Add(new(code, XmlDiagnosticSeverity.Fatal, message, location)); IsComplete = false; }
    }
    private static XsdCompiledSchema Schema() => FixedRestrictionTests.Prepare(FixedRestrictionTests.Restriction(
        FixedRestrictionTests.Element("p:v", "xs:QName", "xmlns:p='urn:same'"),
        FixedRestrictionTests.Element("q:v", "xs:QName", "xmlns:q='urn:same'")));
    [Fact]
    public void ActualTypedWorkAndComponentBudgetsHaveExactBoundaries()
    {
        var graph = Schema(); var ledger = new Ledger(); FixedRestrictionTests.Complete(graph, ledger);
        Assert.True(ledger.Work > 0); Assert.True(ledger.Components > 0);
        Assert.NotNull(FixedRestrictionTests.Complete(graph, new Ledger(ledger.Work, ledger.Components)));
        foreach (var limit in new[] { new Ledger(ledger.Work - 1), new Ledger(componentLimit: ledger.Components - 1) })
        {
            var failure = Assert.Throws<SchemaCompileException>(() => FixedRestrictionTests.Complete(graph, limit));
            Assert.Contains(failure.Diagnostics, d => d.Code == "SCHEMA.Limit"); Assert.False(limit.IsComplete);
        }
    }
    [Fact]
    public void SuppliedFacetsReceiveBothAuthoredContextsRegistryAndSameOperation()
    {
        var graph = Schema(); var ledger = new Ledger(); var recorder = new Facets();
        Assert.NotNull(FixedRestrictionTests.Complete(graph, ledger, recorder));
        Assert.Equal(2, recorder.Calls.Count);
        foreach (var call in recorder.Calls)
        {
            var declaration = Assert.Single(graph.Elements, d => d.FixedValue == call.Lexical);
            Assert.Same(declaration.Context, call.Context); Assert.Same(graph.Datatypes, call.Registry); Assert.Same(ledger, call.Operation);
            Assert.Equal(new SchemaName(FixedRestrictionTests.Xs, "QName"), call.Value.TypeName);
        }
    }
    [Fact]
    public void CancelAtEntryOrBetweenTypedCallsPropagates()
    {
        var graph = Schema(); using var source = new CancellationTokenSource(); source.Cancel();
        Assert.Throws<OperationCanceledException>(() => FixedRestrictionTests.Complete(graph, new Ledger(token: source.Token)));
        using var during = new CancellationTokenSource(); var recorder = new Facets { After = () => during.Cancel() };
        Assert.Throws<OperationCanceledException>(() => FixedRestrictionTests.Complete(graph, new Ledger(token: during.Token), recorder));
        Assert.Single(recorder.Calls);
    }
    [Fact]
    public void ReturnedRefusalAndCallerExceptionsArePreserved()
    {
        var graph = Schema(); var ledger = new Ledger();
        var refusing = new Facets { After = () => ledger.Refuse("SCHEMA.Limit", "during typed validation", default) };
        Assert.Contains(Assert.Throws<SchemaCompileException>(() => FixedRestrictionTests.Complete(graph, ledger, refusing)).Diagnostics, d => d.Code == "SCHEMA.Limit");
        var caller = new InvalidOperationException("caller"); var throwing = new Facets { After = () => throw caller };
        Assert.Same(caller, Assert.Throws<InvalidOperationException>(() => FixedRestrictionTests.Complete(graph, facets: throwing)));
    }
    [Fact]
    public void ExplicitCompleteJoinRejectsMissingExecutableDependencies()
    {
        var context = new SchemaCompileContext();
        var graph = new XmlSchemaCompiler().Compile(context.Parse($"<xs:schema xmlns:xs='{FixedRestrictionTests.Xs}'>{FixedRestrictionTests.Restriction(FixedRestrictionTests.Element("1"), FixedRestrictionTests.Element("1"))}</xs:schema>").Document, context.Options, context.Operation);
        // The compiler registry is declaration-only. Complete composition must
        // fail rather than silently return a structurally accepted program.
        Assert.Throws<SchemaCompileException>(() => FixedRestrictionTests.Complete(graph));
    }
    private sealed class Facets : IXmlFacetEvaluator
    {
        private readonly XmlFacetEvaluator inner = new();
        internal readonly List<(string Lexical, SchemaNamespaceContext Context, IXmlDatatypeRegistry Registry, ISchemaOperation Operation, XmlDatatypeValue Value)> Calls = [];
        internal Action? After;
        public bool ValidateDefinition(XmlDatatypeDefinition type, IXmlDatatypeRegistry registry, ISchemaOperation operation) => inner.ValidateDefinition(type, registry, operation);
        public bool TryValidate(XmlDatatypeDefinition type, ReadOnlySpan<char> lexical, SchemaNamespaceContext context,
            IXmlDatatypeRegistry registry, ISchemaOperation operation, out XmlDatatypeValue? value, out XmlDatatypeError? error)
        {
            bool valid = inner.TryValidate(type, lexical, context, registry, operation, out value, out error);
            if (valid) Calls.Add((new string(lexical), context, registry, operation, value!));
            After?.Invoke(); return valid;
        }
    }
}
