using Xemele.Internal.Operations;
using Xemele.Parsing.Dtd;

namespace Xemele.Parsing.Entities;

// DTD declarations and core namespace/name interning use the same retained-name
// table. All expansion consumers use the same aggregate operation counters.
internal sealed class XmlEntityBudget(OperationBudget operation, Dictionary<string, string> retainedNames) : IDtdBudget
{
    public XmlDiagnostic? ChargeExpandedCharacters(long count, XmlLocation location) =>
        Charge(Quota.ExpandedCharacters, count, location);

    public XmlDiagnostic? EnterEntity(XmlLocation location) => Charge(Quota.EntityDepth, 1, location);

    public void ExitEntity() => operation.Release(Quota.EntityDepth, 1);

    public XmlDiagnostic? ChargeName(ReadOnlySpan<char> name, XmlLocation location)
    {
        if (name.IsEmpty)
        {
            return null;
        }

        var lookup = retainedNames.GetAlternateLookup<ReadOnlySpan<char>>();
        if (lookup.ContainsKey(name))
        {
            return null;
        }

        var refusal = Charge(Quota.InternedNames, 1, location);
        if (refusal is not null)
        {
            return refusal;
        }

        string owned = new(name);
        retainedNames.Add(owned, owned);
        return null;
    }

    private XmlDiagnostic? Charge(Quota quota, long count, XmlLocation location) =>
        operation.TryCharge(quota, count) ? null : new("XML.ResourceLimit", XmlDiagnosticSeverity.Error,
            "XML resource limit exceeded.", location);
}
