using System.Text;
using Xemele.Internal.Operations;
using Xemele.Parsing.Dtd;

namespace Xemele.Parsing.Syntax;

internal sealed partial class XmlParserCore
{
    // Use-site validation and expansion are one bounded walk. A replacement
    // scalar is charged before advancing/scanning it; depth is accepted before
    // allocating a frame. Static declaration validation never uses this path.
    private string? ExpandAttributeLiteral(string literal, IReadOnlyDictionary<string, DtdEntityDeclaration> table, XmlLocation at)
    {
        var output = new StringBuilder();
        var stack = new List<AttributeGraphFrame> { new(literal, null) };
        var active = new HashSet<string>(StringComparer.Ordinal);
        long scalars = 0;
        try
        {
            while (stack.Count != 0 && diagnostic is null)
            {
                activeCancellation.ThrowIfCancellationRequested();
                var frame = stack[^1];
                if (frame.Index == frame.Text.Length)
                {
                    stack.RemoveAt(stack.Count - 1);
                    if (frame.Name is not null) { active.Remove(frame.Name); budget.Release(Quota.EntityDepth, 1); }
                    continue;
                }
                int scalar = TakeAttributeScalar(frame, at);
                if (diagnostic is not null) return null;
                if (scalar == '<') { Reject("XML.Syntax", "Literal less-than in attribute replacement.", at); return null; }
                if (scalar != '&')
                { if (!Append(White(scalar) ? ' ' : scalar)) return null; continue; }
                int start = frame.Index;
                while (frame.Index < frame.Text.Length && frame.Text[frame.Index] != ';')
                { TakeAttributeScalar(frame, at); if (diagnostic is not null) return null; }
                if (frame.Index == frame.Text.Length)
                { Reject("XML.Syntax", "Unfinished attribute reference.", at); return null; }
                int length = frame.Index - start;
                TakeAttributeScalar(frame, at);
                if (diagnostic is not null) return null;
                var name = frame.Text.AsSpan(start, length);
                if (ScalarReference(name))
                {
                    scalar = Reference(name, at);
                    if (diagnostic is not null || !Append(scalar)) return null;
                    continue;
                }
                if (!ValidName(name, false))
                { Reject("XML.Syntax", "Invalid attribute entity reference name.", at); return null; }
                DtdEntityDeclaration? entity = null;
                if (table is Dictionary<string, DtdEntityDeclaration> dictionary)
                    dictionary.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(name, out entity);
                else
                    foreach (var entry in table)
                    { activeCancellation.ThrowIfCancellationRequested(); if (name.SequenceEqual(entry.Key)) { entity = entry.Value; break; } }
                if (entity is null) { Reject("XML.UndeclaredEntity", "Undeclared attribute entity.", at); return null; }
                if (entity.SystemId is not null || entity.NotationName is not null || entity.ReplacementText is null)
                { Reject("XML.Syntax", "External or unparsed entity is forbidden in attributes.", at); return null; }
                if (standalone == true && entity.IsExternal)
                { Reject("XML.Standalone", "Standalone attribute depends on an external entity declaration.", at); return null; }
                if (active.Contains(entity.Name)) { Reject("XML.EntityCycle", "Recursive attribute entity.", at); return null; }
                if (!Charge(Quota.EntityDepth, 1, at)) return null;
                try { stack.Add(new(entity.ReplacementText, entity.Name)); }
                catch { budget.Release(Quota.EntityDepth, 1); throw; }
                active.Add(entity.Name);
            }
            return diagnostic is null ? output.ToString() : null;
        }
        finally
        {
            while (stack.Count != 0)
            {
                var frame = stack[^1]; stack.RemoveAt(stack.Count - 1);
                if (frame.Name is not null) budget.Release(Quota.EntityDepth, 1);
            }
        }

        bool Append(int scalar)
        {
            if (scalars == int.MaxValue || budget.Options.Limits.TokenCharacters is int max && scalars >= max)
            { Reject("XML.ResourceLimit", "Attribute token limit exceeded.", at); return false; }
            int width = scalar > 0xFFFF ? 2 : 1;
            if (output.Length > int.MaxValue - width)
            { Reject("XML.ResourceLimit", "Attribute value capacity exceeded.", at); return false; }
            Span<char> pair = stackalloc char[2]; int count = new Rune(scalar).EncodeToUtf16(pair);
            output.Append(pair[..count]); scalars++; return true;
        }
    }

    private int TakeAttributeScalar(AttributeGraphFrame frame, XmlLocation at)
    {
        activeCancellation.ThrowIfCancellationRequested();
        if (frame.Name is not null && !Charge(Quota.ExpandedCharacters, 1, at)) return 0;
        int scalar = frame.Text[frame.Index++];
        if (char.IsHighSurrogate((char)scalar))
        {
            if (frame.Index == frame.Text.Length || !char.IsLowSurrogate(frame.Text[frame.Index]))
            { Reject("XML.Syntax", "Unpaired surrogate in attribute replacement.", at); return 0; }
            scalar = char.ConvertToUtf32((char)scalar, frame.Text[frame.Index++]);
        }
        if (!XmlScalar(scalar)) { Reject("XML.Syntax", "Invalid attribute replacement scalar.", at); return 0; }
        return scalar;
    }
}
