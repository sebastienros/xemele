namespace Xemele;
public enum XmlDiagnosticSeverity { Warning, Error, Fatal }
public readonly record struct XmlLocation(Uri? SourceUri, int LineNumber, int LinePosition, long? ByteOffset);
public sealed record XmlDiagnostic(string Code, XmlDiagnosticSeverity Severity, string Message, XmlLocation Location);
public sealed class XmlParseException : Exception
{
    public IReadOnlyList<XmlDiagnostic> Diagnostics { get; }
    public XmlParseException(IEnumerable<XmlDiagnostic> diagnostics) : base("XML parsing failed.")
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
        if (Diagnostics.Count == 0) throw new ArgumentException("A parse exception requires diagnostics.", nameof(diagnostics));
    }
}
public sealed record XmlParseResult<T> where T : class
{
    public T? Value { get; }
    public bool IsComplete { get; }
    public bool IsWellFormed { get; }
    public IReadOnlyList<XmlDiagnostic> Diagnostics { get; }
    public XmlParseResult(T? value, bool isComplete, bool isWellFormed, IEnumerable<XmlDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        if (isWellFormed && (!isComplete || value is null)) throw new ArgumentException("Well-formed results require a complete value.");
        Value = value; IsComplete = isComplete; IsWellFormed = isWellFormed;
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
    }
}
