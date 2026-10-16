using System.Globalization;
namespace Xemele.Validation;

internal sealed partial class NumericDatatypeProvider
{
    internal static void WriteConstraint(DatatypeAtomicValue value, ref ConstraintWriter writer)
    {
        writer.Operation.Charge(1, default);
        if (value is DecimalValue d)
        {
            var integer = d.TypeName.LocalName != "decimal";
            var whole = d.Digits.Length - d.Scale;
            var capacity = (d.Sign < 0 ? 1L : 0L) + (integer ? d.Digits.Length : Math.Max(1, whole) + 1L + Math.Max(1, d.Scale));
            _ = ConstraintWriter.CheckedLength(capacity, writer.Operation);
            if (d.Sign < 0)
            {
                writer.Append('-');
            }

            if (integer)
            {
                writer.Append(d.Digits);
                return;
            }
            if (whole <= 0)
            {
                writer.Append('0');
            }
            else
            {
                writer.Append(d.Digits[..whole]);
            }

            writer.Append('.');
            if (d.Scale == 0)
            {
                writer.Append('0');
            }
            else
            {
                for (var i = whole; i < 0; i++)
                {
                    writer.Append('0');
                }

                writer.Append(d.Digits[Math.Max(0, whole)..]);
            }
            return;
        }
        var number = ((FloatingValue)value).Number;
        if (double.IsNaN(number))
        {
            writer.Append("NaN");
            return;
        }
        if (double.IsPositiveInfinity(number))
        {
            writer.Append("INF");
            return;
        }
        if (double.IsNegativeInfinity(number))
        {
            writer.Append("-INF");
            return;
        }
        // XSD1.0 has one zero; negative IEEE zero canonicalizes to this form.
        if (number == 0)
        {
            writer.Append("0.0E0");
            return;
        }
        Span<char> text = stackalloc char[32];
        writer.Operation.Charge(32, default);
        var ok = value.Primitive == DatatypePrimitive.Float
            ? ((float)number).TryFormat(text, out var n, "R", CultureInfo.InvariantCulture)
            : number.TryFormat(text, out n, "R", CultureInfo.InvariantCulture);
        if (!ok)
        {
            throw new InvalidOperationException("Bounded floating scratch exhausted.");
        }

        var start = text[0] == '-' ? 1 : 0;
        int end = n, point = -1, exponent = 0;
        for (var i = start; i < n; i++)
        {
            writer.Operation.Charge(1, default);
            if (text[i] == '.')
            {
                point = i;
            }

            if (text[i] == 'E')
            {
                end = i;
                var j = i + 1;
                var negative = text[j] == '-';
                if (text[j] is '+' or '-')
                {
                    j++;
                }

                for (; j < n; j++)
                {
                    writer.Operation.Charge(1, default);
                    exponent = exponent * 10 + text[j] - '0';
                }
                if (negative)
                {
                    exponent = -exponent;
                }

                break;
            }
        }
        if (point < 0)
        {
            point = end;
        }

        Span<char> digits = stackalloc char[24];
        int count = 0, first = -1;
        for (var i = start; i < end; i++)
        {
            writer.Operation.Charge(1, default);
            if (text[i] == '.')
            {
                continue;
            }

            if (first < 0 && text[i] != '0')
            {
                first = count;
                exponent += point - start - count - 1;
            }
            digits[count++] = text[i];
        }
        while (count > first + 1)
        {
            writer.Operation.Charge(1, default);
            if (digits[count - 1] != '0')
            {
                break;
            }

            count--;
        }
        if (start != 0)
        {
            writer.Append('-');
        }

        writer.Append(digits[first]);
        writer.Append('.');
        if (count == first + 1)
        {
            writer.Append('0');
        }
        else
        {
            writer.Append(digits[(first + 1)..count]);
        }

        writer.Append('E');
        writer.Number(exponent);
    }
}
