using Xemele;
using Xemele.Parsing.Dtd;
// Common-only demonstration operation accounting, not a production entity context.
internal sealed class OperationLedger : IDtdBudget
{
    internal long Expanded;
    internal int Names, Depth;
    private readonly HashSet<string> retainedNames = new(StringComparer.Ordinal);
    public XmlDiagnostic? ChargeExpandedCharacters(long count, XmlLocation location)
    { Expanded = checked(Expanded + count); return null; }
    public XmlDiagnostic? ChargeName(ReadOnlySpan<char> name, XmlLocation location)
    { foreach (var retained in retainedNames) if (name.SequenceEqual(retained)) return null;
      retainedNames.Add(name.ToString()); Names = checked(Names + 1); return null; }
    public XmlDiagnostic? EnterEntity(XmlLocation location) { Depth = checked(Depth + 1); return null; }
    public void ExitEntity() { if (Depth <= 0) throw new InvalidOperationException("Unbalanced entity depth."); Depth--; }
}
