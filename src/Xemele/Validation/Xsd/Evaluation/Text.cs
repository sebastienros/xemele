namespace Xemele.Validation;

internal sealed class XsdEvaluationStopped : Exception
{
}
internal static class XsdEvaluationText
{
    internal static void Work(ISchemaOperation op, XmlLocation at, long work = 1)
    {
        op.CancellationToken.ThrowIfCancellationRequested();
        if (!op.Charge(work, at))
        {
            throw new XsdEvaluationStopped();
        }
    }
    internal static void Retain(ISchemaOperation op, XmlLocation at)
    {
        Work(op, at);
        if (!op.ChargeComponents(1, at))
        {
            throw new XsdEvaluationStopped();
        }
    }
    internal static int IndexOf(ReadOnlySpan<char> text, char target, ISchemaOperation op, XmlLocation at)
    {
        for (var i = 0; i < text.Length; i++)
        {
            Work(op, at);
            if (text[i] == target)
            {
                return i;
            }
        }
        return -1;
    }
    internal static ReadOnlySpan<char> TrimXml(ReadOnlySpan<char> text, ISchemaOperation op, XmlLocation at)
    {
        while (!text.IsEmpty)
        {
            Work(op, at);
            if (!White(text[0]))
            {
                break;
            }

            text = text[1..];
        }
        while (!text.IsEmpty)
        {
            Work(op, at);
            if (!White(text[^1]))
            {
                break;
            }

            text = text[..^1];
        }
        return text;
    }
    private static bool White(char c) => c is ' ' or '\t' or '\r' or '\n';
}

// The first DOM-owned fragment is borrowed. Subsequent fragments have a charged
// run-owned buffer. Every copied UTF-16 unit is charged before writing; final
// string storage is reserved separately and polled during string.Create copying.
internal sealed class XsdTextBuffer
{
    private string? borrowed;
    private char[]? buffer;
    private int length;
    internal void Append(string value, ISchemaOperation op, XmlLocation at)
    {
        XsdEvaluationText.Work(op, at, 0);
        if (value.Length == 0)
        {
            return;
        }

        if (borrowed is null)
        {
            borrowed = value;
            length = value.Length;
            return;
        }
        if (value.Length > Array.MaxLength - length)
        {
            op.Refuse("VALIDATION.Limit", "Simple content buffer capacity exceeded.", at);
            throw new XsdEvaluationStopped();
        }
        var required = length + value.Length;
        if (buffer is null || buffer.Length < required)
        {
            var capacity = (int)Math.Min(Array.MaxLength, Math.Max(required, (long)(buffer?.Length ?? length) * 2));
            XsdEvaluationText.Retain(op, at);
            var next = new char[capacity];
            for (var i = 0; i < length; i++)
            {
                XsdEvaluationText.Work(op, at);
                next[i] = buffer is null ? borrowed[i] : buffer[i];
            }
            buffer = next;
        }
        for (var i = 0; i < value.Length; i++)
        {
            XsdEvaluationText.Work(op, at);
            buffer[length + i] = value[i];
        }
        length = required;
    }
    internal string Materialize(ISchemaOperation op, XmlLocation at)
    {
        XsdEvaluationText.Work(op, at, 0);
        if (buffer is null)
        {
            return borrowed ?? "";
        }

        XsdEvaluationText.Retain(op, at);
        return string.Create(length, (Buffer: buffer, Operation: op, Location: at), static (target, state) =>
        {
            for (var i = 0; i < target.Length; i++)
            {
                XsdEvaluationText.Work(state.Operation, state.Location);
                target[i] = state.Buffer[i];
            }
        });
    }
}
