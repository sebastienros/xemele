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
        if (!CheckAttributeEntities(literal, table, at, true)) return null;
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

    private sealed class AttributeGraphFrame(string text, string? name)
    {
        internal readonly string Text = text;
        internal readonly string? Name = name;
        internal int Index;
    }

    private bool CheckAttributeEntities(string literal, IReadOnlyDictionary<string, DtdEntityDeclaration> table, XmlLocation at, bool useSite)
    {
        activeCancellation.ThrowIfCancellationRequested();
        if (!literal.AsSpan().ContainsAny('&', '<')) return true;
        var seen = new HashSet<string>(StringComparer.Ordinal);
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
            if (ScalarReference(name)) continue;
            if (useSite ? !ValidName(name, false) : !EntityName(name))
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
            if (useSite && standalone == true && entity.IsExternal)
            { Reject("XML.Standalone", "Standalone attribute depends on an external entity declaration.", at); return false; }
            if (active.Contains(entity.Name)) { Reject("XML.EntityCycle", "Recursive attribute entity.", at); return false; }
            if (seen.Contains(entity.Name)) continue;
            active.Add(entity.Name); stack.Add(new(entity.ReplacementText, entity.Name));
        }
        return true;
    }

    private bool ValidateEffectiveDefaults()
    {
        var table = AttributeEntities();
        foreach (var declaration in entities!.Model.Attributes)
        {
            activeCancellation.ThrowIfCancellationRequested();
            if (declaration.DefaultValue is not null &&
                !CheckAttributeEntities(declaration.DefaultValue, table, declaration.Location, false)) return false;
        }
        return true;
    }
}
