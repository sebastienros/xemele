namespace Xemele.Validation;

internal enum DatatypeFamily
{
    Numeric, Temporal, TextBinary
}
internal enum DatatypePrimitive
{
    String, Boolean, Decimal, Float, Double, Duration, DateTime, Time, Date, GYearMonth, GYear, GMonthDay, GDay, GMonth, HexBinary, Base64Binary, AnyUri, QName, Notation
}
internal readonly record struct DatatypeMetadata(string LocalName, string? BaseType, DatatypePrimitive? Primitive,
    DatatypeFamily Family, XmlDatatypeVariety? Variety, XmlWhitespaceRule Whitespace, bool WhitespaceFixed, string? ItemType = null);

internal static class DatatypeTypes
{
    internal const string XsdNamespace = "http://www.w3.org/2001/XMLSchema";
    internal const string RngNamespace = "http://www.w3.org/2001/XMLSchema-datatypes";
    // Immutable setup, ordinal keys. Names are never inferred from CLR type names.
    private static readonly System.Collections.Frozen.FrozenDictionary<string, DatatypeMetadata> Types = Create();
    internal static IReadOnlyDictionary<string, DatatypeMetadata> All => Types;
    internal static bool TryGet(SchemaName name, out DatatypeMetadata metadata)
    {
        if (name.NamespaceUri != XsdNamespace)
        {
            metadata = default;
            return false;
        }
        return Types.TryGetValue(name.LocalName, out metadata);
    }
    internal static SchemaName Name(string localName) => new(XsdNamespace, localName);
    private static System.Collections.Frozen.FrozenDictionary<string, DatatypeMetadata> Create()
    {
        var types = new Dictionary<string, DatatypeMetadata>(StringComparer.Ordinal);
        void Add(string name, string? parent, DatatypePrimitive? primitive, DatatypeFamily family,
            XmlWhitespaceRule whitespace = XmlWhitespaceRule.Collapse, bool fixedWhitespace = true,
            XmlDatatypeVariety? variety = XmlDatatypeVariety.Atomic, string? item = null) =>
            types.Add(name, new(name, parent, primitive, family, variety, whitespace, fixedWhitespace, item));
        Add("anySimpleType", null, null, DatatypeFamily.TextBinary, XmlWhitespaceRule.Preserve, false, null);
        Add("string", "anySimpleType", DatatypePrimitive.String, DatatypeFamily.TextBinary, XmlWhitespaceRule.Preserve, false);
        Add("normalizedString", "string", DatatypePrimitive.String, DatatypeFamily.TextBinary, XmlWhitespaceRule.Replace, false);
        Add("token", "normalizedString", DatatypePrimitive.String, DatatypeFamily.TextBinary, fixedWhitespace: false);
        Add("language", "token", DatatypePrimitive.String, DatatypeFamily.TextBinary, fixedWhitespace: false);
        Add("Name", "token", DatatypePrimitive.String, DatatypeFamily.TextBinary, fixedWhitespace: false);
        Add("NMTOKEN", "token", DatatypePrimitive.String, DatatypeFamily.TextBinary, fixedWhitespace: false);
        Add("NCName", "Name", DatatypePrimitive.String, DatatypeFamily.TextBinary, fixedWhitespace: false);
        foreach (var name in new[] { "ID", "IDREF", "ENTITY" })
        {
            Add(name, "NCName", DatatypePrimitive.String, DatatypeFamily.TextBinary, fixedWhitespace: false);
        }

        foreach (var pair in new[] { ("NMTOKENS", "NMTOKEN"), ("IDREFS", "IDREF"), ("ENTITIES", "ENTITY") })
        {
            Add(pair.Item1, "anySimpleType", null, DatatypeFamily.TextBinary, variety: XmlDatatypeVariety.List, item: pair.Item2);
        }

        Add("boolean", "anySimpleType", DatatypePrimitive.Boolean, DatatypeFamily.TextBinary);
        Add("decimal", "anySimpleType", DatatypePrimitive.Decimal, DatatypeFamily.Numeric);
        Add("float", "anySimpleType", DatatypePrimitive.Float, DatatypeFamily.Numeric);
        Add("double", "anySimpleType", DatatypePrimitive.Double, DatatypeFamily.Numeric);
        Add("integer", "decimal", DatatypePrimitive.Decimal, DatatypeFamily.Numeric);
        foreach (var pair in new[] { ("nonPositiveInteger", "integer"), ("negativeInteger", "nonPositiveInteger"), ("long", "integer"), ("int", "long"), ("short", "int"), ("byte", "short"), ("nonNegativeInteger", "integer"), ("unsignedLong", "nonNegativeInteger"), ("unsignedInt", "unsignedLong"), ("unsignedShort", "unsignedInt"), ("unsignedByte", "unsignedShort"), ("positiveInteger", "nonNegativeInteger") })
        {
            Add(pair.Item1, pair.Item2, DatatypePrimitive.Decimal, DatatypeFamily.Numeric);
        }

        foreach (var pair in new[] { ("duration", DatatypePrimitive.Duration), ("dateTime", DatatypePrimitive.DateTime), ("time", DatatypePrimitive.Time), ("date", DatatypePrimitive.Date), ("gYearMonth", DatatypePrimitive.GYearMonth), ("gYear", DatatypePrimitive.GYear), ("gMonthDay", DatatypePrimitive.GMonthDay), ("gDay", DatatypePrimitive.GDay), ("gMonth", DatatypePrimitive.GMonth) })
        {
            Add(pair.Item1, "anySimpleType", pair.Item2, DatatypeFamily.Temporal);
        }

        foreach (var pair in new[] { ("hexBinary", DatatypePrimitive.HexBinary), ("base64Binary", DatatypePrimitive.Base64Binary), ("anyURI", DatatypePrimitive.AnyUri), ("QName", DatatypePrimitive.QName), ("NOTATION", DatatypePrimitive.Notation) })
        {
            Add(pair.Item1, "anySimpleType", pair.Item2, DatatypeFamily.TextBinary);
        }

        return System.Collections.Frozen.FrozenDictionary.ToFrozenDictionary(types, StringComparer.Ordinal);
    }
}
