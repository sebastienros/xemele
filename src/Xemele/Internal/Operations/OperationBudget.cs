using System.Runtime.CompilerServices;
[assembly: InternalsVisibleTo("Xemele.Tests")]
[assembly: InternalsVisibleTo("Xemele.CommonBenchmarks")]
[assembly: InternalsVisibleTo("Xemele.Benchmarks")]
namespace Xemele.Internal.Operations;

// Each operation owns one budget. Consumers choose the correct unit and scope;
// depth/token/tag counters are released/reset by their corresponding consumer.
internal enum Quota
{
    DocumentCharacters, Depth, AttributesPerElement, TokenCharacters, InternedNames,
    EntityDepth, ExpandedCharacters, ExternalBytes, ExternalResources, InclusionDepth,
    EvaluationWork
}
internal sealed class OperationBudget
{
    private readonly long[] maximums;
    private readonly long[] counts = new long[11];
    private readonly CancellationToken cancellation;
    private int workUntilCheck = 4096;
    internal XmlParseOptions Options { get; }
    internal OperationBudget(XmlParseOptions? options, CancellationToken cancellation)
    {
        options ??= new();
        ArgumentNullException.ThrowIfNull(options.Limits);
        options.Limits.Validate();
        Options = options with { Limits = options.Limits with { } };
        var l = Options.Limits;
        // Options are an immutable operation snapshot. Compute nullable and
        // representational ceilings once rather than on every scalar/lookup.
        maximums = [l.DocumentCharacters ?? long.MaxValue, l.Depth ?? int.MaxValue,
            l.AttributesPerElement ?? int.MaxValue, l.TokenCharacters ?? int.MaxValue,
            l.InternedNames ?? int.MaxValue, l.EntityDepth ?? int.MaxValue,
            l.ExpandedCharacters ?? long.MaxValue, l.ExternalBytes ?? long.MaxValue,
            l.ExternalResources ?? int.MaxValue, l.InclusionDepth ?? int.MaxValue,
            l.EvaluationWork ?? long.MaxValue];
        this.cancellation = cancellation;
        CheckCancellation();
    }
    internal void CheckCancellation() => cancellation.ThrowIfCancellationRequested();
    // Call BEFORE a bounded segment (<=4096 units), and before external work or
    // callbacks use CheckCancellation directly. No callback exception is caught.
    internal void BeforeWork(int units)
    {
        if (units is < 0 or > 4096) throw new ArgumentOutOfRangeException(nameof(units));
        if (units >= workUntilCheck)
        {
            CheckCancellation();
            workUntilCheck = 4096;
        }
        workUntilCheck -= units;
    }
    internal long Count(Quota quota) => counts[Index(quota)];
    internal long Remaining(Quota quota)
    {
        int i = Index(quota);
        return maximums[i] - counts[i];
    }
    internal bool TryCharge(Quota quota, long amount)
    {
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
        int i = Index(quota);
        long maximum = maximums[i];
        // Subtraction rejects representational overflow without overflowing or
        // mutating counters; exactly maximum succeeds, even under null quotas.
        if (amount > maximum - counts[i]) return false;
        counts[i] += amount;
        return true;
    }
    internal void Release(Quota quota, long amount)
    {
        int i = Index(quota);
        if (amount < 0 || amount > counts[i]) throw new ArgumentOutOfRangeException(nameof(amount));
        counts[i] -= amount;
    }
    internal void Reset(Quota quota) => counts[Index(quota)] = 0;
    private static int Index(Quota quota)
    {
        int i = (int)quota;
        if ((uint)i >= 11) throw new ArgumentOutOfRangeException(nameof(quota));
        return i;
    }
}
