namespace Xemele.Validation;

// Family implementations own immutable scalar/binary representations. This is not
// an advertised library and provides no parser until all family lanes compose.
internal interface IDatatypeFamilyProvider
{
    DatatypeFamily Family { get; }
    bool TryParse(DatatypeMetadata type, ReadOnlySpan<char> normalized, SchemaNamespaceContext context,
        ISchemaOperation operation, out DatatypeAtomicValue? value, out XmlDatatypeError? error);
    bool ValueEquals(DatatypeAtomicValue left, DatatypeAtomicValue right, ISchemaOperation operation);
    XmlValueOrder Compare(DatatypeAtomicValue left, DatatypeAtomicValue right, ISchemaOperation operation);
    int ValueHashCode(DatatypeAtomicValue value, ISchemaOperation operation);
    long? ValueLength(DatatypeAtomicValue value, ISchemaOperation operation);
    int? TotalDigits(DatatypeAtomicValue value, ISchemaOperation operation);
    int? FractionDigits(DatatypeAtomicValue value, ISchemaOperation operation);
}

internal abstract class DatatypeAtomicValue : XmlDatatypeValue
{
    private readonly SchemaName type;
    internal IDatatypeFamilyProvider Provider { get; }
    internal DatatypePrimitive Primitive { get; }
    protected DatatypeAtomicValue(SchemaName type, DatatypePrimitive primitive, IDatatypeFamilyProvider provider)
    {
        DatatypeValues.RequireName(type); ArgumentNullException.ThrowIfNull(provider);
        if (!Enum.IsDefined(primitive)) throw new ArgumentOutOfRangeException(nameof(primitive));
        if (DatatypeValues.Family(primitive) != provider.Family) throw new ArgumentException("Primitive belongs to another family.", nameof(provider));
        this.type = type; Primitive = primitive; Provider = provider;
    }
    internal sealed override SchemaName TypeName => type;
    internal sealed override XmlDatatypeVariety Variety => XmlDatatypeVariety.Atomic;
    internal sealed override IReadOnlyList<XmlDatatypeValue>? Items => null;
    internal sealed override XmlDatatypeValue? SelectedMember => null;
}

internal static class DatatypeErrors
{
    internal static readonly XmlDatatypeError InvalidUnicode = new("XSD.Datatype.Unicode", "Invalid Unicode scalar in datatype lexical input.");
    internal static readonly XmlDatatypeError Stopped = new("XSD.Datatype.Limit", "Datatype operation stopped.");
    internal static readonly XmlDatatypeError InvalidLexical = new("XSD.Datatype.Lexical", "Invalid datatype lexical value.");
    internal static readonly XmlDatatypeError UnknownType = new("XSD.Datatype.Type", "Unknown datatype name.");
}
