// Frozen M8 shared declarations; implementations own all retained inputs.
using Xemele.XPath;
namespace Xemele.Validation;

/// <summary>Configures schema resource resolution, compilation budgets, and cancellation.</summary>
/// <remarks>External schema documents are loaded during compilation, not instance validation. Resolver and caller-stream exceptions propagate rather than being converted into schema syntax errors. Resolved resources are disposed after loading according to their ownership policy.</remarks>
public sealed record SchemaCompileOptions
{
    /// <summary>Gets the resolver for external schema resources, or <see langword="null"/> to deny external resolution.</summary>
    public XmlResourceResolver? Resolver
    {
        get; init;
    }
    /// <summary>Gets the absolute base URI for the principal schema, or <see langword="null"/> when none is supplied.</summary>
    public Uri? BaseUri
    {
        get; init;
    }
    /// <summary>Gets the resource limits shared by schema parsing and compilation.</summary>
    public XmlResourceLimits Limits { get; init; } = new();
    /// <summary>Gets the positive compilation work allowance, or <see langword="null"/> for no additional work cap.</summary>
    /// <remarks>The default is 10,000,000. A configured evaluation-work limit also applies; the smaller allowance is used.</remarks>
    public long? MaxOperations { get; init; } = 10_000_000;
    /// <summary>Gets the positive retained-component allowance, or <see langword="null"/> for no configured component cap.</summary>
    /// <remarks>The default is 100,000. This accounts for compiler-retained structures, not just authored schema declarations.</remarks>
    public int? MaxComponents { get; init; } = 100_000;
    /// <summary>Gets the token used to cancel compilation and resource loading.</summary>
    public CancellationToken CancellationToken
    {
        get; init;
    }
}
/// <summary>Reports schema compilation failure with a detached snapshot of its diagnostics.</summary>
public sealed class SchemaCompileException : Exception
{
    /// <summary>Gets the compilation diagnostics, including at least one error or fatal diagnostic.</summary>
    public IReadOnlyList<XmlDiagnostic> Diagnostics
    {
        get;
    }
    /// <summary>Creates an exception from a snapshot of compilation diagnostics.</summary>
    /// <param name="diagnostics">The diagnostics describing the compilation failure.</param>
    /// <exception cref="ArgumentNullException"><paramref name="diagnostics"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The sequence contains a null item or contains no error or fatal diagnostic.</exception>
    public SchemaCompileException(IEnumerable<XmlDiagnostic> diagnostics) : base("Schema compilation failed.")
    {
        Diagnostics = SchemaSnapshots.List(diagnostics);
        if (Diagnostics.Count == 0 || !Diagnostics.Any(d => d.Severity is XmlDiagnosticSeverity.Error or XmlDiagnosticSeverity.Fatal))
        {
            throw new ArgumentException("Compilation requires an error or fatal diagnostic.", nameof(diagnostics));
        }
    }
}
/// <summary>Identifies the supported Schematron language edition.</summary>
public enum SchematronEdition
{
    /// <summary>The ISO Schematron 2016 edition.</summary>
    Iso2016 = 0
}
/// <summary>Identifies the query language used by Schematron expressions.</summary>
public enum SchematronQueryBinding
{
    /// <summary>The XPath 1.0 query binding.</summary>
    XPath1 = 0
}
/// <summary>Configures compilation of the supported ISO Schematron XPath 1.0 profile.</summary>
public sealed record SchematronCompileOptions
{
    /// <summary>Gets the common schema resource, budget, and cancellation options.</summary>
    public SchemaCompileOptions Schema { get; init; } = new();
    /// <summary>Gets the Schematron edition to compile.</summary>
    public SchematronEdition Edition { get; init; } = SchematronEdition.Iso2016;
    /// <summary>Gets the expression query binding to use.</summary>
    public SchematronQueryBinding QueryBinding { get; init; } = SchematronQueryBinding.XPath1;
    /// <summary>Gets the limits for compiling individual XPath expressions.</summary>
    public XPathCompileLimits XPathLimits { get; init; } = new();
}
/// <summary>Configures one Schematron evaluation, including phase selection and XPath limits.</summary>
public sealed record SchematronValidationOptions
{
    /// <summary>Gets the resource budgets and cancellation token for the overall validation operation.</summary>
    public ValidationOptions Validation { get; init; } = new();
    /// <summary>Gets the phase ID, <c>#ALL</c> for all patterns, or <see langword="null"/> or <c>#DEFAULT</c> for the schema's default phase.</summary>
    public string? Phase
    {
        get; init;
    }
    /// <summary>Gets the limits applied to individual XPath evaluations within the overall validation budget.</summary>
    public XPathEvaluationLimits XPathLimits { get; init; } = new();
}
/// <summary>Distinguishes a failed assertion from a successful report test.</summary>
public enum SchematronFindingKind
{
    /// <summary>An assertion whose test evaluated to false.</summary>
    FailedAssert = 0,
    /// <summary>A report whose test evaluated to true; this finding does not itself make validation invalid.</summary>
    SuccessfulReport = 1
}
/// <summary>Contains the evaluated text of a referenced Schematron property.</summary>
/// <param name="Id">The referenced property ID.</param>
/// <param name="Text">The evaluated property text.</param>
/// <exception cref="ArgumentNullException"><paramref name="Id"/> or <paramref name="Text"/> is null.</exception>
public sealed record SchematronPropertyReference(string Id, string Text)
{
    private string id = SchemaSnapshots.Required(Id), text = SchemaSnapshots.Required(Text);
    /// <summary>Gets or initializes the non-null property ID.</summary>
    public string Id
    {
        get => id; init => id = SchemaSnapshots.Required(value);
    }
    /// <summary>Gets or initializes the non-null evaluated property text.</summary>
    public string Text
    {
        get => text; init => text = SchemaSnapshots.Required(value);
    }
}
/// <summary>Contains the evaluated text of a referenced Schematron diagnostic.</summary>
/// <param name="Id">The referenced diagnostic ID.</param>
/// <param name="Text">The evaluated diagnostic text.</param>
/// <exception cref="ArgumentNullException"><paramref name="Id"/> or <paramref name="Text"/> is null.</exception>
public sealed record SchematronDiagnosticReference(string Id, string Text)
{
    private string id = SchemaSnapshots.Required(Id), text = SchemaSnapshots.Required(Text);
    /// <summary>Gets or initializes the non-null diagnostic ID.</summary>
    public string Id
    {
        get => id; init => id = SchemaSnapshots.Required(value);
    }
    /// <summary>Gets or initializes the non-null evaluated diagnostic text.</summary>
    public string Text
    {
        get => text; init => text = SchemaSnapshots.Required(value);
    }
}
/// <summary>Contains a detached Schematron assertion or report finding and its rule and node locations.</summary>
public sealed record SchematronFinding
{
    /// <summary>Gets whether this finding is a failed assertion or a successful report.</summary>
    public SchematronFindingKind Kind
    {
        get;
    }
    /// <summary>Gets the authored assertion or report ID, if present.</summary>
    public string? Id
    {
        get;
    }
    /// <summary>Gets the finding's role metadata, if present.</summary>
    public string? Role
    {
        get;
    }
    /// <summary>Gets the finding's flag metadata, if present.</summary>
    public string? Flag
    {
        get;
    }
    /// <summary>Gets the nonnegative key linking this finding to its active pattern.</summary>
    public int PatternKey
    {
        get;
    }
    /// <summary>Gets the nonnegative key linking this finding to its fired-rule event.</summary>
    public int FiredRuleKey
    {
        get;
    }
    /// <summary>Gets the authored ID of the containing pattern, if present.</summary>
    public string? PatternId
    {
        get;
    }
    /// <summary>Gets the authored context expression of the fired rule.</summary>
    public string RuleContext
    {
        get;
    }
    /// <summary>Gets the authored assertion or report test expression.</summary>
    public string Test
    {
        get;
    }
    /// <summary>Gets the instance path reported for the finding, including any subject selection.</summary>
    public string Location
    {
        get;
    }
    /// <summary>Gets the original source coordinates of the reported instance node.</summary>
    public XmlLocation SourceLocation
    {
        get;
    }
    /// <summary>Gets the instance path of the node that matched the rule context.</summary>
    public string ContextLocation
    {
        get;
    }
    /// <summary>Gets the original source coordinates of the rule-context node.</summary>
    public XmlLocation ContextSourceLocation
    {
        get;
    }
    /// <summary>Gets the evaluated assertion or report message text.</summary>
    public string Text
    {
        get;
    }
    /// <summary>Gets the read-only snapshot of evaluated diagnostic references.</summary>
    public IReadOnlyList<SchematronDiagnosticReference> Diagnostics
    {
        get;
    }
    /// <summary>Gets the read-only snapshot of evaluated property references.</summary>
    public IReadOnlyList<SchematronPropertyReference> Properties
    {
        get;
    }
    /// <summary>Creates a finding and snapshots its diagnostic and property references.</summary>
    /// <param name="kind">The assertion or report outcome.</param>
    /// <param name="id">The optional authored assertion or report ID.</param>
    /// <param name="role">The optional role metadata.</param>
    /// <param name="flag">The optional flag metadata.</param>
    /// <param name="patternKey">The nonnegative active-pattern key.</param>
    /// <param name="firedRuleKey">The nonnegative fired-rule event key.</param>
    /// <param name="patternId">The optional authored pattern ID.</param>
    /// <param name="ruleContext">The authored rule-context expression.</param>
    /// <param name="test">The authored test expression.</param>
    /// <param name="location">The reported instance path.</param>
    /// <param name="sourceLocation">The reported node's source coordinates.</param>
    /// <param name="contextLocation">The matching rule-context node's instance path.</param>
    /// <param name="contextSourceLocation">The matching rule-context node's source coordinates.</param>
    /// <param name="text">The evaluated message text.</param>
    /// <param name="diagnostics">The evaluated diagnostic references to snapshot.</param>
    /// <param name="properties">The evaluated property references to snapshot.</param>
    /// <exception cref="ArgumentOutOfRangeException">The kind is undefined or either key is negative.</exception>
    /// <exception cref="ArgumentNullException">A required string or reference sequence is null.</exception>
    /// <exception cref="ArgumentException">A reference sequence contains a null item.</exception>
    public SchematronFinding(SchematronFindingKind kind, string? id, string? role, string? flag,
        int patternKey, int firedRuleKey, string? patternId, string ruleContext, string test,
        string location, XmlLocation sourceLocation, string contextLocation, XmlLocation contextSourceLocation,
        string text, IEnumerable<SchematronDiagnosticReference> diagnostics, IEnumerable<SchematronPropertyReference> properties)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        if (patternKey < 0 || firedRuleKey < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(patternKey));
        }

        ArgumentNullException.ThrowIfNull(ruleContext);
        ArgumentNullException.ThrowIfNull(test);
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(contextLocation);
        ArgumentNullException.ThrowIfNull(text);
        Kind = kind;
        Id = id;
        Role = role;
        Flag = flag;
        PatternKey = patternKey;
        FiredRuleKey = firedRuleKey;
        PatternId = patternId;
        RuleContext = ruleContext;
        Test = test;
        Location = location;
        SourceLocation = sourceLocation;
        ContextLocation = contextLocation;
        ContextSourceLocation = contextSourceLocation;
        Text = text;
        Diagnostics = SchemaSnapshots.List(diagnostics);
        Properties = SchemaSnapshots.List(properties);
    }
}
/// <summary>Identifies one rule firing at a particular instance node.</summary>
/// <param name="Key">The nonnegative event key, unique within a result.</param>
/// <param name="PatternKey">The nonnegative containing active-pattern key.</param>
/// <param name="RuleKey">The nonnegative compiled-rule key.</param>
/// <param name="Context">The authored rule-context expression.</param>
/// <param name="Location">The matching node's instance path.</param>
/// <exception cref="ArgumentOutOfRangeException">A key is negative.</exception>
/// <exception cref="ArgumentNullException"><paramref name="Context"/> or <paramref name="Location"/> is null.</exception>
public sealed record SchematronFiredRule(int Key, int PatternKey, int RuleKey, string Context, string Location)
{
    private int key = SchemaSnapshots.Nonnegative(Key), patternKey = SchemaSnapshots.Nonnegative(PatternKey), ruleKey = SchemaSnapshots.Nonnegative(RuleKey);
    private string context = SchemaSnapshots.Required(Context), location = SchemaSnapshots.Required(Location);
    /// <summary>Gets or initializes the nonnegative event key.</summary>
    public int Key
    {
        get => key; init => key = SchemaSnapshots.Nonnegative(value);
    }
    /// <summary>Gets or initializes the nonnegative containing active-pattern key.</summary>
    public int PatternKey
    {
        get => patternKey; init => patternKey = SchemaSnapshots.Nonnegative(value);
    }
    /// <summary>Gets or initializes the nonnegative compiled-rule key.</summary>
    public int RuleKey
    {
        get => ruleKey; init => ruleKey = SchemaSnapshots.Nonnegative(value);
    }
    /// <summary>Gets or initializes the non-null authored rule-context expression.</summary>
    public string Context
    {
        get => context; init => context = SchemaSnapshots.Required(value);
    }
    /// <summary>Gets or initializes the non-null matching node's instance path.</summary>
    public string Location
    {
        get => location; init => location = SchemaSnapshots.Required(value);
    }
}
/// <summary>Identifies a pattern selected for a Schematron evaluation.</summary>
/// <param name="Key">The nonnegative pattern key, unique within a result.</param>
/// <param name="Id">The optional authored pattern ID.</param>
/// <param name="Name">The optional pattern name.</param>
/// <exception cref="ArgumentOutOfRangeException"><paramref name="Key"/> is negative.</exception>
public sealed record SchematronActivePattern(int Key, string? Id, string? Name)
{
    private int key = SchemaSnapshots.Nonnegative(Key);
    /// <summary>Gets or initializes the nonnegative pattern key.</summary>
    public int Key
    {
        get => key; init => key = SchemaSnapshots.Nonnegative(value);
    }
}
/// <summary>Contains detached Schematron validation status, selected patterns, rule firings, and findings.</summary>
public sealed class SchematronResult
{
    /// <summary>Gets the completion and validity status and operation diagnostics.</summary>
    public ValidationResult Validation
    {
        get;
    }
    /// <summary>Gets the selected phase ID or <c>#ALL</c>.</summary>
    public string Phase
    {
        get;
    }
    /// <summary>Gets the schema title, if present.</summary>
    public string? Title
    {
        get;
    }
    /// <summary>Gets the read-only snapshot of namespace prefixes used in report attribute values.</summary>
    public IReadOnlyDictionary<string, string> Namespaces
    {
        get;
    }
    /// <summary>Gets the read-only snapshot of active patterns in evaluation order.</summary>
    public IReadOnlyList<SchematronActivePattern> ActivePatterns
    {
        get;
    }
    /// <summary>Gets the read-only snapshot of rule-firing events in evaluation order.</summary>
    public IReadOnlyList<SchematronFiredRule> FiredRules
    {
        get;
    }
    /// <summary>Gets the read-only snapshot of assertion and report findings in evaluation order.</summary>
    public IReadOnlyList<SchematronFinding> Findings
    {
        get;
    }
    /// <summary>Creates a result, snapshotting its collections and checking their cross-references and validity status.</summary>
    /// <param name="validation">The validation operation's status and diagnostics.</param>
    /// <param name="phase">The selected phase ID or <c>#ALL</c>.</param>
    /// <param name="title">The optional schema title.</param>
    /// <param name="namespaces">The namespace-prefix map to snapshot.</param>
    /// <param name="activePatterns">The active patterns to snapshot.</param>
    /// <param name="firedRules">The rule-firing events to snapshot.</param>
    /// <param name="findings">The assertion and report findings to snapshot.</param>
    /// <exception cref="ArgumentNullException">A required argument or namespace-map entry is null.</exception>
    /// <exception cref="ArgumentException">A collection contains a null item, keys are duplicated, cross-references disagree, or validity is inconsistent with failed assertions.</exception>
    public SchematronResult(ValidationResult validation, string phase, string? title,
        IReadOnlyDictionary<string, string> namespaces, IEnumerable<SchematronActivePattern> activePatterns,
        IEnumerable<SchematronFiredRule> firedRules, IEnumerable<SchematronFinding> findings)
    {
        ArgumentNullException.ThrowIfNull(validation);
        ArgumentNullException.ThrowIfNull(phase);
        Validation = validation;
        Phase = phase;
        Title = title;
        Namespaces = SchemaSnapshots.Map(namespaces);
        ActivePatterns = SchemaSnapshots.List(activePatterns);
        FiredRules = SchemaSnapshots.List(firedRules);
        Findings = SchemaSnapshots.List(findings);
        var patterns = new Dictionary<int, SchematronActivePattern>();
        foreach (var pattern in ActivePatterns)
        {
            if (!patterns.TryAdd(pattern.Key, pattern))
            {
                throw new ArgumentException("Duplicate active pattern key.", nameof(activePatterns));
            }
        }

        var events = new Dictionary<int, SchematronFiredRule>();
        foreach (var fired in FiredRules)
        {
            if (!patterns.ContainsKey(fired.PatternKey))
            {
                throw new ArgumentException("Fired event has no active pattern.", nameof(firedRules));
            }

            if (!events.TryAdd(fired.Key, fired))
            {
                throw new ArgumentException("Duplicate fired event key.", nameof(firedRules));
            }
        }
        var failed = false;
        foreach (var finding in Findings)
        {
            if (!events.TryGetValue(finding.FiredRuleKey, out var fired) || fired.PatternKey != finding.PatternKey)
            {
                throw new ArgumentException("Finding has no matching fired event/pattern.", nameof(findings));
            }

            if (!string.Equals(patterns[finding.PatternKey].Id, finding.PatternId, StringComparison.Ordinal))
            {
                throw new ArgumentException("Finding authored ID differs from its active pattern.", nameof(findings));
            }

            if (!string.Equals(fired.Context, finding.RuleContext, StringComparison.Ordinal) || !string.Equals(fired.Location, finding.ContextLocation, StringComparison.Ordinal))
            {
                throw new ArgumentException("Finding context differs from its fired event.", nameof(findings));
            }

            failed |= finding.Kind == SchematronFindingKind.FailedAssert;
        }
        if (validation.IsValid && failed || validation.IsComplete && !validation.IsValid && !failed)
        {
            throw new ArgumentException("Validation status is inconsistent with findings.", nameof(validation));
        }
    }
}
internal readonly record struct SchemaName(string NamespaceUri, string LocalName);
internal sealed record SchemaNamespaceContext(IReadOnlyDictionary<string, string> Prefixes, string DefaultNamespace, Uri? BaseUri)
{
    private IReadOnlyDictionary<string, string> prefixes = SchemaSnapshots.Map(Prefixes);
    public IReadOnlyDictionary<string, string> Prefixes
    {
        get => prefixes; init => prefixes = SchemaSnapshots.Map(value);
    }
}
internal enum SchemaResourcePurpose
{
    RelaxNgExternalRef, RelaxNgInclude, XsdInclude, XsdImport, XsdRedefine, SchematronInclude
}
internal sealed record SchemaResourceReference(string Href, Uri? Referrer, SchemaResourcePurpose Purpose, XmlLocation Location);
internal interface ISchemaOperation
{
    XmlResourceLimits Limits
    {
        get;
    }
    CancellationToken CancellationToken
    {
        get;
    }
    bool IsComplete
    {
        get;
    }
    long? RemainingWork
    {
        get;
    }
    bool Charge(long work, XmlLocation location);
    bool ChargeComponents(int count, XmlLocation location);
    bool AddDiagnostic(XmlDiagnostic diagnostic);
    void Refuse(string code, string message, XmlLocation location);
}
internal interface ISchemaResourceLoader
{
    XmlDocument Load(SchemaResourceReference reference, ISchemaOperation operation);
}
internal enum XmlDatatypeVariety
{
    Atomic, List, Union
}
internal enum XmlDatatypeDerivation
{
    Restriction, List, Union
}
internal enum XmlValueOrder
{
    Less = -1, Equal = 0, Greater = 1, Indeterminate = 2
}
internal enum XmlWhitespaceRule
{
    Preserve, Replace, Collapse
}
internal enum XmlFacetKind
{
    Length, MinLength, MaxLength, Pattern, Enumeration, WhiteSpace, MaxInclusive, MaxExclusive, MinInclusive, MinExclusive, TotalDigits, FractionDigits
}
internal sealed record XmlFacet(XmlFacetKind Kind, string LexicalValue, bool Fixed, SchemaNamespaceContext Context, XmlLocation Location);
internal sealed record XmlDatatypeDefinition(SchemaName Name, XmlDatatypeVariety Variety, SchemaName? BaseType,
    SchemaName? ItemType, IReadOnlyList<SchemaName> MemberTypes, IReadOnlyList<XmlFacet> Facets,
    IReadOnlyList<XmlDatatypeDerivation> Final, bool Anonymous, XmlLocation Location)
{
    private IReadOnlyList<SchemaName> memberTypes = SchemaSnapshots.List(MemberTypes);
    private IReadOnlyList<XmlFacet> facets = SchemaSnapshots.List(Facets);
    private IReadOnlyList<XmlDatatypeDerivation> final = SchemaSnapshots.List(Final);
    public IReadOnlyList<SchemaName> MemberTypes
    {
        get => memberTypes; init => memberTypes = SchemaSnapshots.List(value);
    }
    public IReadOnlyList<XmlFacet> Facets
    {
        get => facets; init => facets = SchemaSnapshots.List(value);
    }
    public IReadOnlyList<XmlDatatypeDerivation> Final
    {
        get => final; init => final = SchemaSnapshots.List(value);
    }
}
internal abstract class XmlDatatypeValue
{
    internal abstract SchemaName TypeName
    {
        get;
    }
    internal abstract XmlDatatypeVariety Variety
    {
        get;
    }
    internal abstract IReadOnlyList<XmlDatatypeValue>? Items
    {
        get;
    }
    internal abstract XmlDatatypeValue? SelectedMember
    {
        get;
    }
}
internal sealed record XmlDatatypeError(string Code, string Message);
internal interface IXmlDatatypeLibrary
{
    string NamespaceUri
    {
        get;
    }
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
internal abstract class XmlSchemaRegexProgram
{
}
internal interface IXmlSchemaRegexCompiler
{
    XmlSchemaRegexProgram Compile(string pattern, XmlLocation location, ISchemaOperation operation);
}
internal interface IXmlSchemaRegexMatcher
{
    bool IsMatch(XmlSchemaRegexProgram program, ReadOnlySpan<char> input, ISchemaOperation operation);
}

// Named public kind was integrated by the ordered M8-resource-kind prerequisite.
internal static class SchemaResourceKinds
{
    internal const XmlResourceKind Schema = XmlResourceKind.Schema;
}
