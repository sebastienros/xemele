namespace Xemele.Validation;

public sealed record ValidationResult
{
    public bool IsComplete { get; }
    public bool IsValid { get; }
    public IReadOnlyList<XmlDiagnostic> Diagnostics { get; }

    public ValidationResult(bool isComplete, IEnumerable<XmlDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        var snapshot = new List<XmlDiagnostic>();
        bool error = false, fatal = false;
        foreach (var diagnostic in diagnostics)
        {
            if (diagnostic is null) throw new ArgumentException("Null diagnostic.", nameof(diagnostics));
            snapshot.Add(diagnostic);
            error |= diagnostic.Severity is XmlDiagnosticSeverity.Error or XmlDiagnosticSeverity.Fatal;
            fatal |= diagnostic.Severity == XmlDiagnosticSeverity.Fatal;
        }
        if (!isComplete && !fatal)
            throw new ArgumentException("Incomplete results require a fatal diagnostic.", nameof(diagnostics));
        IsComplete = isComplete;
        IsValid = isComplete && !error;
        Diagnostics = Array.AsReadOnly(snapshot.ToArray());
    }
}
