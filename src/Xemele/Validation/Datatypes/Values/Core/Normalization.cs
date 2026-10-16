using System.Buffers;
using System.Text;
namespace Xemele.Validation;

internal static class DatatypeNormalization
{
    internal static bool IsWhitespace(char c) => c is '\t' or '\n' or '\r' or ' ';

    // First pass validates scalars and measures output. One work unit per input
    // scalar, including collapsed whitespace. No output retained or allocated.
    internal static bool TryMeasure(ReadOnlySpan<char> input, XmlWhitespaceRule rule, ISchemaOperation operation,
        out int length, out long scalars, out XmlDatatypeError? error)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (!Enum.IsDefined(rule))
        {
            throw new ArgumentOutOfRangeException(nameof(rule));
        }

        length = 0;
        scalars = 0;
        error = null;
        if (!operation.Charge(0, default))
        {
            error = DatatypeErrors.Stopped;
            return false;
        }
        var pending = false;
        for (var i = 0; i < input.Length;)
        {
            if (!operation.Charge(1, default))
            {
                error = DatatypeErrors.Stopped;
                return false;
            }
            if (Rune.DecodeFromUtf16(input[i..], out var rune, out var consumed) != OperationStatus.Done)
            {
                error = DatatypeErrors.InvalidUnicode;
                return false;
            }
            i += consumed;
            scalars++;
            if (rule == XmlWhitespaceRule.Collapse && rune.IsAscii && IsWhitespace((char)rune.Value))
            {
                if (length != 0)
                {
                    pending = true;
                }

                continue;
            }
            if (pending)
            {
                length++;
                pending = false;
            }
            length += consumed;
        }
        return operation.Charge(0, default) || Fail(out error);
    }

    internal static bool TryNormalize(ReadOnlySpan<char> input, XmlWhitespaceRule rule, Span<char> destination,
        ISchemaOperation operation, out int written, out XmlDatatypeError? error)
    {
        written = 0;
        if (!TryMeasure(input, rule, operation, out var length, out _, out error))
        {
            return false;
        }

        if (destination.Length < length)
        {
            throw new ArgumentException("Normalization destination is too small.", nameof(destination));
        }

        if (input.Overlaps(destination))
        {
            throw new ArgumentException("Normalization input/output must not overlap.", nameof(destination));
        }

        if (!Write(input, rule, destination[..length], operation))
        {
            error = DatatypeErrors.Stopped;
            return false;
        }
        written = length;
        return true;
    }

    // Owns only the returned string. Scratch is bounded stack storage or pooled,
    // returned in finally; no caller buffer or rented memory survives.
    internal static string Normalize(ReadOnlySpan<char> input, XmlWhitespaceRule rule, ISchemaOperation operation)
    {
        if (!TryMeasure(input, rule, operation, out var length, out _, out var error))
        {
            if (error == DatatypeErrors.InvalidUnicode)
            {
                throw new FormatException(error.Message);
            }

            throw SchemaSnapshots.Failure(operation);
        }
        if (!operation.ChargeComponents(1, default))
        {
            throw SchemaSnapshots.Failure(operation);
        }

        char[]? rented = null;
        var output = length <= 256 ? stackalloc char[length] : (rented = ArrayPool<char>.Shared.Rent(length)).AsSpan(0, length);
        try
        {
            if (!Write(input, rule, output, operation))
            {
                throw SchemaSnapshots.Failure(operation);
            }

            return new string(output);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<char>.Shared.Return(rented);
            }
        }
    }

    private static bool Write(ReadOnlySpan<char> input, XmlWhitespaceRule rule, Span<char> output, ISchemaOperation operation)
    {
        var written = 0;
        var pending = false;
        for (var i = 0; i < input.Length;)
        {
            if (!operation.Charge(1, default))
            {
                return false;
            }
            // Validated in first pass; synchronous borrowed input must remain stable.
            Rune.DecodeFromUtf16(input[i..], out var rune, out var consumed);
            var c = input[i];
            i += consumed;
            if (rule == XmlWhitespaceRule.Collapse && rune.IsAscii && IsWhitespace(c))
            {
                if (written != 0)
                {
                    pending = true;
                }

                continue;
            }
            if (pending)
            {
                output[written++] = ' ';
                pending = false;
            }
            if (rule == XmlWhitespaceRule.Replace && IsWhitespace(c))
            {
                output[written++] = ' ';
            }
            else
            {
                input.Slice(i - consumed, consumed).CopyTo(output[written..]);
                written += consumed;
            }
        }
        return operation.Charge(0, default);
    }
    private static bool Fail(out XmlDatatypeError? error)
    {
        error = DatatypeErrors.Stopped;
        return false;
    }
}
