using System.Numerics;
namespace Xemele.Validation;

internal sealed class XsdContentMatcher : IXsdContentMatcher
{
    public IXsdContentCursor CreateCursor(XsdContentProgram program)
    {
        ArgumentNullException.ThrowIfNull(program);
        if (program is not CompiledContent content) throw new ArgumentException("Foreign content program.", nameof(program));
        return new ContentCursor(content);
    }
}

// Counters allocate only after exhausting UInt64 actual iterations. A large
// schema bound itself neither allocates a matching-sized counter nor expands
// the machine. The overflow path remains arbitrary precision and charged.
internal struct ContentCounter
{
    private ulong small;
    private BigInteger large;
    private bool expanded;
    internal bool AtLeast(BigInteger bound, ISchemaOperation op, XmlLocation at)
    {
        if (!expanded)
        { if (bound.GetBitLength() > 64) return false; return small >= (ulong)bound; }
        ContentWork.Integer(op, at, large); ContentWork.Integer(op, at, bound); return large >= bound;
    }
    internal void Increment(ISchemaOperation op, XmlLocation at)
    {
        ContentWork.Tick(op, at);
        if (!expanded && small != ulong.MaxValue) { small++; return; }
        if (!expanded) { large = new BigInteger(small); expanded = true; }
        ContentWork.Integer(op, at, large); large += BigInteger.One;
    }
    internal void Set(BigInteger value) { expanded = value.GetBitLength() > 64; if (expanded) large = value; else small = (ulong)value; }
    internal int Compare(ContentCounter other, ISchemaOperation op, XmlLocation at)
    {
        ContentWork.Tick(op, at);
        if (!expanded && !other.expanded) return small.CompareTo(other.small);
        ContentWork.Integer(op, at, large); ContentWork.Integer(op, at, other.large);
        return (expanded ? large : new BigInteger(small)).CompareTo(other.expanded ? other.large : new BigInteger(other.small));
    }
}

internal sealed class ContentCursor(CompiledContent program) : IXsdContentCursor
{
    private const int Reset = 0, Gate = 1, Start = 2, Body = 3, End = 4;
    private sealed class State(int size)
    {
        internal int Pc;
        internal bool Dead;
        internal readonly ContentCounter[] Counts = new ContentCounter[size];
        internal readonly byte[] Flags = new byte[size]; // 0 inactive, 1 active/empty, 2 active/consumed
        internal void Copy(State other, ISchemaOperation op, XmlLocation at)
        {
            Pc = other.Pc; Dead = false;
            for (int i = 0; i < Counts.Length; i++) { ContentWork.Tick(op, at, 2); Counts[i] = other.Counts[i]; Flags[i] = other.Flags[i]; }
        }
    }
    private State[] current = [], next = [];
    private State? scratch;
    private int currentCount, nextCount;
    private int[] continuation = [];
    private bool initialized, failed;
    private int AcceptPc => checked(program.Nodes.Length * 5);
    private void Initialize(ISchemaOperation op)
    {
        if (initialized) return;
        int size = program.Nodes.Length;
        ContentWork.Retain(op, program.Location, checked(size * 3 + 1));
        continuation = new int[size]; scratch = new(size);
        for (int i = 0; i < size; i++)
        {
            var node = program.Nodes[i]; ContentWork.Tick(op, node.Particle.Location); continuation[i] = AcceptPc;
            if (node.Particle.Term is not XsdGroupTerm group || group.Compositor == XsdCompositor.All) continue;
            for (int j = 0; j < node.Children.Length; j++)
            {
                ContentWork.Tick(op, node.Particle.Location);
                continuation[node.Children[j].Id] = group.Compositor == XsdCompositor.Sequence && j + 1 < node.Children.Length
                    ? node.Children[j + 1].Id * 5 + Reset : node.Id * 5 + End;
            }
        }
        scratch.Pc = program.Root is { } root ? root.Id * 5 : AcceptPc;
        Add(ref current, ref currentCount, scratch, op);
        Close(current, ref currentCount, op);
        initialized = true;
    }
    public bool Accept(SchemaName childName, ISchemaOperation operation, out int? declarationId, out XsdProcessContents processContents)
    {
        ArgumentNullException.ThrowIfNull(operation); ContentWork.Tick(operation, program.Location);
        declarationId = null; processContents = XsdProcessContents.Strict;
        if (failed) return false;
        try
        {
            Initialize(operation); nextCount = 0; int matched = -1;
            for (int i = 0; i < currentCount; i++)
            {
                var state = current[i]; if (state.Dead || state.Pc == AcceptPc || state.Pc % 5 != Body) continue;
                var node = program.Nodes[state.Pc / 5]; ContentWork.Tick(operation, node.Particle.Location);
                if (node.Particle.Term is XsdGroupTerm { Compositor: XsdCompositor.All })
                {
                    foreach (var child in node.Children)
                    {
                        if (child.Particle.MaxOccurs == BigInteger.Zero || state.Counts[child.Id].AtLeast(BigInteger.One, operation, child.Particle.Location)) continue;
                        if (!child.Matches(childName, operation, out var declaration, out var policy)) continue;
                        Match(child, declaration, policy);
                        scratch!.Copy(state, operation, node.Particle.Location); scratch.Counts[child.Id].Set(BigInteger.One); MarkConsumed(scratch, operation);
                        Add(ref next, ref nextCount, scratch, operation);
                    }
                }
                else if (node.Particle.Term is not XsdGroupTerm && node.Matches(childName, operation, out var declaration, out var policy))
                {
                    Match(node, declaration, policy);
                    scratch!.Copy(state, operation, node.Particle.Location); scratch.Pc = node.Id * 5 + End; MarkConsumed(scratch, operation);
                    Add(ref next, ref nextCount, scratch, operation);
                }
            }
            // Capture outputs locally to avoid ref/out closure capture or delegates.
            void Match(ContentNode node, int? declaration, XsdProcessContents policy)
            {
                if (matched >= 0 && matched != node.Id) throw new InvalidOperationException("Compiled content program has an ambiguous frontier.");
                matched = node.Id; matchDeclaration = declaration; matchPolicy = policy;
            }
            if (matched < 0) { failed = true; return false; }
            Close(next, ref nextCount, operation);
            (current, next) = (next, current); currentCount = nextCount;
            declarationId = matchDeclaration; processContents = matchPolicy; return true;
        }
        catch { failed = true; throw; }
    }
    private int? matchDeclaration;
    private XsdProcessContents matchPolicy;
    public bool Complete(ISchemaOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operation); ContentWork.Tick(operation, program.Location);
        if (failed) return false;
        try
        {
            Initialize(operation);
            for (int i = 0; i < currentCount; i++)
            { ContentWork.Tick(operation, program.Location); if (!current[i].Dead && current[i].Pc == AcceptPc) return true; }
            return false;
        }
        catch { failed = true; throw; }
    }
    private void MarkConsumed(State state, ISchemaOperation op)
    {
        for (int i = 0; i < state.Flags.Length; i++)
        { ContentWork.Tick(op, program.Location); if (state.Flags[i] != 0) state.Flags[i] = 2; }
    }
    private void Close(State[] initialBank, ref int count, ISchemaOperation op)
    {
        // Add may grow the bank; update the owning field and reload after each edge.
        bool isCurrent = ReferenceEquals(initialBank, current);
        for (int i = 0; i < count; i++)
        {
            var bank = isCurrent ? current : next; var state = bank[i];
            if (state.Dead || state.Pc == AcceptPc) continue;
            var node = program.Nodes[state.Pc / 5]; int stage = state.Pc % 5; var p = node.Particle;
            ContentWork.Tick(op, p.Location);
            if (stage == Reset)
            {
                scratch!.Copy(state, op, p.Location); scratch.Counts[node.Id] = default; scratch.Flags[node.Id] = 0;
                if (p.Term is XsdGroupTerm { Compositor: XsdCompositor.All }) foreach (var child in node.Children)
                { ContentWork.Tick(op, p.Location); scratch.Counts[child.Id] = default; scratch.Flags[child.Id] = 0; }
                scratch.Pc = node.Id * 5 + Gate; Edge(isCurrent, ref count, op);
            }
            else if (stage == Gate)
            {
                if (node.BodyNullable || state.Counts[node.Id].AtLeast(p.MinOccurs, op, p.Location))
                {
                    scratch!.Copy(state, op, p.Location); scratch.Counts[node.Id] = default; scratch.Flags[node.Id] = 0;
                    if (p.Term is XsdGroupTerm { Compositor: XsdCompositor.All }) foreach (var child in node.Children)
                    { ContentWork.Tick(op, p.Location); scratch.Counts[child.Id] = default; }
                    scratch.Pc = continuation[node.Id]; Edge(isCurrent, ref count, op);
                }
                if (p.MaxOccurs is null || !state.Counts[node.Id].AtLeast(p.MaxOccurs.Value, op, p.Location))
                { scratch!.Copy(state, op, p.Location); scratch.Pc = node.Id * 5 + Start; Edge(isCurrent, ref count, op); }
            }
            else if (stage == Start)
            { scratch!.Copy(state, op, p.Location); scratch.Flags[node.Id] = 1; scratch.Pc = node.Id * 5 + Body; Edge(isCurrent, ref count, op); }
            else if (stage == End)
            {
                // Empty body iterations need no traversal: nullable terms can pad
                // their minimum with empty iterations. Count only consumed bodies.
                if (state.Flags[node.Id] != 2) continue;
                scratch!.Copy(state, op, p.Location); scratch.Flags[node.Id] = 0;
                scratch.Counts[node.Id].Increment(op, p.Location);
                if (p.MaxOccurs is null && (node.BodyNullable || scratch.Counts[node.Id].AtLeast(p.MinOccurs, op, p.Location)))
                    scratch.Counts[node.Id].Set(node.BodyNullable ? BigInteger.Zero : p.MinOccurs);
                scratch.Pc = node.Id * 5 + Gate; Edge(isCurrent, ref count, op);
            }
            else if (p.Term is XsdGroupTerm group)
            {
                if (group.Compositor == XsdCompositor.All)
                {
                    bool complete = true;
                    foreach (var child in node.Children)
                    { ContentWork.Tick(op, p.Location); if (!child.Particle.MinOccurs.IsZero && !state.Counts[child.Id].AtLeast(BigInteger.One, op, p.Location)) complete = false; }
                    if (complete) { scratch!.Copy(state, op, p.Location); scratch.Pc = node.Id * 5 + End; Edge(isCurrent, ref count, op); }
                }
                else if (group.Compositor == XsdCompositor.Sequence)
                { scratch!.Copy(state, op, p.Location); scratch.Pc = node.Children.Length == 0 ? node.Id * 5 + End : node.Children[0].Id * 5; Edge(isCurrent, ref count, op); }
                else foreach (var child in node.Children)
                { ContentWork.Tick(op, p.Location); scratch!.Copy(state, op, p.Location); scratch.Pc = child.Id * 5; Edge(isCurrent, ref count, op); }
            }
        }
    }
    private void Edge(bool isCurrent, ref int count, ISchemaOperation op)
    {
        ContentWork.Tick(op, program.Location);
        if (isCurrent) Add(ref current, ref count, scratch!, op); else Add(ref next, ref count, scratch!, op);
    }
    private void Add(ref State[] bank, ref int count, State candidate, ISchemaOperation op)
    {
        for (int i = 0; i < count; i++)
        {
            var existing = bank[i]; ContentWork.Tick(op, program.Location);
            if (existing.Dead || existing.Pc != candidate.Pc) continue;
            bool existingDominates = true, candidateDominates = true;
            for (int j = 0; j < candidate.Counts.Length; j++)
            {
                ContentWork.Tick(op, program.Location);
                if (existing.Flags[j] != candidate.Flags[j]) { existingDominates = candidateDominates = false; break; }
                int comparison = existing.Counts[j].Compare(candidate.Counts[j], op, program.Location);
                if (program.Nodes[j].BodyNullable) { existingDominates &= comparison <= 0; candidateDominates &= comparison >= 0; }
                else { existingDominates &= comparison == 0; candidateDominates &= comparison == 0; }
            }
            if (existingDominates) return;
            if (candidateDominates) existing.Dead = true;
        }
        if (count == bank.Length)
        {
            int oldLength = bank.Length, newLength = oldLength == 0 ? 8 : checked(oldLength * 2);
            ContentWork.Retain(op, program.Location, checked(newLength + (newLength - oldLength) * (program.Nodes.Length * 2 + 1)));
            Array.Resize(ref bank, newLength);
            for (int i = oldLength; i < newLength; i++) { ContentWork.Tick(op, program.Location); bank[i] = new(program.Nodes.Length); }
        }
        ContentWork.Tick(op, program.Location);
        bank[count++].Copy(candidate, op, program.Location);
    }
}
