namespace Xemele.Validation;

internal static class RngRestrictions
{
    [Flags]
    private enum Context
    {
        None = 0, Start = 1, Attribute = 2, List = 4, Except = 8, Repeat = 16, RepeatGroup = 32
    }
    internal static void Check(int start, IReadOnlyList<RngPattern> patterns, ISchemaOperation op)
    {
        void Work(XmlLocation at)
        {
            if (!op.Charge(1, at))
            {
                throw SchemaSnapshots.Failure(op);
            }
        }
        void Reject(RngPattern p, string message)
        {
            op.AddDiagnostic(new("RNG.SchemaRestriction", XmlDiagnosticSeverity.Error, message, p.Location));
            throw SchemaSnapshots.Failure(op);
        }
        var reached = new HashSet<int>();
        var queue = new Queue<int>();
        queue.Enqueue(start);
        while (queue.Count != 0)
        {
            var id = queue.Dequeue();
            if (!reached.Add(id))
            {
                continue;
            }

            var p = patterns[id];
            Work(p.Location);
            foreach (var child in p.Children)
            {
                queue.Enqueue(child);
            }
        }
        var checks = new Stack<(int Id, Context Context)>();
        checks.Push((start, Context.Start));
        foreach (var id in reached)
        {
            if (patterns[id].Kind == RngPatternKind.Element)
            {
                checks.Push((patterns[id].Children[0], Context.None));
            }
        }

        var seen = new HashSet<(int, Context)>();
        while (checks.Count != 0)
        {
            var (id, context) = checks.Pop();
            if (!seen.Add((id, context)))
            {
                continue;
            }

            var p = patterns[id];
            Work(p.Location);
            var kind = p.Kind;
            if (context.HasFlag(Context.Start) && kind is not (RngPatternKind.Choice or RngPatternKind.Ref or RngPatternKind.NotAllowed))
            {
                Reject(p, "Illegal start content.");
            }

            if (context.HasFlag(Context.Attribute) && kind is RngPatternKind.Ref or RngPatternKind.Attribute)
            {
                Reject(p, "Attributes cannot contain attributes or elements.");
            }

            if (context.HasFlag(Context.List) && kind is RngPatternKind.List or RngPatternKind.Ref or RngPatternKind.Attribute or RngPatternKind.Text or RngPatternKind.Interleave)
            {
                Reject(p, "Illegal list content.");
            }

            if (context.HasFlag(Context.Except) && kind is not (RngPatternKind.Data or RngPatternKind.Value or RngPatternKind.Choice))
            {
                Reject(p, "Illegal data except content.");
            }

            if (kind == RngPatternKind.Ref)
            {
                if (patterns[p.Children[0]].Kind != RngPatternKind.Element)
                {
                    Reject(p, "Normalized ref does not target an element.");
                }

                continue; // Referenced element is a new restriction/content scope.
            }
            if (kind == RngPatternKind.Attribute)
            {
                if (context.HasFlag(Context.RepeatGroup))
                {
                    Reject(p, "Repeated group/interleave contains an attribute.");
                }

                if (!context.HasFlag(Context.Repeat) && Infinite(p.NameClass!))
                {
                    Reject(p, "Infinite attribute name class must be repeated.");
                }

                context |= Context.Attribute;
            }
            if (kind == RngPatternKind.OneOrMore)
            {
                context |= Context.Repeat;
            }

            if (context.HasFlag(Context.Repeat) && kind is RngPatternKind.Group or RngPatternKind.Interleave)
            {
                context |= Context.RepeatGroup;
            }

            if (kind == RngPatternKind.List)
            {
                context |= Context.List;
            }

            if (kind == RngPatternKind.Data)
            {
                context |= Context.Except;
            }

            foreach (var child in p.Children)
            {
                checks.Push((child, context));
            }
        }
        var types = new Dictionary<int, int>();
        foreach (var id in reached)
        {
            var p = patterns[id];
            Work(p.Location);
            if (p.Kind == RngPatternKind.Element)
            {
                Content(p.Children[0]);
            }

            if (p.Kind is not (RngPatternKind.Group or RngPatternKind.Interleave))
            {
                continue;
            }

            var left = Occurs(p.Children[0]);
            var right = Occurs(p.Children[1]);
            foreach (var a in left)
            {
                foreach (var b in right)
                {
                    var x = patterns[a];
                    var y = patterns[b];
                    Work(p.Location);
                    if (x.Kind == RngPatternKind.Attribute && y.Kind == RngPatternKind.Attribute &&
                        RngNameClasses.Overlaps(x.NameClass!, y.NameClass!, op, p.Location))
                    {
                        Reject(p, "Overlapping attribute name classes.");
                    }

                    if (p.Kind != RngPatternKind.Interleave)
                    {
                        continue;
                    }

                    if (x.Kind == RngPatternKind.Text && y.Kind == RngPatternKind.Text)
                    {
                        Reject(p, "Text occurs on both sides of interleave.");
                    }

                    if (x.Kind == RngPatternKind.Ref && y.Kind == RngPatternKind.Ref &&
                        RngNameClasses.Overlaps(patterns[x.Children[0]].NameClass!, patterns[y.Children[0]].NameClass!, op, p.Location))
                    {
                        Reject(p, "Overlapping interleaved element name classes.");
                    }
                }
            }
        }
        List<int> Occurs(int root)
        {
            var result = new List<int>();
            var stack = new Stack<int>();
            stack.Push(root);
            while (stack.Count != 0)
            {
                var id = stack.Pop();
                var p = patterns[id];
                Work(p.Location);
                if (p.Kind is RngPatternKind.Choice or RngPatternKind.Group or RngPatternKind.Interleave or RngPatternKind.OneOrMore)
                {
                    foreach (var child in p.Children)
                    {
                        stack.Push(child);
                    }
                }
                else
                {
                    result.Add(id);
                }
            }
            return result;
        }
        // The graph is acyclic between element/ref boundaries. List content has
        // its independent string-sequence semantics and is excluded from this pass.
        int Content(int root)
        {
            var stack = new Stack<(int Id, bool Exit)>();
            stack.Push((root, false));
            while (stack.Count != 0)
            {
                var (id, exit) = stack.Pop();
                if (types.ContainsKey(id))
                {
                    continue;
                }

                var p = patterns[id];
                Work(p.Location);
                if (!exit)
                {
                    var leaf = p.Kind switch
                    {
                        RngPatternKind.Empty or RngPatternKind.NotAllowed => 0,
                        RngPatternKind.Text or RngPatternKind.Ref => 1,
                        RngPatternKind.Data or RngPatternKind.Value or RngPatternKind.List => 2,
                        _ => -1
                    };
                    if (leaf >= 0)
                    {
                        types.Add(id, leaf);
                        continue;
                    }
                    stack.Push((id, true));
                    foreach (var child in p.Children)
                    {
                        stack.Push((child, false));
                    }
                }
                else
                {
                    int a = types[p.Children[0]], b = p.Children.Count == 2 ? types[p.Children[1]] : a;
                    if (p.Kind is RngPatternKind.Group or RngPatternKind.Interleave or RngPatternKind.OneOrMore && a != 0 && b != 0 && (a == 2 || b == 2))
                    {
                        Reject(p, "Content contains a prohibited string sequence.");
                    }

                    types.Add(id, p.Kind == RngPatternKind.Attribute ? 0 : Math.Max(a, b));
                }
            }
            return types[root];
        }
        bool Infinite(RngNameClass name, int depth = 0)
        {
            Work(patterns[start].Location);
            if (depth == 1024)
            {
                op.Refuse("SCHEMA.Limit", "Name-class representation depth exceeded.", patterns[start].Location);
                throw SchemaSnapshots.Failure(op);
            }
            return name switch
            {
                RngAnyName or RngNsName => true,
                RngNameChoice c => Infinite(c.Left, depth + 1) || Infinite(c.Right, depth + 1),
                _ => false
            };
        }
    }
}
