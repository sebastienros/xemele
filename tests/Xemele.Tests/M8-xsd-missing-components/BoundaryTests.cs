using System.Text;
using Xemele.Validation;
using Xunit;
using static Xemele.Tests.M8XsdMissingComponents.Support;
namespace Xemele.Tests.M8XsdMissingComponents;

public sealed class BoundaryTests
{
    private static readonly ComponentIdentity Owner = ComponentAvailability.Simple(new("", "L"));
    private static ComponentAvailability Graph(Counted operation, int count = 64)
    {
        var builder = new AvailabilityBuilder(operation);
        for (var i = 0; i < count; i++)
        {
            builder.Reference(Owner, ReferenceKind.ListItemType, SymbolSpace.Type,
            new("", "item" + i), default, i != count - 1, DependencyUse.TypeAssessment);
        }

        return builder.Seal();
    }
    [Fact]
    public void SnapshotDoesNotRetainMutableArraysOrBuilder()
    {
        var op = new Counted();
        var entry = new AvailabilityEntry(0, Owner, ReferenceKind.ListItemType, SymbolSpace.Type, new("", "absent"), default, AvailabilityState.Unavailable);
        var entries = new[] { entry };
        var edges = new[] { new DependencyEdge(Owner, 0, DependencyUse.TypeAssessment) };
        var snapshot = new ComponentAvailability(entries, edges, op);
        entries[0] = new(0, Owner, ReferenceKind.ListItemType, SymbolSpace.Type, new("", "different"), default, AvailabilityState.Resolved);
        edges[0] = new(Owner, 0, DependencyUse.SchemaDerivation);
        Assert.Same(entry, snapshot.Missing(Owner, DependencyUse.TypeAssessment, op, default));
        Assert.Throws<NotSupportedException>(() => ((IList<AvailabilityEntry>)snapshot.Entries)[0] = entries[0]);
    }
    private static ComponentIdentity Node(int i) => ComponentAvailability.Simple(new("", "N" + i));
    private static AvailabilityBuilder ReverseDiamond(Counted operation, int length)
    {
        var builder = new AvailabilityBuilder(operation);
        builder.Reference(Node(length), ReferenceKind.ListItemType, SymbolSpace.Type, new("", "item"), default, false, DependencyUse.TypeAssessment);
        builder.Reference(Node(length), ReferenceKind.SubstitutionAffiliation, SymbolSpace.Element, new("", "head"), default, false, DependencyUse.SubstitutionSelection);
        // Consumer-first order forces transitive fixpoint propagation across rounds.
        for (var i = 0; i < length; i++)
        {
            builder.Link(Node(i), Node(i + 1));
        }

        builder.Link(Node(0), Node(2));
        builder.Link(Node(0), Node(2)); // diamond plus duplicate path
        builder.Link(Node(0), Node(length), DependencyUse.SchemaDerivation, DependencyUse.SubstitutionSelection);
        return builder;
    }
    [Fact]
    public void ReverseChainDiamondClosureDeduplicatesAndSeparatesUsesAtExactCaps()
    {
        var observed = new Counted();
        var graph = ReverseDiamond(observed, 8).Seal();
        var expected = Enumerable.Range(0, 9).Select(i => new DependencyEdge(Node(i), 0, DependencyUse.TypeAssessment)).ToHashSet();
        expected.Add(new(Node(8), 1, DependencyUse.SubstitutionSelection));
        expected.Add(new(Node(0), 1, DependencyUse.SchemaDerivation));
        Assert.Equal(2, graph.Entries.Count);
        Assert.Equal(11, graph.ClosureEdges.Count);
        Assert.True(expected.SetEquals(graph.ClosureEdges));
        Assert.Equal(0, graph.Missing(Node(0), DependencyUse.TypeAssessment, new Counted(), default)!.ReferenceId);
        Assert.Equal(1, graph.Missing(Node(0), DependencyUse.SchemaDerivation, new Counted(), default)!.ReferenceId);
        Assert.Null(graph.Missing(Node(0), DependencyUse.SubstitutionSelection, new Counted(), default));
        var exact = new Counted { WorkCap = observed.Work, ComponentCap = observed.Components };
        Assert.True(expected.SetEquals(ReverseDiamond(exact, 8).Seal().ClosureEdges));
        Assert.True(exact.IsComplete);
        var workShort = new Counted { WorkCap = observed.Work - 1 };
        Assert.Throws<SchemaCompileException>(() => ReverseDiamond(workShort, 8).Seal());
        Assert.False(workShort.IsComplete);
        var retainedShort = new Counted { ComponentCap = observed.Components - 1 };
        Assert.Throws<SchemaCompileException>(() => ReverseDiamond(retainedShort, 8).Seal());
        Assert.False(retainedShort.IsComplete);
    }
    [Fact]
    public void UnlimitedReversePropagationPollsCancellationWithoutPublishingSnapshot()
    {
        var operation = new Counted();
        var builder = ReverseDiamond(operation, 128);
        operation.CancelAt = operation.Work + 1024; // after setup, within the propagation walk
        ComponentAvailability? published = null;
        Assert.Throws<OperationCanceledException>(() => published = builder.Seal());
        Assert.Null(published);
        Assert.True(operation.Cancellation.IsCancellationRequested);
    }
    [Fact]
    public void LookupChargesEveryEdgeAndRequiredReferenceWithUnlimitedCaps()
    {
        var graph = Graph(new Counted());
        var observed = new Counted();
        Assert.NotNull(graph.Missing(Owner, DependencyUse.TypeAssessment, observed, default));
        // Independent fixture cardinalities: every edge inspected, every matching
        // reference inspected; entry count does not borrow runtime constants.
        Assert.Equal(graph.ClosureEdges.Count + graph.Entries.Count, observed.Work);
        Assert.True(observed.Polls >= 2);
        Assert.Equal(0, observed.Components);
        var exact = new Counted { WorkCap = observed.Work };
        Assert.NotNull(graph.Missing(Owner, DependencyUse.TypeAssessment, exact, default));
        var shortCap = new Counted { WorkCap = observed.Work - 1 };
        Assert.Throws<SchemaCompileException>(() => graph.Missing(Owner, DependencyUse.TypeAssessment, shortCap, default));
        var cancelled = new Counted { CancelAt = observed.Work / 2 };
        Assert.Throws<OperationCanceledException>(() => graph.Missing(Owner, DependencyUse.TypeAssessment, cancelled, default));
    }
    [Fact]
    public void PublicQuotaAndCancellationPrecedeMissingRefusal()
    {
        var schema = XmlSchema.Compile(Wrap("<xs:element name='bad' type='absent'/><xs:element name='good' type='xs:int'/>"));
        var refused = schema.Validate("<bad/>", new()
        {
            MaxOperations = 1
        });
        Assert.False(refused.IsComplete);
        Assert.DoesNotContain(refused.Diagnostics, d => d.Code == "XSD.MissingComponent");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => schema.Validate("<bad/>", new() { CancellationToken = cancellation.Token }));
        foreach (var cap in new[] { 1, 2 })
        {
            Missing(schema.Validate("<bad/>", new()
            {
                Limits = new()
                {
                    Diagnostics = cap
                }
            }));
        }
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OriginalInstanceAndSchemaReferenceLocationsSurviveExternalDisposal(bool bytes)
    {
        var final = new Uri("https://redirect.test/missing.xsd");
        var stream = new OwnedStream(Wrap("\n<xs:element name='bad' type='absent'/><xs:element name='good' type='xs:int'/>"));
        var resolver = new Resolver(_ => XmlResourceResolution.FromResource(new(stream, final)));
        var schema = XmlSchema.Compile(Wrap("<xs:include schemaLocation='child'/>"), new()
        {
            BaseUri = Basis,
            Resolver = resolver
        });
        Assert.Equal(1, stream.Disposals);
        ValidationResult result;
        if (bytes)
        {
            using var input = new MemoryStream(Encoding.UTF8.GetBytes("\n<bad/>"));
            result = schema.Validate(input);
            Assert.True(input.CanRead);
        }
        else
        {
            result = schema.Validate("\n<bad/>");
        }

        Missing(result);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(2, diagnostic.Location.LineNumber);
        Assert.Equal(bytes, diagnostic.Location.ByteOffset is not null);
        Assert.Contains(final.AbsoluteUri, diagnostic.Message);
        Assert.Contains("line 2", diagnostic.Message);
        Assert.Equal(1, resolver.Calls);
    }
    [Fact]
    public void PublishedSchemaSurvivesSourceMutationAndConcurrentReuse()
    {
        var source = XmlDocument.Parse(Wrap("<xs:element name='bad' type='absent'/><xs:element name='good' type='xs:int'/>"), cancellationToken: TestContext.Current.CancellationToken);
        var schema = XmlSchema.Compile(source);
        source.RootElement.SetAttribute("targetNamespace", "changed");
        Parallel.For(0, 16, i => { if (i % 2 == 0) { Assert.True(schema.Validate("<good>7</good>").IsValid); } else { Missing(schema.Validate("<bad/>")); } });
    }
}
