using System.Buffers;
using System.Text;
using Xemele.Parsing.Dtd;
namespace Xemele.XPath.Internal;
// Reuse verified XML scalar/name classification, polling while scanning rather
// than before an unbounded TreeValidation.Name call. Each charge precedes work.
internal static class ContextScan
{
    internal static void Name(string name, CancellationToken token, IXPathBudget? budget = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (name.Length == 0)
        {
            throw new ArgumentException("Empty XML name.", nameof(name));
        }

        ReadOnlySpan<char> remaining = name;
        var first = true;
        while (!remaining.IsEmpty)
        {
            token.ThrowIfCancellationRequested();
            var units = char.IsHighSurrogate(remaining[0]) && remaining.Length > 1 ? 2 : 1;
            budget?.ChargeOperations(units);
            if (Rune.DecodeFromUtf16(remaining, out var rune, out var length) != OperationStatus.Done || rune.Value == ':' ||
                (first ? !ValueSyntax.IsNameStart(rune.Value) : !ValueSyntax.IsNameChar(rune.Value)))
            {
                throw new ArgumentException("Invalid XML name.", nameof(name));
            }

            first = false;
            remaining = remaining[length..];
        }
    }
    internal static void Text(string value, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(value);
        token.ThrowIfCancellationRequested();
        ReadOnlySpan<char> remaining = value;
        while (!remaining.IsEmpty)
        {
            token.ThrowIfCancellationRequested();
            if (Rune.DecodeFromUtf16(remaining, out var rune, out var length) != OperationStatus.Done || !ValueSyntax.IsXmlScalar(rune.Value))
            {
                throw new ArgumentException("Illegal XML scalar.", nameof(value));
            }

            remaining = remaining[length..];
        }
    }
    internal static void Poll(string value, CancellationToken token, IXPathBudget? budget = null)
    {
        for (var i = 0; i < value.Length;)
        {
            token.ThrowIfCancellationRequested();
            var count = Math.Min(256, value.Length - i);
            budget?.ChargeOperations(count);
            i += count;
        }
        token.ThrowIfCancellationRequested();
    }
}
