using System.Numerics;

namespace Xemele.Validation;

// All arbitrary-precision arithmetic is confined to construction. Published
// keys own decimal strings; comparing/hashing them needs no scratch allocation.
internal sealed class TemporalDatatypeProvider : IDatatypeFamilyProvider
{
    public DatatypeFamily Family => DatatypeFamily.Temporal;
    public bool TryParse(DatatypeMetadata type, ReadOnlySpan<char> normalized, SchemaNamespaceContext context,
        ISchemaOperation operation, out DatatypeAtomicValue? value, out XmlDatatypeError? error)
    {
        ArgumentNullException.ThrowIfNull(context); ArgumentNullException.ThrowIfNull(operation);
        value = null; error = null;
        try
        {
            Poll(operation);
            Work(operation, 1);
            if (type.Family != Family || type.Primitive is not (>= DatatypePrimitive.Duration and <= DatatypePrimitive.GMonth)
                || !DatatypeTypes.All.TryGetValue(type.LocalName, out var known) || known != type)
            { Poll(operation); error = DatatypeErrors.UnknownType; return false; }
            var reader = new Reader(normalized, operation);
            TemporalValue? parsed = type.Primitive == DatatypePrimitive.Duration
                ? ParseDuration(type, ref reader) : ParseCalendar(type, ref reader);
            Poll(operation);
            if (parsed is null) { error = DatatypeErrors.InvalidLexical; return false; }
            value = parsed; return true;
        }
        catch (Stopped) { error = DatatypeErrors.Stopped; return false; }
    }

    private TemporalValue? ParseCalendar(DatatypeMetadata type, ref Reader r)
    {
        var p = type.Primitive!.Value;
        BigInteger year = 2000;
        int month = 1, day = 1, hour = 0, minute = 0, second = 0;
        string fraction = "";
        if (p is DatatypePrimitive.DateTime or DatatypePrimitive.Date or DatatypePrimitive.GYearMonth or DatatypePrimitive.GYear)
        {
            bool negative = r.Take('-');
            int start = r.Position;
            if (!r.Integer(out year)) return null;
            int digits = r.Position - start;
            if (digits < 4 || (digits > 4 && r.At(start) == '0') || year.IsZero) return null;
            if (negative) year = Negate(year, r.Operation);
            if (p != DatatypePrimitive.GYear && (!r.Take('-') || !r.Two(out month))) return null;
            if (p is DatatypePrimitive.DateTime or DatatypePrimitive.Date && (!r.Take('-') || !r.Two(out day))) return null;
        }
        else if (p == DatatypePrimitive.GMonthDay)
        { if (!r.Take('-') || !r.Take('-') || !r.Two(out month) || !r.Take('-') || !r.Two(out day)) return null; }
        else if (p == DatatypePrimitive.GMonth)
        { if (!r.Take('-') || !r.Take('-') || !r.Two(out month)) return null; }
        else if (p == DatatypePrimitive.GDay)
        { if (!r.Take('-') || !r.Take('-') || !r.Take('-') || !r.Two(out day)) return null; }

        if (p is DatatypePrimitive.DateTime or DatatypePrimitive.Time)
        {
            if (p == DatatypePrimitive.DateTime && !r.Take('T')) return null;
            if (!r.Two(out hour) || !r.Take(':') || !r.Two(out minute) || !r.Take(':') || !r.Two(out second)) return null;
            if (r.Take('.') && !r.Fraction(out fraction)) return null;
            Work(r.Operation, 1);
            if (hour > 24 || minute > 59 || second > 59 || (hour == 24 && (minute != 0 || second != 0 || fraction.Length != 0))) return null;
        }
        Work(r.Operation, 1);
        if (month is < 1 or > 12 || day < 1 || day > DaysInMonth(year, month, r.Operation)) return null;
        bool zoned = false; int zone = 0;
        if (r.Take('Z')) zoned = true;
        else if (r.Peek() is '+' or '-')
        {
            bool minus = r.Take('-'); if (!minus && !r.Take('+')) return null;
            if (!r.Two(out int zh) || !r.Take(':') || !r.Two(out int zm)) return null;
            Work(r.Operation, 1);
            if (zh > 14 || zm > 59 || (zh == 14 && zm != 0)) return null;
            zone = (zh * 60 + zm) * (minus ? -60 : 60); zoned = true;
        }
        if (!r.End) return null;
        var op = r.Operation;
        BigInteger whole;
        if (p == DatatypePrimitive.Time)
        {
            // Recurrent time discards the date after UTC normalization (§3.2.8).
            Work(op, 1);
            whole = ((hour * 3600 + minute * 60 + second - zone) % 86400 + 86400) % 86400;
        }
        else
        {
            whole = Add(Multiply(DayNumber(year, month, day, op), 86400, op), hour * 3600 + minute * 60 + second - zone, op);
        }
        Key key = MakeKey(whole, fraction, op);
        Key low = key, high = key;
        if (!zoned)
        {
            // Keep the uncertainty bounds unwrapped, including for recurrent
            // time. Equality at either endpoint remains indeterminate.
            low = MakeKey(Add(whole, -50400, op), fraction, op);
            high = MakeKey(Add(whole, 50400, op), fraction, op);
        }
        Components(op, 1); Poll(op);
        return new(DatatypeTypes.Name(type.LocalName), p, this, zoned, key, low, high, default);
    }

    private TemporalValue? ParseDuration(DatatypeMetadata type, ref Reader r)
    {
        bool negative = r.Take('-');
        if (!r.Take('P')) return null;
        BigInteger months = 0, seconds = 0;
        string fraction = "";
        bool time = false, any = false, timeItem = false;
        int previous = -1;
        while (!r.End)
        {
            if (!time && r.Take('T')) { time = true; previous = 2; if (r.End) return null; }
            if (!r.Integer(out BigInteger number)) return null;
            bool fractional = r.Take('.');
            if (fractional && !r.Fraction(out fraction)) return null;
            char marker = r.Read();
            int rank = marker switch { 'Y' when !time => 0, 'M' when !time => 1, 'D' when !time => 2,
                'H' when time => 3, 'M' when time => 4, 'S' when time => 5, _ => -1 };
            Work(r.Operation, 1);
            if (rank <= previous || (fractional && rank != 5)) return null;
            previous = rank; any = true; if (time) timeItem = true;
            if (rank <= 1) months = Add(months, rank == 0 ? Multiply(number, 12, r.Operation) : number, r.Operation);
            else seconds = Add(seconds, Multiply(number, rank switch { 2 => 86400, 3 => 3600, 4 => 60, _ => 1 }, r.Operation), r.Operation);
        }
        if (!any || (time && !timeItem)) return null;
        var op = r.Operation;
        if (negative)
        {
            months = Negate(months, op); seconds = Negate(seconds, op);
            if (fraction.Length != 0)
            {
                seconds = Add(seconds, -1, op);
                Components(op, Blocks(fraction.Length) + 1); Work(op, fraction.Length);
                // Trimmed positive fraction has a nonzero final digit. Ten's
                // complement is consequently trimmed too; no BigInteger scale.
                fraction = string.Create(fraction.Length, fraction, static (destination, source) =>
                { for (int i = 0; i < source.Length; i++) destination[i] = (char)('0' + (i == source.Length - 1 ? 10 : 9) - (source[i] - '0')); });
            }
        }
        // Appendix E: all four starts have day=1/time=0. Month addition cannot
        // require day clamping; fixed-length seconds are added on the timeline.
        Key a = DurationPoint(1696, 9, months, seconds, fraction, op);
        Key b = DurationPoint(1697, 2, months, seconds, fraction, op);
        Key c = DurationPoint(1903, 3, months, seconds, fraction, op);
        Key d = DurationPoint(1903, 7, months, seconds, fraction, op);
        Components(op, 1); Poll(op);
        return new(DatatypeTypes.Name(type.LocalName), DatatypePrimitive.Duration, this, true, a, b, c, d);
    }

    private static Key DurationPoint(int year, int month, BigInteger months, BigInteger seconds, string fraction, ISchemaOperation op)
    {
        BigInteger index = Add(months, (year - 1) * 12 + month - 1, op);
        BigInteger quotient = FloorDivide(index, 12, op);
        int m = (int)Add(index, Negate(Multiply(quotient, 12, op), op), op) + 1;
        BigInteger y = quotient.Sign >= 0 ? Add(quotient, 1, op) : quotient;
        return MakeKey(Add(Multiply(DayNumber(y, m, 1, op), 86400, op), seconds, op), fraction, op);
    }

    // XSD1.0 uses signed nonzero Gregorian years, not XSD1.1 astronomical
    // lexical years. Appendix E's divisibility rule is applied to that year.
    private static BigInteger DayNumber(BigInteger year, int month, int day, ISchemaOperation op)
    {
        bool negative = year.Sign < 0;
        BigInteger preceding = negative ? Negate(year, op) : Add(year, -1, op);
        BigInteger days = Add(Add(Multiply(preceding, 365, op), Divide(preceding, 4, op), op),
            Add(Negate(Divide(preceding, 100, op), op), Divide(preceding, 400, op), op), op);
        if (negative) days = Negate(days, op);
        for (int m = 1; m < month; m++) { Work(op, 1); days = Add(days, DaysInMonth(year, m, op), op); }
        return Add(days, day - 1, op);
    }
    private static int DaysInMonth(BigInteger year, int month, ISchemaOperation op)
    {
        Work(op, 1);
        if (month == 2)
        {
            bool leap = Remainder(year, 400, op).IsZero || (!Remainder(year, 100, op).IsZero && Remainder(year, 4, op).IsZero);
            return leap ? 29 : 28;
        }
        return month is 4 or 6 or 9 or 11 ? 30 : 31;
    }

    public bool ValueEquals(DatatypeAtomicValue left, DatatypeAtomicValue right, ISchemaOperation operation) => Compare(left, right, operation) == XmlValueOrder.Equal;
    public XmlValueOrder Compare(DatatypeAtomicValue left, DatatypeAtomicValue right, ISchemaOperation operation)
    {
        var a = Require(left); var b = Require(right); PollThrow(operation);
        if (a.Primitive != b.Primitive) { PollThrow(operation); return XmlValueOrder.Indeterminate; }
        int order;
        if (a.Primitive == DatatypePrimitive.Duration)
        {
            order = CompareKey(a.A, b.A, operation);
            if (CompareKey(a.B, b.B, operation) != order || CompareKey(a.C, b.C, operation) != order || CompareKey(a.D, b.D, operation) != order)
            { PollThrow(operation); return XmlValueOrder.Indeterminate; }
        }
        else if (a.Zoned == b.Zoned) order = CompareKey(a.A, b.A, operation);
        else if (a.Zoned)
        {
            if (CompareKey(a.A, b.B, operation) < 0) order = -1;
            else if (CompareKey(a.A, b.C, operation) > 0) order = 1;
            else { PollThrow(operation); return XmlValueOrder.Indeterminate; }
        }
        else
        {
            if (CompareKey(a.C, b.A, operation) < 0) order = -1;
            else if (CompareKey(a.B, b.A, operation) > 0) order = 1;
            else { PollThrow(operation); return XmlValueOrder.Indeterminate; }
        }
        PollThrow(operation); return (XmlValueOrder)order;
    }
    public int ValueHashCode(DatatypeAtomicValue value, ISchemaOperation operation)
    {
        var v = Require(value); PollThrow(operation);
        int hash = Hash(v.A, operation);
        if (v.Primitive == DatatypePrimitive.Duration)
        { hash = unchecked(hash * 31 + Hash(v.B, operation)); hash = unchecked(hash * 31 + Hash(v.C, operation)); hash = unchecked(hash * 31 + Hash(v.D, operation)); }
        else { DatatypeValues.Charge(operation); hash = unchecked(hash * 31 + (v.Zoned ? 1 : 0)); }
        PollThrow(operation); return hash;
    }
    public long? ValueLength(DatatypeAtomicValue value, ISchemaOperation operation) { _ = Require(value); PollThrow(operation); return null; }
    public int? TotalDigits(DatatypeAtomicValue value, ISchemaOperation operation) { _ = Require(value); PollThrow(operation); return null; }
    public int? FractionDigits(DatatypeAtomicValue value, ISchemaOperation operation) { _ = Require(value); PollThrow(operation); return null; }
    private TemporalValue Require(DatatypeAtomicValue value) => value is TemporalValue temporal && ReferenceEquals(temporal.Provider, this)
        ? temporal : throw new ArgumentException("Value belongs to another temporal provider.", nameof(value));
    private static void PollThrow(ISchemaOperation op) => DatatypeValues.Charge(op, 0);
    private static int CompareKey(Key a, Key b, ISchemaOperation op)
    {
        DatatypeValues.Charge(op);
        bool an = a.Whole[0] == '-', bn = b.Whole[0] == '-';
        if (an != bn) return an ? -1 : 1;
        int magnitude = a.Whole.Length.CompareTo(b.Whole.Length);
        if (magnitude != 0) return an ? -Math.Sign(magnitude) : Math.Sign(magnitude);
        for (int i = an ? 1 : 0; i < a.Whole.Length; i++)
        {
            DatatypeValues.Charge(op);
            int c = a.Whole[i].CompareTo(b.Whole[i]);
            if (c != 0) return an ? -Math.Sign(c) : Math.Sign(c);
        }
        int length = Math.Max(a.Fraction.Length, b.Fraction.Length);
        for (int i = 0; i < length; i++)
        {
            DatatypeValues.Charge(op);
            char x = i < a.Fraction.Length ? a.Fraction[i] : '0', y = i < b.Fraction.Length ? b.Fraction[i] : '0';
            if (x != y) return x < y ? -1 : 1;
        }
        return 0;
    }
    private static int Hash(Key key, ISchemaOperation op)
    {
        int hash = 17;
        foreach (char c in key.Whole) { DatatypeValues.Charge(op); hash = unchecked(hash * 31 + c); }
        DatatypeValues.Charge(op); hash = unchecked(hash * 31 + '.');
        foreach (char c in key.Fraction) { DatatypeValues.Charge(op); hash = unchecked(hash * 31 + c); }
        return hash;
    }

    private readonly record struct Key(string Whole, string Fraction);
    private sealed class TemporalValue(SchemaName type, DatatypePrimitive primitive, TemporalDatatypeProvider provider,
        bool zoned, Key a, Key b, Key c, Key d) : DatatypeAtomicValue(type, primitive, provider)
    { internal bool Zoned => zoned; internal Key A => a; internal Key B => b; internal Key C => c; internal Key D => d; }
    private sealed class Stopped : Exception;
    private static void Poll(ISchemaOperation op) => Work(op, 0);
    private static void Work(ISchemaOperation op, long work) { if (!op.Charge(work, default)) throw new Stopped(); }
    private static void Components(ISchemaOperation op, int count) { if (!op.ChargeComponents(count, default)) throw new Stopped(); }
    private static int Blocks(int length) => length / 16 + (length % 16 == 0 ? 0 : 1);
    // Reserve a node and each possible 32-bit result/scratch limb before CLR
    // arithmetic. Small values can use inline BigInteger storage but still pay
    // these stable units. Each small-operand operation visits at most N limbs.
    private static int Limbs(BigInteger value, ISchemaOperation op)
    { Work(op, 1); return (value.GetByteCount() - 1) / 4 + 1; }
    private static void Arithmetic(ISchemaOperation op, BigInteger a, BigInteger b)
    { int n = Math.Max(Limbs(a, op), Limbs(b, op)); Work(op, n); Components(op, checked(n + 2)); }
    private static BigInteger Add(BigInteger a, BigInteger b, ISchemaOperation op) { Arithmetic(op, a, b); return a + b; }
    private static BigInteger Multiply(BigInteger a, int b, ISchemaOperation op) { Arithmetic(op, a, b); return a * b; }
    private static BigInteger Divide(BigInteger a, int b, ISchemaOperation op) { Arithmetic(op, a, b); return a / b; }
    private static BigInteger Remainder(BigInteger a, int b, ISchemaOperation op) { Arithmetic(op, a, b); return a % b; }
    private static BigInteger Negate(BigInteger a, ISchemaOperation op) { Arithmetic(op, a, 0); return -a; }
    private static BigInteger FloorDivide(BigInteger a, int b, ISchemaOperation op)
    { var q = Divide(a, b, op); return a.Sign < 0 && !Remainder(a, b, op).IsZero ? Add(q, -1, op) : q; }
    private static Key MakeKey(BigInteger whole, string fraction, ISchemaOperation op)
    {
        bool negative = whole.Sign < 0; if (negative) whole = Negate(whole, op);
        int limbs = Limbs(whole, op);
        long capacity = (long)limbs * 10 + 1;
        if (capacity > int.MaxValue) { op.Refuse("XSD.Datatype.Limit", "Temporal representation exceeded.", default); throw new Stopped(); }
        Components(op, checked(Blocks((int)capacity) + 1));
        char[] buffer = new char[(int)capacity]; int position = buffer.Length;
        do
        {
            Arithmetic(op, whole, 10);
            whole = BigInteger.DivRem(whole, 10, out var remainder);
            Work(op, 1); buffer[--position] = (char)('0' + (int)remainder);
        } while (!whole.IsZero);
        if (negative) { Work(op, 1); buffer[--position] = '-'; }
        int length = buffer.Length - position;
        Components(op, Blocks(length) + 1); Work(op, length);
        string text = new(buffer, position, length); Poll(op);
        return new(text, fraction);
    }

    private ref struct Reader(ReadOnlySpan<char> source, ISchemaOperation operation)
    {
        private readonly ReadOnlySpan<char> input = source;
        internal readonly ISchemaOperation Operation = operation;
        internal int Position;
        internal bool End => Position == input.Length;
        internal char At(int index) { Work(Operation, 1); return input[index]; }
        internal char Peek() { Poll(Operation); return End ? '\0' : At(Position); }
        internal bool Take(char expected) { if (Peek() != expected) return false; Position++; return true; }
        internal char Read() { char c = Peek(); if (!End) Position++; return c; }
        internal bool Two(out int value)
        {
            value = 0;
            for (int i = 0; i < 2; i++) { char c = Read(); if (c is < '0' or > '9') return false; Work(Operation, 1); value = value * 10 + c - '0'; }
            return true;
        }
        internal bool Integer(out BigInteger value)
        {
            value = 0; int start = Position;
            while (Peek() is >= '0' and <= '9')
            { char c = Read(); value = Add(Multiply(value, 10, Operation), c - '0', Operation); }
            return Position > start;
        }
        internal bool Fraction(out string fraction)
        {
            int start = Position;
            while (Peek() is >= '0' and <= '9') Position++;
            if (Position == start) { fraction = ""; return false; }
            int end = Position;
            while (end > start && At(end - 1) == '0') end--;
            int length = end - start;
            if (length == 0) { fraction = ""; return true; }
            Components(Operation, Blocks(length) + 1); Work(Operation, length);
            fraction = new string(input.Slice(start, length)); return true;
        }
    }
}
