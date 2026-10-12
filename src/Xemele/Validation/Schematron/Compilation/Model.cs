// Frozen M8 compiler seam. All collection properties own immutable snapshots.
using Xemele.XPath;
namespace Xemele.Validation;
internal sealed record SchematronLet(XPathExpandedName Name, XPathExpression Value);
internal abstract record SchematronTextPart;
internal sealed record SchematronLiteral(string Text) : SchematronTextPart;
internal sealed record SchematronValueOf(XPathExpression Select) : SchematronTextPart;
internal sealed record SchematronNameOf(XPathExpression? Path) : SchematronTextPart;
internal sealed record SchematronCompiledCheck(SchematronFindingKind Kind, string? Id, string? Role, string? Flag,
    string TestSource, XPathExpression Test, string? SubjectSource, XPathExpression? Subject, IReadOnlyList<SchematronTextPart> Text, IReadOnlyList<string> Diagnostics, IReadOnlyList<string> Properties, XmlLocation Location)
{
    private IReadOnlyList<SchematronTextPart> text = SchemaSnapshots.List(Text);
    public IReadOnlyList<SchematronTextPart> Text { get => text; init => text = SchemaSnapshots.List(value); }
    private IReadOnlyList<string> diagnostics = SchemaSnapshots.List(Diagnostics);
    public IReadOnlyList<string> Diagnostics { get => diagnostics; init => diagnostics = SchemaSnapshots.List(value); }
    private IReadOnlyList<string> properties = SchemaSnapshots.List(Properties);
    public IReadOnlyList<string> Properties { get => properties; init => properties = SchemaSnapshots.List(value); }
}
internal sealed record SchematronCompiledRule(int Key, string ContextSource, XPathExpression ContextSelector,
    string? SubjectSource, XPathExpression? Subject,
    IReadOnlyList<SchematronLet> Lets, IReadOnlyList<SchematronCompiledCheck> Checks, XmlLocation Location)
{
    private IReadOnlyList<SchematronLet> lets = SchemaSnapshots.List(Lets);
    public IReadOnlyList<SchematronLet> Lets { get => lets; init => lets = SchemaSnapshots.List(value); }
    private IReadOnlyList<SchematronCompiledCheck> checks = SchemaSnapshots.List(Checks);
    public IReadOnlyList<SchematronCompiledCheck> Checks { get => checks; init => checks = SchemaSnapshots.List(value); }
}
internal sealed record SchematronCompiledPattern(int Key, string? Id, string? Name, IReadOnlyList<SchematronLet> Lets,
    IReadOnlyList<SchematronCompiledRule> Rules)
{
    private IReadOnlyList<SchematronLet> lets = SchemaSnapshots.List(Lets);
    public IReadOnlyList<SchematronLet> Lets { get => lets; init => lets = SchemaSnapshots.List(value); }
    private IReadOnlyList<SchematronCompiledRule> rules = SchemaSnapshots.List(Rules);
    public IReadOnlyList<SchematronCompiledRule> Rules { get => rules; init => rules = SchemaSnapshots.List(value); }
}
internal sealed record SchematronCompiledPhase(string Id, IReadOnlyList<SchematronLet> Lets, IReadOnlyList<int> ActivePatternKeys)
{
    private IReadOnlyList<SchematronLet> lets = SchemaSnapshots.List(Lets);
    public IReadOnlyList<SchematronLet> Lets { get => lets; init => lets = SchemaSnapshots.List(value); }
    private IReadOnlyList<int> activePatternKeys = SchemaSnapshots.List(ActivePatternKeys);
    public IReadOnlyList<int> ActivePatternKeys { get => activePatternKeys; init => activePatternKeys = SchemaSnapshots.List(value); }
}
internal sealed record SchematronCompiledModel(string? Title, string DefaultPhase,
    IReadOnlyDictionary<string, string> Namespaces, IReadOnlyList<SchematronLet> Lets,
    IReadOnlyList<SchematronCompiledPattern> Patterns, IReadOnlyDictionary<string, SchematronCompiledPhase> Phases,
    IReadOnlyDictionary<string, IReadOnlyList<SchematronTextPart>> Diagnostics,
    IReadOnlyDictionary<string, IReadOnlyList<SchematronTextPart>> Properties)
{
    private IReadOnlyDictionary<string, string> namespaces = SchemaSnapshots.Map(Namespaces);
    public IReadOnlyDictionary<string, string> Namespaces { get => namespaces; init => namespaces = SchemaSnapshots.Map(value); }
    private IReadOnlyList<SchematronLet> lets = SchemaSnapshots.List(Lets);
    public IReadOnlyList<SchematronLet> Lets { get => lets; init => lets = SchemaSnapshots.List(value); }
    private IReadOnlyList<SchematronCompiledPattern> patterns = SchemaSnapshots.List(Patterns);
    public IReadOnlyList<SchematronCompiledPattern> Patterns { get => patterns; init => patterns = SchemaSnapshots.List(value); }
    private IReadOnlyDictionary<string, SchematronCompiledPhase> phases = SnapshotMap(Phases);
    public IReadOnlyDictionary<string, SchematronCompiledPhase> Phases { get => phases; init => phases = SnapshotMap(value); }
    private IReadOnlyDictionary<string, IReadOnlyList<SchematronTextPart>> diagnostics = SnapshotText(Diagnostics);
    public IReadOnlyDictionary<string, IReadOnlyList<SchematronTextPart>> Diagnostics { get => diagnostics; init => diagnostics = SnapshotText(value); }
    private IReadOnlyDictionary<string, IReadOnlyList<SchematronTextPart>> properties = SnapshotText(Properties);
    public IReadOnlyDictionary<string, IReadOnlyList<SchematronTextPart>> Properties { get => properties; init => properties = SnapshotText(value); }
    private static IReadOnlyDictionary<string, T> SnapshotMap<T>(IReadOnlyDictionary<string, T> source) where T : class
    {
        ArgumentNullException.ThrowIfNull(source);
        var result = new Dictionary<string, T>(StringComparer.Ordinal);
        foreach (var (key, value) in source) { ArgumentNullException.ThrowIfNull(key); ArgumentNullException.ThrowIfNull(value); result.Add(key, value); }
        return new System.Collections.ObjectModel.ReadOnlyDictionary<string, T>(result);
    }
    private static IReadOnlyDictionary<string, IReadOnlyList<SchematronTextPart>> SnapshotText(IReadOnlyDictionary<string, IReadOnlyList<SchematronTextPart>> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var result = new Dictionary<string, IReadOnlyList<SchematronTextPart>>(StringComparer.Ordinal);
        foreach (var (key, value) in source) result.Add(key, SchemaSnapshots.List(value));
        return SnapshotMap(result);
    }
}
internal interface ISchematronCompiler
{
    SchematronCompiledModel Compile(XmlDocument schema, SchematronCompileOptions options, ISchemaOperation operation);
}
