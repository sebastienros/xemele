using System.Numerics;
namespace Xemele.Validation;

internal sealed class XsdContentCompiler : IXsdContentCompiler
{
    public XsdContentProgram Compile(XsdCompiledSchema schema, XsdTypeDefinition type, ISchemaOperation operation)
    {
        ArgumentNullException.ThrowIfNull(schema); ArgumentNullException.ThrowIfNull(type); ArgumentNullException.ThrowIfNull(operation);
        return new ContentCompilation(schema, operation).Compile(type);
    }
}

internal sealed partial class ContentCompilation
{
    private const string Xs = "http://www.w3.org/2001/XMLSchema";
    private static readonly SchemaName Any = new(Xs, "anyType");
    private readonly XsdCompiledSchema schema;
    private readonly ISchemaOperation op;
    private readonly Dictionary<SchemaName, XsdTypeDefinition> types = new();
    private readonly Dictionary<int, (SchemaName, int)[]> substitutions = new();
    internal ContentCompilation(XsdCompiledSchema schema, ISchemaOperation op)
    {
        this.schema = schema; this.op = op;
        foreach (var type in schema.Types)
        { ContentWork.Retain(op, type.Location); types.Add(type.Name, type); }
    }
    internal XsdContentProgram Compile(XsdTypeDefinition requested)
    {
        ContentWork.Tick(op, requested.Location);
        if (!types.TryGetValue(requested.Name, out var actual) || !ReferenceEquals(requested, actual))
            throw new ArgumentException("Type must belong to the supplied compiled schema.", nameof(requested));
        CompiledContent? result = null;
        // Check every type, including unused anonymous old/new redefine carriers.
        // No operation-dependent success is cached across compile operations.
        foreach (var type in schema.Types)
        {
            ContentWork.Tick(op, type.Location);
            if (type.AnyAttribute is { } wildcard) ContentNamespaces.Validate(wildcard, op, type.Location);
            var program = Build(type);
            CheckDerivation(type);
            if (ReferenceEquals(type, requested)) result = program;
        }
        return result!;
    }
    private sealed class Frame(XsdParticle particle, ContentNode[] children)
    {
        internal readonly XsdParticle Particle = particle;
        internal readonly ContentNode[] Children = children;
        internal int Next;
    }
    private CompiledContent Build(XsdTypeDefinition type)
    {
        var nodes = new List<ContentNode>(); ContentWork.Retain(op, type.Location);
        ContentNode? root = null;
        if (type.Particle is { } particle)
        {
            var stack = new Stack<Frame>(); Push(particle);
            void Push(XsdParticle p)
            {
                ContentWork.Integer(op, p.Location, p.MinOccurs);
                if (p.MaxOccurs is { } max) ContentWork.Integer(op, p.Location, max);
                if (p.MinOccurs.Sign < 0 || p.MaxOccurs is { Sign: < 0 } || p.MaxOccurs is { } limit && ContentIntegers.Compare(p.MinOccurs, limit, op, p.Location) > 0)
                    ContentWork.Error(op, p.Location, "Invalid particle occurrence range.");
                int count = p.MaxOccurs != BigInteger.Zero && p.Term is XsdGroupTerm g ? g.Particles.Count : 0;
                ContentWork.Retain(op, p.Location, checked(count + 1));
                stack.Push(new(p, new ContentNode[count]));
            }
            while (stack.Count != 0)
            {
                var frame = stack.Peek(); ContentWork.Tick(op, frame.Particle.Location);
                if (frame.Next < frame.Children.Length)
                { Push(((XsdGroupTerm)frame.Particle.Term).Particles[frame.Next]); continue; }
                var node = Finish(frame.Particle, frame.Children, nodes);
                stack.Pop();
                if (stack.TryPeek(out var parent)) parent.Children[parent.Next++] = node; else root = node;
            }
        }
        CheckConsistency(root);
        ContentWork.Retain(op, type.Location, checked(nodes.Count + 1));
        return new(root, nodes.ToArray(), type.Location);
    }
    private ContentNode Finish(XsdParticle p, ContentNode[] children, List<ContentNode> nodes)
    {
        int id = nodes.Count; bool bodyNullable = false;
        var first = new HashSet<int>(); var bodyTail = new HashSet<int>();
        (SchemaName, int)[] names = [];
        void Add(HashSet<int> set, int value) { ContentWork.Tick(op, p.Location); if (!set.Contains(value)) { ContentWork.Retain(op, p.Location); set.Add(value); } }
        void Union(HashSet<int> set, ReadOnlySpan<int> values) { foreach (int value in values) Add(set, value); }
        if (p.Term is XsdGroupTerm group)
        {
            bodyNullable = group.Compositor != XsdCompositor.Choice;
            foreach (var child in children)
            {
                ContentWork.Tick(op, p.Location);
                if (group.Compositor == XsdCompositor.Choice) bodyNullable |= child.Nullable; else bodyNullable &= child.Nullable;
            }
            bool prefixNullable = true;
            foreach (var child in children)
            {
                if (group.Compositor != XsdCompositor.Sequence || prefixNullable) Union(first, child.First);
                prefixNullable &= child.Nullable;
            }
            if (group.Compositor == XsdCompositor.All)
            {
                if (p.MaxOccurs != BigInteger.One || p.MinOccurs > BigInteger.One)
                    ContentWork.Error(op, p.Location, "All group occurrence must be zero/one to one.");
                foreach (var child in children)
                {
                    if (child.Particle.Term is not XsdElementTerm || child.Particle.MaxOccurs is null || child.Particle.MaxOccurs > BigInteger.One || child.Particle.MinOccurs > BigInteger.One)
                        ContentWork.Error(op, child.Particle.Location, "All members must be zero/one to one element particles.");
                    Union(bodyTail, child.Tail);
                }
            }
            else if (group.Compositor == XsdCompositor.Choice)
            { foreach (var child in children) Union(bodyTail, child.Tail); }
            else
            {
                bool suffixNullable = true;
                for (int i = children.Length - 1; i >= 0; i--)
                { if (suffixNullable) Union(bodyTail, children[i].Tail); suffixNullable &= children[i].Nullable; }
            }
            // Initial choice/all frontiers, or optional paths through sequence.
            for (int i = 0; i < children.Length; i++)
            {
                bool betweenNullable = true;
                for (int j = i + 1; j < children.Length; j++)
                {
                    ContentWork.Tick(op, p.Location);
                    if (group.Compositor != XsdCompositor.Sequence) Disjoint(children[i].First, children[j].First, nodes, p.Location);
                    else if (betweenNullable) Disjoint(children[i].Tail, children[j].First, nodes, p.Location);
                    betweenNullable &= children[j].Nullable;
                }
            }
            if (bodyNullable) Union(bodyTail, ToArray(first, p.Location));
            if (p.MaxOccurs is null || p.MaxOccurs > BigInteger.One)
                Disjoint(ToArray(bodyTail, p.Location), ToArray(first, p.Location), nodes, p.Location);
        }
        else
        {
            if (p.Term is XsdElementTerm element) names = Members(element.DeclarationId);
            else if (p.Term is XsdWildcardTerm wildcard) ContentNamespaces.Validate(wildcard.Wildcard, op, p.Location);
            else throw new ArgumentException("Unknown content term.");
            if (p.MaxOccurs != BigInteger.Zero) Add(first, id);
        }
        var tail = bodyTail;
        if (p.MinOccurs.IsZero || bodyNullable || p.MaxOccurs is null || ContentIntegers.Compare(p.MaxOccurs.Value, p.MinOccurs, op, p.Location) > 0)
            foreach (int value in first) Add(tail, value);
        if (p.MaxOccurs == BigInteger.Zero) { first.Clear(); tail.Clear(); }
        ContentWork.Retain(op, p.Location);
        var result = new ContentNode(p, children, names, id, bodyNullable, ToArray(first, p.Location), ToArray(tail, p.Location));
        nodes.Add(result); return result;
    }
    private int[] ToArray(HashSet<int> set, XmlLocation at)
    { ContentWork.Retain(op, at, set.Count); var result = new int[set.Count]; set.CopyTo(result); return result; }
    private (SchemaName, int)[] Members(int id)
    {
        if (substitutions.TryGetValue(id, out var ready)) return ready;
        var head = schema.Elements[id]; var result = new List<(SchemaName, int)>();
        if (!head.Abstract) { ContentWork.Retain(op, head.Location); result.Add((head.Name, id)); }
        bool global = schema.GlobalElements.TryGetValue(head.Name, out int globalId) && globalId == id;
        if (global && !head.Block.Contains(XsdElementBlock.Substitution))
        {
            foreach (int candidate in schema.GlobalElements.Values)
            {
                ContentWork.Tick(op, head.Location); var element = schema.Elements[candidate];
                if (candidate == id || element.Abstract) continue;
                int current = candidate; bool affiliated = false;
                for (int steps = 0; steps < schema.Elements.Count; steps++)
                {
                    ContentWork.Tick(op, element.Location);
                    if (schema.Elements[current].SubstitutionGroup is not { } parent || !schema.GlobalElements.TryGetValue(parent, out current)) break;
                    if (current == id) { affiliated = true; break; }
                }
                if (!affiliated || !SubstitutionType(element.Type, head)) continue;
                ContentWork.Retain(op, element.Location); result.Add((element.Name, candidate));
            }
        }
        ContentWork.Retain(op, head.Location, checked(result.Count + 1));
        ready = result.ToArray(); substitutions.Add(id, ready); return ready;
    }
    private bool SubstitutionType(SchemaName candidate, XsdElementDeclaration head)
    {
        bool restriction = false, extension = false; int steps = 0;
        while (candidate != head.Type)
        {
            ContentWork.Tick(op, head.Location);
            if (++steps > types.Count || !types.TryGetValue(candidate, out var type) || type.BaseType is not { } parent) return false;
            restriction |= type.Derivation is XsdDerivation.Restriction or XsdDerivation.List or XsdDerivation.Union;
            extension |= type.Derivation == XsdDerivation.Extension;
            if (!types.TryGetValue(parent, out var basis)) return false;
            if (restriction && basis.Block.Contains(XsdTypeBlock.Restriction) || extension && basis.Block.Contains(XsdTypeBlock.Extension)) return false;
            candidate = parent;
        }
        return !(restriction && head.Block.Contains(XsdElementBlock.Restriction) || extension && head.Block.Contains(XsdElementBlock.Extension));
    }
    private void Disjoint(ReadOnlySpan<int> left, ReadOnlySpan<int> right, List<ContentNode> nodes, XmlLocation at)
    {
        foreach (int a in left) foreach (int b in right)
        {
            ContentWork.Tick(op, at); if (a == b) continue;
            var x = nodes[a]; var y = nodes[b]; bool overlap = false;
            if (x.Particle.Term is XsdWildcardTerm wx && y.Particle.Term is XsdWildcardTerm wy)
                overlap = ContentNamespaces.Overlaps(wx.Wildcard, wy.Wildcard, op, at);
            else if (x.Particle.Term is XsdWildcardTerm wildcard)
            { foreach (var name in y.Names) overlap |= ContentNamespaces.Allows(wildcard.Wildcard, name.Name.NamespaceUri, op, at); }
            else if (y.Particle.Term is XsdWildcardTerm wildcard2)
            { foreach (var name in x.Names) overlap |= ContentNamespaces.Allows(wildcard2.Wildcard, name.Name.NamespaceUri, op, at); }
            else foreach (var n in x.Names) foreach (var m in y.Names)
            { ContentWork.Tick(op, at); overlap |= n.Name == m.Name; }
            if (overlap) ContentWork.Error(op, at, "Content model violates unique particle attribution.");
        }
    }
    private void CheckConsistency(ContentNode? root)
    {
        var declarations = new Dictionary<SchemaName, SchemaName>();
        var pending = new Stack<ContentNode>();
        if (root is not null) { ContentWork.Retain(op, root.Particle.Location); pending.Push(root); }
        while (pending.TryPop(out var node))
        {
            ContentWork.Tick(op, node.Particle.Location);
            if (node.Particle.MaxOccurs == BigInteger.Zero) continue;
            foreach (var child in node.Children) { ContentWork.Retain(op, child.Particle.Location); pending.Push(child); }
            if (node.Particle.Term is not XsdElementTerm element) continue;
            void Check(XsdElementDeclaration declaration)
            {
                ContentWork.Tick(op, node.Particle.Location);
                if (declarations.TryGetValue(declaration.Name, out var type))
                { if (type != declaration.Type) ContentWork.Error(op, node.Particle.Location, "Element declarations with one name must have identical types."); }
                else { ContentWork.Retain(op, node.Particle.Location); declarations.Add(declaration.Name, declaration.Type); }
            }
            Check(schema.Elements[element.DeclarationId]);
            foreach (var member in Members(element.DeclarationId)) Check(schema.Elements[member.Item2]);
        }
    }
}
