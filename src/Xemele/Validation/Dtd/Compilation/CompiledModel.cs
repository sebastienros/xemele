using System.Buffers;
using System.Text;
using Xemele.Parsing.Dtd;

namespace Xemele.Validation;

// All storage belongs to compilation; run cursors and budgets belong to callers.
internal sealed class CompiledDtdModel
{
    internal DtdModel Source { get; }
    internal IReadOnlyList<CompiledDtdElement> Elements { get; }
    internal IReadOnlyList<CompiledDtdAttribute> Attributes { get; }
    internal CompiledDtdModel(DtdModel source, List<CompiledDtdElement> elements, List<CompiledDtdAttribute> attributes)
    { Source = source; Elements = Array.AsReadOnly(elements.ToArray()); Attributes = Array.AsReadOnly(attributes.ToArray()); }

    internal CompiledDtdElement? FindElement(ReadOnlySpan<char> name, ValidationOperation operation)
    {
        foreach (var element in Elements)
        { if (!DtdLexical.Equal(name, element.Declaration.Name, operation)) { if (!operation.IsComplete) return null; continue; } return element; }
        return null;
    }
    internal CompiledDtdAttribute? FindAttribute(ReadOnlySpan<char> elementName, ReadOnlySpan<char> name, ValidationOperation operation)
    {
        foreach (var attribute in Attributes)
        {
            if (DtdLexical.Equal(elementName, attribute.Declaration.ElementName, operation) && DtdLexical.Equal(name, attribute.Declaration.Name, operation)) return attribute;
            if (!operation.IsComplete) return null;
        }
        return null;
    }
    internal bool HasNotation(ReadOnlySpan<char> name, ValidationOperation operation)
    {
        foreach (var notation in Source.Notations)
        { if (DtdLexical.Equal(name, notation.Name, operation)) return true; if (!operation.IsComplete) return false; }
        return false;
    }
    internal bool HasUnparsedEntity(ReadOnlySpan<char> name, ValidationOperation operation)
    {
        foreach (var entity in Source.GeneralEntities)
        {
            if (DtdLexical.Equal(name, entity.Name, operation)) return entity.NotationName is not null && HasNotation(entity.NotationName, operation);
            if (!operation.IsComplete) return false;
        }
        return false;
    }
}
internal sealed record CompiledDtdElement(DtdElementDeclaration Declaration, DtdContentModel Content);
internal sealed record CompiledDtdAttribute(DtdAttributeDeclaration Declaration, string? NormalizedDefault)
{
    // ID uniqueness and IDREF resolution are instance-wide evaluator responsibilities.
    internal bool IsValueValid(ReadOnlySpan<char> value, CompiledDtdModel model, ValidationOperation operation, XmlLocation location = default)
    {
        var type = Declaration.Type;
        if (type == DtdAttributeType.CData) return DtdLexical.XmlText(value, operation, location);
        if (type is DtdAttributeType.Enumeration or DtdAttributeType.Notation)
        {
            if (!DtdLexical.Name(value, type == DtdAttributeType.Enumeration, false, operation, location)) return false;
            bool found = false;
            foreach (var token in Declaration.Tokens)
            { if (DtdLexical.Equal(value, token, operation, location)) { found = true; break; } if (!operation.IsComplete) return false; }
            return found && (type != DtdAttributeType.Notation || model.HasNotation(value, operation));
        }
        bool multiple = type is DtdAttributeType.IdRefs or DtdAttributeType.Entities or DtdAttributeType.NmTokens;
        if (!multiple) return Token(value);
        // Values must already be normalized: one U+0020 between nonempty tokens.
        if (value.IsEmpty) return false;
        while (!value.IsEmpty)
        {
            int end = 0;
            while (end < value.Length && value[end] != ' ')
            { if (!operation.ChargeOperations(1, location)) return false; end++; }
            if (!Token(value[..end])) return false;
            if (end == value.Length) return true;
            value = value[(end + 1)..];
            if (value.IsEmpty) return false;
        }
        return false;

        bool Token(ReadOnlySpan<char> token) => DtdLexical.Name(token, type is DtdAttributeType.NmToken or DtdAttributeType.NmTokens, false, operation, location) &&
            (type is not (DtdAttributeType.Entity or DtdAttributeType.Entities) || model.HasUnparsedEntity(token, operation));
    }
}
internal static class DtdLexical
{
    internal static bool Equal(ReadOnlySpan<char> value, ReadOnlySpan<char> other, ValidationOperation operation, XmlLocation location = default)
    {
        if (!operation.ChargeOperations(1, location)) return false;
        if (value.Length != other.Length) return false;
        for (int i = 0; i < value.Length; i++)
        { if (!operation.ChargeOperations(1, location)) return false; if (value[i] != other[i]) return false; }
        return true;
    }
    internal static bool Name(ReadOnlySpan<char> value, bool nmToken, bool ncName, ValidationOperation operation, XmlLocation location = default)
    {
        if (value.IsEmpty) return false;
        bool first = true; int scalars = 0;
        while (!value.IsEmpty)
        {
            if (!operation.ChargeOperations(1, location)) return false;
            if (Rune.DecodeFromUtf16(value, out var rune, out int consumed) != OperationStatus.Done) return false;
            if (scalars == int.MaxValue || operation.Limits.TokenCharacters is int cap && scalars >= cap)
            { operation.Refuse("VALIDATION.Limit", "Name/token limit exceeded.", location); return false; }
            scalars++;
            int c = rune.Value;
            if (ncName && c == ':' || (first && !nmToken ? !ValueSyntax.IsNameStart(c) : !ValueSyntax.IsNameChar(c))) return false;
            first = false; value = value[consumed..];
        }
        return true;
    }
    internal static bool XmlText(ReadOnlySpan<char> value, ValidationOperation operation, XmlLocation location = default)
    {
        while (!value.IsEmpty)
        {
            if (!operation.ChargeOperations(1, location)) return false;
            if (Rune.DecodeFromUtf16(value, out var rune, out int count) != OperationStatus.Done || !ValueSyntax.IsXmlScalar(rune.Value)) return false;
            value = value[count..];
        }
        return true;
    }
}
