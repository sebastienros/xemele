namespace Xemele.Validation;

// The composer supplies one shared immutable provider. Payloads are privately
// owned arrays; no lexical span, operation or namespace map survives parsing.
internal sealed partial class TextBinaryDatatypeProvider : IDatatypeFamilyProvider
{
    public DatatypeFamily Family => DatatypeFamily.TextBinary;
    public bool TryParse(DatatypeMetadata type, ReadOnlySpan<char> normalized, SchemaNamespaceContext context,
        ISchemaOperation operation, out DatatypeAtomicValue? value, out XmlDatatypeError? error)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(operation);
        value = null;
        error = DatatypeErrors.InvalidLexical;
        if (!operation.Charge(0, default) || !operation.Charge(1, default))
        {
            return Stop(out error);
        }

        if (!DatatypeTypes.All.TryGetValue(type.LocalName ?? "", out var known) || known != type || type.Family != Family || type.Variety == XmlDatatypeVariety.List)
        {
            error = DatatypeErrors.UnknownType;
            return false;
        }
        var success = type.Primitive switch
        {
            DatatypePrimitive.Boolean => Boolean(type, normalized, operation, out value, out error),
            DatatypePrimitive.HexBinary => Hex(type, normalized, operation, out value, out error),
            DatatypePrimitive.Base64Binary => Base64(type, normalized, operation, out value, out error),
            DatatypePrimitive.QName or DatatypePrimitive.Notation => QName(type, normalized, context, operation, out value, out error),
            _ => Text(type, normalized, operation, out value, out error)
        };
        if (!operation.Charge(0, default))
        {
            value = null;
            return Stop(out error);
        }
        return success;
    }

    private bool Text(DatatypeMetadata type, ReadOnlySpan<char> input, ISchemaOperation op,
        out DatatypeAtomicValue? value, out XmlDatatypeError? error)
    {
        value = null;
        error = DatatypeErrors.InvalidLexical;
        var name = type.LocalName is "Name" or "NCName" or "NMTOKEN" or "ID" or "IDREF" or "ENTITY";
        var colon = type.LocalName is "Name" or "NMTOKEN";
        var token = type.Whitespace == XmlWhitespaceRule.Collapse;
        int scalars = 0, part = 0;
        bool firstPart = true, previousSpace = false;
        for (var i = 0; i < input.Length;)
        {
            if (!Read(input, ref i, op, out var c, out error))
            {
                return false;
            }

            if (!XmlCharacter(c))
            {
                return Invalid(out error);
            }

            if (type.LocalName == "normalizedString" && c is 9 or 10 or 13)
            {
                return Invalid(out error);
            }

            if (token && (c is 9 or 10 or 13 || c == 32 && (scalars == 0 || previousSpace || i == input.Length)))
            {
                return Invalid(out error);
            }

            previousSpace = c == 32;
            if (name && ((!colon && c == ':') || !TextBinaryNames.Contains(c, scalars != 0 || type.LocalName == "NMTOKEN", op)))
            {
                return op.IsComplete ? Invalid(out error) : Stop(out error);
            }

            if (type.LocalName == "language")
            {
                if (c == '-')
                {
                    if (part == 0)
                    {
                        return Invalid(out error);
                    }

                    firstPart = false;
                    part = 0;
                }
                else if (!(Alpha(c) || !firstPart && c is >= '0' and <= '9') || ++part > 8)
                {
                    return Invalid(out error);
                }
            }
            scalars++;
        }
        if (name && scalars == 0 || type.LocalName == "language" && part == 0)
        {
            return Invalid(out error);
        }

        if (type.Primitive == DatatypePrimitive.AnyUri && !TextBinaryUri.Validate(input, op, out error))
        {
            return false;
        }

        if (!Reserve(op, input.Length))
        {
            return Stop(out error);
        }

        if (!Copy(input, op, out var owned, out error))
        {
            return false;
        }

        value = new TextValue(DatatypeTypes.Name(type.LocalName), type.Primitive ?? DatatypePrimitive.String, this, owned!, scalars);
        error = null;
        return true;
    }
    private bool Boolean(DatatypeMetadata type, ReadOnlySpan<char> input, ISchemaOperation op,
        out DatatypeAtomicValue? value, out XmlDatatypeError? error)
    {
        value = null;
        error = DatatypeErrors.InvalidLexical;
        // At most five inspected units. No culture conversion or runtime parser.
        if (!op.Charge(1, default))
        {
            return Stop(out error);
        }

        ReadOnlySpan<char> token = input.Length switch
        {
            1 => input[0] == '1' ? "1" : "0",
            4 => "true",
            5 => "false",
            _ => default
        };
        if (token.IsEmpty)
        {
            return false;
        }

        for (var i = 0; i < token.Length; i++)
        {
            if (!op.Charge(1, default))
            {
                return Stop(out error);
            }

            if (input[i] != token[i])
            {
                return false;
            }
        }
        if (!op.ChargeComponents(1, default))
        {
            return Stop(out error);
        }

        value = new BooleanValue(DatatypeTypes.Name(type.LocalName), this, input.Length == 4 || input[0] == '1');
        error = null;
        return true;
    }
    private bool Hex(DatatypeMetadata type, ReadOnlySpan<char> input, ISchemaOperation op,
        out DatatypeAtomicValue? value, out XmlDatatypeError? error)
    {
        value = null;
        error = DatatypeErrors.InvalidLexical;
        if (!op.Charge(1, default))
        {
            return Stop(out error);
        }

        if ((input.Length & 1) != 0)
        {
            return false;
        }

        for (var i = 0; i < input.Length; i++)
        {
            if (!op.Charge(1, default))
            {
                return Stop(out error);
            }

            if (HexDigit(input[i]) < 0)
            {
                return false;
            }
        }
        var count = input.Length / 2;
        if (!Reserve(op, count))
        {
            return Stop(out error);
        }

        var bytes = count == 0 ? [] : new byte[count];
        for (var i = 0; i < count; i++)
        {
            if (!op.Charge(1, default))
            {
                return Stop(out error);
            }

            var high = HexDigit(input[i * 2]);
            if (!op.Charge(1, default))
            {
                return Stop(out error);
            }

            bytes[i] = (byte)((high << 4) | HexDigit(input[i * 2 + 1]));
        }
        value = new BinaryValue(DatatypeTypes.Name(type.LocalName), DatatypePrimitive.HexBinary, this, bytes);
        error = null;
        return true;
    }
    private bool Base64(DatatypeMetadata type, ReadOnlySpan<char> input, ISchemaOperation op,
        out DatatypeAtomicValue? value, out XmlDatatypeError? error)
    {
        value = null;
        error = DatatypeErrors.InvalidLexical;
        // Count and validate without materializing a whitespace-stripped copy.
        int count = 0, padding = 0, last = 0;
        var previousSpace = true;
        for (var i = 0; i < input.Length; i++)
        {
            if (!op.Charge(1, default))
            {
                return Stop(out error);
            }

            var c = input[i];
            if (c == ' ')
            {
                if (previousSpace || i == input.Length - 1)
                {
                    return false;
                }

                previousSpace = true;
                continue;
            }
            previousSpace = false;
            if (c == '=')
            {
                if (++padding > 2)
                {
                    return false;
                }
            }
            else
            {
                if (padding != 0)
                {
                    return false;
                }

                var digit = Base64Digit(c);
                if (digit < 0)
                {
                    return false;
                }

                last = digit;
            }
            count++;
        }
        if (!op.Charge(1, default))
        {
            return Stop(out error);
        }

        if ((count & 3) != 0 || padding == 1 && (last & 3) != 0 || padding == 2 && (last & 15) != 0)
        {
            return false;
        }

        var length = count / 4 * 3 - padding;
        if (!Reserve(op, length))
        {
            return Stop(out error);
        }

        var bytes = length == 0 ? [] : new byte[length];
        int accumulator = 0, bits = 0, written = 0;
        foreach (var c in input)
        {
            if (!op.Charge(1, default))
            {
                return Stop(out error);
            }

            if (c is ' ' or '=')
            {
                continue;
            }

            accumulator = ((accumulator & 0xFFFF) << 6) | Base64Digit(c);
            bits += 6;
            if (bits >= 8)
            {
                bits -= 8;
                if (!op.Charge(1, default))
                {
                    return Stop(out error);
                }

                bytes[written++] = (byte)(accumulator >> bits);
            }
        }
        value = new BinaryValue(DatatypeTypes.Name(type.LocalName), DatatypePrimitive.Base64Binary, this, bytes);
        error = null;
        return true;
    }
    private bool QName(DatatypeMetadata type, ReadOnlySpan<char> input, SchemaNamespaceContext context, ISchemaOperation op,
        out DatatypeAtomicValue? value, out XmlDatatypeError? error)
    {
        value = null;
        error = DatatypeErrors.InvalidLexical;
        int colon = -1, part = 0;
        for (var i = 0; i < input.Length;)
        {
            var at = i;
            if (!Read(input, ref i, op, out var c, out error))
            {
                return false;
            }

            if (c == ':')
            {
                if (colon >= 0 || part == 0)
                {
                    return Invalid(out error);
                }

                colon = at;
                part = 0;
            }
            else
            {
                if (!TextBinaryNames.Contains(c, part != 0, op))
                {
                    return op.IsComplete ? Invalid(out error) : Stop(out error);
                }

                part++;
            }
        }
        if (part == 0)
        {
            return Invalid(out error);
        }
        // Reserve retained node, namespace reference and local-name blocks before
        // namespace enumeration (whose boxed enumerator is a parse-only allocation).
        var localLength = input.Length - colon - 1;
        if (!op.ChargeComponents(2 + Blocks(localLength), default))
        {
            return Stop(out error);
        }

        string? uri = null;
        if (!op.Charge(1, default))
        {
            return Stop(out error);
        }

        if (colon < 0)
        {
            uri = context.DefaultNamespace;
        }
        else if (Equal(input[..colon], "xml", op))
        {
            uri = "http://www.w3.org/XML/1998/namespace";
        }
        else
        {
            foreach (var pair in context.Prefixes)
            {
                if (!op.Charge(1, default))
                {
                    return Stop(out error);
                }

                if (Equal(input[..colon], pair.Key, op))
                {
                    uri = pair.Value;
                    break;
                }
                if (!op.IsComplete)
                {
                    return Stop(out error);
                }
            }
        }
        if (!op.IsComplete)
        {
            return Stop(out error);
        }

        if (uri is null || colon >= 0 && uri.Length == 0 || colon >= 0 && Equal(input[..colon], "xmlns", op))
        {
            return Invalid(out error);
        }
        // Strings are immutable. Retain only the resolved namespace string, never
        // the context or map; validate/charge each retained namespace scalar.
        for (var i = 0; i < uri.Length;)
        {
            if (!Read(uri, ref i, op, out _, out error))
            {
                return false;
            }
        }

        if (!Copy(input[(colon + 1)..], op, out var local, out error))
        {
            return false;
        }

        value = new QNameValue(DatatypeTypes.Name(type.LocalName), type.Primitive!.Value, this, uri, local!);
        error = null;
        return true;
    }

    private static bool Copy(ReadOnlySpan<char> input, ISchemaOperation op, out char[]? output, out XmlDatatypeError? error)
    {
        output = input.IsEmpty ? [] : new char[input.Length];
        error = null;
        for (var i = 0; i < input.Length;)
        {
            var start = i;
            if (!Read(input, ref i, op, out _, out error))
            {
                output = null;
                return false;
            }
            output[start] = input[start];
            if (i - start == 2)
            {
                output[start + 1] = input[start + 1];
            }
        }
        return true;
    }
    internal static bool Read(ReadOnlySpan<char> input, ref int position, ISchemaOperation op, out int scalar, out XmlDatatypeError? error)
    {
        scalar = 0;
        error = null;
        if (!op.Charge(1, default))
        {
            return Stop(out error);
        }

        var c = input[position++];
        if (!char.IsSurrogate(c))
        {
            scalar = c;
            return true;
        }
        if (!char.IsHighSurrogate(c) || position == input.Length || !char.IsLowSurrogate(input[position]))
        {
            error = DatatypeErrors.InvalidUnicode;
            return false;
        }
        scalar = char.ConvertToUtf32(c, input[position++]);
        return true;
    }
    private static bool Reserve(ISchemaOperation op, int units) => op.ChargeComponents(1 + Blocks(units), default);
    private static int Blocks(int units) => units == 0 ? 0 : 1 + (units - 1) / 256;
    internal static int HexDigit(int c) => c is >= '0' and <= '9' ? c - '0' : c is >= 'A' and <= 'F' ? c - 'A' + 10 : c is >= 'a' and <= 'f' ? c - 'a' + 10 : -1;
    private static int Base64Digit(int c) => c is >= 'A' and <= 'Z' ? c - 'A' : c is >= 'a' and <= 'z' ? c - 'a' + 26 : c is >= '0' and <= '9' ? c - '0' + 52 : c == '+' ? 62 : c == '/' ? 63 : -1;
    internal static bool Alpha(int c) => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z';
    private static bool XmlCharacter(int c) => c is 9 or 10 or 13 or >= 0x20 and <= 0xD7FF or >= 0xE000 and <= 0xFFFD or >= 0x10000 and <= 0x10FFFF;
    internal static bool Stop(out XmlDatatypeError? error)
    {
        error = DatatypeErrors.Stopped;
        return false;
    }
    internal static bool Invalid(out XmlDatatypeError? error)
    {
        error = DatatypeErrors.InvalidLexical;
        return false;
    }
    private static bool Equal(ReadOnlySpan<char> a, ReadOnlySpan<char> b, ISchemaOperation op)
    {
        if (!op.Charge(1, default))
        {
            return false;
        }

        if (a.Length != b.Length)
        {
            return false;
        }

        for (int i = 0, j = 0; i < a.Length;)
        {
            if (!Read(a, ref i, op, out var x, out _) || !Read(b, ref j, op, out var y, out _))
            {
                return false;
            }

            if (x != y)
            {
                return false;
            }
        }
        return true;
    }

    public bool ValueEquals(DatatypeAtomicValue left, DatatypeAtomicValue right, ISchemaOperation operation)
    {
        DatatypeValues.Charge(operation);
        bool equal;
        if (left is TextValue a && right is TextValue b)
        {
            equal = CompareText(a.Text, b.Text, operation);
        }
        else if (left is QNameValue q && right is QNameValue r)
        {
            equal = CompareText(q.Namespace, r.Namespace, operation) && CompareText(q.Local, r.Local, operation);
        }
        else if (left is BinaryValue x && right is BinaryValue y)
        {
            equal = x.Bytes.Length == y.Bytes.Length;
            if (equal)
            {
                for (var i = 0; i < x.Bytes.Length; i++)
                {
                    DatatypeValues.Charge(operation);
                    if (x.Bytes[i] != y.Bytes[i])
                    {
                        equal = false;
                        break;
                    }
                }
            }
        }
        else
        {
            equal = ((BooleanValue)left).Value == ((BooleanValue)right).Value;
        }

        DatatypeValues.Charge(operation, 0);
        return equal;
    }
    // All primitives in this family are unordered in XSD 1.0 (including boolean).
    public XmlValueOrder Compare(DatatypeAtomicValue left, DatatypeAtomicValue right, ISchemaOperation operation) =>
        ValueEquals(left, right, operation) ? XmlValueOrder.Equal : XmlValueOrder.Indeterminate;
    private static bool CompareText(ReadOnlySpan<char> a, ReadOnlySpan<char> b, ISchemaOperation op)
    {
        DatatypeValues.Charge(op);
        if (a.Length != b.Length)
        {
            return false;
        }

        for (int i = 0, j = 0; i < a.Length;)
        {
            DatatypeValues.Charge(op); // One compared scalar pair (not UTF-16 units).
            int x = Scalar(a, ref i), y = Scalar(b, ref j);
            if (x != y)
            {
                return false;
            }
        }
        return true;
    }
    private static int Scalar(ReadOnlySpan<char> text, ref int i)
    {
        var c = text[i++];
        return char.IsHighSurrogate(c) ? char.ConvertToUtf32(c, text[i++]) : c;
    }
    private static int HashText(ReadOnlySpan<char> text, ISchemaOperation op)
    {
        var hash = 17;
        for (var i = 0; i < text.Length;)
        {
            DatatypeValues.Charge(op);
            hash = unchecked(hash * 31 + Scalar(text, ref i));
        }
        return hash;
    }
    public int ValueHashCode(DatatypeAtomicValue value, ISchemaOperation operation)
    {
        DatatypeValues.Charge(operation);
        int hash;
        if (value is TextValue text)
        {
            hash = HashText(text.Text, operation);
        }
        else if (value is QNameValue q)
        {
            hash = unchecked(HashText(q.Namespace, operation) * 397 ^ HashText(q.Local, operation));
        }
        else if (value is BinaryValue binary)
        {
            hash = 17;
            foreach (var b in binary.Bytes)
            {
                DatatypeValues.Charge(operation);
                hash = unchecked(hash * 31 + b);
            }
        }
        else
        {
            hash = ((BooleanValue)value).Value ? 1 : 0;
        }

        DatatypeValues.Charge(operation, 0);
        return hash;
    }
    public long? ValueLength(DatatypeAtomicValue value, ISchemaOperation operation)
    {
        DatatypeValues.Charge(operation);
        long? result = value is TextValue text ? text.Length : value is BinaryValue binary ? binary.Bytes.Length : null;
        DatatypeValues.Charge(operation, 0);
        return result;
    }
    public int? TotalDigits(DatatypeAtomicValue value, ISchemaOperation operation)
    {
        DatatypeValues.Charge(operation);
        DatatypeValues.Charge(operation, 0);
        return null;
    }
    public int? FractionDigits(DatatypeAtomicValue value, ISchemaOperation operation)
    {
        DatatypeValues.Charge(operation);
        DatatypeValues.Charge(operation, 0);
        return null;
    }

    private sealed class TextValue(SchemaName type, DatatypePrimitive primitive, TextBinaryDatatypeProvider provider, char[] text, int length)
        : DatatypeAtomicValue(type, primitive, provider)
    {
        private readonly char[] text = text; internal ReadOnlySpan<char> Text => text; internal int Length { get; } = length;
    }
    private sealed class BooleanValue(SchemaName type, TextBinaryDatatypeProvider provider, bool value)
        : DatatypeAtomicValue(type, DatatypePrimitive.Boolean, provider)
    {
        internal bool Value { get; } = value;
    }
    private sealed class BinaryValue(SchemaName type, DatatypePrimitive primitive, TextBinaryDatatypeProvider provider, byte[] bytes)
        : DatatypeAtomicValue(type, primitive, provider)
    {
        private readonly byte[] bytes = bytes; internal ReadOnlySpan<byte> Bytes => bytes;
    }
    private sealed class QNameValue(SchemaName type, DatatypePrimitive primitive, TextBinaryDatatypeProvider provider, string ns, char[] local)
        : DatatypeAtomicValue(type, primitive, provider)
    {
        private readonly char[] local = local; internal ReadOnlySpan<char> Local => local; internal string Namespace { get; } = ns;
    }
}
