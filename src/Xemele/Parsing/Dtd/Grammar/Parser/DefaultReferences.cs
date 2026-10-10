namespace Xemele.Parsing.Dtd;
internal sealed partial class DtdParser
{
    private sealed class DefaultFrame(string text, string? name)
    { internal readonly string Text = text; internal readonly string? Name = name; internal int Index; }
    // Only document-entity references covered by Entity Declared are WFCs.
    // Validate against the live table before publishing this default, never the
    // completed table where forward declarations could hide their order. A missing
    // declaration is retained only as a location until whole-subset PE applicability
    // is known; no unknown value or replacement is constructed.
    private bool ValidateDeclaredDefault(string value, XmlLocation at, CancellationToken cancellation)
    {
        if (value.AsSpan().IndexOf('&') < 0 || !nonvalidatingReadState || declarationExternal || declarationInParameter ||
            !standaloneReadState && (externalSubsetReadState || HasParameterReferences)) return true;
        var stack = new Stack<DefaultFrame>();
        var path = new HashSet<string>(StringComparer.Ordinal);
        var checkedEntities = new HashSet<string>(StringComparer.Ordinal);
        stack.Push(new(value, null));
        while (stack.Count != 0)
        {
            cancellation.ThrowIfCancellationRequested();
            var frame = stack.Peek();
            if (frame.Index == frame.Text.Length)
            { stack.Pop(); if (frame.Name is not null) { path.Remove(frame.Name); checkedEntities.Add(frame.Name); } continue; }
            if (frame.Text[frame.Index++] != '&') continue;
            int start = frame.Index;
            while (frame.Index < frame.Text.Length && frame.Text[frame.Index] != ';')
            { cancellation.ThrowIfCancellationRequested(); frame.Index++; }
            if (frame.Index == frame.Text.Length)
            { Reject("Invalid reference in an attribute default.", at, "DTD.InvalidDefault"); return false; }
            var name = frame.Text.AsSpan(start, frame.Index++ - start);
            // Lexical/numeric validity is checked by DecodeLiteral and the
            // existing known-default validator; predefined names need no table.
            if (!name.IsEmpty && name[0] == '#' || name.SequenceEqual("lt") || name.SequenceEqual("gt") ||
                name.SequenceEqual("amp") || name.SequenceEqual("apos") || name.SequenceEqual("quot")) continue;
            DtdEntityDeclaration? entity = null;
            foreach (var candidate in general)
            { cancellation.ThrowIfCancellationRequested(); if (name.SequenceEqual(candidate.Name)) { entity = candidate; break; } }
            if (entity is null || entity.IsExternal || parameterOriginGeneral?.Contains(entity.Name) == true)
            {
                if (standaloneReadState)
                { Reject("Entity must be declared before the attribute default in the document entity.", at, "DTD.UndeclaredEntity"); return false; }
                undeclaredDefault ??= at;
                continue;
            }
            if (checkedEntities.Contains(entity.Name)) continue;
            if (entity.SystemId is not null || entity.NotationName is not null || entity.ReplacementText is null || !path.Add(entity.Name))
            { Reject("Forbidden or recursive entity reference in an attribute default.", at, "DTD.InvalidDefault"); return false; }
            stack.Push(new(entity.ReplacementText, entity.Name));
        }
        return true;
    }
    // This validates only references resolvable in this subset's model. Unknown
    // names remain for the caller's effective (internal + external) table. It
    // neither applies a default nor recharges expansion work at its use site.
    private bool ValidateKnownDefaults(CancellationToken cancellation)
    {
        // An independently parsed external subset lacks the higher-precedence internal table.
        if (sources[0].External || attributes.Count == 0) return true;
        var checkedEntities = new HashSet<string>(StringComparer.Ordinal);
        var path = new HashSet<string>(StringComparer.Ordinal);
        var stack = new Stack<DefaultFrame>();
        foreach (var attribute in attributes)
        {
            cancellation.ThrowIfCancellationRequested();
            if (attribute.DefaultValue is null) continue;
            stack.Push(new(attribute.DefaultValue, null));
            while (stack.Count != 0)
            {
                cancellation.ThrowIfCancellationRequested();
                var frame = stack.Peek();
                if (frame.Index == frame.Text.Length)
                { stack.Pop(); if (frame.Name is not null) { path.Remove(frame.Name); checkedEntities.Add(frame.Name); } continue; }
                char c = frame.Text[frame.Index++];
                if (c == '<') { Reject("Entity replacement contains literal less-than in an attribute default.", attribute.Location, "DTD.InvalidDefault"); return false; }
                if (c != '&') continue;
                int start = frame.Index;
                while (frame.Index < frame.Text.Length && frame.Text[frame.Index] != ';') { cancellation.ThrowIfCancellationRequested(); frame.Index++; }
                if (frame.Index == frame.Text.Length) { Reject("Invalid reference in an attribute default.", attribute.Location, "DTD.InvalidDefault"); return false; }
                ReadOnlySpan<char> name = frame.Text.AsSpan(start, frame.Index++ - start);
                if (name.IsEmpty) { Reject("Empty entity reference.", attribute.Location, "DTD.InvalidDefault"); return false; }
                if (name[0] == '#')
                {
                    int i = 1, radix = 10, value = 0;
                    if (i < name.Length && name[i] == 'x') { radix = 16; i++; }
                    int first = i;
                    for (; i < name.Length; i++)
                    { cancellation.ThrowIfCancellationRequested(); int digit = ValueSyntax.Digit(name[i], radix); if (digit < 0 || value > (0x10FFFF - digit) / radix) break; value = value * radix + digit; }
                    if (i != name.Length || i == first || !ValueSyntax.IsXmlScalar(value)) { Reject("Invalid numeric reference in a default replacement.", attribute.Location, "DTD.InvalidDefault"); return false; }
                    continue;
                }
                bool firstName = true;
                foreach (var rune in name.EnumerateRunes())
                { cancellation.ThrowIfCancellationRequested(); if (firstName ? !ValueSyntax.IsNameStart(rune.Value) : !ValueSyntax.IsNameChar(rune.Value)) { Reject("Invalid entity name in a default replacement.", attribute.Location, "DTD.InvalidDefault"); return false; } firstName = false; }
                if (name.SequenceEqual("lt") || name.SequenceEqual("gt") || name.SequenceEqual("amp") || name.SequenceEqual("apos") || name.SequenceEqual("quot")) continue;
                DtdEntityDeclaration? declaration = null;
                foreach (var entity in general) { cancellation.ThrowIfCancellationRequested(); if (name.SequenceEqual(entity.Name)) { declaration = entity; break; } }
                if (declaration is null) continue;
                if (declaration.SystemId is not null || declaration.NotationName is not null || declaration.ReplacementText is null || path.Contains(declaration.Name))
                { Reject("Forbidden or recursive entity reference in an attribute default.", attribute.Location, "DTD.InvalidDefault"); return false; }
                if (checkedEntities.Contains(declaration.Name)) continue;
                path.Add(declaration.Name); stack.Push(new(declaration.ReplacementText, declaration.Name));
            }
        }
        return true;
    }
}
