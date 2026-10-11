// Frozen M8 shared declarations; implementations own all retained inputs.
using Xemele.XPath;
namespace Xemele.Validation;
public sealed record SchemaCompileOptions
{
    public XmlResourceResolver? Resolver { get; init; }
    public Uri? BaseUri { get; init; }
    public XmlResourceLimits Limits { get; init; } = new();
    public long? MaxOperations { get; init; } = 10_000_000;
    public int? MaxComponents { get; init; } = 100_000;
    public CancellationToken CancellationToken { get; init; }
}
public sealed class SchemaCompileException : Exception
{
    public IReadOnlyList<XmlDiagnostic> Diagnostics { get; }
    public SchemaCompileException(IEnumerable<XmlDiagnostic> diagnostics) : base("Schema compilation failed.")
    {
        Diagnostics = SchemaSnapshots.List(diagnostics);
        if (Diagnostics.Count == 0 || !Diagnostics.Any(d => d.Severity is XmlDiagnosticSeverity.Error or XmlDiagnosticSeverity.Fatal))
            throw new ArgumentException("Compilation requires an error or fatal diagnostic.", nameof(diagnostics));
    }
}
public enum SchematronEdition { Iso2016 = 0 }
public enum SchematronQueryBinding { XPath1 = 0 }
public sealed record SchematronCompileOptions
{
    public SchemaCompileOptions Schema { get; init; } = new();
    public SchematronEdition Edition { get; init; } = SchematronEdition.Iso2016;
    public SchematronQueryBinding QueryBinding { get; init; } = SchematronQueryBinding.XPath1;
    public XPathCompileLimits XPathLimits { get; init; } = new();
}
public sealed record SchematronValidationOptions
{
    public ValidationOptions Validation { get; init; } = new();
    public string? Phase { get; init; }
    public XPathEvaluationLimits XPathLimits { get; init; } = new();
}
public enum SchematronFindingKind { FailedAssert = 0, SuccessfulReport = 1 }
public sealed record SchematronPropertyReference(string Id, string Text)
{
    private string id = SchemaSnapshots.Required(Id), text = SchemaSnapshots.Required(Text);
    public string Id { get => id; init => id = SchemaSnapshots.Required(value); }
    public string Text { get => text; init => text = SchemaSnapshots.Required(value); }
}
public sealed record SchematronDiagnosticReference(string Id, string Text)
{
    private string id = SchemaSnapshots.Required(Id), text = SchemaSnapshots.Required(Text);
    public string Id { get => id; init => id = SchemaSnapshots.Required(value); }
    public string Text { get => text; init => text = SchemaSnapshots.Required(value); }
}
public sealed record SchematronFinding
{
    public SchematronFindingKind Kind { get; }
    public string? Id { get; }
    public string? Role { get; }
    public string? Flag { get; }
    public int PatternKey { get; }
    public int FiredRuleKey { get; }
    public string? PatternId { get; }
    public string RuleContext { get; }
    public string Test { get; }
    public string Location { get; }
    public XmlLocation SourceLocation { get; }
    public string ContextLocation { get; }
    public XmlLocation ContextSourceLocation { get; }
    public string Text { get; }
    public IReadOnlyList<SchematronDiagnosticReference> Diagnostics { get; }
    public IReadOnlyList<SchematronPropertyReference> Properties { get; }
    public SchematronFinding(SchematronFindingKind kind, string? id, string? role, string? flag,
        int patternKey, int firedRuleKey, string? patternId, string ruleContext, string test,
        string location, XmlLocation sourceLocation, string contextLocation, XmlLocation contextSourceLocation,
        string text, IEnumerable<SchematronDiagnosticReference> diagnostics, IEnumerable<SchematronPropertyReference> properties)
    {
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        if (patternKey < 0 || firedRuleKey < 0) throw new ArgumentOutOfRangeException(nameof(patternKey));
        ArgumentNullException.ThrowIfNull(ruleContext); ArgumentNullException.ThrowIfNull(test);
        ArgumentNullException.ThrowIfNull(location); ArgumentNullException.ThrowIfNull(contextLocation);
        ArgumentNullException.ThrowIfNull(text);
        Kind = kind; Id = id; Role = role; Flag = flag; PatternKey = patternKey; FiredRuleKey = firedRuleKey;
        PatternId = patternId; RuleContext = ruleContext; Test = test; Location = location; SourceLocation = sourceLocation;
        ContextLocation = contextLocation; ContextSourceLocation = contextSourceLocation; Text = text;
        Diagnostics = SchemaSnapshots.List(diagnostics); Properties = SchemaSnapshots.List(properties);
    }
}
public sealed record SchematronFiredRule(int Key, int PatternKey, int RuleKey, string Context, string Location)
{
    private int key = SchemaSnapshots.Nonnegative(Key), patternKey = SchemaSnapshots.Nonnegative(PatternKey), ruleKey = SchemaSnapshots.Nonnegative(RuleKey);
    private string context = SchemaSnapshots.Required(Context), location = SchemaSnapshots.Required(Location);
    public int Key { get => key; init => key = SchemaSnapshots.Nonnegative(value); }
    public int PatternKey { get => patternKey; init => patternKey = SchemaSnapshots.Nonnegative(value); }
    public int RuleKey { get => ruleKey; init => ruleKey = SchemaSnapshots.Nonnegative(value); }
    public string Context { get => context; init => context = SchemaSnapshots.Required(value); }
    public string Location { get => location; init => location = SchemaSnapshots.Required(value); }
}
public sealed record SchematronActivePattern(int Key, string? Id, string? Name)
{
    private int key = SchemaSnapshots.Nonnegative(Key);
    public int Key { get => key; init => key = SchemaSnapshots.Nonnegative(value); }
}
public sealed class SchematronResult
{
    public ValidationResult Validation { get; }
    public string Phase { get; }
    public string? Title { get; }
    public IReadOnlyDictionary<string, string> Namespaces { get; }
    public IReadOnlyList<SchematronActivePattern> ActivePatterns { get; }
    public IReadOnlyList<SchematronFiredRule> FiredRules { get; }
    public IReadOnlyList<SchematronFinding> Findings { get; }
    public SchematronResult(ValidationResult validation, string phase, string? title,
        IReadOnlyDictionary<string, string> namespaces, IEnumerable<SchematronActivePattern> activePatterns,
        IEnumerable<SchematronFiredRule> firedRules, IEnumerable<SchematronFinding> findings)
    {
        ArgumentNullException.ThrowIfNull(validation); ArgumentNullException.ThrowIfNull(phase);
        Validation = validation; Phase = phase; Title = title;
        Namespaces = SchemaSnapshots.Map(namespaces);
        ActivePatterns = SchemaSnapshots.List(activePatterns); FiredRules = SchemaSnapshots.List(firedRules);
        Findings = SchemaSnapshots.List(findings);
        var patterns = new Dictionary<int, SchematronActivePattern>();
        foreach (var pattern in ActivePatterns)
            if (!patterns.TryAdd(pattern.Key, pattern)) throw new ArgumentException("Duplicate active pattern key.", nameof(activePatterns));
        var events = new Dictionary<int, SchematronFiredRule>();
        foreach (var fired in FiredRules)
        {
            if (!patterns.ContainsKey(fired.PatternKey)) throw new ArgumentException("Fired event has no active pattern.", nameof(firedRules));
            if (!events.TryAdd(fired.Key, fired)) throw new ArgumentException("Duplicate fired event key.", nameof(firedRules));
        }
        bool failed = false;
        foreach (var finding in Findings)
        {
            if (!events.TryGetValue(finding.FiredRuleKey, out var fired) || fired.PatternKey != finding.PatternKey)
                throw new ArgumentException("Finding has no matching fired event/pattern.", nameof(findings));
            if (!string.Equals(patterns[finding.PatternKey].Id, finding.PatternId, StringComparison.Ordinal))
                throw new ArgumentException("Finding authored ID differs from its active pattern.", nameof(findings));
            if (!string.Equals(fired.Context, finding.RuleContext, StringComparison.Ordinal) || !string.Equals(fired.Location, finding.ContextLocation, StringComparison.Ordinal))
                throw new ArgumentException("Finding context differs from its fired event.", nameof(findings));
            failed |= finding.Kind == SchematronFindingKind.FailedAssert;
        }
        if (validation.IsValid && failed || validation.IsComplete && !validation.IsValid && !failed)
            throw new ArgumentException("Validation status is inconsistent with findings.", nameof(validation));
    }
}
internal readonly record struct SchemaName(string NamespaceUri, string LocalName);
internal sealed record SchemaNamespaceContext(IReadOnlyDictionary<string, string> Prefixes, string DefaultNamespace, Uri? BaseUri)
{
    private IReadOnlyDictionary<string, string> prefixes = SchemaSnapshots.Map(Prefixes);
    public IReadOnlyDictionary<string, string> Prefixes { get => prefixes; init => prefixes = SchemaSnapshots.Map(value); }
}
internal enum SchemaResourcePurpose { RelaxNgExternalRef, RelaxNgInclude, XsdInclude, XsdImport, XsdRedefine, SchematronInclude }
internal sealed record SchemaResourceReference(string Href, Uri? Referrer, SchemaResourcePurpose Purpose, XmlLocation Location);
internal interface ISchemaOperation
{
    XmlResourceLimits Limits { get; }
    CancellationToken CancellationToken { get; }
    bool IsComplete { get; }
    long? RemainingWork { get; }
    bool Charge(long work, XmlLocation location);
    bool ChargeComponents(int count, XmlLocation location);
    bool AddDiagnostic(XmlDiagnostic diagnostic);
    void Refuse(string code, string message, XmlLocation location);
}
internal interface ISchemaResourceLoader
{
    XmlDocument Load(SchemaResourceReference reference, ISchemaOperation operation);
}
internal enum XmlDatatypeVariety { Atomic, List, Union }
internal enum XmlDatatypeDerivation { Restriction, List, Union }
internal enum XmlValueOrder { Less = -1, Equal = 0, Greater = 1, Indeterminate = 2 }
internal enum XmlWhitespaceRule { Preserve, Replace, Collapse }
internal enum XmlFacetKind { Length, MinLength, MaxLength, Pattern, Enumeration, WhiteSpace, MaxInclusive, MaxExclusive, MinInclusive, MinExclusive, TotalDigits, FractionDigits }
internal sealed record XmlFacet(XmlFacetKind Kind, string LexicalValue, bool Fixed, SchemaNamespaceContext Context, XmlLocation Location);
internal sealed record XmlDatatypeDefinition(SchemaName Name, XmlDatatypeVariety Variety, SchemaName? BaseType,
    SchemaName? ItemType, IReadOnlyList<SchemaName> MemberTypes, IReadOnlyList<XmlFacet> Facets,
    IReadOnlyList<XmlDatatypeDerivation> Final, bool Anonymous, XmlLocation Location)
{
    private IReadOnlyList<SchemaName> memberTypes = SchemaSnapshots.List(MemberTypes);
    private IReadOnlyList<XmlFacet> facets = SchemaSnapshots.List(Facets);
    private IReadOnlyList<XmlDatatypeDerivation> final = SchemaSnapshots.List(Final);
    public IReadOnlyList<SchemaName> MemberTypes { get => memberTypes; init => memberTypes = SchemaSnapshots.List(value); }
    public IReadOnlyList<XmlFacet> Facets { get => facets; init => facets = SchemaSnapshots.List(value); }
    public IReadOnlyList<XmlDatatypeDerivation> Final { get => final; init => final = SchemaSnapshots.List(value); }
}
internal abstract class XmlDatatypeValue
{
    internal abstract SchemaName TypeName { get; }
    internal abstract XmlDatatypeVariety Variety { get; }
    internal abstract IReadOnlyList<XmlDatatypeValue>? Items { get; }
    internal abstract XmlDatatypeValue? SelectedMember { get; }
}
internal sealed record XmlDatatypeError(string Code, string Message);
internal interface IXmlDatatypeLibrary
{
    string NamespaceUri { get; }
    bool IsDefined(SchemaName name);
    XmlWhitespaceRule Whitespace(SchemaName name);
    string Normalize(ReadOnlySpan<char> lexical, XmlWhitespaceRule rule, ISchemaOperation operation);
    bool TryParse(SchemaName type, ReadOnlySpan<char> lexical, SchemaNamespaceContext context,
        ISchemaOperation operation, out XmlDatatypeValue? value, out XmlDatatypeError? error);
    bool ValueEquals(XmlDatatypeValue left, XmlDatatypeValue right, ISchemaOperation operation);
    XmlValueOrder Compare(XmlDatatypeValue left, XmlDatatypeValue right, ISchemaOperation operation);
    int ValueHashCode(XmlDatatypeValue value, ISchemaOperation operation);
    long? ValueLength(XmlDatatypeValue value, ISchemaOperation operation);
    int? TotalDigits(XmlDatatypeValue value, ISchemaOperation operation);
    int? FractionDigits(XmlDatatypeValue value, ISchemaOperation operation);
    XmlDatatypeValue Retype(SchemaName type, XmlDatatypeValue value, ISchemaOperation operation);
    XmlDatatypeValue CreateList(SchemaName type, IReadOnlyList<XmlDatatypeValue> items, ISchemaOperation operation);
    XmlDatatypeValue CreateUnion(SchemaName type, XmlDatatypeValue selectedMember, ISchemaOperation operation);
}
internal interface IXmlDatatypeRegistry
{
    IXmlDatatypeLibrary? Find(string namespaceUri);
    XmlDatatypeDefinition? FindType(SchemaName name);
}
internal interface IXmlFacetEvaluator
{
    bool ValidateDefinition(XmlDatatypeDefinition type, IXmlDatatypeRegistry registry, ISchemaOperation operation);
    bool TryValidate(XmlDatatypeDefinition type, ReadOnlySpan<char> lexical, SchemaNamespaceContext context,
        IXmlDatatypeRegistry registry, ISchemaOperation operation, out XmlDatatypeValue? value, out XmlDatatypeError? error);
}
internal abstract class XmlSchemaRegexProgram { }
internal interface IXmlSchemaRegexCompiler
{
    XmlSchemaRegexProgram Compile(string pattern, XmlLocation location, ISchemaOperation operation);
}
internal interface IXmlSchemaRegexMatcher
{
    bool IsMatch(XmlSchemaRegexProgram program, ReadOnlySpan<char> input, ISchemaOperation operation);
}

// Named public kind was integrated by the ordered M8-resource-kind prerequisite.
internal static class SchemaResourceKinds { internal const XmlResourceKind Schema = XmlResourceKind.Schema; }
