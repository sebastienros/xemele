namespace Xemele.Validation;

internal sealed class XsdIdentityFactory : IXsdIdentityFactory
{
    public IXsdIdentitySession Create(XsdCompiledSchema schema, XsdIdentityProgram program, XmlDocument document)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(document);
        if (program is not IdentityProgram prepared || !ReferenceEquals(prepared.Schema, schema))
        {
            throw new ArgumentException("Identity program belongs to a different schema.", nameof(program));
        }

        return new XsdIdentitySession(prepared, document);
    }
}

internal sealed class XsdIdentitySession(IdentityProgram program, XmlDocument document) : IXsdIdentitySession
{
    private readonly long revision = document.ContentRevision;
    private readonly List<Frame> stack = [];
    private bool complete, started;
    private ISchemaOperation? operation;
    private sealed class Frame(XmlNode node, XsdElementDeclaration? declaration)
    {
        internal readonly XmlNode Node = node;
        internal readonly XsdElementDeclaration? Declaration = declaration;
        internal readonly List<Run> Runs = [];
        internal Dictionary<int, List<Entry>>? Tables;
        internal HashSet<SchemaName>? Attributes;
        internal XmlNode NextChild;
    }
    private sealed class Run(IdentityConstraint constraint)
    {
        internal readonly IdentityConstraint Constraint = constraint;
        internal readonly List<Row> Pending = [];
        internal readonly List<Entry> Tuples = [];
        internal bool Valid = true;
    }
    private sealed class Row(XmlNode node, Cell[] cells)
    {
        internal readonly XmlNode Node = node;
        internal readonly Cell[] Cells = cells;
        internal bool Valid = true;
    }
    private struct Cell
    {
        internal int Count;
        internal XmlDatatypeValue? Value;
    }
    private sealed class Entry(XmlNode node, XmlDatatypeValue[] values, bool inherited = false)
    {
        internal readonly XmlNode Node = node;
        internal readonly XmlDatatypeValue[] Values = values;
        internal bool Inherited = inherited, Conflict;
    }
    private XmlLocation At(XmlNode node) => document.OriginalLocation(node);
    private void Check(ISchemaOperation op)
    {
        ArgumentNullException.ThrowIfNull(op);
        IdentityWork.Tick(op, default, 0);
        if (complete)
        {
            throw new InvalidOperationException("Identity session is complete.");
        }

        if (document.ContentRevision != revision)
        {
            throw new InvalidOperationException("Identity source changed.");
        }

        if (operation is not null && !ReferenceEquals(operation, op))
        {
            throw new ArgumentException("Identity session requires one shared operation.", nameof(op));
        }

        operation = op;
    }
    private Frame Current(XmlNode node)
    {
        if (node.IsDefault || !ReferenceEquals(node.Document, document) || stack.Count == 0 || stack[^1].Node != node)
        {
            throw new ArgumentException("Identity hooks must follow document order.", nameof(node));
        }

        return stack[^1];
    }
    public void EnterElement(XmlNode node, XsdElementDeclaration? declaration, ISchemaOperation op)
    {
        Check(op);
        if (node.IsDefault || !ReferenceEquals(node.Document, document) || node.Kind != XmlNodeKind.Element ||
            (stack.Count == 0 ? started || node != document.RootElement : node != stack[^1].NextChild))
        {
            throw new ArgumentException("Identity hooks must follow document order.", nameof(node));
        }

        var at = At(node);
        IdentityWork.Retain(op, at, 2);
        if (op.Limits.Depth is int maximum && stack.Count >= maximum)
        {
            op.Refuse("XSD.IdentityLimit", "Identity depth limit exceeded.", at);
            throw SchemaSnapshots.Failure(op);
        }
        if (declaration is not null && ((uint)declaration.Id >= (uint)program.Schema.Elements.Count || !ReferenceEquals(program.Schema.Elements[declaration.Id], declaration)))
        {
            throw new ArgumentException("Declaration belongs to a different schema.", nameof(declaration));
        }

        var frame = new Frame(node, declaration) { NextChild = NextElement(node.FirstChild, op, at) };
        if (stack.Count != 0)
        {
            stack[^1].NextChild = NextElement(node.NextSibling, op, at);
        }

        stack.Add(frame);
        started = true;
        if (declaration is not null)
        {
            foreach (var id in declaration.IdentityConstraints)
            {
                IdentityWork.Retain(op, at, 3);
                var run = new Run(program.Constraint(id));
                if (run.Constraint.Selector.SelectsNonElements && NonElementDescendant(node, op, at) is { IsDefault: false } unsupported)
                {
                    run.Valid = false;
                    IdentityWork.Error(op, At(unsupported), "Identity selector selects a non-element node.");
                }
                frame.Runs.Add(run);
            }
        }
        foreach (var scope in stack)
        {
            IdentityWork.Tick(op, at);
            foreach (var run in scope.Runs)
            {
                IdentityWork.Tick(op, at);
                if (!run.Constraint.Selector.Matches(scope.Node, node, null, op, at))
                {
                    continue;
                }

                IdentityWork.Retain(op, at, checked(run.Constraint.FieldCount + 2));
                var row = new Row(node, new Cell[run.Constraint.FieldCount]);
                for (var i = 0; i < row.Cells.Length; i++)
                {
                    IdentityWork.Tick(op, at);
                    if (run.Constraint.Field(i).SelectsNonElements && NonElementDescendant(node, op, at) is { IsDefault: false } unsupported)
                    {
                        Bad(run, row, op, At(unsupported), "Identity field selects a node without a simple type.");
                    }
                }
                run.Pending.Add(row);
            }
        }
    }
    private static XmlNode NextElement(XmlNode cursor, ISchemaOperation op, XmlLocation at)
    {
        while (!cursor.IsDefault)
        {
            IdentityWork.Tick(op, at);
            if (cursor.Kind == XmlNodeKind.Element)
            {
                return cursor;
            }

            cursor = cursor.NextSibling;
        }
        return default;
    }
    private static XmlNode NonElementDescendant(XmlNode context, ISchemaOperation op, XmlLocation at)
    {
        var cursor = context.FirstChild;
        while (!cursor.IsDefault)
        {
            IdentityWork.Tick(op, at);
            if (cursor.Kind != XmlNodeKind.Element)
            {
                return cursor;
            }

            if (!cursor.FirstChild.IsDefault)
            {
                cursor = cursor.FirstChild;
                continue;
            }
            while (cursor.NextSibling.IsDefault)
            {
                IdentityWork.Tick(op, at);
                cursor = cursor.Parent;
                if (cursor == context)
                {
                    return default;
                }
            }
            cursor = cursor.NextSibling;
        }
        return default;
    }
    public void Attribute(XmlNode owner, SchemaName name, XsdValidatedValue value, ISchemaOperation op)
    {
        ArgumentNullException.ThrowIfNull(value);
        Check(op);
        var frame = Current(owner);
        var at = At(owner);
        IdentityWork.Retain(op, at);
        frame.Attributes ??= [];
        if (!frame.Attributes.Add(name))
        {
            throw new ArgumentException("Effective attribute delivered more than once.", nameof(name));
        }

        Capture(owner, name, value, false, op, at);
    }
    private void Capture(XmlNode node, SchemaName? attribute, XsdValidatedValue value, bool nillable, ISchemaOperation op, XmlLocation at)
    {
        foreach (var frame in stack)
        {
            IdentityWork.Tick(op, at);
            foreach (var run in frame.Runs)
            {
                IdentityWork.Tick(op, at);
                foreach (var row in run.Pending)
                {
                    IdentityWork.Tick(op, at);
                    for (var i = 0; i < row.Cells.Length; i++)
                    {
                        IdentityWork.Tick(op, at);
                        if (!run.Constraint.Field(i).Matches(row.Node, node, attribute, op, at))
                        {
                            continue;
                        }

                        ref var cell = ref row.Cells[i];
                        if (cell.Count != 0)
                        {
                            Bad(run, row, op, at, "Identity field selects multiple nodes.");
                            continue;
                        }
                        cell.Count = 1;
                        if (!value.IsValid)
                        {
                            Bad(run, row, op, at, "Identity field has an invalid value.");
                            continue;
                        }
                        if (attribute is null && nillable && run.Constraint.Definition.Kind == XsdIdentityKind.Key)
                        {
                            Bad(run, row, op, at, "Key element field is declared nillable.");
                            continue;
                        }
                        if (value.Value is null)
                        {
                            IdentityWork.Tick(op, at);
                            // Nil prevents tuple qualification but does not waive
                            // the selected field's effective simple-type requirement.
                            var nil = attribute is null ? node.GetAttribute("nil", "http://www.w3.org/2001/XMLSchema-instance") : null;
                            var lexical = nil.AsSpan().Trim(" \t\r\n");
                            if (!(nillable && (lexical.SequenceEqual("true") || lexical.SequenceEqual("1")) && SimpleFieldType(value.Type, op, at)))
                            {
                                Bad(run, row, op, at, "Identity field requires a simple typed value.");
                            }

                            continue;
                        }
                        cell.Value = value.Value;
                    }
                }
            }
        }
    }
    private bool SimpleFieldType(SchemaName name, ISchemaOperation op, XmlLocation at)
    {
        IdentityWork.Tick(op, at);
        if (program.Schema.Datatypes.FindType(name) is not null)
        {
            return true;
        }

        foreach (var type in program.Schema.Types)
        {
            IdentityWork.Tick(op, at);
            if (type.Name == name)
            {
                return type.ContentKind == XsdContentKind.Simple && type.SimpleType is not null;
            }
        }
        return false;
    }
    private static void Bad(Run run, Row row, ISchemaOperation op, XmlLocation at, string message)
    {
        run.Valid = row.Valid = false;
        IdentityWork.Error(op, at, message);
    }
    public void LeaveElement(XmlNode node, XsdValidatedValue value, ISchemaOperation op)
    {
        ArgumentNullException.ThrowIfNull(value);
        Check(op);
        var frame = Current(node);
        var at = At(node);
        if (!frame.NextChild.IsDefault)
        {
            throw new ArgumentException("Identity hooks skipped element children.", nameof(node));
        }

        Capture(node, null, value, frame.Declaration?.Nillable == true, op, at);
        foreach (var scope in stack)
        {
            IdentityWork.Tick(op, at);
            foreach (var run in scope.Runs)
            {
                IdentityWork.Tick(op, at);
                for (var i = run.Pending.Count - 1; i >= 0; i--)
                {
                    IdentityWork.Tick(op, at);
                    var row = run.Pending[i];
                    if (row.Node != node)
                    {
                        continue;
                    }

                    Finish(run, row, op, at);
                    run.Pending.RemoveAt(i);
                }
            }
        }
        foreach (var run in frame.Runs)
        {
            IdentityWork.Tick(op, at);
            if (!run.Valid || run.Constraint.Definition.Kind == XsdIdentityKind.KeyRef)
            {
                continue;
            }

            var table = Table(frame, run.Constraint.Definition.Id, op, at);
            foreach (var entry in run.Tuples)
            {
                Insert(table, entry, false, op, at);
            }
        }
        foreach (var run in frame.Runs)
        {
            IdentityWork.Tick(op, at);
            if (run.Constraint.Definition.Kind != XsdIdentityKind.KeyRef)
            {
                continue;
            }

            foreach (var entry in run.Tuples)
            {
                IdentityWork.Tick(op, at);
                var found = false;
                if (frame.Tables is not null && frame.Tables.TryGetValue(run.Constraint.Target, out var table))
                {
                    foreach (var candidate in table)
                    {
                        IdentityWork.Tick(op, at);
                        if (!candidate.Conflict && Equal(entry, candidate, op, at))
                        {
                            found = true;
                            break;
                        }
                    }
                }

                if (!found)
                {
                    IdentityWork.Error(op, At(entry.Node), "Key reference has no matching key in its subtree.");
                }
            }
        }
        if (stack.Count > 1 && frame.Tables is not null)
        {
            foreach (var pair in frame.Tables)
            {
                IdentityWork.Tick(op, at);
                var target = Table(stack[^2], pair.Key, op, at);
                foreach (var entry in pair.Value)
                {
                    IdentityWork.Tick(op, at);
                    if (!entry.Conflict)
                    {
                        Insert(target, entry, true, op, at);
                    }
                }
            }
        }

        stack.RemoveAt(stack.Count - 1);
        IdentityWork.Tick(op, at, 0);
    }
    private void Finish(Run run, Row row, ISchemaOperation op, XmlLocation at)
    {
        var full = true;
        foreach (var cell in row.Cells)
        {
            IdentityWork.Tick(op, at);
            if (cell.Count == 0 || cell.Value is null)
            {
                full = false;
            }
        }
        if (!row.Valid)
        {
            return;
        }

        if (!full)
        {
            if (run.Constraint.Definition.Kind == XsdIdentityKind.Key)
            {
                Bad(run, row, op, at, "Key field is missing or nil.");
            }

            return;
        }
        IdentityWork.Retain(op, at, checked(2 + row.Cells.Length));
        var values = new XmlDatatypeValue[row.Cells.Length];
        for (var i = 0; i < values.Length; i++)
        {
            IdentityWork.Tick(op, at);
            values[i] = row.Cells[i].Value!;
        }
        var entry = new Entry(row.Node, values);
        if (run.Constraint.Definition.Kind != XsdIdentityKind.KeyRef)
        {
            foreach (var prior in run.Tuples)
            {
                IdentityWork.Tick(op, at);
                if (Equal(prior, entry, op, at))
                {
                    Bad(run, row, op, at, "Duplicate identity key tuple.");
                    return;
                }
            }
        }

        IdentityWork.Retain(op, at);
        run.Tuples.Add(entry);
    }
    private static List<Entry> Table(Frame frame, int id, ISchemaOperation op, XmlLocation at)
    {
        IdentityWork.Tick(op, at);
        if (frame.Tables is not null && frame.Tables.TryGetValue(id, out var existing))
        {
            return existing;
        }

        IdentityWork.Retain(op, at, 2);
        frame.Tables ??= [];
        var table = new List<Entry>();
        frame.Tables.Add(id, table);
        return table;
    }
    private void Insert(List<Entry> table, Entry entry, bool inherited, ISchemaOperation op, XmlLocation at)
    {
        for (var index = 0; index < table.Count; index++)
        {
            var prior = table[index];
            IdentityWork.Tick(op, at);
            if (!Equal(prior, entry, op, at))
            {
                continue;
            }

            if (prior.Node == entry.Node)
            {
                if (!inherited)
                {
                    table[index] = entry;
                }

                return;
            }
            if (inherited)
            {
                if (prior.Inherited)
                {
                    prior.Conflict = true;
                }

                return;
            }
            if (prior.Inherited)
            {
                IdentityWork.Retain(op, at);
                table[index] = entry;
                return;
            }
            return;
        }
        IdentityWork.Retain(op, at, inherited ? 2 : 1);
        table.Add(inherited ? new(entry.Node, entry.Values, true) : entry);
    }
    private bool Equal(Entry left, Entry right, ISchemaOperation op, XmlLocation at)
    {
        IdentityWork.Tick(op, at);
        for (var i = 0; i < left.Values.Length; i++)
        {
            IdentityWork.Tick(op, at);
            // Compiler registries contain definitions only; actual typed values
            // arrive through the verified facet engine. The library belongs to
            // that selected value, rather than to a user-defined restriction name.
            if (!DatatypeValues.ValueEquals(left.Values[i], right.Values[i], op))
            {
                return false;
            }
        }
        return true;
    }
    public void Complete(ISchemaOperation op)
    {
        Check(op);
        if (!started || stack.Count != 0)
        {
            throw new InvalidOperationException("Identity element scopes are incomplete.");
        }

        complete = true;
        operation = null;
    }
}
