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
        foreach (var declaration in dtdAttributes)
        {
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
        attribute.OwnedValue = NormalizeAttribute(literal, AttributeDeclaration(AttributeName(attribute)), attribute.Location);
        return diagnostic is null;
    }

    private string? NormalizeAttribute(string literal, DtdAttributeDeclaration? declaration, XmlLocation at)
    {
        var table = AttributeEntities();
        if (standalone == true && !CheckStandaloneAttributeEntities(literal, table, at)) return null;
        var result = DtdValueProcessor.NormalizeAttributeDefault(literal, DtdAttributeType.CData,
            table, budget.Options.Limits, at, entityBudget, activeCancellation);
        if (result.Diagnostic is not null) { diagnostic = result.Diagnostic; return null; }
        string value = result.Value!;
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
        foreach (var declaration in dtdAttributes!)
        {
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
            string? value = NormalizeAttribute(declaration.DefaultValue, declaration, opening);
            if (diagnostic is not null) return false;
            if (attributeCount == attributes.Length)
            {
                if (attributeCount == Array.MaxLength) { Reject("XML.ResourceLimit", "Attribute capacity exceeded.", opening); return false; }
                Array.Resize(ref attributes, (int)Math.Min(Array.MaxLength, (long)attributes.Length * 2));
            }
            attributes[attributeCount++] = new() { OwnedName = declaration.Name, OwnedValue = value, Location = opening };
        }
        return true;
    }

    private bool CheckStandaloneAttributeEntities(string literal, IReadOnlyDictionary<string, DtdEntityDeclaration> table, XmlLocation at)
    {
        // Inspect the reachable declaration graph once, before expansion. This
        // avoids a second expansion (and second ledger charge) for standalone.
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var stack = new Stack<string>(); stack.Push(literal);
        while (stack.Count != 0)
        {
            string text = stack.Pop();
            for (int i = 0; i < text.Length; i++)
            {
                Work(i); if (text[i] != '&') continue;
                int start = ++i;
                while (i < text.Length && text[i] != ';') { Work(i); i++; }
                if (i == text.Length) break;
                var name = text.AsSpan(start, i - start);
                if (ScalarReference(name)) continue;
                DtdEntityDeclaration? entity = null;
                foreach (var entry in table)
                { activeCancellation.ThrowIfCancellationRequested(); if (name.SequenceEqual(entry.Key)) { entity = entry.Value; break; } }
                if (entity is null) continue;
                if (entity.IsExternal)
                { Reject("XML.Standalone", "Standalone attribute depends on an external entity declaration.", at); return false; }
                if (entity.ReplacementText is not null && seen.Add(entity.Name)) stack.Push(entity.ReplacementText);
            }
        }
        return true;
    }
}
