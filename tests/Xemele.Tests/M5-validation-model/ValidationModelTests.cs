using Xemele.Validation;
using Xunit;

namespace Xemele.Tests.M5ValidationModel;

public sealed class ValidationModelTests
{
    private static XmlDiagnostic Diagnostic(XmlDiagnosticSeverity severity = XmlDiagnosticSeverity.Error) =>
        new("DTD.Test", severity, "Test", new(new Uri("urn:declaration"), 2, 3, 4));

    [Fact]
    public void ResultOwnsImmutableOrderedSnapshot()
    {
        var first = Diagnostic(XmlDiagnosticSeverity.Warning);
        var second = Diagnostic();
        var source = new List<XmlDiagnostic> { first, second };
        var result = new ValidationResult(true, source);
        source.Clear();
        Assert.Equal([first, second], result.Diagnostics);
        Assert.True(result.IsComplete);
        Assert.False(result.IsValid);
        Assert.Throws<NotSupportedException>(() => ((IList<XmlDiagnostic>)result.Diagnostics).Clear());
        Assert.Equal(result.Diagnostics, (result with
        {
        }).Diagnostics);
    }

    [Theory]
    [InlineData(XmlDiagnosticSeverity.Warning, true)]
    [InlineData(XmlDiagnosticSeverity.Error, false)]
    [InlineData(XmlDiagnosticSeverity.Fatal, false)]
    public void CompleteOutcomeComputesValidity(XmlDiagnosticSeverity severity, bool valid)
    {
        var result = new ValidationResult(true, [Diagnostic(severity)]);
        Assert.True(result.IsComplete);
        Assert.Equal(valid, result.IsValid);
        Assert.True(new ValidationResult(true, []).IsValid);
    }

    [Fact]
    public void IncompleteAndNullInvariants()
    {
        Assert.Throws<ArgumentNullException>(() => new ValidationResult(true, null!));
        Assert.Throws<ArgumentException>(() => new ValidationResult(true, [null!]));
        Assert.Throws<ArgumentException>(() => new ValidationResult(false, []));
        Assert.Throws<ArgumentException>(() => new ValidationResult(false, [Diagnostic()]));
        var result = new ValidationResult(false, [Diagnostic(XmlDiagnosticSeverity.Fatal)]);
        Assert.False(result.IsComplete);
        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData(3, 5)]
    [InlineData(5, 3)]
    [InlineData(3, 3)]
    public void WorkUsesMinimumAndExactBoundary(long operations, long evaluation)
    {
        var run = new ValidationOperation(new()
        {
            MaxOperations = operations,
            Limits = new()
            {
                EvaluationWork = evaluation
            }
        });
        Assert.True(run.ChargeOperations(3));
        Assert.True(run.ChargeOperations(0));
        Assert.True(run.Complete().IsValid);
        var location = new XmlLocation(new Uri("urn:instance"), 4, 5, null);
        Assert.False(run.ChargeOperations(1, location));
        Assert.Equal(3, run.Operations);
        var result = run.Complete();
        Assert.False(result.IsComplete);
        Assert.False(result.IsValid);
        Assert.Equal("VALIDATION.Limit", Assert.Single(result.Diagnostics).Code);
        Assert.Equal(location, result.Diagnostics[0].Location);
        Assert.False(run.AddDiagnostic(Diagnostic()));
    }

    [Theory]
    [InlineData(null, 2L)]
    [InlineData(2L, null)]
    public void OneNullCapDoesNotDisableOther(long? operations, long? evaluation)
    {
        var run = new ValidationOperation(new()
        {
            MaxOperations = operations,
            Limits = new()
            {
                EvaluationWork = evaluation
            }
        });
        Assert.True(run.ChargeOperations(2));
        Assert.False(run.ChargeOperations());
    }

    [Fact]
    public void UnlimitedStillRejectsRepresentationalOverflow()
    {
        var run = new ValidationOperation(new()
        {
            MaxOperations = null,
            Limits = new()
            {
                EvaluationWork = null
            }
        });
        Assert.Throws<ArgumentOutOfRangeException>(() => run.ChargeOperations(-1));
        Assert.True(run.ChargeOperations(long.MaxValue));
        Assert.True(run.ChargeOperations(0));
        Assert.False(run.ChargeOperations());
        Assert.Equal(long.MaxValue, run.Operations);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(128)]
    public void DiagnosticCapReservesTerminalWithinCap(int cap)
    {
        var run = new ValidationOperation(new()
        {
            Limits = new()
            {
                Diagnostics = cap
            }
        });
        for (var i = 0; i < cap; i++)
        {
            Assert.True(run.AddDiagnostic(Diagnostic()));
        }

        var before = run.Complete();
        Assert.True(before.IsComplete);
        Assert.Equal(cap, before.Diagnostics.Count);
        Assert.False(run.AddDiagnostic(Diagnostic()));
        for (var i = 0; i < 10; i++)
        {
            Assert.False(run.AddDiagnostic(Diagnostic()));
        }

        var after = run.Complete();
        Assert.False(after.IsComplete);
        Assert.Equal(cap, after.Diagnostics.Count);
        Assert.Equal("VALIDATION.Limit", after.Diagnostics[^1].Code);
        Assert.Equal(XmlDiagnosticSeverity.Fatal, after.Diagnostics[^1].Severity);
        Assert.All(before.Diagnostics, d => Assert.Equal(XmlDiagnosticSeverity.Error, d.Severity));
    }

    [Fact]
    public void ProvenanceRefusalAndUnlimitedDiagnostics()
    {
        var run = new ValidationOperation(new()
        {
            Limits = new()
            {
                Diagnostics = null
            }
        });
        for (var i = 0; i < 257; i++)
        {
            Assert.True(run.AddDiagnostic(Diagnostic()));
        }

        run.Refuse("DTD.IncompleteInput", "Missing provenance.");
        run.Refuse("DTD.Other", "Ignored after refusal.");
        Assert.Equal(258, run.Complete().Diagnostics.Count);
        Assert.Equal("DTD.IncompleteInput", run.Complete().Diagnostics[^1].Code);
    }

    [Fact]
    public void OptionsValidatedAndSnapshottedAtEntry()
    {
        Assert.Equal(10_000_000, new ValidationOptions().MaxOperations);
        Assert.Equal(128, new ValidationOptions().Limits.Diagnostics);
        Assert.Throws<ArgumentNullException>(() => new ValidationOperation(new() { Limits = null! }));
        foreach (var cap in new long[] { 0, -1 })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ValidationOperation(new() { MaxOperations = cap }));
        }

        Assert.Throws<ArgumentOutOfRangeException>(() => new ValidationOperation(new() { Limits = new() { Diagnostics = 0 } }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ValidationOperation(new() { Limits = new() { Depth = 0 } }));
        var options = new ValidationOptions();
        var run = new ValidationOperation(options);
        Assert.Equal(options.Limits, run.Limits);
        Assert.NotSame(options.Limits, run.Limits);
        options = options with
        {
            MaxOperations = 1,
            Limits = new()
            {
                EvaluationWork = 1
            }
        };
        Assert.True(run.ChargeOperations(2));
    }

    [Fact]
    public void CancellationThrowsOriginalTokenAtEveryBoundary()
    {
        using var cancellation = new CancellationTokenSource();
        var options = new ValidationOptions() { CancellationToken = cancellation.Token };
        var run = new ValidationOperation(options);
        cancellation.Cancel();
        Action[] actions = [() => new ValidationOperation(options), () => run.ChargeOperations(),
            () => run.AddDiagnostic(Diagnostic()), () => run.Refuse("DTD.IncompleteInput", "Missing"), () => run.Complete()];
        foreach (var action in actions)
        {
            Assert.Equal(cancellation.Token, Assert.Throws<OperationCanceledException>(action).CancellationToken);
        }
    }

    [Fact]
    public async Task ConcurrentRunsShareOnlyImmutableOptions()
    {
        var options = new ValidationOptions() { MaxOperations = 2 };
        var results = await Task.WhenAll(Enumerable.Range(0, 32).Select(i => Task.Run(() =>
        {
            var run = new ValidationOperation(options);
            Assert.True(run.ChargeOperations(2));
            if (i % 2 == 0)
            {
                Assert.False(run.ChargeOperations());
            }
            else
            {
                Assert.True(run.AddDiagnostic(Diagnostic()));
            }

            return run.Complete();
        })));
        Assert.Equal(16, results.Count(r => r.IsComplete));
        Assert.Equal(16, results.Count(r => !r.IsComplete));
        Assert.All(results, r => Assert.Single(r.Diagnostics));
        Assert.False(ReferenceEquals(results[0].Diagnostics, results[1].Diagnostics));
    }
}
