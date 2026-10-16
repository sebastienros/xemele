using System.Text;
using System.Runtime.ExceptionServices;

namespace Xemele.Parsing.Dtd;

internal readonly record struct DtdNormalizedValue(string? Value, XmlDiagnostic? Diagnostic);

internal static class DtdValueProcessor
{
    private readonly record struct Frame(string Text, int Offset, string? Name);

    // Literal source newlines are already normalized by the caller. ReplacementText
    // is the entity-declaration decoder's output; its literal whitespace is normalized
    // here, while references parsed here preserve the scalar they produce.
    internal static DtdNormalizedValue NormalizeAttributeDefault(string literal, DtdAttributeType type,
        IReadOnlyDictionary<string, DtdEntityDeclaration> effectiveGeneralEntities, XmlResourceLimits limits,
        XmlLocation location = default, IDtdBudget? sharedBudget = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(literal);
        ArgumentNullException.ThrowIfNull(effectiveGeneralEntities);
        var budget = new ValueBudget(limits, sharedBudget);
        cancellationToken.ThrowIfCancellationRequested();
        var output = new StringBuilder();
        var frames = new List<Frame> { new(literal, 0, null) };
        var active = new HashSet<string>(StringComparer.Ordinal);
        var pendingSpace = false;
        long scalars = 0;
        XmlDiagnostic? diagnostic = null;
        try
        {
            while (frames.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var frame = frames[^1];
                if (frame.Offset == frame.Text.Length)
                {
                    frames.RemoveAt(frames.Count - 1);
                    if (frame.Name is not null)
                    {
                        active.Remove(frame.Name);
                        budget.Exit();
                    }
                    continue;
                }
                var position = frame.Offset;
                var c = ReadScalar(frame.Text, ref position);
                if (c < 0)
                {
                    return Failure("Illegal XML scalar.");
                }

                if (frame.Name is not null && !Charge(1))
                {
                    return new(null, diagnostic);
                }

                if (c != '&')
                {
                    if (c == '<')
                    {
                        return Failure("Literal less-than is forbidden in attribute values.");
                    }

                    if (!Append(c is 9 or 10 or 13 ? 32 : c))
                    {
                        return new(null, diagnostic);
                    }

                    frames[^1] = frame with
                    {
                        Offset = position
                    };
                    continue;
                }
                if (position == frame.Text.Length)
                {
                    return Failure("Incomplete reference.");
                }

                if (frame.Text[position] == '#')
                {
                    if (!TakeAscii(frame, ref position))
                    {
                        return new(null, diagnostic);
                    }

                    int radix = 10, value = 0;
                    var digits = false;
                    if (position < frame.Text.Length && frame.Text[position] == 'x')
                    {
                        radix = 16;
                        if (!TakeAscii(frame, ref position))
                        {
                            return new(null, diagnostic);
                        }
                    }
                    while (position < frame.Text.Length && frame.Text[position] != ';')
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var digit = ValueSyntax.Digit(frame.Text[position], radix);
                        if (digit < 0 || value > (0x10FFFF - digit) / radix)
                        {
                            return Failure("Invalid or overflowing character reference.");
                        }

                        value = value * radix + digit;
                        digits = true;
                        if (!TakeAscii(frame, ref position))
                        {
                            return new(null, diagnostic);
                        }
                    }
                    if (position == frame.Text.Length || !digits || !ValueSyntax.IsXmlScalar(value))
                    {
                        return Failure("Illegal character reference.");
                    }

                    if (!TakeAscii(frame, ref position) || !ExpandScalar(value))
                    {
                        return new(null, diagnostic);
                    }

                    frames[^1] = frame with
                    {
                        Offset = position
                    };
                    continue;
                }
                var nameStart = position;
                var first = true;
                while (position < frame.Text.Length && frame.Text[position] != ';')
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var nameScalar = ReadScalar(frame.Text, ref position);
                    if (first ? !ValueSyntax.IsNameStart(nameScalar) : !ValueSyntax.IsNameChar(nameScalar))
                    {
                        return Failure("Invalid entity name.");
                    }

                    first = false;
                    if (frame.Name is not null && !Charge(1))
                    {
                        return new(null, diagnostic);
                    }
                }
                if (first || position == frame.Text.Length)
                {
                    return Failure("Incomplete general reference.");
                }

                var name = frame.Text.AsSpan(nameStart, position - nameStart);
                if (!TakeAscii(frame, ref position))
                {
                    return new(null, diagnostic);
                }

                frames[^1] = frame with
                {
                    Offset = position
                };
                var predefined = name switch
                {
                    "lt" => '<',
                    "gt" => '>',
                    "amp" => '&',
                    "apos" => '\'',
                    "quot" => '"',
                    _ => -1
                };
                if (predefined >= 0)
                {
                    if (!ExpandScalar(predefined))
                    {
                        return new(null, diagnostic);
                    }

                    continue;
                }
                // Borrowed ordinal probe: do not allocate a temporary lookup string.
                DtdEntityDeclaration? entity = null;
                foreach (var entry in effectiveGeneralEntities)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (name.SequenceEqual(entry.Key))
                    {
                        entity = entry.Value;
                        break;
                    }
                }
                if (entity is null)
                {
                    return Failure("Undeclared attribute entity.");
                }

                if (entity.IsParameter || entity.SystemId is not null || entity.NotationName is not null || entity.ReplacementText is null)
                {
                    return Failure("External or unparsed entity is forbidden in attributes.");
                }

                if (active.Contains(entity.Name))
                {
                    return Failure("Recursive attribute entity.");
                }

                diagnostic = budget.Enter(location);
                if (diagnostic is not null)
                {
                    return new(null, diagnostic);
                }
                // Push before any operation which can throw; finally owns every entered frame.
                try
                {
                    frames.Add(new(entity.ReplacementText, 0, entity.Name));
                }
                catch { try { budget.Exit(); } catch { } throw; }
                active.Add(entity.Name);
            }
            return new(output.ToString(), null);
        }
        catch
        {
            Drain(suppressExceptions: true);
            throw;
        }
        finally { Drain(suppressExceptions: false); }

        DtdNormalizedValue Failure(string message) => new(null, ValueSyntax.Error(message, location));
        bool Charge(long count)
        {
            diagnostic = budget.Charge(count, location);
            return diagnostic is null;
        }
        bool TakeAscii(Frame frame, ref int position)
        {
            if (frame.Name is not null && !Charge(1))
            {
                return false;
            }

            position++;
            return true;
        }
        bool ExpandScalar(int c)
        {
            diagnostic = budget.Enter(location);
            if (diagnostic is not null)
            {
                return false;
            }

            bool success;
            try
            {
                success = Charge(1) && Append(c);
            }
            catch { try { budget.Exit(); } catch { } throw; }
            budget.Exit();
            return success;
        }
        void Drain(bool suppressExceptions)
        {
            ExceptionDispatchInfo? first = null;
            while (frames.Count > 0)
            {
                var frame = frames[^1];
                frames.RemoveAt(frames.Count - 1);
                if (frame.Name is null)
                {
                    continue;
                }

                active.Remove(frame.Name);
                try
                {
                    budget.Exit();
                }
                catch (Exception exception)
                {
                    if (!suppressExceptions && first is null)
                    {
                        first = ExceptionDispatchInfo.Capture(exception);
                    }
                }
            }
            first?.Throw();
        }
        bool Append(int c)
        {
            if (type != DtdAttributeType.CData && c == 32)
            {
                pendingSpace = output.Length != 0;
                return true;
            }
            if (pendingSpace)
            {
                if (!AppendScalar(32))
                {
                    return false;
                }

                pendingSpace = false;
            }
            return AppendScalar(c);
        }
        bool AppendScalar(int c)
        {
            diagnostic = budget.CheckToken(scalars, location);
            if (diagnostic is not null)
            {
                return false;
            }

            var units = c > 0xFFFF ? 2 : 1;
            if (!CanAppend(output.Length, units))
            {
                diagnostic = ValueBudget.Limit(location);
                return false;
            }
            Span<char> pair = stackalloc char[2];
            var length = ValueSyntax.WriteScalar(c, pair);
            output.Append(pair[..length]);
            scalars++;
            return true;
        }
    }

    internal static bool CanAppend(int existingUnits, int additionalUnits) => existingUnits >= 0 && additionalUnits >= 0 && additionalUnits <= int.MaxValue - existingUnits;
    private static int ReadScalar(string text, ref int position)
    {
        int c = text[position++];
        if (char.IsHighSurrogate((char)c))
        {
            if (position == text.Length || !char.IsLowSurrogate(text[position]))
            {
                return -1;
            }

            c = char.ConvertToUtf32((char)c, text[position++]);
        }
        return ValueSyntax.IsXmlScalar(c) ? c : -1;
    }
}
