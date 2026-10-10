namespace Xemele.Parsing.Dtd;

// A refusal is an owned diagnostic. Exceptions from the operation's ledger
// remain user exceptions; consumers do not translate them to grammar failure.
internal interface IDtdBudget
{
    XmlDiagnostic? ChargeExpandedCharacters(long count, XmlLocation location);
    XmlDiagnostic? ChargeName(ReadOnlySpan<char> name, XmlLocation location);
    XmlDiagnostic? EnterEntity(XmlLocation location);
    void ExitEntity();
}
