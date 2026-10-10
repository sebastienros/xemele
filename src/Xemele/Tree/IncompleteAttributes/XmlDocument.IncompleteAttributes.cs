namespace Xemele;

public sealed partial class XmlDocument
{
    private List<XmlIncompleteAttributeDefault>? incompleteAttributeDefaults;
    private List<XmlIncompleteAttributeValue>? incompleteAttributeValues;
    private IReadOnlyList<XmlIncompleteAttributeDefault>? incompleteAttributeDefaultView;
    private IReadOnlyList<XmlIncompleteAttributeValue>? incompleteAttributeValueView;
    private HashSet<int>? incompleteAttributeSlots;
    public IReadOnlyList<XmlIncompleteAttributeDefault> IncompleteAttributeDefaults =>
        incompleteAttributeDefaultView ?? Array.Empty<XmlIncompleteAttributeDefault>();
    public IReadOnlyList<XmlIncompleteAttributeValue> IncompleteAttributeValues =>
        incompleteAttributeValueView ?? Array.Empty<XmlIncompleteAttributeValue>();
    internal bool HasIncompleteAttributes => incompleteAttributeDefaults is { Count: > 0 } || incompleteAttributeValues is { Count: > 0 };
    internal string ReadAttributeValue(int slot)
    {
        if (incompleteAttributeSlots?.Contains(slot) == true)
            throw new InvalidOperationException("Attribute value contains unresolved entity references.");
        return AttributeSlots[slot].Value;
    }
    internal bool CanAddIncompleteDefault => (incompleteAttributeDefaults?.Count ?? 0) < Array.MaxLength;
    internal bool CanAddIncompleteValue => (incompleteAttributeValues?.Count ?? 0) < Array.MaxLength;
    internal void AddIncompleteDefault(XmlIncompleteAttributeDefault declaration)
    {
        incompleteAttributeDefaults ??= [];
        incompleteAttributeDefaultView ??= incompleteAttributeDefaults.AsReadOnly();
        PreflightNames(declaration.ElementName, declaration.AttributeName);
        string element = Intern(declaration.ElementName), attribute = Intern(declaration.AttributeName);
        incompleteAttributeDefaults.Add(declaration with { ElementName = element, AttributeName = attribute });
    }
    internal void AddIncompleteValue(XmlAttribute attribute, XmlNode parent, string lexicalValue, bool isTokenized,
        XmlLocation location, int? defaultSequence, XmlAttributeValuePart[]? parts)
    {
        incompleteAttributeValues ??= []; incompleteAttributeSlots ??= [];
        incompleteAttributeValueView ??= incompleteAttributeValues.AsReadOnly();
        var declaration = defaultSequence is int index ? incompleteAttributeDefaults![index] : null;
        var persistedParts = declaration?.Parts ?? Array.AsReadOnly(parts!);
        var value = new XmlIncompleteAttributeValue(attribute, parent, lexicalValue, isTokenized, location,
            declaration, persistedParts, incompleteAttributeValues.Count);
        incompleteAttributeValues.Add(value); incompleteAttributeSlots.Add(attribute.Slot);
        // Builder persistence is atomic through the callback: only the sidecar
        // represents this member's value when control returns to any consumer.
        AttributeSlots[attribute.Slot].Value = null!;
    }
}
