namespace Xemele.Validation;

internal enum RngPatternKind { NotAllowed, Empty, Text, Choice, Interleave, Group, OneOrMore, List, Data, Value, Attribute, Element, Ref }
internal abstract record RngNameClass;
internal sealed record RngName(SchemaName Name) : RngNameClass;
internal sealed record RngAnyName(RngNameClass? Except) : RngNameClass;
internal sealed record RngNsName(string NamespaceUri, RngNameClass? Except) : RngNameClass;
internal sealed record RngNameChoice(RngNameClass Left, RngNameClass Right) : RngNameClass;
internal sealed record RngPattern(int Id, RngPatternKind Kind, IReadOnlyList<int> Children, RngNameClass? NameClass,
    SchemaName? Datatype, IReadOnlyList<XmlFacet> Parameters, string? Value,
    SchemaNamespaceContext Context, XmlLocation Location)
{
    private IReadOnlyList<int> children = SchemaSnapshots.List(Children);
    private IReadOnlyList<XmlFacet> parameters = SchemaSnapshots.List(Parameters);
    public IReadOnlyList<int> Children { get => children; init => children = SchemaSnapshots.List(value); }
    public IReadOnlyList<XmlFacet> Parameters { get => parameters; init => parameters = SchemaSnapshots.List(value); }
    private readonly bool valid = RngGraph.ValidatePattern(Id, Kind, Children, NameClass, Datatype, Parameters, Value, Context);
}
internal sealed record RngCompiledSchema(int Start, IReadOnlyList<RngPattern> Patterns, IXmlDatatypeRegistry Datatypes)
{
    private IReadOnlyList<RngPattern> patterns = RngGraph.Snapshot(Start, Patterns, Datatypes);
    public IReadOnlyList<RngPattern> Patterns { get => patterns; init => patterns = RngGraph.Snapshot(Start, value, Datatypes); }
}
internal interface IRelaxNgCompiler
{
    RngCompiledSchema Compile(XmlDocument schema, SchemaCompileOptions options, ISchemaOperation operation);
}

internal static class RngGraph
{
    internal static bool ValidatePattern(int id, RngPatternKind kind, IReadOnlyList<int> children, RngNameClass? name,
        SchemaName? datatype, IReadOnlyList<XmlFacet> parameters, string? value, SchemaNamespaceContext context)
    {
        ArgumentNullException.ThrowIfNull(children); ArgumentNullException.ThrowIfNull(parameters); ArgumentNullException.ThrowIfNull(context);
        if (id < 0 || !Enum.IsDefined(kind)) throw new ArgumentException("Invalid pattern identity/kind.");
        int count = kind switch
        {
            RngPatternKind.Choice or RngPatternKind.Group or RngPatternKind.Interleave => 2,
            RngPatternKind.OneOrMore or RngPatternKind.List or RngPatternKind.Element or RngPatternKind.Attribute or RngPatternKind.Ref => 1,
            RngPatternKind.Data => children.Count <= 1 ? children.Count : -1,
            _ => 0
        };
        if (children.Count != count) throw new ArgumentException("Invalid pattern arity.");
        foreach (int child in children) if (child < 0) throw new ArgumentException("Negative pattern edge.");
        if ((name is not null) != (kind is RngPatternKind.Element or RngPatternKind.Attribute) ||
            (datatype is not null) != (kind is RngPatternKind.Data or RngPatternKind.Value) ||
            (value is not null) != (kind == RngPatternKind.Value) || parameters.Count != 0 && kind != RngPatternKind.Data)
            throw new ArgumentException("Invalid pattern payload.");
        if (name is not null) ValidateName(name);
        return true;
    }
    private static void ValidateName(RngNameClass name, int depth = 0)
    {
        if (depth == 1024) throw new ArgumentException("Name class representation depth exceeded.");
        switch (name)
        {
            case RngName n: ArgumentNullException.ThrowIfNull(n.Name.NamespaceUri); ArgumentNullException.ThrowIfNull(n.Name.LocalName); break;
            case RngAnyName a: if (a.Except is not null) ValidateName(a.Except, depth + 1); break;
            case RngNsName n: ArgumentNullException.ThrowIfNull(n.NamespaceUri); if (n.Except is not null) ValidateName(n.Except, depth + 1); break;
            case RngNameChoice c: ArgumentNullException.ThrowIfNull(c.Left); ArgumentNullException.ThrowIfNull(c.Right); ValidateName(c.Left, depth + 1); ValidateName(c.Right, depth + 1); break;
            default: throw new ArgumentException("Unknown name class.");
        }
    }
    internal static IReadOnlyList<RngPattern> Snapshot(int start, IReadOnlyList<RngPattern> patterns, IXmlDatatypeRegistry datatypes)
    {
        ArgumentNullException.ThrowIfNull(datatypes);
        var owned = SchemaSnapshots.List(patterns);
        if ((uint)start >= (uint)owned.Count) throw new ArgumentException("Invalid start edge.");
        for (int i = 0; i < owned.Count; i++)
        {
            var p = owned[i];
            if (p.Id != i) throw new ArgumentException("Pattern IDs are not dense.");
            ValidatePattern(p.Id, p.Kind, p.Children, p.NameClass, p.Datatype, p.Parameters, p.Value, p.Context);
            foreach (int child in p.Children) if ((uint)child >= (uint)owned.Count) throw new ArgumentException("Pattern edge out of range.");
        }
        return owned;
    }
}
