using System.Text;
using Xemele.Internal.Operations;
using Xemele.Parsing.Dtd;
using Xemele.Parsing.Entities;

namespace Xemele.Parsing.Syntax;

internal sealed partial class XmlParserCore
{
    private IReadOnlyList<DtdAttributeDeclaration>? dtdAttributes;
    private IReadOnlyDictionary<string, DtdEntityDeclaration>? attributeEntities;
    private Dictionary<string, XmlLocation>? ids;

    private ReadOnlySpan<char> AttributeName(in AttributeToken attribute) => attribute.OwnedName is { } name
        ? name.AsSpan() : token.AsSpan(attribute.NameStart, attribute.NameLength);
    private ReadOnlySpan<char> AttributeValue(in AttributeToken attribute) => attribute.OwnedValue is { } value
        ? value.AsSpan() : token.AsSpan(attribute.ValueStart, attribute.ValueLength);

    private DtdAttributeDeclaration? AttributeDeclaration(ReadOnlySpan<char> name)
    {
        if (dtdAttributes is null) return null;
        for (int d = 0; d < dtdAttributes.Count; d++)
        {
            var declaration = dtdAttributes[d];
            activeCancellation.ThrowIfCancellationRequested();
            if (name.SequenceEqual(declaration.Name)) return declaration;
        }
        return null;
    }

    private IReadOnlyDictionary<string, DtdEntityDeclaration> AttributeEntities()
    {
        if (attributeEntities is not null) return attributeEntities;
        if (entities is XmlEntityContext context) return attributeEntities = context.GeneralEntities;
        var table = new Dictionary<string, DtdEntityDeclaration>(StringComparer.Ordinal);
        foreach (var declaration in entities!.Model.GeneralEntities)
        { activeCancellation.ThrowIfCancellationRequested(); table.TryAdd(declaration.Name, declaration); }
        return attributeEntities = table;
    }

    private bool ReadDtdAttribute(ref int i, ref AttributeToken attribute, char delimiter)
    {
        int start = i;
        while (i < tokenLength && token[i] != delimiter) { Work(i); i++; }
        if (i == tokenLength) { Reject("XML.Syntax", "Unfinished attribute.", attribute.Location); return false; }
        // Normalization owns this bounded result through pending callbacks. It
        // cannot overwrite unconsumed tag suffixes when entities grow the value.
        string literal = new(token.AsSpan(start, i++ - start));
        var declaration = AttributeDeclaration(AttributeName(attribute));
        var locations = attributePhysicalLocations.AsSpan(start, literal.Length).ToArray();
        var origins = attributePhysicalOrigins.AsSpan(start, literal.Length).ToArray();
        var physicalIdentities = attributePhysicalIdentities.AsSpan(start, literal.Length).ToArray();
        string? lexical = physicalTag.Length == 0 ? literal : OwnPhysicalAttribute(physicalStarts[start], physicalStarts[i - 1], attribute.Location);
        if (diagnostic is not null) return false;
        var valueLocation = locations.Length == 0 ? tokenLocations[i - 1] : locations[0];
        attribute.OwnedValue = ExpandAttributeLiteral(literal, AttributeEntities(), valueLocation, locations, origins, out var parts, physicalIdentities: physicalIdentities);
        if (parts is not null) attribute.Incomplete = new(lexical!, valueLocation, parts, declaration is not null && declaration.Type != DtdAttributeType.CData);
        else if (attribute.OwnedValue is { } complete) attribute.OwnedValue = NormalizeKnownAttribute(complete, declaration, valueLocation);
        return diagnostic is null;
    }

    private string? OwnPhysicalAttribute(int start, int end, XmlLocation at)
    {
        long scalars = 0;
        for (int i = start; i < end; i++)
        {
            Work(i);
            if (scalars == int.MaxValue || budget.Options.Limits.TokenCharacters is int max && scalars >= max)
            { Reject("XML.ResourceLimit", "Physical attribute literal limit exceeded.", at); return null; }
            scalars++; if (char.IsHighSurrogate(physicalTag[i])) i++;
        }
        return physicalTag.ToString(start, end - start);
    }

    private string? NormalizeAttribute(string literal, DtdAttributeDeclaration? declaration, XmlLocation at)
    {
        var table = AttributeEntities();
        string? value = ExpandAttributeLiteral(literal, table, at, null, null, out _);
        return value is null ? null : NormalizeKnownAttribute(value, declaration, at);
    }

    private string? NormalizeKnownAttribute(string value, DtdAttributeDeclaration? declaration, XmlLocation at)
    {
        if (declaration is not null && declaration.Type != DtdAttributeType.CData)
        {
            string normalized = CollapseSpaces(value);
            if (standalone == true && declaration.IsExternal && normalized != value)
            { Reject("XML.Standalone", "Standalone document depends on external attribute normalization.", at); return null; }
            value = normalized;
        }
        if (declaration?.Type == DtdAttributeType.Id)
        {
            ids ??= new(StringComparer.Ordinal);
            // Duplicate IDs and lexical type constraints are validity conditions;
            // retain the first location without changing nonvalidating acceptance.
            ids.TryAdd(value, at);
        }
        return value;
    }

    private string CollapseSpaces(string value)
    {
        int first = 0, last = value.Length;
        while (first < last && value[first] == ' ') { Work(first); first++; }
        while (last > first && value[last - 1] == ' ') { Work(last); last--; }
        bool duplicate = false;
        for (int i = first + 1; i < last; i++) { Work(i); if (value[i] == ' ' && value[i - 1] == ' ') { duplicate = true; break; } }
        if (first == 0 && last == value.Length && !duplicate) return value;
        var normalized = new StringBuilder(last - first); bool space = false;
        for (int i = first; i < last; i++)
        {
            Work(i); char c = value[i];
            if (c == ' ') { space = true; continue; }
            if (space) { normalized.Append(' '); space = false; }
            normalized.Append(c);
        }
        return normalized.ToString();
    }

    private bool StandaloneElementWhitespace(ReadOnlySpan<char> elementName)
    {
        if (standalone != true || entities is null) return false;
        foreach (var element in entities.Model.Elements)
        {
            activeCancellation.ThrowIfCancellationRequested();
            if (elementName.SequenceEqual(element.Name))
                return element.IsExternal && element.Content.Kind is DtdParticleKind.Name or DtdParticleKind.Sequence or DtdParticleKind.Choice;
        }
        return false;
    }

    private bool ApplyDefaultAttributes(ReadOnlySpan<char> elementName)
    {
        for (int d = 0; d < dtdAttributes!.Count; d++)
        {
            var declaration = dtdAttributes[d];
            activeCancellation.ThrowIfCancellationRequested();
            bool explicitValue = false;
            for (int i = 0; i < attributeCount; i++)
            {
                Work(i);
                if (AttributeName(attributes[i]).SequenceEqual(declaration.Name)) { explicitValue = true; break; }
            }
            if (explicitValue || declaration.DefaultValue is null) continue;
            if (standalone == true && declaration.IsExternal)
            { Reject("XML.Standalone", "Standalone document depends on an external default attribute.", opening); return false; }
            if (!ValidName(declaration.Name, true)) { Reject("XML.Namespace", "Default attribute is not a namespace name.", opening); return false; }
            if (!Charge(Quota.AttributesPerElement, 1, opening)) return false;
            AttributeInterpretation? incomplete = null;
            incompleteDefaultByDeclaration?.TryGetValue(declaration, out incomplete);
            if (standalone == true && incomplete?.ExternalDependency == true)
            { Reject("XML.Standalone", "Standalone attribute depends on an external entity declaration.", opening); return false; }
            string? value = incomplete is null ? NormalizeAttribute(declaration.DefaultValue, declaration, opening) : null;
            if (diagnostic is not null) return false;
            if (attributeCount == attributes.Length)
            {
                if (attributeCount == Array.MaxLength) { Reject("XML.ResourceLimit", "Attribute capacity exceeded.", opening); return false; }
                Array.Resize(ref attributes, (int)Math.Min(Array.MaxLength, (long)attributes.Length * 2));
            }
            attributes[attributeCount++] = new() { OwnedName = declaration.Name, OwnedValue = value, Location = opening, Incomplete = incomplete };
        }
        return true;
    }

    private sealed class AttributeGraphFrame(string text, string? name, XmlLocation[]? locations = null, bool[]? origins = null, XmlLocation at = default, object?[]? physicalIdentities = null)
    {
        internal readonly string Text = text;
        internal readonly string? Name = name;
        internal int Index;
        internal readonly XmlLocation[]? Locations = locations;
        internal readonly bool[]? Origins = origins;
        internal readonly XmlLocation Initial = at;
        internal readonly object?[]? PhysicalIdentities = physicalIdentities;
        internal XmlLocation At(int index) => Locations is not null ? Locations[index] : Initial;
    }

    private bool CheckAttributeEntities(string literal, IReadOnlyDictionary<string, DtdEntityDeclaration> table, XmlLocation at, HashSet<string> seen)
    {
        activeCancellation.ThrowIfCancellationRequested();
        var active = new HashSet<string>(StringComparer.Ordinal);
        var stack = new List<AttributeGraphFrame> { new(literal, null) };
        while (stack.Count != 0)
        {
            activeCancellation.ThrowIfCancellationRequested();
            var frame = stack[^1];
            if (frame.Index == frame.Text.Length)
            {
                stack.RemoveAt(stack.Count - 1);
                if (frame.Name is not null) { active.Remove(frame.Name); seen.Add(frame.Name); }
                continue;
            }
            int c = frame.Text[frame.Index++];
            if (c == '<') { Reject("XML.Syntax", "Literal less-than in attribute replacement.", at); return false; }
            if (c != '&') continue;
            int start = frame.Index;
            while (frame.Index < frame.Text.Length && frame.Text[frame.Index] != ';')
            { Work(frame.Index); frame.Index++; }
            if (frame.Index == frame.Text.Length) { Reject("XML.Syntax", "Unfinished attribute reference.", at); return false; }
            var name = frame.Text.AsSpan(start, frame.Index++ - start);
            if (ScalarReference(name))
            { ScalarReferenceValue(name, at); if (diagnostic is not null) return false; continue; }
            if (!EntityName(name))
            { Reject("XML.Syntax", "Invalid attribute entity reference name.", at); return false; }
            DtdEntityDeclaration? entity = null;
            if (table is Dictionary<string, DtdEntityDeclaration> dictionary)
                dictionary.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(name, out entity);
            else
                foreach (var entry in table)
                { activeCancellation.ThrowIfCancellationRequested(); if (name.SequenceEqual(entry.Key)) { entity = entry.Value; break; } }
            if (entity is null) { Reject("XML.UndeclaredEntity", "Undeclared attribute entity.", at); return false; }
            if (entity.SystemId is not null || entity.NotationName is not null || entity.ReplacementText is null)
            { Reject("XML.Syntax", "External or unparsed entity is forbidden in attributes.", at); return false; }
            if (active.Contains(entity.Name)) { Reject("XML.EntityCycle", "Recursive attribute entity.", at); return false; }
            if (seen.Contains(entity.Name)) continue;
            active.Add(entity.Name); stack.Add(new(entity.ReplacementText, entity.Name));
        }
        return true;
    }

    private sealed record IncompleteDefault(string ElementName, string AttributeName, bool IsExternal,
        XmlLocation Location, AttributeInterpretation Interpretation);
    private List<IncompleteDefault>? incompleteDefaults;
    private Dictionary<DtdAttributeDeclaration, AttributeInterpretation>? incompleteDefaultByDeclaration;
    private int incompleteDefaultIndex;
    private static XmlLocation FirstUnknown(XmlAttributeValuePart[] parts)
    { foreach (var part in parts) if (part is XmlUnknownAttributeReference) return part.Location; throw new InvalidOperationException("Missing unknown part."); }
    private bool PreflightIncompleteDefaults(IXmlEventSink sink)
    {
        if (incompleteDefaults is not { Count: > 0 } || sink is IXmlIncompleteAttributeSink) return true;
        Reject("XML.IncompleteAttributeSinkRequired", "Sink cannot represent incomplete attribute defaults.", FirstUnknown(incompleteDefaults[0].Interpretation.Parts)); return false;
    }
    private bool ValidateEffectiveDefaults()
    {
        var finalTable = AttributeEntities();
        foreach (var declaration in entities!.Model.Attributes)
        {
            activeCancellation.ThrowIfCancellationRequested();
            if (declaration.DefaultValue is not { } value) continue;
            DtdLiteralInfo.Defaults.TryGetValue(declaration, out var info);
            var table = info?.PrecedingEntities ?? finalTable;
            // A known direct forward declaration is the independent declaration-
            // order WFC, even when the missing-declaration sentence is VC-only.
            for (int i = 0; i < value.Length; i++)
            {
                Work(i); if (value[i] != '&') continue;
                int start = ++i; while (i < value.Length && value[i] != ';') { Work(i); i++; }
                var name = value.AsSpan(start, i - start);
                if (ScalarReference(name)) continue;
                if (!Contains(table, name) && Contains(finalTable, name))
                { Reject("DTD.InvalidDefault", "General entity must precede its direct use in an attribute default.", info?.Locations[start - 1] ?? declaration.Location); return false; }
            }
            bool missing = HasUnknown(value, table, new HashSet<string>(StringComparer.Ordinal), out bool externalDependency);
            if (diagnostic is not null) return false;
            if (!missing)
            {
                if (!CheckAttributeEntities(value, table, declaration.Location, new(StringComparer.Ordinal))) return false;
                continue;
            }
            string? complete = ExpandAttributeLiteral(value, table, info?.ValueLocation ?? declaration.Location,
                info?.Locations, info?.ExternalOrigins, out var parts, enforceStandalone: false, physicalIdentities: info?.PhysicalIdentities);
            if (diagnostic is not null) return false;
            if (parts is null) continue;
            incompleteDefaults ??= []; incompleteDefaultByDeclaration ??= new(ReferenceEqualityComparer.Instance);
            if (incompleteDefaults.Count == Array.MaxLength) { Reject("XML.ResourceLimit", "Incomplete default capacity exceeded.", declaration.Location); return false; }
            var interpretation = new AttributeInterpretation(info?.LexicalValue ?? value, info?.ValueLocation ?? declaration.Location,
                parts, declaration.Type != DtdAttributeType.CData, incompleteDefaults.Count, externalDependency);
            incompleteDefaults.Add(new(declaration.ElementName, declaration.Name, declaration.IsExternal, info?.DeclarationLocation ?? declaration.Location, interpretation));
            incompleteDefaultByDeclaration.Add(declaration, interpretation);
        }
        return true;
    }
    private bool Contains(IReadOnlyDictionary<string, DtdEntityDeclaration> table, ReadOnlySpan<char> name)
    { foreach (var entry in table) { activeCancellation.ThrowIfCancellationRequested(); if (name.SequenceEqual(entry.Key)) return true; } return false; }
    private bool HasUnknown(string value, IReadOnlyDictionary<string, DtdEntityDeclaration> table, HashSet<string> seen, out bool externalDependency)
    {
        externalDependency = false;
        var stack = new List<AttributeGraphFrame> { new(value, null) }; bool missing = false;
        while (stack.Count != 0)
        {
            activeCancellation.ThrowIfCancellationRequested(); var frame = stack[^1];
            if (frame.Index == frame.Text.Length) { stack.RemoveAt(stack.Count - 1); continue; }
            if (frame.Text[frame.Index++] != '&') continue;
            int start = frame.Index; while (frame.Index < frame.Text.Length && frame.Text[frame.Index] != ';') { Work(frame.Index); frame.Index++; }
            if (frame.Index == frame.Text.Length) { Reject("DTD.InvalidDefault", "Unfinished default reference.", opening); return false; }
            var name = frame.Text.AsSpan(start, frame.Index++ - start); if (ScalarReference(name)) continue;
            DtdEntityDeclaration? entity = null;
            foreach (var entry in table) { activeCancellation.ThrowIfCancellationRequested(); if (name.SequenceEqual(entry.Key)) { entity = entry.Value; break; } }
            if (entity is null) { missing = true; continue; }
            externalDependency |= entity.IsExternal;
            if (entity.ReplacementText is { } text && seen.Add(entity.Name)) stack.Add(new(text, entity.Name));
        }
        return missing;
    }
}
