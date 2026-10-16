namespace Xemele.Validation;

// Counts use a native word or an owned canonical decimal string. No arbitrary
// precision arithmetic or unaccounted limb allocation is needed to order them.
internal readonly struct FacetNumber
{
    private readonly ulong small;
    private readonly string? digits;
    internal FacetNumber(ulong value)
    {
        small = value;
        digits = null;
    }
    private FacetNumber(string value)
    {
        small = 0;
        digits = value;
    }
    internal bool IsZero => digits is null && small == 0;
    internal static bool TryParse(string text, ISchemaOperation op, out FacetNumber number)
    {
        number = default;
        int start = 0, first = -1;
        var negative = false;
        if (text.Length != 0 && text[0] is '+' or '-')
        {
            if (!op.Charge(1, default))
            {
                return false;
            }

            negative = text[0] == '-';
            start = 1;
        }
        if (start == text.Length)
        {
            return false;
        }

        for (var i = start; i < text.Length; i++)
        {
            if (!op.Charge(1, default))
            {
                return false;
            }

            if (text[i] is < '0' or > '9')
            {
                return false;
            }

            if (first < 0 && text[i] != '0')
            {
                first = i;
            }
        }
        if (first < 0)
        {
            return true;
        }

        if (negative)
        {
            return false;
        }

        var length = text.Length - first;
        if (length <= 19)
        {
            ulong value = 0;
            for (var i = first; i < text.Length; i++)
            {
                if (!op.Charge(1, default))
                {
                    return false;
                }

                value = value * 10 + (uint)(text[i] - '0');
            }
            number = new(value);
            return true;
        }
        if (!op.ChargeComponents(1, default))
        {
            return false;
        }

        try
        {
            number = new(string.Create(length, (text, first, op), static (output, state) =>
            {
                for (var i = 0; i < output.Length; i++)
                {
                    if (!state.op.Charge(1, default))
                    {
                        throw new CopyStopped();
                    }

                    output[i] = state.text[state.first + i];
                }
            }));
            return true;
        }
        catch (CopyStopped) { return false; }
    }
    private sealed class CopyStopped : Exception;
    internal int? Compare(FacetNumber other, ISchemaOperation op, XmlLocation at = default)
    {
        if (!op.Charge(1, at))
        {
            return null;
        }

        if (digits is null)
        {
            return other.digits is null ? small.CompareTo(other.small) : -1;
        }

        if (other.digits is null)
        {
            return 1;
        }

        if (!op.Charge(1, at))
        {
            return null;
        }

        if (digits.Length != other.digits.Length)
        {
            return digits.Length.CompareTo(other.digits.Length);
        }

        for (var i = 0; i < digits.Length; i++)
        {
            if (!op.Charge(1, at))
            {
                return null;
            }

            if (digits[i] != other.digits[i])
            {
                return digits[i].CompareTo(other.digits[i]);
            }
        }
        return 0;
    }
}
