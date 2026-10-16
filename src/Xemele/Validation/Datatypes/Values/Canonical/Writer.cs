using System.Globalization;
namespace Xemele.Validation;

// First pass measures; second pass writes directly into the sole owned string.
// Fixed stack buffers are bounded independently of input. No rented output survives.
internal ref struct ConstraintWriter(Span<char> destination, bool writing, ISchemaOperation operation)
{
    private readonly Span<char> destination = destination;
    private readonly bool writing = writing;
    internal readonly ISchemaOperation Operation = operation;
    internal int Length
    {
        get; private set;
    }
    internal static int CheckedLength(long length, ISchemaOperation op)
    {
        // Keep UTF-16 byte capacity representable as a positive Int32 too.
        if (length < 0 || length > (int.MaxValue - 2) / 2)
        {
            op.Refuse("XSD.Datatype.Limit", "Constraint lexical representation exceeded.", default);
            throw new ConstraintOperation.Stopped();
        }
        return (int)length;
    }
    internal void Append(char c)
    {
        Operation.Charge(1, default);
        var next = CheckedLength((long)Length + 1, Operation);
        if (writing)
        {
            destination[Length] = c;
        }

        Length = next;
    }
    internal void Append(scoped ReadOnlySpan<char> text)
    {
        _ = CheckedLength((long)Length + text.Length, Operation);
        foreach (var c in text)
        {
            Append(c);
        }
    }
    internal void Number(int number, int minimum = 1)
    {
        Span<char> scratch = stackalloc char[11];
        Operation.Charge(11, default); // bounded invariant formatter reservation
        if (!number.TryFormat(scratch, out var n, provider: CultureInfo.InvariantCulture))
        {
            throw new InvalidOperationException();
        }

        for (var i = n; i < minimum; i++)
        {
            Append('0');
        }

        Append(scratch[..n]);
    }
    internal void Collapsed(ReadOnlySpan<char> input)
    {
        bool pending = false, any = false;
        foreach (var c in input)
        {
            Operation.Charge(1, default);
            if (DatatypeNormalization.IsWhitespace(c))
            {
                if (any)
                {
                    pending = true;
                }

                continue;
            }
            if (pending)
            {
                Append(' ');
                pending = false;
            }
            Append(c);
            any = true;
        }
    }
}
