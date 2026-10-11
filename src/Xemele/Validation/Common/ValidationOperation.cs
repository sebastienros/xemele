namespace Xemele.Validation;

// One operation owns its counters and lazily retained diagnostics. No schema state.
internal sealed class ValidationOperation
{
    private readonly long? maximum;
    private readonly int diagnosticMaximum;
    private List<XmlDiagnostic>? diagnostics;
    internal XmlResourceLimits Limits { get; }
    internal CancellationToken CancellationToken { get; }
    internal long Operations { get; private set; }
    internal bool IsComplete { get; private set; } = true;

    internal ValidationOperation(ValidationOptions? options = null)
    {
        options ??= new();
        ArgumentNullException.ThrowIfNull(options.Limits);
        Limits = options.Limits with { };
        Limits.Validate();
        if (options.MaxOperations is <= 0) throw new ArgumentOutOfRangeException(nameof(options.MaxOperations));
        maximum = options.MaxOperations is long a && Limits.EvaluationWork is long b
            ? Math.Min(a, b) : options.MaxOperations ?? Limits.EvaluationWork;
        diagnosticMaximum = Limits.Diagnostics ?? int.MaxValue;
        CancellationToken = options.CancellationToken;
        CancellationToken.ThrowIfCancellationRequested();
    }

    internal bool ChargeOperations(long amount = 1, XmlLocation location = default)
    {
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
        CancellationToken.ThrowIfCancellationRequested();
        if (!IsComplete) return false;
        if (amount > long.MaxValue - Operations || (maximum is long cap && amount > cap - Operations))
        {
            Refuse("VALIDATION.Limit", "Validation work limit exceeded.", location);
            return false;
        }
        Operations += amount;
        return true;
    }

    internal bool AddDiagnostic(XmlDiagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);
        CancellationToken.ThrowIfCancellationRequested();
        if (!IsComplete) return false;
        diagnostics ??= new();
        if (diagnostics.Count == diagnosticMaximum)
        {
            Refuse("VALIDATION.Limit", "Validation diagnostic limit exceeded.", diagnostic.Location);
            return false;
        }
        diagnostics.Add(diagnostic);
        return true;
    }

    internal void Refuse(string code, string message, XmlLocation location = default)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(message);
        CancellationToken.ThrowIfCancellationRequested();
        if (!IsComplete) return;
        var terminal = new XmlDiagnostic(code, XmlDiagnosticSeverity.Fatal, message, location);
        diagnostics ??= new();
        if (diagnostics.Count == diagnosticMaximum) diagnostics[^1] = terminal;
        else diagnostics.Add(terminal);
        IsComplete = false;
    }

    internal ValidationResult Complete()
    {
        CancellationToken.ThrowIfCancellationRequested();
        return new(IsComplete, diagnostics is null ? Array.Empty<XmlDiagnostic>() : diagnostics);
    }
}
