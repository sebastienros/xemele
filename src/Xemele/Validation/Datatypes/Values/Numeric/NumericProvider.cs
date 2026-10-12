using System.Globalization;

namespace Xemele.Validation;

// Canonical decimal digits are private owned storage, never CLR decimal/double.
// The provider is stateless; final library composition owns its shared instance.
internal sealed partial class NumericDatatypeProvider : IDatatypeFamilyProvider
{
    public DatatypeFamily Family => DatatypeFamily.Numeric;
    public bool TryParse(DatatypeMetadata type, ReadOnlySpan<char> normalized, SchemaNamespaceContext context,
        ISchemaOperation operation, out DatatypeAtomicValue? value, out XmlDatatypeError? error)
    {
        ArgumentNullException.ThrowIfNull(context); ArgumentNullException.ThrowIfNull(operation);
        value = null; error = DatatypeErrors.InvalidLexical;
        if (!operation.Charge(0, default)) { error = DatatypeErrors.Stopped; return false; }
        // Only canonical known metadata may reach a family; reject fabricated joins.
        if (!operation.Charge(1, default)) { error = DatatypeErrors.Stopped; return false; }
        if (!DatatypeTypes.All.TryGetValue(type.LocalName ?? "", out var known) || known != type || type.Family != Family)
        { error = DatatypeErrors.UnknownType; return false; }
        if (type.Primitive == DatatypePrimitive.Decimal)
            return ParseDecimal(type, normalized, operation, out value, out error);
        return ParseFloating(type, normalized, operation, out value, out error);
    }

    private bool ParseDecimal(DatatypeMetadata type, ReadOnlySpan<char> input, ISchemaOperation op,
        out DatatypeAtomicValue? value, out XmlDatatypeError? error)
    {
        value = null; error = DatatypeErrors.InvalidLexical;
        int digits = 0, fraction = 0, first = -1, firstDigit = -1, lastDigit = -1;
        bool negative = false, point = false;
        for (int i = 0; i < input.Length; i++)
        {
            if (!op.Charge(1, default)) return Stop(out error);
            char c = input[i];
            if (i == 0 && c is '+' or '-') { negative = c == '-'; continue; }
            if (c == '.' && !point && type.LocalName == "decimal") { point = true; continue; }
            if (c is < '0' or > '9') return false;
            if (c != '0') { if (first < 0) { first = i; firstDigit = digits; } lastDigit = digits; }
            digits++; if (point) fraction++;
        }
        if (digits == 0) return false;
        int trim = first < 0 ? 0 : Math.Min(fraction, digits - 1 - lastDigit);
        int count = first < 0 ? 1 : digits - firstDigit - trim;
        int scale = first < 0 ? 0 : fraction - trim;
        int sign = first < 0 ? 0 : negative ? -1 : 1;
        if (!Range(type.LocalName, sign, count, input, first, op, out bool valid)) return Stop(out error);
        if (!valid) return false;
        // One retained value, one component per nine-digit storage block. Zero
        // uses a static literal and needs only the value component.
        int blocks = first < 0 ? 0 : 1 + (count - 1) / 9;
        if (!op.ChargeComponents(1 + blocks, default)) return Stop(out error);
        char[]? owned = first < 0 ? null : new char[count];
        if (owned is not null)
        {
            int written = 0;
            for (int i = first; written < count; i++)
            {
                if (!op.Charge(1, default)) return Stop(out error);
                if (input[i] != '.') owned[written++] = input[i];
            }
        }
        if (!op.Charge(0, default)) return Stop(out error);
        value = new DecimalValue(DatatypeTypes.Name(type.LocalName), this, owned, scale, sign);
        error = null; return true;
    }

    private static bool Range(string name, int sign, int count, ReadOnlySpan<char> input, int first,
        ISchemaOperation op, out bool valid)
    {
        valid = true;
        if (!op.Charge(1, default)) return false;
        if (name is "nonPositiveInteger" or "negativeInteger") valid = name == "negativeInteger" ? sign < 0 : sign <= 0;
        else if (name is "nonNegativeInteger" or "positiveInteger" or "unsignedLong" or "unsignedInt" or "unsignedShort" or "unsignedByte")
            valid = name == "positiveInteger" ? sign > 0 : sign >= 0;
        if (!valid || sign == 0) return true;
        ReadOnlySpan<char> bound = name switch
        {
            "long" => sign < 0 ? "9223372036854775808" : "9223372036854775807",
            "int" => sign < 0 ? "2147483648" : "2147483647",
            "short" => sign < 0 ? "32768" : "32767",
            "byte" => sign < 0 ? "128" : "127",
            "unsignedLong" => "18446744073709551615",
            "unsignedInt" => "4294967295",
            "unsignedShort" => "65535",
            "unsignedByte" => "255",
            _ => default
        };
        if (bound.IsEmpty) return true;
        if (count != bound.Length) { valid = count < bound.Length; return true; }
        for (int i = 0; i < count; i++)
        {
            if (!op.Charge(1, default)) return false;
            int difference = input[first + i] - bound[i];
            if (difference != 0) { valid = difference < 0; return true; }
        }
        return true;
    }

    private bool ParseFloating(DatatypeMetadata type, ReadOnlySpan<char> input, ISchemaOperation op,
        out DatatypeAtomicValue? value, out XmlDatatypeError? error)
    {
        value = null; error = DatatypeErrors.InvalidLexical;
        // Each token probe charges its length check and inspected characters.
        bool infinity = Match(input, "INF", op, out bool stopped); if (stopped) return Stop(out error);
        bool negativeInfinity = Match(input, "-INF", op, out stopped); if (stopped) return Stop(out error);
        bool nan = Match(input, "NaN", op, out stopped); if (stopped) return Stop(out error);
        double number;
        if (infinity) number = double.PositiveInfinity;
        else if (negativeInfinity) number = double.NegativeInfinity;
        else if (nan) number = double.NaN;
        else
        {
            // Every binary32/binary64 rounding midpoint is a terminating
            // decimal with fewer than 1100 significant digits (at worst a
            // 54-bit coefficient times 5^1075). A prefix of 1100 digits plus
            // one sticky digit therefore preserves its rounding interval.
            // This bounds the uninterruptible CLR conversion independently
            // of lexical length. No arbitrary precision is discarded for the
            // Decimal primitive; this scratch is only for IEEE values.
            const int precision = 1100;
            Span<char> digits = stackalloc char[precision + 1];
            Span<char> converted = stackalloc char[precision + 32];
            bool mantissaDigit = false, exponentDigit = false, point = false, exponent = false;
            bool negative = false, exponentNegative = false, sticky = false;
            int mantissaCount = 0, integerCount = 0, firstSignificant = -1, kept = 0;
            long exponentMagnitude = 0;
            for (int i = 0; i < input.Length; i++)
            {
                if (!op.Charge(1, default)) return Stop(out error);
                char c = input[i];
                if (c is '+' or '-' && (i == 0 || input[i - 1] is 'e' or 'E'))
                { if (i == 0) negative = c == '-'; else exponentNegative = c == '-'; continue; }
                if (c is >= '0' and <= '9')
                {
                    // Conversion work is separate from lexical validation.
                    if (!op.Charge(1, default)) return Stop(out error);
                    if (exponent)
                    {
                        exponentDigit = true;
                        // Larger exponents dominate every possible Int32-sized
                        // lexical displacement. Saturate only that proven range.
                        exponentMagnitude = Math.Min(10_000_000_000L, exponentMagnitude * 10 + c - '0');
                    }
                    else
                    {
                        mantissaDigit = true;
                        if (!point) integerCount++;
                        if (firstSignificant < 0 && c != '0') firstSignificant = mantissaCount;
                        if (firstSignificant >= 0)
                        {
                            if (kept < precision) digits[kept++] = c;
                            else if (c != '0') sticky = true;
                        }
                        mantissaCount++;
                    }
                    continue;
                }
                if (c == '.' && !point && !exponent) { point = true; continue; }
                if (c is 'e' or 'E' && !exponent && mantissaDigit) { exponent = true; continue; }
                return false;
            }
            if (!mantissaDigit || exponent && !exponentDigit) return false;
            if (firstSignificant < 0) number = negative ? -0.0 : 0.0;
            else
            {
                if (sticky) { if (!op.Charge(1, default)) return Stop(out error); digits[kept++] = '1'; }
                long scientificExponent = (long)integerCount - firstSignificant - 1 + (exponentNegative ? -exponentMagnitude : exponentMagnitude);
                int written = 0;
                if (negative) { if (!op.Charge(1, default)) return Stop(out error); converted[written++] = '-'; }
                for (int i = 0; i < kept; i++)
                {
                    if (!op.Charge(1, default)) return Stop(out error);
                    converted[written++] = digits[i];
                    if (i == 0) { if (!op.Charge(1, default)) return Stop(out error); converted[written++] = '.'; }
                }
                if (!op.Charge(1, default)) return Stop(out error);
                converted[written++] = 'e';
                // Int64 formatting is a bounded primitive (<=12 digits here).
                int exponentUnits = scientificExponent < 0 ? 1 : 0;
                for (long remaining = Math.Abs(scientificExponent); ; remaining /= 10)
                {
                    if (!op.Charge(1, default)) return Stop(out error);
                    exponentUnits++; if (remaining < 10) break;
                }
                if (!op.Charge(exponentUnits, default)) return Stop(out error);
                if (!scientificExponent.TryFormat(converted[written..], out int exponentWritten, provider: CultureInfo.InvariantCulture)) throw new InvalidOperationException("Bounded numeric scratch exhausted.");
                written += exponentWritten;
                // Separate bounded runtime scan/conversion reservation. The
                // original span is never handed to an opaque unbounded parser.
                if (!op.Charge(written, default)) return Stop(out error);
                const NumberStyles styles = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent;
                if (type.Primitive == DatatypePrimitive.Float)
                { if (!float.TryParse(converted[..written], styles, CultureInfo.InvariantCulture, out float single)) return false; number = single; }
                else if (!double.TryParse(converted[..written], styles, CultureInfo.InvariantCulture, out number)) return false;
            }
        }
        if (!op.ChargeComponents(1, default) || !op.Charge(0, default)) return Stop(out error);
        value = new FloatingValue(DatatypeTypes.Name(type.LocalName), type.Primitive!.Value, this, number);
        error = null; return true;
    }
    private static bool Match(ReadOnlySpan<char> input, ReadOnlySpan<char> token, ISchemaOperation op, out bool stopped)
    {
        stopped = !op.Charge(1, default); if (stopped || input.Length != token.Length) return false;
        for (int i = 0; i < token.Length; i++)
        {
            stopped = !op.Charge(1, default); if (stopped || input[i] != token[i]) return false;
        }
        return true;
    }
    private static bool Stop(out XmlDatatypeError? error) { error = DatatypeErrors.Stopped; return false; }

    public bool ValueEquals(DatatypeAtomicValue left, DatatypeAtomicValue right, ISchemaOperation operation) => Compare(left, right, operation) == XmlValueOrder.Equal;
    public XmlValueOrder Compare(DatatypeAtomicValue left, DatatypeAtomicValue right, ISchemaOperation operation)
    {
        DatatypeValues.Charge(operation);
        XmlValueOrder result;
        if (left is DecimalValue a && right is DecimalValue b)
        {
            int comparison = a.Sign.CompareTo(b.Sign);
            if (comparison == 0 && a.Sign != 0)
            {
                comparison = ((long)a.Digits.Length - a.Scale).CompareTo((long)b.Digits.Length - b.Scale);
                if (comparison == 0)
                    for (int i = 0, length = Math.Max(a.Digits.Length, b.Digits.Length); i < length; i++)
                    {
                        DatatypeValues.Charge(operation);
                        char x = i < a.Digits.Length ? a.Digits[i] : '0';
                        char y = i < b.Digits.Length ? b.Digits[i] : '0';
                        comparison = x.CompareTo(y); if (comparison != 0) break;
                    }
                comparison *= a.Sign;
            }
            result = (XmlValueOrder)Math.Sign(comparison);
        }
        else
        {
            double aNumber = ((FloatingValue)left).Number, bNumber = ((FloatingValue)right).Number;
            result = double.IsNaN(aNumber) || double.IsNaN(bNumber)
                ? double.IsNaN(aNumber) && double.IsNaN(bNumber) ? XmlValueOrder.Equal : XmlValueOrder.Indeterminate
                : (XmlValueOrder)aNumber.CompareTo(bNumber);
        }
        DatatypeValues.Charge(operation, 0); return result;
    }
    public int ValueHashCode(DatatypeAtomicValue value, ISchemaOperation operation)
    {
        DatatypeValues.Charge(operation);
        int hash;
        if (value is DecimalValue decimalValue)
        {
            hash = unchecked(decimalValue.Sign * 397 ^ decimalValue.Scale);
            foreach (char digit in decimalValue.Digits) { DatatypeValues.Charge(operation); hash = unchecked(hash * 31 + digit); }
        }
        else
        {
            double number = ((FloatingValue)value).Number;
            hash = double.IsNaN(number) ? 0x7ff80000 : number == 0 ? 0 : number.GetHashCode();
        }
        DatatypeValues.Charge(operation, 0); return hash;
    }
    public long? ValueLength(DatatypeAtomicValue value, ISchemaOperation operation) { DatatypeValues.Charge(operation); DatatypeValues.Charge(operation, 0); return null; }
    public int? TotalDigits(DatatypeAtomicValue value, ISchemaOperation operation)
    { DatatypeValues.Charge(operation); int? result = value is DecimalValue d ? Math.Max(d.Digits.Length, d.Scale) : null; DatatypeValues.Charge(operation, 0); return result; }
    public int? FractionDigits(DatatypeAtomicValue value, ISchemaOperation operation)
    { DatatypeValues.Charge(operation); int? result = value is DecimalValue d ? d.Scale : null; DatatypeValues.Charge(operation, 0); return result; }

    private sealed class DecimalValue(SchemaName type, NumericDatatypeProvider provider, char[]? digits, int scale, int sign)
        : DatatypeAtomicValue(type, DatatypePrimitive.Decimal, provider)
    {
        private readonly char[]? digits = digits;
        internal ReadOnlySpan<char> Digits => digits is null ? "0" : digits;
        internal int Scale { get; } = scale;
        internal int Sign { get; } = sign;
    }
    internal sealed class FloatingValue(SchemaName type, DatatypePrimitive primitive, NumericDatatypeProvider provider, double number)
        : DatatypeAtomicValue(type, primitive, provider)
    { internal double Number { get; } = number; }
}
