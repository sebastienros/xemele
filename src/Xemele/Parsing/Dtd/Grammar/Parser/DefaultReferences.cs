namespace Xemele.Parsing.Dtd;
internal sealed partial class DtdParser
{
    private sealed class DefaultFrame(string text, string? name)
    { internal readonly string Text = text; internal readonly string? Name = name; internal int Index; }
    // This validates only references resolvable in this subset's model. Unknown
    // names remain for the caller's effective (internal + external) table. It
    // neither applies a default nor recharges expansion work at its use site.
    private bool ValidateKnownDefaults(CancellationToken cancellation)
    {
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
                if (name[0] == '#' || name.SequenceEqual("lt") || name.SequenceEqual("gt") || name.SequenceEqual("amp") || name.SequenceEqual("apos") || name.SequenceEqual("quot")) continue;
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
