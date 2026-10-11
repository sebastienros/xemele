namespace Xemele.Validation;

internal sealed partial class TemporalDatatypeProvider
{
    // Parse-only signed base10^9 integer. Arrays are private scratch, written
    // only before construction; sign changes share immutable limb storage.
    // Single-limb values stay inline. Every size-dependent loop calls Work
    // before its next limb. No opaque arbitrary-precision CLR arithmetic occurs.
    private readonly struct Integer
    {
        private const uint Radix = 1_000_000_000;
        private readonly uint small;
        private readonly uint[]? digits;
        internal readonly int Count;
        internal readonly int Sign;
        internal bool IsZero => Sign == 0;
        internal Integer(int value)
        {
            Sign = Math.Sign(value); Count = value == 0 ? 0 : 1;
            small = value < 0 ? checked((uint)-(long)value) : (uint)value; digits = null;
            if (small >= Radix) throw new ArgumentOutOfRangeException(nameof(value));
        }
        private Integer(uint[] buffer, int count, int sign)
        {
            Count = count; Sign = sign;
            if (count <= 1) { small = count == 0 ? 0 : buffer[0]; digits = null; }
            else { small = 0; digits = buffer; }
        }
        private Integer(uint small, uint[]? digits, int count, int sign)
        { this.small = small; this.digits = digits; Count = count; Sign = sign; }
        internal uint Digit(int index) => index >= Count ? 0 : digits is null ? small : digits[index];
        internal Integer Negative() => new(small, digits, Count, -Sign);
        public static implicit operator Integer(int value) => new(value);
        public static explicit operator int(Integer value) => value.Count <= 1 ? checked((int)value.small * value.Sign) : throw new OverflowException();
        internal static uint Base => Radix;
        internal static Integer FromResult(uint[] buffer, ISchemaOperation op, int sign)
        {
            int count = buffer.Length;
            while (count > 1)
            {
                Work(op, 1);
                if (buffer[count - 1] != 0) break;
                count--;
            }
            if (count == 1) { Work(op, 1); if (buffer[0] == 0) count = 0; }
            return new(buffer, count, count == 0 ? 0 : sign);
        }
    }

    private static uint[] Buffer(int capacity, ISchemaOperation op)
    {
        if (capacity == int.MaxValue)
        { op.Refuse("XSD.Datatype.Limit", "Temporal representation exceeded.", default); throw new Stopped(); }
        Components(op, checked(capacity + 1));
        return new uint[capacity];
    }
    private static int MagnitudeCompare(Integer a, Integer b, ISchemaOperation op)
    {
        Work(op, 1);
        if (a.Count != b.Count) return a.Count < b.Count ? -1 : 1;
        for (int i = a.Count - 1; i >= 0; i--)
        { Work(op, 1); uint x = a.Digit(i), y = b.Digit(i); if (x != y) return x < y ? -1 : 1; }
        return 0;
    }
    private static Integer Negate(Integer a, ISchemaOperation op) { Work(op, 1); return a.Negative(); }
    private static Integer Add(Integer a, Integer b, ISchemaOperation op)
    {
        Work(op, 1);
        if (a.IsZero) return b;
        if (b.IsZero) return a;
        if (a.Sign != b.Sign)
        {
            int comparison = MagnitudeCompare(a, b, op);
            if (comparison == 0) return default;
            Integer large = comparison > 0 ? a : b, little = comparison > 0 ? b : a;
            var result = Buffer(large.Count, op); long borrow = 0;
            for (int i = 0; i < result.Length; i++)
            {
                Work(op, 1); long digit = (long)large.Digit(i) - little.Digit(i) - borrow;
                if (digit < 0) { digit += Integer.Base; borrow = 1; } else borrow = 0;
                result[i] = (uint)digit;
            }
            return Integer.FromResult(result, op, large.Sign);
        }
        int count = Math.Max(a.Count, b.Count);
        if (count == 1)
        {
            Work(op, 1); ulong sum = (ulong)a.Digit(0) + b.Digit(0);
            if (sum < Integer.Base) return new Integer(checked((int)sum * a.Sign));
        }
        var output = Buffer(checked(count + 1), op); ulong carry = 0;
        for (int i = 0; i < count; i++)
        {
            Work(op, 1); ulong sum = (ulong)a.Digit(i) + b.Digit(i) + carry;
            output[i] = (uint)(sum % Integer.Base); carry = sum / Integer.Base;
        }
        Work(op, 1); output[count] = (uint)carry;
        return Integer.FromResult(output, op, a.Sign);
    }
    private static Integer Multiply(Integer a, int factor, ISchemaOperation op)
    {
        Work(op, 1);
        if (a.IsZero || factor == 0) return default;
        if (factor == 1) return a;
        ulong magnitude = factor < 0 ? (ulong)-(long)factor : (uint)factor;
        int sign = factor < 0 ? -a.Sign : a.Sign;
        if (a.Count == 1)
        {
            Work(op, 1); ulong product = a.Digit(0) * magnitude;
            if (product < Integer.Base) return new Integer(checked((int)product * sign));
        }
        // Factors used by temporal arithmetic are <=86400: one extra limb
        // suffices. This is a private small-factor helper, not general multiply.
        if (magnitude >= Integer.Base) throw new ArgumentOutOfRangeException(nameof(factor));
        var output = Buffer(checked(a.Count + 1), op); ulong carry = 0;
        for (int i = 0; i < a.Count; i++)
        {
            Work(op, 1); ulong product = a.Digit(i) * magnitude + carry;
            output[i] = (uint)(product % Integer.Base); carry = product / Integer.Base;
        }
        Work(op, 1); output[a.Count] = (uint)carry;
        return Integer.FromResult(output, op, sign);
    }
    private static Integer DivideRemainder(Integer a, int divisor, ISchemaOperation op, out Integer remainder)
    {
        Work(op, 1);
        if (divisor <= 0) throw new ArgumentOutOfRangeException(nameof(divisor));
        if (a.IsZero) { remainder = default; return default; }
        if (a.Count == 1)
        {
            Work(op, 1); uint digit = a.Digit(0);
            remainder = new Integer(checked((int)(digit % divisor) * a.Sign));
            return new Integer(checked((int)(digit / divisor) * a.Sign));
        }
        var output = Buffer(a.Count, op); ulong carry = 0;
        for (int i = a.Count - 1; i >= 0; i--)
        {
            Work(op, 1); ulong dividend = carry * Integer.Base + a.Digit(i);
            output[i] = (uint)(dividend / (uint)divisor); carry = dividend % (uint)divisor;
        }
        remainder = new Integer(checked((int)carry * a.Sign));
        return Integer.FromResult(output, op, a.Sign);
    }
    private static Integer Divide(Integer a, int divisor, ISchemaOperation op) => DivideRemainder(a, divisor, op, out _);
    private static Integer Remainder(Integer a, int divisor, ISchemaOperation op)
    {
        Work(op, 1);
        if (divisor <= 0) throw new ArgumentOutOfRangeException(nameof(divisor));
        ulong carry = 0;
        for (int i = a.Count - 1; i >= 0; i--)
        { Work(op, 1); carry = (carry * Integer.Base + a.Digit(i)) % (uint)divisor; }
        return new Integer(checked((int)carry * a.Sign));
    }
    private static Integer FloorDivide(Integer a, int divisor, ISchemaOperation op)
    {
        var quotient = DivideRemainder(a, divisor, op, out var remainder);
        return a.Sign < 0 && !remainder.IsZero ? Add(quotient, -1, op) : quotient;
    }
}
