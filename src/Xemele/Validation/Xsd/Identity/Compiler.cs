namespace Xemele.Validation;

internal sealed class XsdIdentityCompiler : IXsdIdentityCompiler
{
    public XsdIdentityProgram Compile(XsdCompiledSchema schema, ISchemaOperation operation)
    {
        ArgumentNullException.ThrowIfNull(schema); ArgumentNullException.ThrowIfNull(operation);
        IdentityWork.Retain(operation, default);
        var constraints = new Dictionary<int, IdentityConstraint>();
        var names = new Dictionary<SchemaName, IdentityConstraint>();
        foreach (var definition in schema.Identities)
        {
            IdentityWork.Retain(operation, definition.Location, 3);
            IdentityWork.Tick(operation, definition.Location, 1L + definition.Name.NamespaceUri.Length + definition.Name.LocalName.Length);
            if (definition.Id < 0 || constraints.ContainsKey(definition.Id) || names.ContainsKey(definition.Name) ||
                !Enum.IsDefined(definition.Kind) || definition.Fields.Count == 0)
                IdentityWork.Invalid(operation, definition.Location, "Invalid identity constraint definition.");
            var selector = IdentityXPath.Compile(definition.Selector, false, definition.Context, operation, definition.Location);
            IdentityWork.Retain(operation, definition.Location, definition.Fields.Count);
            var fields = new IdentityXPath[definition.Fields.Count];
            for (int i = 0; i < fields.Length; i++)
                fields[i] = IdentityXPath.Compile(definition.Fields[i], true, definition.Context, operation, definition.Location);
            var compiled = new IdentityConstraint(definition, selector, fields);
            constraints.Add(definition.Id, compiled); names.Add(definition.Name, compiled);
        }
        foreach (var constraint in constraints.Values)
        {
            var definition = constraint.Definition; IdentityWork.Tick(operation, definition.Location);
            if (definition.Kind == XsdIdentityKind.KeyRef)
            {
                if (definition.Refer is { } reference)
                    IdentityWork.Tick(operation, definition.Location, 1L + reference.NamespaceUri.Length + reference.LocalName.Length);
                if (definition.Refer is not { } refer || !names.TryGetValue(refer, out var target) ||
                    target.Definition.Kind == XsdIdentityKind.KeyRef || target.Fields.Length != constraint.Fields.Length)
                    IdentityWork.Invalid(operation, definition.Location, "Invalid keyref target or field count.");
                constraint.Target = names[definition.Refer!.Value].Definition.Id;
            }
            else if (definition.Refer is not null)
                IdentityWork.Invalid(operation, definition.Location, "Only keyref can have a reference.");
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

internal sealed class IdentityConstraint(XsdIdentityConstraint definition, IdentityXPath selector, IdentityXPath[] fields)
{
    internal XsdIdentityConstraint Definition { get; } = definition;
    internal IdentityXPath Selector { get; } = selector;
    internal IdentityXPath[] Fields { get; } = fields;
    // Assigned only during compilation, before the program escapes.
    internal int Target { get; set; } = -1;
}
internal sealed class IdentityProgram(XsdCompiledSchema schema, Dictionary<int, IdentityConstraint> constraints) : XsdIdentityProgram
{
    internal XsdCompiledSchema Schema { get; } = schema;
    internal Dictionary<int, IdentityConstraint> Constraints { get; } = constraints;
}
