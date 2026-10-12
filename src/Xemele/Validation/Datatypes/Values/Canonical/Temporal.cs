namespace Xemele.Validation;
internal sealed partial class TemporalDatatypeProvider
{
    internal static void WriteConstraint(DatatypeAtomicValue value, ReadOnlySpan<char> authored, ref ConstraintWriter writer)
    {
        var p = value.Primitive; var op = writer.Operation;
        Work(op, 1);
        // The pinned 1.0 edition prescribes no canonical subsection for duration
        // or g*. Preserve the validated authored representative, not 1.1 rules.
        if (p is not (DatatypePrimitive.DateTime or DatatypePrimitive.Date or DatatypePrimitive.Time))
        { writer.Collapsed(authored); return; }
        int begin = 0, end = authored.Length;
        while (begin < end) { Work(op, 1); if (!DatatypeNormalization.IsWhitespace(authored[begin])) break; begin++; }
        while (end > begin) { Work(op, 1); if (!DatatypeNormalization.IsWhitespace(authored[end - 1])) break; end--; }
        var input = authored[begin..end]; var r = new Reader(input, op);
        Integer year = 2000; int month = 1, day = 1, hour = 0, minute = 0, second = 0;
        if (p != DatatypePrimitive.Time)
        {
            bool negative = r.Take('-'); _ = r.Integer(out year); if (negative) year = Negate(year, op);
            _ = r.Take('-'); _ = r.Two(out month); _ = r.Take('-'); _ = r.Two(out day);
        }
        int fractionStart = 0, fractionEnd = 0;
        if (p != DatatypePrimitive.Date)
        {
            if (p == DatatypePrimitive.DateTime) _ = r.Take('T');
            _ = r.Two(out hour); _ = r.Take(':'); _ = r.Two(out minute); _ = r.Take(':'); _ = r.Two(out second);
            if (r.Take('.'))
            {
                fractionStart = r.Position;
                while (r.Peek() is >= '0' and <= '9') r.Position++;
                fractionEnd = r.Position;
                while (fractionEnd > fractionStart) { Work(op, 1); if (input[fractionEnd - 1] != '0') break; fractionEnd--; }
            }
        }
        bool zoned = r.Take('Z'); int zone = 0;
        if (!zoned && r.Peek() is '+' or '-')
        {
            bool negative = r.Take('-'); if (!negative) _ = r.Take('+');
            _ = r.Two(out int zh); _ = r.Take(':'); _ = r.Two(out int zm);
            zone = (zh * 60 + zm) * (negative ? -1 : 1); zoned = true;
        }
        if (p == DatatypePrimitive.Date)
        {
            // Midpoint normalization determines date; preserve the recoverable
            // offset, rather than discarding the interval's first-moment minutes.
            Work(op, 1);
            if (zone > 720) { ShiftDay(ref year, ref month, ref day, -1, op); zone -= 1440; }
            else if (zone <= -720) { ShiftDay(ref year, ref month, ref day, 1, op); zone += 1440; }
        }
        else if (((TemporalValue)value).A.Phase == 0)
        {
            Work(op, 1); int total = hour * 3600 + minute * 60 + second - zone * 60;
            while (total < 0) { Work(op, 1); total += 86400; if (p == DatatypePrimitive.DateTime) ShiftDay(ref year, ref month, ref day, -1, op); }
            while (total >= 86400) { Work(op, 1); total -= 86400; if (p == DatatypePrimitive.DateTime) ShiftDay(ref year, ref month, ref day, 1, op); }
            hour = total / 3600; minute = total / 60 % 60; second = total % 60;
        }
        // Phase -1 is the provider's supported leap instant, distinct from the
        // following minute even when its whole-second coordinate is identical.
        if (p != DatatypePrimitive.Time)
        {
            WriteYear(year, ref writer); writer.Append('-'); writer.Number(month, 2); writer.Append('-'); writer.Number(day, 2);
        }
        if (p != DatatypePrimitive.Date)
        {
            if (p == DatatypePrimitive.DateTime) writer.Append('T');
            writer.Number(hour, 2); writer.Append(':'); writer.Number(minute, 2); writer.Append(':'); writer.Number(second, 2);
            if (fractionEnd != fractionStart) { writer.Append('.'); writer.Append(input[fractionStart..fractionEnd]); }
        }
        if (zoned)
        {
            if (p != DatatypePrimitive.Date || zone == 0) writer.Append('Z');
            else { writer.Append(zone < 0 ? '-' : '+'); int offset = Math.Abs(zone); writer.Number(offset / 60, 2); writer.Append(':'); writer.Number(offset % 60, 2); }
        }
    }
    private static void ShiftDay(ref Integer year, ref int month, ref int day, int delta, ISchemaOperation op)
    {
        Work(op, 1); day += delta;
        if (day > DaysInMonth(year, month, op))
        {
            day = 1; month++;
            if (month == 13) { month = 1; year = Add(year, 1, op); if (year.IsZero) year = 1; }
        }
        else if (day == 0)
        {
            month--; if (month == 0) { month = 12; year = Add(year, -1, op); if (year.IsZero) year = -1; }
            day = DaysInMonth(year, month, op);
        }
    }
    private static void WriteYear(Integer year, ref ConstraintWriter writer)
    {
        Work(writer.Operation, 1);
        if (year.Sign < 0) writer.Append('-');
        // Existing Integer stores little-endian base10^9 limbs. Emit each limb
        // once; no CLR BigInteger, repeated division, or narrowed year conversion.
        for (int limb = year.Count - 1; limb >= 0; limb--)
        { Work(writer.Operation, 1); writer.Number((int)year.Digit(limb), limb == year.Count - 1 ? Math.Max(1, 4 - limb * 9) : 9); }
    }
}
