namespace Xemele.Parsing.Recovery;

// One operation owns the list; the public result takes a separate snapshot.
// Reserve the final finite slot for terminal rejection/truncation.
internal sealed class RecoveryDiagnostics(int? limit)
{
    private readonly List<XmlDiagnostic> items = [];
    internal IReadOnlyList<XmlDiagnostic> Items => items;
    internal bool Repair(string code, string message, XmlLocation location, out XmlDiagnostic? terminal)
    {
        terminal = null;
        if (items.Count >= (limit ?? int.MaxValue) - 1)
        {
            terminal = new("XML.DiagnosticLimit", XmlDiagnosticSeverity.Error, "Recovery diagnostic capacity exhausted.", location);
            items.Add(terminal);
            return false;
        }
        items.Add(new(code, XmlDiagnosticSeverity.Error, message, location));
        return true;
    }
    internal void Reject(XmlDiagnostic? diagnostic)
    {
        if (diagnostic is null || (items.Count != 0 && ReferenceEquals(items[^1], diagnostic)))
        {
            return;
        }

        if (items.Count < (limit ?? int.MaxValue))
        {
            items.Add(diagnostic);
        }
    }
}
