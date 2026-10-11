namespace Xemele.XPath.Internal;
// Engine lanes share this instance; extension exceptions are never caught here.
internal sealed class XPathBudget : IXPathBudget
{
    private readonly XPathEvaluationLimits limits;
    private readonly XPathSourceSpan span;
    private long operations;
    internal XPathBudget(XPathEvaluationLimits limits, XPathSourceSpan span, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(limits); this.limits = limits with { }; this.span = ModelGuard.Span(span);
        CancellationToken = cancellationToken; cancellationToken.ThrowIfCancellationRequested();
    }
    public CancellationToken CancellationToken { get; }
    internal long Operations => operations;
    public void ChargeOperations(long amount)
    {
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
        CancellationToken.ThrowIfCancellationRequested();
        if (amount > long.MaxValue - operations || (limits.MaxOperations is long max && amount > max - operations)) Fail();
        operations += amount;
    }
    public void CheckNodeSetSize(int count)
    { if (count < 0) throw new ArgumentOutOfRangeException(nameof(count)); CancellationToken.ThrowIfCancellationRequested(); if (limits.MaxNodeSetItems is int max && count > max) Fail(); }
    public void CheckStringSize(int utf16Characters)
    { if (utf16Characters < 0) throw new ArgumentOutOfRangeException(nameof(utf16Characters)); CancellationToken.ThrowIfCancellationRequested(); if (limits.MaxStringCharacters is int max && utf16Characters > max) Fail(); }
    internal void CheckInputSize(int items, long characters)
    {
        if (items < 0 || characters < 0) throw new ArgumentOutOfRangeException(nameof(items)); CancellationToken.ThrowIfCancellationRequested();
        if ((limits.MaxInputItems is int maxItems && items > maxItems) || (limits.MaxInputCharacters is long maxChars && characters > maxChars)) Fail();
    }
    private void Fail() => throw new XPathEvaluationException(new("XPATH-EVALUATION-LIMIT", XPathDiagnosticCategory.EvaluationLimit, "XPath evaluation limit exceeded.", span));
}
