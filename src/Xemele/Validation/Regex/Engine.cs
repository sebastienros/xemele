using System.Buffers;
using System.Text;

namespace Xemele.Validation;

// Programs own the automaton; every invocation owns its scratch and ledger.
internal sealed class XmlSchemaRegexEngine : IXmlSchemaRegexCompiler, IXmlSchemaRegexMatcher
{
    public XmlSchemaRegexProgram Compile(string pattern, XmlLocation location, ISchemaOperation operation)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(operation);
        if (!operation.Charge(0, location))
        {
            throw SchemaSnapshots.Failure(operation);
        }

        return new RegexParser(pattern, location, operation).Compile();
    }

    public bool IsMatch(XmlSchemaRegexProgram program, ReadOnlySpan<char> input, ISchemaOperation operation)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(operation);
        if (program is not RegexAutomaton automaton)
        {
            throw new ArgumentException("Foreign regex program.", nameof(program));
        }

        var at = automaton.Location;
        if (!operation.Charge(0, at))
        {
            return false;
        }

        var n = automaton.States.Length;
        // Five bounded slices: current/next lists, visited sets, epsilon queue.
        if (n > int.MaxValue / 5)
        {
            operation.Refuse("SCHEMA.Limit", "Regex scratch capacity exceeded.", at);
            return false;
        }
        int[]? rented = null;
        var scratch = n <= 128 ? stackalloc int[640] : (rented = ArrayPool<int>.Shared.Rent(n * 5));
        scratch = scratch[..(n * 5)];
        scratch.Clear();
        var current = scratch[..n];
        var next = scratch.Slice(n, n);
        var seen = scratch.Slice(n * 2, n);
        var nextSeen = scratch.Slice(n * 3, n);
        var queue = scratch.Slice(n * 4, n);
        try
        {
            var count = 0;
            if (!Closure(automaton, automaton.Start, current, ref count, seen, queue, operation))
            {
                return false;
            }

            long scalars = 0;
            while (!input.IsEmpty)
            {
                if (!operation.Charge(1, at))
                {
                    return false;
                }

                if (Rune.DecodeFromUtf16(input, out var scalar, out var consumed) != OperationStatus.Done || !RegexCharacters.IsXml(scalar.Value))
                {
                    return false;
                }

                if (++scalars > (operation.Limits.TokenCharacters ?? int.MaxValue))
                {
                    operation.Refuse("SCHEMA.Limit", "Regex input character limit exceeded.", at);
                    return false;
                }
                input = input[consumed..];
                var nextCount = 0;
                nextSeen.Clear();
                for (var i = 0; i < count; i++)
                {
                    if (!operation.Charge(1, at))
                    {
                        return false;
                    }

                    var state = automaton.States[current[i]];
                    if (state.Set is not null && state.Set.Contains(scalar.Value, operation, at) &&
                        !Closure(automaton, state.First, next, ref nextCount, nextSeen, queue, operation))
                    {
                        return false;
                    }

                    if (!operation.IsComplete)
                    {
                        return false;
                    }
                }
                var swap = current;
                current = next;
                next = swap;
                swap = seen;
                seen = nextSeen;
                nextSeen = swap;
                count = nextCount;
                // Continue scanning a rejected input: validate scalar/length accounting and poll cancellation.
            }
            if (!operation.Charge(0, at))
            {
                return false;
            }

            for (var i = 0; i < count; i++)
            {
                if (!operation.Charge(1, at))
                {
                    return false;
                }

                if (current[i] == 0)
                {
                    return true;
                }
            }
            return false;
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<int>.Shared.Return(rented);
            }
        }
    }

    private static bool Closure(RegexAutomaton program, int start, Span<int> output, ref int count,
        Span<int> seen, Span<int> queue, ISchemaOperation operation)
    {
        int head = 0, tail = 0;
        if (seen[start] == 0)
        {
            seen[start] = 1;
            queue[tail++] = start;
        }
        while (head < tail)
        {
            if (!operation.Charge(1, program.Location))
            {
                return false;
            }

            var index = queue[head++];
            var state = program.States[index];
            if (state.Set is not null || index == 0)
            {
                output[count++] = index;
                continue;
            }
            if (seen[state.First] == 0)
            {
                seen[state.First] = 1;
                queue[tail++] = state.First;
            }
            if (state.Second >= 0 && seen[state.Second] == 0)
            {
                seen[state.Second] = 1;
                queue[tail++] = state.Second;
            }
        }
        return true;
    }
}

internal sealed class RegexAutomaton(RegexState[] states, int start, XmlLocation location) : XmlSchemaRegexProgram
{
    private readonly RegexState[] states = states;
    internal ReadOnlySpan<RegexState> States => states;
    internal readonly int Start = start;
    internal readonly XmlLocation Location = location;
}
internal readonly record struct RegexState(RegexSet? Set, int First, int Second = -1);
internal readonly record struct RegexTerm(int First, int Last, uint Categories = 0, int Name = 0, bool Complement = false)
{
    internal bool Contains(int scalar, ISchemaOperation operation, XmlLocation at)
    {
        var found = Name == 3 ? scalar is 9 or 10 or 13 or 32 : Name == 4 ? scalar == 0xFEFF || scalar is >= 0xFFF0 and <= 0xFFFD : Name != 0 ? RegexCharacters.IsName(scalar, Name == 2, operation, at) : Categories != 0
            ? (Categories & (1u << (int)Rune.GetUnicodeCategory(new Rune(scalar)))) != 0 : scalar >= First && scalar <= Last;
        return found != Complement;
    }
}
internal sealed class RegexSet(RegexTerm[] terms, bool negative = false, RegexSet? subtraction = null)
{
    internal bool Contains(int scalar, ISchemaOperation operation, XmlLocation at)
    {
        var found = false;
        foreach (var term in terms)
        {
            if (!operation.Charge(1, at))
            {
                return false;
            }

            if (term.Contains(scalar, operation, at))
            {
                found = true;
                break;
            }
        }
        if (found == negative)
        {
            return false;
        }

        return subtraction is null || !subtraction.Contains(scalar, operation, at);
    }
}
