namespace Xemele;

/// <summary>Specifies the severity of an XML diagnostic.</summary>
public enum XmlDiagnosticSeverity
{
    /// <summary>An issue that does not itself prevent processing.</summary>
    Warning,
    /// <summary>An XML error that may permit recovery.</summary>
    Error,
    /// <summary>An error that prevents processing from continuing.</summary>
    Fatal
}
/// <summary>Identifies a position in an XML source.</summary>
/// <param name="SourceUri">The source URI, or <see langword="null"/> when unavailable.</param>
/// <param name="LineNumber">The source line number, using one-based numbering when known.</param>
/// <param name="LinePosition">The position within the line, using one-based numbering when known.</param>
/// <param name="ByteOffset">The byte offset in the source, or <see langword="null"/> when unavailable.</param>
public readonly record struct XmlLocation(Uri? SourceUri, int LineNumber, int LinePosition, long? ByteOffset);
/// <summary>Describes an XML processing issue at a source location.</summary>
/// <param name="Code">The machine-readable diagnostic code.</param>
/// <param name="Severity">The severity of the issue.</param>
/// <param name="Message">The human-readable description.</param>
/// <param name="Location">The source location associated with the issue.</param>
public sealed record XmlDiagnostic(string Code, XmlDiagnosticSeverity Severity, string Message, XmlLocation Location);
/// <summary>The exception thrown when strict XML parsing fails.</summary>
public sealed class XmlParseException : Exception
{
    /// <summary>Gets the read-only snapshot of diagnostics explaining the failure.</summary>
    public IReadOnlyList<XmlDiagnostic> Diagnostics
    {
        get;
    }
    /// <summary>Creates a parse exception from a nonempty sequence of diagnostics.</summary>
    /// <param name="diagnostics">The diagnostics to copy into the exception.</param>
    /// <exception cref="ArgumentNullException"><paramref name="diagnostics"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="diagnostics"/> is empty.</exception>
    public XmlParseException(IEnumerable<XmlDiagnostic> diagnostics) : base("XML parsing failed.")
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
        if (Diagnostics.Count == 0)
        {
            throw new ArgumentException("A parse exception requires diagnostics.", nameof(diagnostics));
        }
    }
}
/// <summary>Contains a parsed value, its completion status, and parsing diagnostics.</summary>
/// <typeparam name="T">The reference type produced by parsing.</typeparam>
public sealed record XmlParseResult<T> where T : class
{
    /// <summary>Gets the parsed or recovered value, or <see langword="null"/> when no usable value was produced.</summary>
    public T? Value
    {
        get;
    }
    /// <summary>Gets whether parsing completed rather than stopping prematurely.</summary>
    public bool IsComplete
    {
        get;
    }
    /// <summary>Gets whether parsing produced a complete, well-formed value.</summary>
    public bool IsWellFormed
    {
        get;
    }
    /// <summary>Gets the read-only snapshot of diagnostics collected during parsing.</summary>
    public IReadOnlyList<XmlDiagnostic> Diagnostics
    {
        get;
    }
    /// <summary>Creates a result with a snapshot of the supplied diagnostics.</summary>
    /// <param name="value">The parsed or recovered value, if any.</param>
    /// <param name="isComplete">Whether parsing completed.</param>
    /// <param name="isWellFormed">Whether the complete value is well-formed.</param>
    /// <param name="diagnostics">The diagnostics to copy into the result.</param>
    /// <exception cref="ArgumentNullException"><paramref name="diagnostics"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="isWellFormed"/> is true but the value is absent or incomplete.</exception>
    public XmlParseResult(T? value, bool isComplete, bool isWellFormed, IEnumerable<XmlDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        if (isWellFormed && (!isComplete || value is null))
        {
            throw new ArgumentException("Well-formed results require a complete value.");
        }

        Value = value;
        IsComplete = isComplete;
        IsWellFormed = isWellFormed;
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
    }
}
