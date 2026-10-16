using System.Reflection;
using System.Text;
using Xemele.Internal.Operations;
using Xunit;
namespace Xemele.Tests.Common;

public class CommonTests
{
    private static XmlDiagnostic Diagnostic(string code) => new(code, XmlDiagnosticSeverity.Error,
        "Owned message", new(new Uri("file:///source.xml"), 2, 3, 4));
    [Fact]
    public void ConstructorsOwnSnapshotsAndPreserveInvariants()
    {
        var source = new List<XmlDiagnostic> { Diagnostic("test.syntax") };
        var exception = new XmlParseException(source);
        var result = new XmlParseResult<string>("value", true, true, source);
        source.Clear();
        Assert.Single(exception.Diagnostics);
        Assert.Single(result.Diagnostics);
        Assert.Equal("XML parsing failed.", exception.Message);
        Assert.Throws<NotSupportedException>(() => ((IList<XmlDiagnostic>)result.Diagnostics).Clear());
        Assert.Throws<ArgumentException>(() => new XmlParseException([]));
        Assert.Throws<ArgumentNullException>(() => new XmlParseException(null!));
        Assert.Throws<ArgumentNullException>(() => new XmlParseResult<string>(null, false, false, null!));
        Assert.Throws<ArgumentException>(() => new XmlParseResult<string>(null, true, true, []));
        Assert.Throws<ArgumentException>(() => new XmlParseResult<string>("x", false, true, []));
        Assert.Null(new XmlParseResult<string>(null, false, false, []).Value);
        Assert.Equal(default, new XmlLocation(null, 0, 0, null));
    }
    [Fact]
    public void AllLimitsHaveApprovedDefaultsAndRejectZeroAndNegative()
    {
        long[] expected = [16777216, 256, 256, 1048576, 65536, 32, 16777216, 16777216, 64, 32, 128, 10000000];
        string[] names = ["DocumentCharacters","Depth","AttributesPerElement","TokenCharacters","InternedNames",
            "EntityDepth","ExpandedCharacters","ExternalBytes","ExternalResources","InclusionDepth","Diagnostics","EvaluationWork"];
        var defaults = new XmlResourceLimits();
        defaults.Validate();
        for (var i = 0; i < names.Length; i++)
        {
            var p = typeof(XmlResourceLimits).GetProperty(names[i])!;
            Assert.Equal(expected[i], Convert.ToInt64(p.GetValue(defaults)));
            foreach (var invalid in new long[] { 0, -1, long.MinValue })
            {
                var limits = new XmlResourceLimits();
                var value = p.PropertyType == typeof(int?) ? (object)(invalid == long.MinValue ? int.MinValue : (int)invalid) : invalid;
                p.SetValue(limits, value);
                Assert.Throws<ArgumentOutOfRangeException>(limits.Validate);
                Assert.Throws<ArgumentOutOfRangeException>(() => new OperationBudget(new() { Limits = limits }, default));
            }
            var unlimited = new XmlResourceLimits();
            p.SetValue(unlimited, null);
            unlimited.Validate();
        }
        Assert.Throws<ArgumentNullException>(() => new OperationBudget(new() { Limits = null! }, default));
    }
    [Fact]
    public void EveryBudgetAllowsExactThresholdRejectsOneOverWithoutMutation()
    {
        foreach (var q in Enum.GetValues<Quota>())
        {
            var limits = new XmlResourceLimits();
            var property = typeof(XmlResourceLimits).GetProperty(q.ToString())!;
            property.SetValue(limits, property.PropertyType == typeof(int?) ? (object)3 : 3L);
            var b = new OperationBudget(new()
            {
                Limits = limits
            }, default);
            Assert.True(b.TryCharge(q, 2));
            Assert.True(b.TryCharge(q, 1));
            Assert.False(b.TryCharge(q, 1));
            Assert.Equal(3, b.Count(q));
            b.Release(q, 1);
            Assert.True(b.TryCharge(q, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => b.TryCharge(q, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => b.Release(q, 4));
            b.Reset(q);
            Assert.Equal(0, b.Count(q));
            property.SetValue(limits, null);
            b = new OperationBudget(new()
            {
                Limits = limits
            }, default);
            var capacity = property.PropertyType == typeof(int?) ? int.MaxValue : long.MaxValue;
            Assert.True(b.TryCharge(q, capacity));
            Assert.False(b.TryCharge(q, 1));
            Assert.Equal(capacity, b.Count(q));
            Assert.True(b.TryCharge(q, 0));
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => new OperationBudget(null, default).TryCharge((Quota)99, 1));
    }
    [Fact]
    public void OperationSnapshotIsIndependentEvenOfReflectionMutation()
    {
        var limits = new XmlResourceLimits { Depth = 3 };
        var options = new XmlParseOptions { Limits = limits };
        var b = new OperationBudget(options, default);
        Assert.NotSame(options, b.Options);
        Assert.NotSame(limits, b.Options.Limits);
        typeof(XmlResourceLimits).GetProperty("Depth")!.SetValue(limits, 1);
        Assert.Equal(3, b.Options.Limits.Depth);
        Assert.True(b.TryCharge(Quota.Depth, 3));
    }
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(128)]
    public void DiagnosticRetentionIncludesTerminalAndSnapshotsRemainOwned(int maximum)
    {
        var buffer = new DiagnosticBuffer(maximum);
        for (var i = 0; i < maximum; i++)
        {
            buffer.Add(Diagnostic("test.syntax"));
        }

        var previous = buffer.Snapshot();
        Assert.Equal(maximum, previous.Count);
        buffer.Add(Diagnostic("test.extra"));
        for (var i = 0; i < 1000; i++)
        {
            buffer.Add(Diagnostic("test.ignored"));
        }

        var now = buffer.Snapshot();
        Assert.Equal(maximum, now.Count);
        Assert.Equal("common.diagnostics-limit", now[^1].Code);
        Assert.Equal("test.syntax", previous[^1].Code);
        Assert.Equal(Diagnostic("x").Location, now[^1].Location);
        Assert.Throws<NotSupportedException>(() => ((IList<XmlDiagnostic>)now).Clear());
        Assert.Throws<ArgumentNullException>(() => buffer.Add(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DiagnosticBuffer(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DiagnosticBuffer(-1));
        var unlimited = new DiagnosticBuffer(null);
        for (var i = 0; i < 200; i++)
        {
            unlimited.Add(Diagnostic("x"));
        }

        Assert.Equal(200, unlimited.Snapshot().Count);
    }
    [Fact]
    public void ScalarLocationsNormalizeCrLfAndCountAstralAndTabOnce()
    {
        var uri = new Uri("file:///source.xml");
        var tracker = new ScalarLocation(uri);
        Assert.Equal(new(uri, 1, 1, 0), tracker.Current(0));
        Assert.True(tracker.TryAdvance(new Rune(0x1f600)));
        Assert.Equal(new(uri, 1, 2, 4), tracker.Current(4));
        Assert.True(tracker.TryAdvance(new Rune('\t')));
        Assert.Equal(3, tracker.Current().LinePosition);
        Assert.True(tracker.TryAdvance(new Rune('\r')));
        Assert.True(tracker.TryAdvance(new Rune('\n')));
        Assert.Equal(new(uri, 2, 1, null), tracker.Current());
        Assert.True(tracker.TryAdvance(new Rune('a')));
        Assert.True(tracker.TryAdvance(new Rune('\n')));
        Assert.Equal(new(uri, 3, 1, 9), tracker.Current(9));
        Assert.Throws<ArgumentOutOfRangeException>(() => tracker.Current(-1));
        typeof(ScalarLocation).GetField("line", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(tracker, int.MaxValue);
        Assert.False(tracker.TryAdvance(new Rune('\n')));
        Assert.Equal(int.MaxValue, tracker.Current().LineNumber);
        typeof(ScalarLocation).GetField("column", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(tracker, int.MaxValue);
        Assert.False(tracker.TryAdvance(new Rune('x')));
        Assert.Equal(int.MaxValue, tracker.Current().LinePosition);
    }
    [Fact]
    public void CancellationChecksEntryExternalBoundariesAndBoundedSegments()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var ex = Assert.Throws<OperationCanceledException>(() => new OperationBudget(null, cancellation.Token));
        Assert.Equal(cancellation.Token, ex.CancellationToken);
        using var later = new CancellationTokenSource();
        var b = new OperationBudget(null, later.Token);
        b.BeforeWork(2000);
        later.Cancel();
        b.BeforeWork(2000);
        Assert.Throws<OperationCanceledException>(() => b.BeforeWork(96));
        Assert.Throws<OperationCanceledException>(b.CheckCancellation);
        Assert.Throws<ArgumentOutOfRangeException>(() => b.BeforeWork(4097));
        Assert.Throws<ArgumentOutOfRangeException>(() => b.BeforeWork(-1));
    }
    [Fact]
    public void WarmCountersAndLocationsAllocateNothing()
    {
        var budget = new OperationBudget(null, default);
        var tracker = new ScalarLocation(null);
        static void Run(OperationBudget b, ScalarLocation l)
        {
            for (var i = 0; i < 10000; i++)
            {
                b.BeforeWork(1);
                if (!b.TryCharge(Quota.TokenCharacters, 1))
                {
                    throw new Exception();
                }

                b.Release(Quota.TokenCharacters, 1);
                l.TryAdvance(new Rune('x'));
                _ = l.Current();
            }
        }
        Run(budget, tracker);
        var before = GC.GetAllocatedBytesForCurrentThread();
        Run(budget, tracker);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
