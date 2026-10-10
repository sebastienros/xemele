using System.Text;
using Xemele.Internal.Operations;
using Xemele.Parsing.Dtd;

namespace Xemele.Parsing.Syntax;

internal sealed partial class XmlParserCore
{
    private sealed record AttributeInterpretation(string LexicalValue, XmlLocation ValueLocation,
        XmlAttributeValuePart[] Parts, bool IsTokenized, int? DefaultSequence = null, bool ExternalDependency = false);
    private string? ExpandAttributeLiteral(string literal, IReadOnlyDictionary<string, DtdEntityDeclaration> table,
        XmlLocation at, XmlLocation[]? locations, bool[]? origins, out XmlAttributeValuePart[]? incomplete, bool enforceStandalone = true, object?[]? physicalIdentities = null)
    {
        incomplete = null;
        var output = new StringBuilder();
        var parts = new List<XmlAttributeValuePart>();
        var stack = new List<AttributeGraphFrame> { new(literal, null, locations, origins, at, physicalIdentities) };
        var active = new HashSet<string>(StringComparer.Ordinal);
        long scalars = 0; bool unknown = false;
        AttributeGraphFrame? runFrame = null; object? runIdentity = null; XmlLocation runLocation = at;
        try
        {
            while (stack.Count != 0 && diagnostic is null)
            {
                activeCancellation.ThrowIfCancellationRequested();
                var frame = stack[^1];
                if (frame.Index == frame.Text.Length)
                {
                    Flush(); runFrame = null;
                    stack.RemoveAt(stack.Count - 1);
                    if (frame.Name is not null) { active.Remove(frame.Name); budget.Release(Quota.EntityDepth, 1); }
                    continue;
                }
                int index = frame.Index; XmlLocation location = frame.At(index);
                bool excluded = frame.Origins is { } o && o[index];
                object? identity = frame.PhysicalIdentities?[index];
                if (runFrame != frame || !ReferenceEquals(runIdentity, identity))
                { Flush(); runFrame = frame; runIdentity = identity; runLocation = location; }
                int scalar = TakeAttributeScalar(frame, location);
                if (diagnostic is not null) return null;
                if (scalar == '<') { Reject("XML.Syntax", "Literal less-than in attribute replacement.", location); return null; }
                if (scalar != '&') { if (!Append(White(scalar) ? ' ' : scalar, location)) return null; continue; }
                int start = frame.Index;
                while (frame.Index < frame.Text.Length && frame.Text[frame.Index] != ';')
                { TakeAttributeScalar(frame, location); if (diagnostic is not null) return null; }
                if (frame.Index == frame.Text.Length)
                { Reject("XML.Syntax", "Unfinished attribute reference.", location); return null; }
                int length = frame.Index - start; TakeAttributeScalar(frame, location);
                if (diagnostic is not null) return null;
                var name = frame.Text.AsSpan(start, length);
                if (ScalarReference(name))
                { scalar = Reference(name, location); if (diagnostic is not null || !Append(scalar, location)) return null; continue; }
                if (!ValidName(name, false))
                { Reject("XML.Syntax", "Invalid attribute entity reference name.", location); return null; }
                DtdEntityDeclaration? entity = null;
                if (table is Dictionary<string, DtdEntityDeclaration> dictionary)
                    dictionary.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(name, out entity);
                else foreach (var entry in table)
                { activeCancellation.ThrowIfCancellationRequested(); if (name.SequenceEqual(entry.Key)) { entity = entry.Value; break; } }
                if (entity is null)
                {
                    if (!excluded && !AllowsUnresolvedContent)
                    { Reject("XML.UndeclaredEntity", "Undeclared attribute entity.", location); return null; }
                    Flush(); runFrame = null;
                    int count = 2;
                    foreach (var rune in name.EnumerateRunes()) { activeCancellation.ThrowIfCancellationRequested(); count++; }
                    if (!Reserve(count, location)) return null;
                    string ownedName = Intern(name, location);
                    if (diagnostic is not null) return null;
                    if (parts.Count == Array.MaxLength) { Reject("XML.ResourceLimit", "Attribute part capacity exceeded.", location); return null; }
                    parts.Add(new XmlUnknownAttributeReference(ownedName, new string(frame.Text.AsSpan(start - 1, length + 2)), location));
                    unknown = true; continue;
                }
                if (entity.SystemId is not null || entity.NotationName is not null || entity.ReplacementText is null)
                { Reject("XML.Syntax", "External or unparsed entity is forbidden in attributes.", location); return null; }
                if (enforceStandalone && standalone == true && entity.IsExternal)
                { Reject("XML.Standalone", "Standalone attribute depends on an external entity declaration.", location); return null; }
                if (active.Contains(entity.Name)) { Reject("XML.EntityCycle", "Recursive attribute entity.", location); return null; }
                if (!Charge(Quota.EntityDepth, 1, location)) return null;
                Flush(); runFrame = null;
                DtdLiteralInfo.Entities.TryGetValue(entity, out var info);
                try { stack.Add(new(entity.ReplacementText, entity.Name, info?.Locations, info?.ExternalOrigins, info?.ValueLocation ?? entity.Location, info?.PhysicalIdentities)); }
                catch { budget.Release(Quota.EntityDepth, 1); throw; }
                active.Add(entity.Name);
            }
            if (diagnostic is not null) return null;
            if (unknown) { incomplete = parts.ToArray(); return null; }
            // Complete values retain ordinary normalization; owned parts are only
            // intermediate declaration/expansion scratch and never public provenance.
            var complete = new StringBuilder();
            foreach (XmlKnownAttributeText part in parts) complete.Append(part.Text);
            return complete.ToString();
        }
        finally
        {
            while (stack.Count != 0)
            { var frame = stack[^1]; stack.RemoveAt(stack.Count - 1); if (frame.Name is not null) budget.Release(Quota.EntityDepth, 1); }
        }
        void Flush()
        {
            if (output.Length == 0) return;
            if (parts.Count == Array.MaxLength) { Reject("XML.ResourceLimit", "Attribute part capacity exceeded.", runLocation); return; }
            parts.Add(new XmlKnownAttributeText(output.ToString(), runLocation)); output.Clear();
        }
        bool Reserve(int count, XmlLocation location)
        {
            if (count > int.MaxValue - scalars || budget.Options.Limits.TokenCharacters is int max && count > max - scalars)
            { Reject("XML.ResourceLimit", "Attribute token limit exceeded.", location); return false; }
            scalars += count; return true;
        }
        bool Append(int scalar, XmlLocation location)
        {
            if (!Reserve(1, location)) return false;
            int width = scalar > 0xFFFF ? 2 : 1;
            if (output.Length > int.MaxValue - width)
            { Reject("XML.ResourceLimit", "Attribute value capacity exceeded.", location); return false; }
            Span<char> pair = stackalloc char[2]; int count = new Rune(scalar).EncodeToUtf16(pair);
            output.Append(pair[..count]); return true;
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
