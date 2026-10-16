namespace Xemele.Validation;

/// <summary>Contains validation completion status and a detached, read-only diagnostic snapshot.</summary>
public sealed record ValidationResult
{
    /// <summary>Gets whether validation finished rather than stopping with a fatal diagnostic.</summary>
    public bool IsComplete
    {
        get;
    }
    /// <summary>Gets whether validation completed without error or fatal diagnostics.</summary>
    public bool IsValid
    {
        get;
    }
    /// <summary>Gets the diagnostics in their supplied order.</summary>
    public IReadOnlyList<XmlDiagnostic> Diagnostics
    {
        get;
    }

    /// <summary>Creates a result, copying the diagnostics and deriving validity from their severities.</summary>
    /// <param name="isComplete">Whether the validation operation completed.</param>
    /// <param name="diagnostics">The diagnostics to snapshot.</param>
    /// <exception cref="ArgumentNullException"><paramref name="diagnostics"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A diagnostic is null, or an incomplete result has no fatal diagnostic.</exception>
    public ValidationResult(bool isComplete, IEnumerable<XmlDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        var snapshot = new List<XmlDiagnostic>();
        bool error = false, fatal = false;
        foreach (var diagnostic in diagnostics)
        {
            if (diagnostic is null)
            {
                throw new ArgumentException("Null diagnostic.", nameof(diagnostics));
            }

            snapshot.Add(diagnostic);
            error |= diagnostic.Severity is XmlDiagnosticSeverity.Error or XmlDiagnosticSeverity.Fatal;
            fatal |= diagnostic.Severity == XmlDiagnosticSeverity.Fatal;
        }
        if (!isComplete && !fatal)
        {
            throw new ArgumentException("Incomplete results require a fatal diagnostic.", nameof(diagnostics));
        }

        IsComplete = isComplete;
        IsValid = isComplete && !error;
        Diagnostics = Array.AsReadOnly(snapshot.ToArray());
    }
}
