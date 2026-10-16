using System.Numerics;
namespace Xemele.Validation;

internal sealed class XsdContentCompiler : IXsdContentCompiler
{
    public XsdContentProgram Compile(XsdCompiledSchema schema, XsdTypeDefinition type, ISchemaOperation operation)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(operation);
        return new ContentCompilation(schema, operation).Compile(type);
    }

    // Complete composition opts in explicitly. The frozen three-argument lane
    // remains structural and needs only the declaration-model interfaces.
    internal XsdContentProgram Compile(XsdCompiledSchema schema, XsdTypeDefinition type,
        ISchemaOperation operation, IXmlFacetEvaluator facets)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(facets);
        return new ContentCompilation(schema, operation, facets).Compile(type);
    }

    // The facade consumes every program. Share one invocation-local traversal,
    // retaining both existing overloads' whole-graph/requested-type contract.
    internal Dictionary<SchemaName, XsdContentProgram> CompileAll(XsdCompiledSchema schema,
        ISchemaOperation operation, IXmlFacetEvaluator facets) => CompileAll(schema, operation, facets, null);
    internal Dictionary<SchemaName, XsdContentProgram> CompileAll(XsdCompiledSchema schema,
        ISchemaOperation operation, IXmlFacetEvaluator facets, AvailabilityBuilder? availability)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(facets);
        return new ContentCompilation(schema, operation, facets, availability).CompileAll();
    }
}

internal sealed partial class ContentCompilation
{
    private const string Xs = "http://www.w3.org/2001/XMLSchema";
    private static readonly SchemaName Any = new(Xs, "anyType");
    private readonly XsdCompiledSchema schema;
    private readonly ISchemaOperation op;
    private readonly IXmlFacetEvaluator? facets;
    private readonly AvailabilityBuilder? availability;
    private ComponentIdentity consumer;
    private readonly Dictionary<SchemaName, XsdTypeDefinition> types = [];
    private readonly Dictionary<int, (SchemaName, int)[]> substitutions = [];
    internal ContentCompilation(XsdCompiledSchema schema, ISchemaOperation op, IXmlFacetEvaluator? facets = null)
        : this(schema, op, facets, null) { }
    internal ContentCompilation(XsdCompiledSchema schema, ISchemaOperation op, IXmlFacetEvaluator? facets, AvailabilityBuilder? availability)
    {
        this.schema = schema;
        this.op = op;
        this.facets = facets;
        this.availability = availability;
        if (availability is null)
        {
            ComponentAvailability.RequireComplete(schema, op);
        }

        foreach (var type in schema.Types)
        {
            ContentWork.Retain(op, type.Location);
            types.Add(type.Name, type);
        }
    }
    internal XsdContentProgram Compile(XsdTypeDefinition requested)
    {
        ContentWork.Tick(op, requested.Location);
        if (!types.TryGetValue(requested.Name, out var actual) || !ReferenceEquals(requested, actual))
        {
            throw new ArgumentException("Type must belong to the supplied compiled schema.", nameof(requested));
        }

        CompiledContent? result = null;
        // Check every type, including unused anonymous old/new redefine carriers.
        // No operation-dependent success is cached across compile operations.
        foreach (var type in schema.Types)
        {
            ContentWork.Tick(op, type.Location);
            if (type.AnyAttribute is { } wildcard)
            {
                ContentNamespaces.Validate(wildcard, op, type.Location);
            }

            consumer = ComponentAvailability.Type(type.Name);
            var program = Build(type);
            CheckDerivation(type);
            if (ReferenceEquals(type, requested))
            {
                result = program;
            }
        }
        return result!;
    }
    internal Dictionary<SchemaName, XsdContentProgram> CompileAll()
    {
        ContentWork.Retain(op, default);
        var programs = new Dictionary<SchemaName, XsdContentProgram>();
        foreach (var type in schema.Types)
        {
            ContentWork.Tick(op, type.Location);
            if (type.AnyAttribute is { } wildcard)
            {
                ContentNamespaces.Validate(wildcard, op, type.Location);
            }

            consumer = ComponentAvailability.Type(type.Name);
            var program = Build(type);
            CheckDerivation(type);
            // The known projection is useful for independently decidable checks,
            // but it is never published as an executable partial program. The
            // concrete envelope retains its specific unavailable operands.
            if (availability?.Unavailable(consumer) == true)
            {
                continue;
            }

            ContentWork.Retain(op, type.Location);
            programs.Add(type.Name, program);
        }
        ContentWork.Tick(op, default, 0);
        return programs;
    }
    private sealed class Frame(XsdParticle particle, ContentNode[] children)
    {
        internal readonly XsdParticle Particle = particle;
        internal readonly ContentNode[] Children = children;
        internal int Next;
    }
    private CompiledContent Build(XsdTypeDefinition type)
    {
        // Named-group carriers finalize declarations and redefine relations; UPA
        // applies when their particles enter a complex type's actual content.
        var checkUpa = !type.Anonymous || !type.Name.LocalName.StartsWith("#model-group:", StringComparison.Ordinal);
        var nodes = new List<ContentNode>();
        ContentWork.Retain(op, type.Location);
        ContentNode? root = null;
        if (type.Particle is { } particle)
        {
            var stack = new Stack<Frame>();
            Push(particle);
            void Push(XsdParticle p)
            {
                ContentWork.Integer(op, p.Location, p.MinOccurs);
                if (p.MaxOccurs is { } max)
                {
                    ContentWork.Integer(op, p.Location, max);
                }

                if (p.MinOccurs.Sign < 0 || p.MaxOccurs is { Sign: < 0 } || p.MaxOccurs is { } limit && ContentIntegers.Compare(p.MinOccurs, limit, op, p.Location) > 0)
                {
                    ContentWork.Error(op, p.Location, "Invalid particle occurrence range.");
                }

                var count = p.MaxOccurs != BigInteger.Zero && p.Term is XsdGroupTerm g ? g.Particles.Count : 0;
                ContentWork.Retain(op, p.Location, checked(count + 1));
                stack.Push(new(p, new ContentNode[count]));
            }
            while (stack.Count != 0)
            {
                var frame = stack.Peek();
                ContentWork.Tick(op, frame.Particle.Location);
                if (frame.Next < frame.Children.Length)
                {
                    Push(((XsdGroupTerm)frame.Particle.Term).Particles[frame.Next]);
                    continue;
                }
                var node = Finish(frame.Particle, frame.Children, nodes, checkUpa);
                stack.Pop();
                if (stack.TryPeek(out var parent))
                {
                    parent.Children[parent.Next++] = node;
                }
                else
                {
                    root = node;
                }
            }
        }
        CheckConsistency(root);
        ContentWork.Retain(op, type.Location, checked(nodes.Count + 1));
        return new(root, [.. nodes], type.Location);
    }
    private ContentNode Finish(XsdParticle p, ContentNode[] children, List<ContentNode> nodes, bool checkUpa)
    {
        var id = nodes.Count;
        var bodyNullable = false;
        var first = new HashSet<int>();
        var bodyTail = new HashSet<int>();
        (SchemaName, int)[] names = [];
        void Add(HashSet<int> set, int value)
        {
            ContentWork.Tick(op, p.Location);
            if (!set.Contains(value))
            {
                ContentWork.Retain(op, p.Location);
                set.Add(value);
            }
        }
        void Union(HashSet<int> set, ReadOnlySpan<int> values)
        {
            foreach (var value in values)
            {
                Add(set, value);
            }
        }
        if (p.Term is XsdGroupTerm group)
        {
            bodyNullable = group.Compositor != XsdCompositor.Choice;
            foreach (var child in children)
            {
                ContentWork.Tick(op, p.Location);
                if (group.Compositor == XsdCompositor.Choice)
                {
                    bodyNullable |= child.Nullable;
                }
                else
                {
                    bodyNullable &= child.Nullable;
                }
            }
            var prefixNullable = true;
            foreach (var child in children)
            {
                if (group.Compositor != XsdCompositor.Sequence || prefixNullable)
                {
                    Union(first, child.First);
                }

                prefixNullable &= child.Nullable;
            }
            if (group.Compositor == XsdCompositor.All)
            {
                if (p.MaxOccurs != BigInteger.One || p.MinOccurs > BigInteger.One)
                {
                    ContentWork.Error(op, p.Location, "All group occurrence must be zero/one to one.");
                }

                foreach (var child in children)
                {
                    if (child.Particle.Term is not XsdElementTerm || child.Particle.MaxOccurs is null || child.Particle.MaxOccurs > BigInteger.One || child.Particle.MinOccurs > BigInteger.One)
                    {
                        ContentWork.Error(op, child.Particle.Location, "All members must be zero/one to one element particles.");
                    }

                    Union(bodyTail, child.Tail);
                }
            }
            else if (group.Compositor == XsdCompositor.Choice)
            {
                foreach (var child in children)
                {
                    Union(bodyTail, child.Tail);
                }
            }
            else
            {
                var suffixNullable = true;
                for (var i = children.Length - 1; i >= 0; i--)
                {
                    if (suffixNullable)
                    {
                        Union(bodyTail, children[i].Tail);
                    }

                    suffixNullable &= children[i].Nullable;
                }
            }
            // Initial choice/all frontiers, or optional paths through sequence.
            for (var i = 0; checkUpa && i < children.Length; i++)
            {
                var betweenNullable = true;
                for (var j = i + 1; j < children.Length; j++)
                {
                    ContentWork.Tick(op, p.Location);
                    if (group.Compositor != XsdCompositor.Sequence)
                    {
                        Disjoint(children[i].First, children[j].First, nodes, p.Location);
                    }
                    else if (betweenNullable)
                    {
                        Disjoint(children[i].Tail, children[j].First, nodes, p.Location);
                    }

                    betweenNullable &= children[j].Nullable;
                }
            }
            if (bodyNullable)
            {
                Union(bodyTail, ToArray(first, p.Location));
            }

            if (checkUpa && (p.MaxOccurs is null || p.MaxOccurs > BigInteger.One))
            {
                Disjoint(ToArray(bodyTail, p.Location), ToArray(first, p.Location), nodes, p.Location);
            }
        }
        else
        {
            if (p.Term is XsdElementTerm element)
            {
                names = Members(element.DeclarationId);
            }
            else if (p.Term is XsdWildcardTerm wildcard)
            {
                ContentNamespaces.Validate(wildcard.Wildcard, op, p.Location);
            }
            else
            {
                throw new ArgumentException("Unknown content term.");
            }

            if (p.MaxOccurs != BigInteger.Zero)
            {
                Add(first, id);
            }
        }
        var tail = bodyTail;
        if (p.MinOccurs.IsZero || bodyNullable || p.MaxOccurs is null || ContentIntegers.Compare(p.MaxOccurs.Value, p.MinOccurs, op, p.Location) > 0)
        {
            foreach (var value in first)
            {
                Add(tail, value);
            }
        }

        if (p.MaxOccurs == BigInteger.Zero)
        {
            first.Clear();
            tail.Clear();
        }
        ContentWork.Retain(op, p.Location);
        var result = new ContentNode(p, children, names, id, bodyNullable, ToArray(first, p.Location), ToArray(tail, p.Location));
        nodes.Add(result);
        return result;
    }
    private int[] ToArray(HashSet<int> set, XmlLocation at)
    {
        ContentWork.Retain(op, at, set.Count);
        var result = new int[set.Count];
        set.CopyTo(result);
        return result;
    }
    private (SchemaName, int)[] Members(int id)
    {
        if (availability is null && substitutions.TryGetValue(id, out var cached))
        {
            return cached;
        }

        var head = schema.Elements[id];
        var result = new List<(SchemaName, int)>();
        var global = schema.GlobalElements.TryGetValue(head.Name, out var globalId) && globalId == id;
        if (global && !head.Block.Contains(XsdElementBlock.Substitution))
        {
            // Use one declaration order for every implicit substitution choice.
            // Giving each head priority creates incompatible orders for nested
            // substitution sets. Authored group particle lists are untouched.
            foreach (var candidate in schema.GlobalElements.Values)
            {
                ContentWork.Tick(op, head.Location);
                var element = schema.Elements[candidate];
                if (element.Abstract)
                {
                    continue;
                }

                if (candidate == id)
                {
                    ContentWork.Retain(op, head.Location);
                    result.Add((head.Name, id));
                    continue;
                }
                var current = candidate;
                var affiliated = false;
                for (var steps = 0; steps < schema.Elements.Count; steps++)
                {
                    ContentWork.Tick(op, element.Location);
                    if (schema.Elements[current].SubstitutionGroup is not { } parent)
                    {
                        break;
                    }

                    if (!schema.GlobalElements.TryGetValue(parent, out var next))
                    {
                        availability?.Link(consumer, ComponentAvailability.Element(schema.Elements[current]),
                            operandUse: DependencyUse.SubstitutionSelection);
                        break;
                    }
                    current = next;
                    if (current == id)
                    {
                        affiliated = true;
                        break;
                    }
                }
                if (!affiliated)
                {
                    continue;
                }

                if (element.Type is not { } candidateType || head.Type is not { } headType || !types.ContainsKey(candidateType) || !types.ContainsKey(headType))
                {
                    if (availability is null)
                    {
                        throw new ArgumentException("Complete substitution graph required.", nameof(schema));
                    }

                    availability.Link(consumer, ComponentAvailability.Element(element));
                    availability.Link(consumer, ComponentAvailability.Element(head));
                    continue;
                }
                if (!SubstitutionType(candidateType, head))
                {
                    continue;
                }

                ContentWork.Retain(op, element.Location);
                result.Add((element.Name, candidate));
            }
        }
        else if (!head.Abstract)
        {
            ContentWork.Retain(op, head.Location);
            result.Add((head.Name, id));
        }
        ContentWork.Retain(op, head.Location, checked(result.Count + 1));
        var ready = result.ToArray();
        if (availability is null)
        {
            substitutions.Add(id, ready);
        }

        return ready;
    }
    private bool SubstitutionType(SchemaName candidate, XsdElementDeclaration head)
    {
        return XsdSubstitutionGroups.TypeRelation(types, null, schema.Datatypes, candidate, head.Type!.Value, op, head.Location,
            true, head.Block.Contains(XsdElementBlock.Restriction), head.Block.Contains(XsdElementBlock.Extension), out _, out _);
    }
    private void Disjoint(ReadOnlySpan<int> left, ReadOnlySpan<int> right, List<ContentNode> nodes, XmlLocation at)
    {
        foreach (var a in left)
        {
            foreach (var b in right)
            {
                ContentWork.Tick(op, at);
                if (a == b)
                {
                    continue;
                }

                var x = nodes[a];
                var y = nodes[b];
                var overlap = false;
                if (x.Particle.Term is XsdWildcardTerm wx && y.Particle.Term is XsdWildcardTerm wy)
                {
                    overlap = ContentNamespaces.Overlaps(wx.Wildcard, wy.Wildcard, op, at);
                }
                else if (x.Particle.Term is XsdWildcardTerm wildcard)
                {
                    foreach (var (Name, Id) in y.Names)
                    {
                        overlap |= ContentNamespaces.Allows(wildcard.Wildcard, Name.NamespaceUri, op, at);
                    }
                }
                else if (y.Particle.Term is XsdWildcardTerm wildcard2)
                {
                    foreach (var (Name, Id) in x.Names)
                    {
                        overlap |= ContentNamespaces.Allows(wildcard2.Wildcard, Name.NamespaceUri, op, at);
                    }
                }
                else
                {
                    foreach (var (Name, Id) in x.Names)
                    {
                        foreach (var m in y.Names)
                        {
                            ContentWork.Tick(op, at);
                            overlap |= Name == m.Name;
                        }
                    }
                }

                if (overlap)
                {
                    ContentWork.Error(op, at, "Content model violates unique particle attribution.");
                }
            }
        }
    }
    private void CheckConsistency(ContentNode? root)
    {
        if (root is null)
        {
            ContentWork.Tick(op, default, 0);
            return;
        }
        ContentWork.Retain(op, default, 4);
        var known = new Dictionary<SchemaName, SchemaName>();
        var operands = new Dictionary<SchemaName, List<XsdElementDeclaration>>();
        var visited = new HashSet<int>();
        var pending = new Stack<ContentNode>();
        ContentWork.Retain(op, root.Particle.Location);
        pending.Push(root);
        while (pending.TryPop(out var node))
        {
            ContentWork.Tick(op, node.Particle.Location);
            if (node.Particle.MaxOccurs == BigInteger.Zero)
            {
                continue;
            }

            foreach (var child in node.Children)
            {
                ContentWork.Retain(op, child.Particle.Location);
                pending.Push(child);
            }
            if (node.Particle.Term is not XsdElementTerm element)
            {
                continue;
            }

            void Check(XsdElementDeclaration declaration)
            {
                ContentWork.Tick(op, node.Particle.Location);
                if (visited.Contains(declaration.Id))
                {
                    return;
                }

                ContentWork.Retain(op, node.Particle.Location);
                visited.Add(declaration.Id);
                // An unavailable first operand must never become the representative
                // for later independently unequal known declaration types.
                if (declaration.Type is { } name)
                {
                    if (known.TryGetValue(declaration.Name, out var previous) && name != previous)
                    {
                        ContentWork.Error(op, node.Particle.Location, "Element declarations with one name must have identical types.");
                    }

                    if (!known.ContainsKey(declaration.Name))
                    {
                        ContentWork.Retain(op, node.Particle.Location);
                        known.Add(declaration.Name, name);
                    }
                }
                if (!operands.TryGetValue(declaration.Name, out var sameName))
                {
                    ContentWork.Retain(op, node.Particle.Location, 2);
                    operands.Add(declaration.Name, sameName = []);
                }
                foreach (var other in sameName)
                {
                    ContentWork.Tick(op, node.Particle.Location);
                    if (other.Type is not null && declaration.Type is not null)
                    {
                        continue;
                    }

                    if (availability is null)
                    {
                        throw new ArgumentException("Complete declaration types required.", nameof(schema));
                    }

                    availability.Link(consumer, ComponentAvailability.Element(declaration));
                    availability.Link(consumer, ComponentAvailability.Element(other));
                }
                ContentWork.Retain(op, node.Particle.Location);
                sameName.Add(declaration);
            }
            Check(schema.Elements[element.DeclarationId]);
            foreach (var member in Members(element.DeclarationId))
            {
                Check(schema.Elements[member.Item2]);
            }
        }
    }
}
