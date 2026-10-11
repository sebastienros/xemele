namespace Xemele.Html;

// WHATWG replacement-mode decoders. Lead bytes retain their source offset across
// refills. Invalid ASCII trail bytes are reconsumed, preserving markup boundaries.
internal sealed class HtmlByteDecoder
{
    private readonly string encoding;
    private readonly HtmlTokenizer tokenizer;
    private readonly char[] output = new char[4096];
    private readonly long[] offsets = new long[4096];
    private int count, lead, second, third, needed, seen, code, lower = 0x80, upper = 0xBF, surrogate;
    private long opening, secondOffset, thirdOffset, surrogateOffset;
    private bool jis0212, replacementReturned, isoOutput;
    private int isoState, isoOutputState;
    internal HtmlByteDecoder(string encoding, HtmlTokenizer tokenizer) { this.encoding = encoding; this.tokenizer = tokenizer; }
    internal void Feed(ReadOnlySpan<byte> input, long offset)
    {
        for (int i = 0; i < input.Length; i++) Byte(input[i], checked(offset + i));
        Flush();
    }
    internal void Complete(long offset)
    {
        if (encoding == "iso-2022-jp")
        {
            if (isoState == 4 || isoState == 5) Emit(0xFFFD, opening);
            else if (isoState == 6) { Emit(0xFFFD, opening); int saved = lead; isoState = isoOutputState; Byte(saved, secondOffset); if (isoState == 4) { Emit(0xFFFD, secondOffset); isoState = 3; } }
        }
        else if (lead != 0 || needed != 0 || second != 0 || third != 0 || surrogate != 0) Emit(0xFFFD, surrogate != 0 ? surrogateOffset : opening);
        lead = needed = second = third = surrogate = 0; Flush();
    }
    private void Flush() { if (count == 0) return; tokenizer.Feed(output.AsSpan(0, count), offsets.AsSpan(0, count)); count = 0; }
    private void Emit(int scalar, long offset)
    {
        if (scalar < 0) scalar = 0xFFFD;
        if (count > output.Length - 2) Flush();
        if (scalar <= 0xFFFF) { output[count] = (char)scalar; offsets[count++] = offset; }
        else { scalar -= 0x10000; output[count] = (char)(0xD800 + (scalar >> 10)); offsets[count++] = offset; output[count] = (char)(0xDC00 + (scalar & 1023)); offsets[count++] = offset; }
    }
    private static int Index(ReadOnlySpan<int> table, int pointer) => (uint)pointer < (uint)table.Length ? table[pointer] : -1;
    private void Byte(int b, long offset)
    {
        switch (encoding)
        {
            case "utf-8": Utf8(b, offset); return;
            case "utf-16le": case "utf-16be": Utf16(b, offset); return;
            case "gbk": case "gb18030": Gb(b, offset); return;
            case "big5": Big5(b, offset); return;
            case "euc-jp": EucJp(b, offset); return;
            case "shift_jis": ShiftJis(b, offset); return;
            case "euc-kr": EucKr(b, offset); return;
            case "iso-2022-jp": IsoJp(b, offset); return;
            case "x-user-defined": Emit(b < 0x80 ? b : 0xF780 + b - 0x80, offset); return;
            case "replacement": if (!replacementReturned) { replacementReturned = true; Emit(0xFFFD, offset); } return;
            default: Emit(b < 128 ? b : Index(HtmlEncodingTables.Single(encoding), b - 128), offset); return;
        }
    }
    private void Utf8(int b, long offset)
    {
        if (needed == 0)
        {
            opening = offset;
            if (b < 0x80) { Emit(b, offset); return; }
            if (b is >= 0xC2 and <= 0xDF) { needed = 1; code = b & 31; return; }
            if (b is >= 0xE0 and <= 0xEF) { needed = 2; code = b & 15; if (b == 0xE0) lower = 0xA0; if (b == 0xED) upper = 0x9F; return; }
            if (b is >= 0xF0 and <= 0xF4) { needed = 3; code = b & 7; if (b == 0xF0) lower = 0x90; if (b == 0xF4) upper = 0x8F; return; }
            Emit(0xFFFD, offset); return;
        }
        if (b < lower || b > upper) { needed = seen = code = 0; lower = 0x80; upper = 0xBF; Emit(0xFFFD, opening); Utf8(b, offset); return; }
        lower = 0x80; upper = 0xBF; code = (code << 6) | (b & 63);
        if (++seen != needed) return; Emit(code, opening); code = needed = seen = 0;
    }
    private void Utf16(int b, long offset)
    {
        if (lead == 0) { lead = b + 1; opening = offset; return; }
        int unit = encoding == "utf-16le" ? lead - 1 | b << 8 : (lead - 1) << 8 | b; lead = 0;
        if (surrogate != 0)
        {
            int high = surrogate; surrogate = 0;
            if (unit is >= 0xDC00 and <= 0xDFFF) { Emit(0x10000 + ((high - 0xD800) << 10) + unit - 0xDC00, surrogateOffset); return; }
            Emit(0xFFFD, surrogateOffset);
        }
        if (unit is >= 0xD800 and <= 0xDBFF) { surrogate = unit; surrogateOffset = opening; }
        else Emit(unit is >= 0xDC00 and <= 0xDFFF ? 0xFFFD : unit, opening);
    }
    private void ShiftJis(int b, long offset)
    {
        if (lead != 0)
        {
            int saved = lead; lead = 0;
            int pointer = b is >= 0x40 and <= 0x7E or >= 0x80 and <= 0xFC ? (saved - (saved < 0xA0 ? 0x81 : 0xC1)) * 188 + b - (b < 0x7F ? 0x40 : 0x41) : -1;
            int scalar = pointer is >= 8836 and <= 10715 ? 0xE000 - 8836 + pointer : Index(HtmlEncodingTables.jis0208, pointer);
            Emit(scalar, opening); if (scalar < 0 && b < 0x80) ShiftJis(b, offset); return;
        }
        if (b <= 0x80) Emit(b, offset);
        else if (b is >= 0xA1 and <= 0xDF) Emit(0xFF61 - 0xA1 + b, offset);
        else if (b is >= 0x81 and <= 0x9F or >= 0xE0 and <= 0xFC) { lead = b; opening = offset; }
        else Emit(0xFFFD, offset);
    }
    private void EucKr(int b, long offset)
    {
        if (lead != 0) { int saved = lead; lead = 0; int scalar = b is >= 0x41 and <= 0xFE ? Index(HtmlEncodingTables.euc_kr, (saved - 0x81) * 190 + b - 0x41) : -1; Emit(scalar, opening); if (scalar < 0 && b < 0x80) EucKr(b, offset); return; }
        if (b < 0x80) Emit(b, offset); else if (b is >= 0x81 and <= 0xFE) { lead = b; opening = offset; } else Emit(0xFFFD, offset);
    }
    private void Big5(int b, long offset)
    {
        if (lead != 0)
        {
            int saved = lead; lead = 0; int pointer = b is >= 0x40 and <= 0x7E or >= 0xA1 and <= 0xFE ? (saved - 0x81) * 157 + b - (b < 0x7F ? 0x40 : 0x62) : -1;
            if (pointer is 1133 or 1135 or 1164 or 1166) { Emit(pointer is 1133 or 1135 ? 0xCA : 0xEA, opening); Emit(pointer is 1133 or 1164 ? 0x304 : 0x30C, opening); return; }
            int scalar = Index(HtmlEncodingTables.big5, pointer); Emit(scalar, opening); if (scalar < 0 && b < 0x80) Big5(b, offset); return;
        }
        if (b < 0x80) Emit(b, offset); else if (b is >= 0x81 and <= 0xFE) { lead = b; opening = offset; } else Emit(0xFFFD, offset);
    }
    private void EucJp(int b, long offset)
    {
        if (lead == 0x8E && b is >= 0xA1 and <= 0xDF) { lead = 0; Emit(0xFF61 - 0xA1 + b, opening); return; }
        if (lead == 0x8F && b is >= 0xA1 and <= 0xFE) { jis0212 = true; lead = b; return; }
        if (lead != 0)
        {
            int saved = lead; lead = 0; int scalar = saved is >= 0xA1 and <= 0xFE && b is >= 0xA1 and <= 0xFE ? Index(jis0212 ? HtmlEncodingTables.jis0212 : HtmlEncodingTables.jis0208, (saved - 0xA1) * 94 + b - 0xA1) : -1;
            jis0212 = false; Emit(scalar, opening); if (scalar < 0 && b < 0x80) EucJp(b, offset); return;
        }
        if (b < 0x80) Emit(b, offset); else if (b is 0x8E or 0x8F or >= 0xA1 and <= 0xFE) { lead = b; opening = offset; } else Emit(0xFFFD, offset);
    }
    private void Gb(int b, long offset)
    {
        if (third != 0)
        {
            int a = lead, c = third, d = second; lead = second = third = 0;
            if (b is >= 0x30 and <= 0x39)
            {
                int pointer = ((a - 0x81) * 10 + d - 0x30) * 1260 + (c - 0x81) * 10 + b - 0x30;
                int scalar = -1;
                if (pointer == 7457) scalar = 0xE7C7;
                else if (!(pointer is > 39419 and < 189000 or > 1237575))
                {
                    var ranges = HtmlEncodingTables.gb18030_ranges; int found = 0;
                    for (int i = 0; i < ranges.Length; i += 2) { if (ranges[i] > pointer) break; found = i; }
                    scalar = ranges[found + 1] + pointer - ranges[found];
                }
                Emit(scalar, opening); return;
            }
            Emit(0xFFFD, opening); Gb(d, secondOffset); Gb(c, thirdOffset); Gb(b, offset); return;
        }
        if (second != 0)
        {
            if (b is >= 0x81 and <= 0xFE) { third = b; thirdOffset = offset; return; }
            int saved = second; lead = second = 0; Emit(0xFFFD, opening); Gb(saved, secondOffset); Gb(b, offset); return;
        }
        if (lead != 0)
        {
            if (b is >= 0x30 and <= 0x39) { second = b; secondOffset = offset; return; }
            int saved = lead; lead = 0; int pointer = b is >= 0x40 and <= 0x7E or >= 0x80 and <= 0xFE ? (saved - 0x81) * 190 + b - (b < 0x7F ? 0x40 : 0x41) : -1;
            int scalar = Index(HtmlEncodingTables.gb18030, pointer); Emit(scalar, opening); if (scalar < 0 && b < 0x80) Gb(b, offset); return;
        }
        if (b < 0x80) Emit(b, offset); else if (b == 0x80) Emit(0x20AC, offset); else if (b is >= 0x81 and <= 0xFE) { lead = b; opening = offset; } else Emit(0xFFFD, offset);
    }
    private void IsoJp(int b, long offset)
    {
        switch (isoState)
        {
            case 0: case 1: case 2: case 3:
                if (b == 0x1B) { isoState = 5; opening = offset; return; }
                isoOutput = false;
                if (isoState == 3) { if (b is >= 0x21 and <= 0x7E) { lead = b; opening = offset; isoState = 4; } else Emit(0xFFFD, offset); return; }
                if (isoState == 2) { Emit(b is >= 0x21 and <= 0x5F ? 0xFF61 - 0x21 + b : 0xFFFD, offset); return; }
                if (b > 0x7F || b is 0x0E or 0x0F) Emit(0xFFFD, offset);
                else Emit(isoState == 1 && b == 0x5C ? 0xA5 : isoState == 1 && b == 0x7E ? 0x203E : b, offset);
                return;
            case 4:
                if (b == 0x1B) { Emit(0xFFFD, opening); isoState = 5; opening = offset; return; }
                isoState = 3; Emit(b is >= 0x21 and <= 0x7E ? Index(HtmlEncodingTables.jis0208, (lead - 0x21) * 94 + b - 0x21) : 0xFFFD, opening); return;
            case 5:
                if (b is 0x24 or 0x28) { lead = b; secondOffset = offset; isoState = 6; return; }
                isoOutput = false; isoState = isoOutputState; Emit(0xFFFD, opening); IsoJp(b, offset); return;
            case 6:
                int next = lead == 0x28 ? b switch { 0x42 => 0, 0x4A => 1, 0x49 => 2, _ => -1 } : b is 0x40 or 0x42 ? 3 : -1;
                if (next >= 0) { isoState = isoOutputState = next; bool old = isoOutput; isoOutput = true; if (old) Emit(0xFFFD, opening); return; }
                int saved = lead; isoOutput = false; isoState = isoOutputState; Emit(0xFFFD, opening); IsoJp(saved, secondOffset); IsoJp(b, offset); return;
        }
    }
}
