namespace Xemele.Validation;

internal sealed class XsdIdentityCompiler : IXsdIdentityCompiler
{
    public XsdIdentityProgram Compile(XsdCompiledSchema schema, ISchemaOperation operation)
    {
        ArgumentNullException.ThrowIfNull(schema); ArgumentNullException.ThrowIfNull(operation);
        IdentityWork.Retain(operation, default, 4);
        var constraints = new Dictionary<int, IdentityConstraint>();
        var names = new Dictionary<SchemaName, XsdIdentityConstraint>();
        var ids = new HashSet<int>();
        foreach (var definition in schema.Identities)
        {
            IdentityWork.Retain(operation, definition.Location, 4);
            IdentityWork.Tick(operation, definition.Location, 1L + definition.Name.NamespaceUri.Length + definition.Name.LocalName.Length);
            if (definition.Id < 0 || !ids.Add(definition.Id) || names.ContainsKey(definition.Name) ||
                !Enum.IsDefined(definition.Kind) || definition.Fields.Count == 0)
                IdentityWork.Invalid(operation, definition.Location, "Invalid identity constraint definition.");
            names.Add(definition.Name, definition);
        }
        foreach (var definition in schema.Identities)
        {
            IdentityWork.Tick(operation, definition.Location);
            int targetId = -1;
            if (definition.Kind == XsdIdentityKind.KeyRef)
            {
                if (definition.Refer is { } reference)
                    IdentityWork.Tick(operation, definition.Location, 1L + reference.NamespaceUri.Length + reference.LocalName.Length);
                if (definition.Refer is not { } refer || !names.TryGetValue(refer, out var target) ||
                    target.Kind == XsdIdentityKind.KeyRef || target.Fields.Count != definition.Fields.Count)
                    IdentityWork.Invalid(operation, definition.Location, "Invalid keyref target or field count.");
                targetId = names[definition.Refer!.Value].Id;
            }
            else if (definition.Refer is not null)
                IdentityWork.Invalid(operation, definition.Location, "Only keyref can have a reference.");
            var selector = IdentityXPath.Compile(definition.Selector, false, definition.Context, operation, definition.Location);
            IdentityWork.Retain(operation, definition.Location, definition.Fields.Count);
            var fields = new IdentityXPath[definition.Fields.Count];
            for (int i = 0; i < fields.Length; i++)
                fields[i] = IdentityXPath.Compile(definition.Fields[i], true, definition.Context, operation, definition.Location);
            constraints.Add(definition.Id, new(definition, selector, fields, targetId));
        }
        foreach (var element in schema.Elements)
            foreach (int id in element.IdentityConstraints)
            {
                IdentityWork.Tick(operation, element.Location);
                if (!constraints.ContainsKey(id)) IdentityWork.Invalid(operation, element.Location, "Unknown identity constraint ID.");
            }
        IdentityWork.Tick(operation, default, 0);
        return new IdentityProgram(schema, constraints);
    }
}

internal sealed class IdentityConstraint(XsdIdentityConstraint definition, IdentityXPath selector, IdentityXPath[] fields, int target)
{
    internal XsdIdentityConstraint Definition { get; } = definition;
    internal IdentityXPath Selector { get; } = selector;
    internal int FieldCount => fields.Length;
    internal IdentityXPath Field(int index) => fields[index];
    internal int Target { get; } = target;
}
internal sealed class IdentityProgram(XsdCompiledSchema schema, Dictionary<int, IdentityConstraint> constraints) : XsdIdentityProgram
{
    internal XsdCompiledSchema Schema { get; } = schema;
    internal IdentityConstraint Constraint(int id) => constraints[id];
}
