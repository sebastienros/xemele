namespace Xemele.Html;

internal sealed partial class HtmlTokenizer
{
    private bool ReferenceInAttribute => returnState is State.DoubleValue or State.SingleValue or State.UnquotedValue;
    private void StartReference()
    {
        returnState = state; referenceOpening = current; referenceLength = 0; state = State.Reference;
    }
    private void ReferenceCharacter(int c)
    {
        if (ReferenceInAttribute) AttributeChar(attrValue, c);
        else Character(c, referenceOpening);
    }
    private void ReferenceCharacters(ReadOnlySpan<char> value)
    {
        for (int i = 0; i < value.Length; i++)
        {
            int c = value[i];
            if (char.IsHighSurrogate(value[i]) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1])) c = char.ConvertToUtf32(value[i], value[++i]);
            ReferenceCharacter(c);
        }
    }
    private bool ReferenceStep(int c)
    {
        switch (state)
        {
            case State.Reference:
                if (c == '#') { state = State.NumericStart; numericValue = 0;  return false; }
                if (Alnum(c)) { state = State.NamedReference; return true; }
                ReferenceCharacter('&'); state = returnState; return true;
            case State.NamedReference:
                if (Alnum(c) && referenceLength < reference.Length) { reference[referenceLength++] = (char)c; return false; }
                if (c == ';' && referenceLength < reference.Length)
                {
                    reference[referenceLength++] = ';'; ResolveNamed(-1); return false;
                }
                ResolveNamed(c); return true;
            case State.AmbiguousReference:
                if (Alnum(c)) { ReferenceCharacter(c); return false; }
                if (c == ';') { Error("unknown-named-character-reference"); ReferenceCharacter(c); state = returnState; return false; }
                state = returnState; return true;
            case State.NumericStart:
                if (c is 'x' or 'X') { reference[0] = (char)c; numericBase = 16; state = State.HexStart; return false; }
                numericBase = 10; state = State.DecimalStart; return true;
            case State.HexStart:
            case State.DecimalStart:
                if (Digit(c, numericBase) >= 0) { state = State.Numeric; return true; }
                Error("absence-of-digits-in-numeric-character-reference");
                ReferenceCharacters("&#"); if (numericBase == 16) ReferenceCharacter(reference[0]);
                state = returnState; return true;
            case State.Numeric:
                int digit = Digit(c, numericBase);
                if (digit >= 0)
                {
                    // Saturation keeps arbitrarily long malformed references bounded, never wraps.
                    if (numericValue <= 0x110000) numericValue = Math.Min(0x110001u, numericValue * (uint)numericBase + (uint)digit);
                    return false;
                }
                if (c != ';') Error("missing-semicolon-after-character-reference");
                ResolveNumeric(); state = returnState; return c != ';';
            default: throw new InvalidOperationException();
        }
    }

    private void ResolveNamed(int next)
    {
        state = returnState;
        var candidate = reference.AsSpan(0, referenceLength);
        if (HtmlNamedReferences.Match(candidate, out int consumed, out string value))
        {
            bool terminated = candidate[consumed - 1] == ';';
            int following = consumed < referenceLength ? candidate[consumed] : next;
            if (ReferenceInAttribute && !terminated && (Alnum(following) || following == '='))
            {
                ReferenceCharacter('&'); ReferenceCharacters(candidate); return;
            }
            if (!terminated) Error("missing-semicolon-after-character-reference");
            ReferenceCharacters(value.AsSpan()); ReferenceCharacters(candidate[consumed..]);
        }
        else
        {
            if (referenceLength > 0 && candidate[^1] == ';') Error("unknown-named-character-reference");
            ReferenceCharacter('&'); ReferenceCharacters(candidate);
            if (candidate.IsEmpty || candidate[^1] != ';') state = State.AmbiguousReference;
        }
    }
    private static int Digit(int c, int radix)
    {
        if (c is >= '0' and <= '9') return c - '0';
        if (radix == 16 && c is >= 'a' and <= 'f') return c - 'a' + 10;
        if (radix == 16 && c is >= 'A' and <= 'F') return c - 'A' + 10;
        return -1;
    }
    private void ResolveNumeric()
    {
        int c = (int)numericValue;
        if (c == 0) { Error("null-character-reference"); c = 0xFFFD; }
        else if (c > 0x10FFFF) { Error("character-reference-outside-unicode-range"); c = 0xFFFD; }
        else if (c is >= 0xD800 and <= 0xDFFF) { Error("surrogate-character-reference"); c = 0xFFFD; }
        else
        {
            if (c is >= 0xFDD0 and <= 0xFDEF || (c & 0xFFFF) is 0xFFFE or 0xFFFF) Error("noncharacter-character-reference");
            if (c is 13 or >= 1 and <= 8 or 11 or >= 14 and <= 31 or >= 0x7F and <= 0x9F) Error("control-character-reference");
            c = c switch
            {
                0x80 => 0x20AC, 0x82 => 0x201A, 0x83 => 0x192, 0x84 => 0x201E, 0x85 => 0x2026,
                0x86 => 0x2020, 0x87 => 0x2021, 0x88 => 0x2C6, 0x89 => 0x2030, 0x8A => 0x160,
                0x8B => 0x2039, 0x8C => 0x152, 0x8E => 0x17D, 0x91 => 0x2018, 0x92 => 0x2019,
                0x93 => 0x201C, 0x94 => 0x201D, 0x95 => 0x2022, 0x96 => 0x2013, 0x97 => 0x2014,
                0x98 => 0x2DC, 0x99 => 0x2122, 0x9A => 0x161, 0x9B => 0x203A, 0x9C => 0x153,
                0x9E => 0x17E, 0x9F => 0x178, _ => c,
            };
        }
        ReferenceCharacter(c);
    }
}
