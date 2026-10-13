using Xemele.Validation;
using Xunit;
using static Xemele.Tests.M8XsdMissingComponents.Support;
namespace Xemele.Tests.M8XsdResolvedAvailability;

public sealed class QueryTests
{
    private static ComponentIdentity Node(int i) => ComponentAvailability.Simple(new("", "N" + i));
    private static AvailabilityBuilder Resolved(Counted op, int length)
    {
        var builder = new AvailabilityBuilder(op);
        builder.Reference(Node(length), ReferenceKind.ListItemType, SymbolSpace.Type, new("", "resolved"), default, true, DependencyUse.TypeAssessment);
        for (int i = 0; i < length; i++) builder.Link(Node(i), Node(i + 1));
        return builder;
    }
    [Theory]
    [InlineData(8)] [InlineData(32)] [InlineData(64)]
    public void ResolvedQueriesSucceedWithNoRemainingTraversalBudget(int length)
    {
        var op = new Counted(); var builder = Resolved(op, length);
        long work = op.Work, retained = op.Components, polls = op.Polls;
        op.WorkCap = work; op.ComponentCap = retained;
        for (int i = 0; i < 32; i++) Assert.False(builder.Unavailable(Node(0)));
        Assert.Equal(work, op.Work); Assert.Equal(retained, op.Components);
        Assert.True(op.Polls >= polls + 32); Assert.True(op.IsComplete);
    }
    [Fact]
    public void LateMissingRegistrationInvalidatesEarlierNegativeAnswersAndSeparatesUses()
    {
        var op = new Counted(); var builder = Resolved(op, 8);
        Assert.False(builder.Unavailable(Node(0)));
        builder.Reference(Node(8), ReferenceKind.SubstitutionAffiliation, SymbolSpace.Element, new("", "absent"), default, false, DependencyUse.SubstitutionSelection);
        Assert.False(builder.Unavailable(Node(0)));
        builder.Link(Node(0), Node(8), DependencyUse.SchemaDerivation, DependencyUse.SubstitutionSelection);
        Assert.True(builder.Unavailable(Node(0), DependencyUse.SchemaDerivation));
        Assert.False(builder.Unavailable(Node(0), DependencyUse.SubstitutionSelection));
        builder.Reference(Node(8), ReferenceKind.ListItemType, SymbolSpace.Type, new("", "missing-item"), default, false, DependencyUse.TypeAssessment);
        Assert.True(builder.Unavailable(Node(0)));
        var graph = builder.Seal(); Assert.Equal(3, graph.Entries.Count);
        Assert.Equal(2, graph.Missing(Node(0), DependencyUse.TypeAssessment, new Counted(), default)!.ReferenceId);
        Assert.Equal(1, graph.Missing(Node(0), DependencyUse.SchemaDerivation, new Counted(), default)!.ReferenceId);
    }
    [Fact]
    public void ResolvedQueriesDoNotEraseFullChargedImmutablePublication()
    {
        var op = new Counted(); var builder = Resolved(op, 8);
        builder.Link(Node(0), Node(2)); builder.Link(Node(0), Node(2));
        for (int i = 0; i < 8; i++) Assert.False(builder.Unavailable(Node(i)));
        long beforeWork = op.Work, beforeComponents = op.Components;
        var graph = builder.Seal();
        Assert.True(op.Work > beforeWork); Assert.True(op.Components > beforeComponents);
        Assert.True(Enumerable.Range(0, 9).Select(i => new DependencyEdge(Node(i), 0, DependencyUse.TypeAssessment)).ToHashSet().SetEquals(graph.ClosureEdges));
        Assert.Equal(AvailabilityState.Resolved, Assert.Single(graph.Entries).State);
        builder.Reference(Node(8), ReferenceKind.ListItemType, SymbolSpace.Type, new("", "late"), default, false, DependencyUse.TypeAssessment);
        Assert.Single(graph.Entries); Assert.Null(graph.Missing(Node(0), DependencyUse.TypeAssessment, new Counted(), default));
        Assert.Throws<NotSupportedException>(() => ((IList<DependencyEdge>)graph.ClosureEdges).Clear());
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ResolvedQueriesPreserveCancellationAndPriorRefusalWithNullCaps(bool empty)
    {
        var cancelled = new Counted(); var builder = empty ? new AvailabilityBuilder(cancelled) : Resolved(cancelled, 8);
        cancelled.Cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => builder.Unavailable(Node(0)));
        var refused = new Counted(); var other = empty ? new AvailabilityBuilder(refused) : Resolved(refused, 8);
        refused.Refuse("SCHEMA.Limit", "prior refusal", default);
        Assert.Throws<SchemaCompileException>(() => other.Unavailable(Node(0)));
        Assert.False(refused.IsComplete);
    }
    [Fact]
    public void RegistrationAndPublicationRetainExactQuotaBoundaries()
    {
        var observed = new Counted(); Resolved(observed, 8).Seal();
        var exact = new Counted { WorkCap = observed.Work, ComponentCap = observed.Components };
        Assert.Equal(9, Resolved(exact, 8).Seal().ClosureEdges.Count);
        var shortWork = new Counted { WorkCap = observed.Work - 1 };
        Assert.Throws<SchemaCompileException>(() => Resolved(shortWork, 8).Seal());
        var shortComponents = new Counted { ComponentCap = observed.Components - 1 };
        Assert.Throws<SchemaCompileException>(() => Resolved(shortComponents, 8).Seal());
        var registration = new Counted(); var builder = new AvailabilityBuilder(registration);
        registration.WorkCap = registration.Work;
        Assert.Throws<SchemaCompileException>(() => builder.Reference(Node(0), ReferenceKind.ListItemType, SymbolSpace.Type, new("", "T"), default, true, DependencyUse.TypeAssessment));
    }
}
