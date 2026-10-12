namespace Xemele.Validation;
internal sealed partial class XsdValidationRun
{
    private void Attributes(Frame frame)
    {
        var seen = new HashSet<SchemaName>(); Retain();
        foreach (var attribute in frame.Node.Attributes)
        {
            current = document.OriginalLocation(attribute); Work(); Retain();
            Text(attribute.LocalName, true); Text(attribute.NamespaceUri, true); Text(attribute.Prefix, true); Text(attribute.Value, true);
            var name = Name(attribute); seen.Add(name);
            if (attribute.NamespaceUri == Xsi)
            {
                bool valid = true; XmlDatatypeValue? value = null;
                var type = new SchemaName(Xs, attribute.LocalName switch { "type" => "QName", "nil" => "boolean", _ => "anyURI" });
                if (attribute.LocalName is "type" or "nil" or "noNamespaceSchemaLocation") valid = Value(type, attribute.Value, frame.Context, out value);
                else if (attribute.LocalName == "schemaLocation")
                {
                    var library = schema.Datatypes.Find(Xs)!; var normalized = library.Normalize(attribute.Value, XmlWhitespaceRule.Collapse, this); Poll();
                    int tokens = 0; var remaining = normalized.AsSpan();
                    while (!remaining.IsEmpty) { Work(); int end = remaining.IndexOf(' '); if (end < 0) end = remaining.Length; tokens++; remaining = end == remaining.Length ? [] : remaining[(end + 1)..]; }
                    if (tokens % 2 != 0) valid = Error("XSD.Attribute", "xsi:schemaLocation requires namespace/location pairs.");
                }
                else valid = Error("XSD.Attribute", "Unknown schema-instance attribute.");
                frame.Valid &= valid; identity?.Attribute(frame.Node, name, new(valid, type, value), this); Poll(); continue;
            }
            XsdAttributeBinding? binding = null; XsdAttributeDeclaration? declaration = null;
            if (frame.Type is not null) foreach (var candidate in frame.Type.Attributes)
            { Work(); var component = schema.Attributes[candidate.DeclarationId]; if (component.Name == name) { binding = candidate; declaration = component; break; } }
            bool attributeValid = true;
            if (!frame.Skip && declaration is null)
            {
                var wildcard = frame.Type?.AnyAttribute;
                if (wildcard is null || !Allows(wildcard, name.NamespaceUri)) attributeValid = Error("XSD.Attribute", "Attribute is not allowed by this type.");
                else if (wildcard.ProcessContents != XsdProcessContents.Skip)
                {
                    if (schema.GlobalAttributes.TryGetValue(name, out int id)) declaration = schema.Attributes[id];
                    else if (wildcard.ProcessContents == XsdProcessContents.Strict) attributeValid = Error("XSD.Attribute", "Strict wildcard requires an attribute declaration.");
                }
            }
            if (binding?.Use == XsdAttributeUse.Prohibited) attributeValid = Error("XSD.Attribute", "Attribute use is prohibited.");
            XmlDatatypeValue? parsed = null; var declaredType = declaration?.Type ?? new SchemaName(Xs, "anySimpleType");
            if (declaration is not null && !frame.Skip)
            {
                attributeValid &= Value(declaration.Type, attribute.Value, frame.Context, out parsed);
                var fixedLexical = binding?.FixedValue ?? declaration.FixedValue;
                var fixedContext = binding?.FixedValue is not null ? binding.Context : declaration.Context;
                if (parsed is not null && fixedLexical is not null) attributeValid &= Fixed(declaration.Type, parsed, fixedLexical, fixedContext);
            }
            frame.Valid &= attributeValid;
            identity?.Attribute(frame.Node, name, new(attributeValid, ActualType(parsed, declaredType), parsed), this); Poll();
        }
        current = document.OriginalLocation(frame.Node);
        if (frame.Type is null || frame.Skip) return;
        foreach (var binding in frame.Type.Attributes)
        {
            Work(); var declaration = schema.Attributes[binding.DeclarationId];
            if (seen.Contains(declaration.Name) || binding.Use == XsdAttributeUse.Prohibited) continue;
            if (binding.Use == XsdAttributeUse.Required) { frame.Valid = Error("XSD.RequiredAttribute", "A required attribute is missing."); continue; }
            string? lexical = binding.FixedValue ?? binding.DefaultValue ?? declaration.FixedValue ?? declaration.DefaultValue;
            if (lexical is null) continue;
            // Local overrides retain their own namespace/base provenance.
            var context = binding.FixedValue is not null || binding.DefaultValue is not null ? binding.Context : declaration.Context;
            bool valid = Value(declaration.Type, lexical, context, out var value);
            frame.Valid &= valid;
            identity?.Attribute(frame.Node, declaration.Name, new(valid, ActualType(value, declaration.Type), value), this); Poll();
        }
    }
    private bool Allows(XsdWildcard wildcard, string uri)
    {
        bool found = false;
        for (int i = 0; i < wildcard.Namespaces.Count; i++) { Work(); if (wildcard.Namespaces[i] == uri) { found = true; break; } }
        return wildcard.Negated ? !found : found;
    }
    private static SchemaName ActualType(XmlDatatypeValue? value, SchemaName fallback)
    { while (value?.SelectedMember is { } member) value = member; return value?.TypeName ?? fallback; }
    private bool Value(SchemaName name, string lexical, SchemaNamespaceContext context, out XmlDatatypeValue? value, bool track = true)
    {
        Work(); value = null;
        var definition = schema.Datatypes.FindType(name);
        if (definition is null) throw new ArgumentException("Simple type is missing from the sealed executable datatype registry.");
        bool valid = facets.TryValidate(definition, lexical, context, schema.Datatypes, this, out value, out var error); Poll();
        if (!valid) return Error(error?.Code ?? "XSD.Datatype", error?.Message ?? "Value does not satisfy its datatype.");
        if (track && value is not null && !ContextValue(value, lexical, context)) valid = false;
        return valid;
    }
    private bool Fixed(SchemaName type, XmlDatatypeValue value, string lexical, SchemaNamespaceContext context)
    {
        if (!Value(type, lexical, context, out var fixedValue, false)) return false;
        var library = schema.Datatypes.Find(Xs) ?? throw new ArgumentException("Executable XSD datatype library is missing.");
        bool equal = library.ValueEquals(value, fixedValue!, this); Poll();
        return equal || Error("XSD.Fixed", "Value differs from its typed fixed constraint.");
    }
    private bool ContextValue(XmlDatatypeValue value, string lexical, SchemaNamespaceContext context)
    {
        Work(); while (value.SelectedMember is { } selected) { Work(); value = selected; }
        var library = schema.Datatypes.Find(Xs)!;
        var normalized = library.Normalize(lexical, XmlWhitespaceRule.Collapse, this); Poll();
        if (value.Items is { } items)
        {
            var remaining = normalized.AsSpan(); bool valid = true;
            foreach (var item in items)
            {
                Work(); int end = remaining.IndexOf(' '); if (end < 0) end = remaining.Length;
                valid &= AtomicContext(item, remaining[..end], context);
                remaining = end == remaining.Length ? [] : remaining[(end + 1)..];
            }
            return valid;
        }
        return AtomicContext(value, normalized, context);
    }
    private bool AtomicContext(XmlDatatypeValue value, ReadOnlySpan<char> lexical, SchemaNamespaceContext context)
    {
        while (value.SelectedMember is { } selected) { Work(); value = selected; }
        var type = schema.Datatypes.FindType(value.TypeName); bool enumeration = false; string? special = null;
        var visited = new HashSet<SchemaName>(); Retain();
        while (type is not null)
        {
            Work(); Retain(); if (!visited.Add(type.Name)) throw new ArgumentException("Cyclic sealed datatype graph.");
            foreach (var facet in type.Facets)
            {
                Work();
                if (facet.Kind == XmlFacetKind.Enumeration) enumeration = true;
            }
            if (type.Name.NamespaceUri == Xs && type.Name.LocalName is "ID" or "IDREF" or "ENTITY" or "NOTATION") { special = type.Name.LocalName; break; }
            type = type.BaseType is { } parent ? schema.Datatypes.FindType(parent) : null;
        }
        if (special == "ID")
        {
            Work(lexical.Length + 1L); Retain(); ids ??= new(StringComparer.Ordinal);
            if (!ids.Add(new string(lexical))) return Error("XSD.DuplicateId", "ID value is already used in this document.");
        }
        else if (special == "IDREF")
        { Work(lexical.Length + 1L); Retain(); references ??= new(StringComparer.Ordinal); references.TryAdd(new string(lexical), current); }
        else if (special == "ENTITY")
        {
            if (document.Dtd is not { UnreadParameterLocation: null } dtd) return Error("XSD.Entity", "ENTITY requires retained complete DTD unparsed entity declarations.");
            foreach (var entity in dtd.GeneralEntities) { Work(entity.Name.Length + 1L); if (entity.NotationName is not null && lexical.SequenceEqual(entity.Name)) return true; }
            return Error("XSD.Entity", "ENTITY does not name a declared unparsed entity.");
        }
        else if (special == "NOTATION")
        {
            if (!enumeration) return Error("XSD.Notation", "NOTATION requires an effective enumeration restriction.");
            if (!QName(lexical, context, out var notation) || !schema.Notations.ContainsKey(notation)) return Error("XSD.Notation", "NOTATION does not name a declared schema notation.");
            // Declaration validity of all authored enumeration values is the
            // facade's prepublication obligation, including unused definitions.
        }
        return true;
    }
    private bool QName(ReadOnlySpan<char> lexical, SchemaNamespaceContext context, out SchemaName name)
    {
        Work(lexical.Length + 1L); name = default;
        int colon = lexical.IndexOf(':'); var local = colon < 0 ? lexical : lexical[(colon + 1)..];
        if (local.IsEmpty || local.IndexOf(':') >= 0 || colon == 0) return false;
        if (!NcName(local) || colon >= 0 && !NcName(lexical[..colon])) return false;
        string uri;
        if (colon < 0) uri = context.DefaultNamespace;
        else
        {
            uri = ""; bool found = false;
            foreach (var prefix in context.Prefixes) { Work(); if (lexical[..colon].SequenceEqual(prefix.Key)) { uri = prefix.Value; found = true; break; } }
            if (!found) return false;
        }
        Retain(); name = new(uri, new string(local)); return true;
    }
    private bool NcName(ReadOnlySpan<char> name)
    {
        bool first = true;
        foreach (var rune in name.EnumerateRunes()) { Work(); if (rune.Value == ':' || !TextBinaryNames.Contains(rune.Value, !first, this)) return false; first = false; }
        return !first;
    }
    private bool Derived(SchemaName actual, SchemaName declared, IReadOnlyList<XsdElementBlock> elementBlock, IReadOnlyList<XsdTypeBlock> typeBlock)
    {
        var visited = new HashSet<SchemaName>(); Retain();
        while (actual != declared)
        {
            Work(); Retain(); if (!visited.Add(actual) || !types.TryGetValue(actual, out var type) || type.BaseType is not { } parent) return false;
            var derivation = type.Derivation;
            if (derivation == XsdDerivation.Extension && (elementBlock.Contains(XsdElementBlock.Extension) || typeBlock.Contains(XsdTypeBlock.Extension)) ||
                derivation == XsdDerivation.Restriction && (elementBlock.Contains(XsdElementBlock.Restriction) || typeBlock.Contains(XsdTypeBlock.Restriction))) return false;
            if (types.TryGetValue(parent, out var baseType) && baseType.Final.Contains(derivation)) return false;
            if (schema.Datatypes.FindType(parent) is { } simpleBase)
            {
                var simpleDerivation = derivation switch { XsdDerivation.List => XmlDatatypeDerivation.List, XsdDerivation.Union => XmlDatatypeDerivation.Union, _ => XmlDatatypeDerivation.Restriction };
                if (simpleBase.Final.Contains(simpleDerivation)) return false;
            }
            actual = parent;
        }
        return true;
    }
}
