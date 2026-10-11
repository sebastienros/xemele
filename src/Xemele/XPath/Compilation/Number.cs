using System.Globalization;
namespace Xemele.XPath.Internal;

internal static class XPathCompileNumber
{
    // Every finite binary64 value and rounding midpoint has a terminating decimal
    // representation with at most 1075 fractional digits (2^-1075). Keeping 1100
    // significant digits plus a nonzero sticky digit preserves which side of any
    // midpoint the exact nonnegative Number lies on. The runtime conversion then
    // receives bounded input, even for unlimited million-character expressions.
    internal static double Parse(ReadOnlySpan<char> text, CancellationToken cancellation)
    {
        const int maximum = 1100;
        Span<char> buffer = stackalloc char[maximum + 32];
        int digits = 0, before = -1, leading = 0, kept = 0; bool started = false, sticky = false;
        for (int i = 0; i < text.Length; i++)
        {
            if ((i & 255) == 0) cancellation.ThrowIfCancellationRequested();
            char c = text[i]; if (c == '.') { before = digits; continue; }
            digits++;
            if (!started && c == '0') { leading++; continue; }
            started = true;
            if (kept < maximum) buffer[kept++] = c; else if (c != '0') sticky = true;
        }
        cancellation.ThrowIfCancellationRequested();
        if (!started) return 0;
        if (before < 0) before = digits;
        int order = before - leading;
        if (order > 310) return double.PositiveInfinity;
        if (order < -324) return 0;
        if (sticky) buffer[kept++] = '1';
        int exponent = order - kept;
        buffer[kept++] = 'E';
        if (!exponent.TryFormat(buffer[kept..], out int written, provider: CultureInfo.InvariantCulture)) throw new InvalidOperationException("Bounded exponent buffer.");
        return double.Parse(buffer[..(kept + written)], NumberStyles.AllowExponent | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
    }
}
