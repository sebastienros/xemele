using System.Buffers;
using System.Text;
namespace Xemele.Validation;

// Each registry owns one immutable provider set. Namespace adapters share this set.
internal sealed class XsdDatatypeLibrary : IXmlDatatypeLibrary
{
    private readonly NumericDatatypeProvider numeric = new();
    private readonly TemporalDatatypeProvider temporal = new();
    private readonly TextBinaryDatatypeProvider text = new();
    public string NamespaceUri => DatatypeTypes.XsdNamespace;
    public bool IsDefined(SchemaName name) => DatatypeTypes.TryGet(name, out _);
    public XmlWhitespaceRule Whitespace(SchemaName name) => DatatypeTypes.TryGet(name, out var metadata)
        ? metadata.Whitespace : throw new ArgumentException("Unknown datatype name.", nameof(name));
    public string Normalize(ReadOnlySpan<char> lexical, XmlWhitespaceRule rule, ISchemaOperation operation) =>
        DatatypeNormalization.Normalize(lexical, rule, operation);

    public bool TryParse(SchemaName type, ReadOnlySpan<char> lexical, SchemaNamespaceContext context,
        ISchemaOperation operation, out XmlDatatypeValue? value, out XmlDatatypeError? error)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(operation);
        value = null;
        error = null;
        if (!operation.Charge(1, default))
        {
            error = DatatypeErrors.Stopped;
            return false;
        }
        if (!DatatypeTypes.TryGet(type, out var metadata))
        {
            error = DatatypeErrors.UnknownType;
            return false;
        }
        if (!DatatypeNormalization.TryMeasure(lexical, metadata.Whitespace, operation, out var length, out _, out error))
        {
            return false;
        }

        char[]? rented = null;
        // Only normalized-capacity scratch is rented; no normalized string is owned.
        // The preliminary measure and core's measure/write cost 3N scalar work.
        var scratch = length <= 256 ? stackalloc char[length] : default;
        try
        {
            if (length > 256)
            {
                if (!operation.Charge(1, default) || !operation.ChargeComponents(1, default))
                {
                    error = DatatypeErrors.Stopped;
                    return false;
                }
                rented = ArrayPool<char>.Shared.Rent(length);
                scratch = rented.AsSpan(0, length);
            }
            if (!DatatypeNormalization.TryNormalize(lexical, metadata.Whitespace, scratch, operation, out var written, out error))
            {
                return false;
            }

            if (metadata.Variety == XmlDatatypeVariety.List)
            {
                return ParseList(type, metadata, scratch[..written], context, operation, out value, out error);
            }

            if (!Provider(metadata.Family).TryParse(metadata, scratch[..written], context, operation, out var atomic, out error))
            {
                return false;
            }

            if (!operation.Charge(0, default))
            {
                error = DatatypeErrors.Stopped;
                return false;
            }
            value = atomic;
            return true;
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<char>.Shared.Return(rented);
            }
        }
    }

    private IDatatypeFamilyProvider Provider(DatatypeFamily family) => family switch
    {
        DatatypeFamily.Numeric => numeric,
        DatatypeFamily.Temporal => temporal,
        _ => text
    };

    private bool ParseList(SchemaName type, DatatypeMetadata metadata, ReadOnlySpan<char> normalized,
        SchemaNamespaceContext context, ISchemaOperation operation, out XmlDatatypeValue? value, out XmlDatatypeError? error)
    {
        value = null;
        error = DatatypeErrors.InvalidLexical;
        if (normalized.IsEmpty)
        {
            return false;
        }

        var count = 1;
        for (var i = 0; i < normalized.Length;)
        {
            if (!operation.Charge(1, default))
            {
                error = DatatypeErrors.Stopped;
                return false;
            }
            Rune.DecodeFromUtf16(normalized[i..], out var rune, out var consumed);
            i += consumed;
            if (rune.Value == ' ')
            {
                count++;
            }
        }
        if (!operation.Charge(1, default) || !operation.ChargeComponents(count + 1, default))
        {
            error = DatatypeErrors.Stopped;
            return false;
        }
        // Private, temporary exact-capacity references; CreateList makes the owned snapshot.
        var items = new XmlDatatypeValue[count];
        if (!operation.Charge(1, default))
        {
            error = DatatypeErrors.Stopped;
            return false;
        }
        var itemType = DatatypeTypes.All[metadata.ItemType!];
        int start = 0, item = 0;
        for (var i = 0; i <= normalized.Length;)
        {
            var end = i == normalized.Length;
            if (!end)
            {
                if (!operation.Charge(1, default))
                {
                    error = DatatypeErrors.Stopped;
                    return false;
                }
                Rune.DecodeFromUtf16(normalized[i..], out var rune, out var consumed);
                if (rune.Value != ' ')
                {
                    i += consumed;
                    continue;
                }
            }
            if (!Provider(itemType.Family).TryParse(itemType, normalized[start..i], context, operation, out var atomic, out error))
            {
                return false;
            }

            if (!operation.Charge(1, default))
            {
                error = DatatypeErrors.Stopped;
                return false;
            }
            items[item++] = atomic!;
            if (end)
            {
                break;
            }

            start = ++i;
        }
        // Core throws a structured failure on quota refusal. No caller exception is caught.
        if (!operation.Charge(0, default))
        {
            error = DatatypeErrors.Stopped;
            return false;
        }
        try
        {
            value = DatatypeValues.CreateList(type, items, new DatatypeStructuredOperation(operation));
        }
        catch (DatatypeStructuredOperation.StoppedException)
        {
            value = null;
            error = DatatypeErrors.Stopped;
            return false;
        }
        error = null;
        return true;
    }

    public bool ValueEquals(XmlDatatypeValue left, XmlDatatypeValue right, ISchemaOperation operation) => DatatypeValues.ValueEquals(left, right, operation);
    public XmlValueOrder Compare(XmlDatatypeValue left, XmlDatatypeValue right, ISchemaOperation operation) => DatatypeValues.Compare(left, right, operation);
    public int ValueHashCode(XmlDatatypeValue value, ISchemaOperation operation) => DatatypeValues.ValueHashCode(value, operation);
    public long? ValueLength(XmlDatatypeValue value, ISchemaOperation operation) => DatatypeValues.ValueLength(value, operation);
    public int? TotalDigits(XmlDatatypeValue value, ISchemaOperation operation) => DatatypeValues.TotalDigits(value, operation);
    public int? FractionDigits(XmlDatatypeValue value, ISchemaOperation operation) => DatatypeValues.FractionDigits(value, operation);
    public XmlDatatypeValue Retype(SchemaName type, XmlDatatypeValue value, ISchemaOperation operation) => DatatypeValues.Retype(type, value, operation);
    public XmlDatatypeValue CreateList(SchemaName type, IReadOnlyList<XmlDatatypeValue> items, ISchemaOperation operation) => DatatypeValues.CreateList(type, items, operation);
    public XmlDatatypeValue CreateUnion(SchemaName type, XmlDatatypeValue selectedMember, ISchemaOperation operation) => DatatypeValues.CreateUnion(type, selectedMember, operation);
}

internal sealed class RngDatatypeLibrary(XsdDatatypeLibrary library, bool empty) : IXmlDatatypeLibrary
{
    public string NamespaceUri => empty ? "" : DatatypeTypes.RngNamespace;
    private bool Canonical(SchemaName name, out SchemaName canonical)
    {
        canonical = DatatypeTypes.Name(name.LocalName);
        return name.NamespaceUri == NamespaceUri && (!empty || name.LocalName is "string" or "token");
    }
    public bool IsDefined(SchemaName name) => Canonical(name, out var canonical) && library.IsDefined(canonical);
    public XmlWhitespaceRule Whitespace(SchemaName name) => Canonical(name, out var canonical)
        ? library.Whitespace(canonical) : throw new ArgumentException("Unknown datatype name.", nameof(name));
    public bool TryParse(SchemaName type, ReadOnlySpan<char> lexical, SchemaNamespaceContext context,
        ISchemaOperation operation, out XmlDatatypeValue? value, out XmlDatatypeError? error)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(operation);
        value = null;
        error = null;
        if (!operation.Charge(1, default))
        {
            error = DatatypeErrors.Stopped;
            return false;
        }
        if (!Canonical(type, out var canonical))
        {
            error = DatatypeErrors.UnknownType;
            return false;
        }
        if (!library.TryParse(canonical, lexical, context, operation, out var parsed, out error))
        {
            return false;
        }

        try
        {
            value = library.Retype(type, parsed!, new DatatypeStructuredOperation(operation));
        }
        catch (DatatypeStructuredOperation.StoppedException)
        {
            error = DatatypeErrors.Stopped;
            return false;
        }
        return true;
    }
    public string Normalize(ReadOnlySpan<char> lexical, XmlWhitespaceRule rule, ISchemaOperation operation) => library.Normalize(lexical, rule, operation);
    public bool ValueEquals(XmlDatatypeValue left, XmlDatatypeValue right, ISchemaOperation operation) => library.ValueEquals(left, right, operation);
    public XmlValueOrder Compare(XmlDatatypeValue left, XmlDatatypeValue right, ISchemaOperation operation) => library.Compare(left, right, operation);
    public int ValueHashCode(XmlDatatypeValue value, ISchemaOperation operation) => library.ValueHashCode(value, operation);
    public long? ValueLength(XmlDatatypeValue value, ISchemaOperation operation) => library.ValueLength(value, operation);
    public int? TotalDigits(XmlDatatypeValue value, ISchemaOperation operation) => library.TotalDigits(value, operation);
    public int? FractionDigits(XmlDatatypeValue value, ISchemaOperation operation) => library.FractionDigits(value, operation);
    public XmlDatatypeValue Retype(SchemaName type, XmlDatatypeValue value, ISchemaOperation operation) => library.Retype(type, value, operation);
    public XmlDatatypeValue CreateList(SchemaName type, IReadOnlyList<XmlDatatypeValue> items, ISchemaOperation operation) => library.CreateList(type, items, operation);
    public XmlDatatypeValue CreateUnion(SchemaName type, XmlDatatypeValue selectedMember, ISchemaOperation operation) => library.CreateUnion(type, selectedMember, operation);
}
