namespace Xemele.Internal.Operations;

// Lazily allocated, owned diagnostic retention; snapshots never expose the list.
internal sealed class DiagnosticBuffer
{
    private readonly int maximum;
    private List<XmlDiagnostic>? entries;
    private bool truncated;
    internal DiagnosticBuffer(int? maximum)
    {
        if (maximum is <= 0) throw new ArgumentOutOfRangeException(nameof(maximum));
        this.maximum = maximum ?? int.MaxValue;
    }
    internal void Add(XmlDiagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);
        if (truncated) return;
        entries ??= new();
        if (entries.Count < maximum) { entries.Add(diagnostic); return; }
        entries[^1] = new("common.diagnostics-limit", XmlDiagnosticSeverity.Fatal,
            "Diagnostic retention limit exceeded.", diagnostic.Location);
        truncated = true;
    }
    internal IReadOnlyList<XmlDiagnostic> Snapshot() =>
        Array.AsReadOnly(entries?.ToArray() ?? []);
}
