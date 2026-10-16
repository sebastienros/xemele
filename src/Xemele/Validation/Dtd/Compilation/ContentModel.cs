using Xemele.Parsing.Dtd;

namespace Xemele.Validation;

internal sealed class DtdContentModel
{
    private readonly string[] names;
    private readonly int[][] next;
    private readonly bool[] accepting;
    internal DtdParticleKind Kind
    {
        get;
    }
    internal int InitialState => 0;
    internal DtdContentModel(DtdParticleKind kind, string[] names, int[][] next, bool[] accepting)
    {
        Kind = kind;
        this.names = names;
        this.next = next;
        this.accepting = accepting;
    }
    internal bool IsAccepting(int state) => state >= 0 && state < accepting.Length && accepting[state];
    // False means mismatch or exhausted operation; inspect operation.IsComplete.
    // A mismatched cursor is sticky (-1). ANY accepts child names; declaration
    // existence and text rules are checked by the tree evaluator.
    internal bool TryAdvance(ref int state, ReadOnlySpan<char> name, ValidationOperation operation)
    {
        if (!operation.ChargeOperations())
        {
            return false;
        }

        if (state < 0 || state >= next.Length)
        {
            return false;
        }

        if (Kind == DtdParticleKind.Any)
        {
            return true;
        }

        foreach (var position in next[state])
        {
            if (DtdLexical.Equal(name, names[position - 1], operation))
            {
                state = Kind == DtdParticleKind.Mixed ? 0 : position;
                return true;
            }
            if (!operation.IsComplete)
            {
                return false;
            }
        }
        state = -1;
        return false;
    }
}
