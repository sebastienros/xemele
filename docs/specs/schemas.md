# Schema validation contracts — M8

Decision owner: `M8-contracts`. Prerequisites: M5 validation result model, M4 XPath for Schematron, and M2 resolver. HTML is not a dependency.

```csharp
namespace Xemele.Validation;
public sealed class RelaxNgSchema
{
    public static RelaxNgSchema Compile(Document schema, SchemaCompileOptions? options = null);
    public ValidationResult Validate(Document document, ValidationOptions? options = null);
}
public sealed class XmlSchema
{
    public static XmlSchema Compile(Document schema, SchemaCompileOptions? options = null);
    public ValidationResult Validate(Document document, ValidationOptions? options = null);
}
public sealed class SchematronSchema
{
    public static SchematronSchema Compile(Document schema, SchematronCompileOptions? options = null);
    public SchematronResult Validate(Document document, SchematronValidationOptions? options = null);
}
public sealed record SchemaCompileOptions
{
    public XmlResourceResolver? Resolver { get; init; }
    public XmlResourceLimits Limits { get; init; }
    public CancellationToken CancellationToken { get; init; }
}
```

Contract fixtures add string/stream conveniences, exact profile options, schema compilation diagnostics, phase selection, assertion/report records, and supported structured-report output. Compiled schemas are immutable; per-run traversal, identity tables, diagnostics, and resolution state are independent. Validation returns results, while malformed schema compilation throws a dedicated diagnostic exception. No CLR model serialization or future XSLT dependency.

## Shared datatype lane

An internal `IXmlDatatypeLibrary` defines lexical parsing, value equality/comparison, whitespace normalization, and facet checking with namespace context. `M8-datatypes` implements the XSD 1.0 datatype set and comparisons; `M8-regex` implements XML Schema regex syntax separately. Neither assumes .NET regex semantics. They run after `M8-model`; datatype facet integration depends on regex where required. These are shared prerequisites for RELAX NG's XSD datatype library and XSD validation.

## RELAX NG lane

`M8-rng-compile` parses XML syntax, namespace/name classes, references/includes, and simplification into an immutable pattern graph. `M8-rng-validate` implements pattern matching with attributes/interleave/lists/datatype constraints and resolver budgets. Tests distinguish schema restrictions from invalid instances. XML syntax and declared datatype libraries are the release profile; compact syntax remains excluded. Own schema and instance suites plus pinned libxml2 differentials.

## XSD lane

`M8-xsd-compile` handles component model, imports/includes/redefine, namespace resolution, element/attribute/type declarations, substitution and derivation constraints. `M8-xsd-content` compiles occurrence/compositor/wildcard models and checks relevant determinism restrictions. `M8-xsd-validate` applies types/facets, xsi:type/nil, defaults/fixed, and instance constraints. `M8-xsd-identity` adds key/unique/keyref and their restricted XPath rules. It owns a dedicated hook implementation; integration occurs in a later validation facade task rather than two workers editing one engine.

XSD 1.0 Second Edition is the contract. W3C suite metadata defines applicability; XSD 1.1 cases are excluded with reasons. Full schema claims are blocked by missing applicable cases. Validation does not silently mutate documents; augmentation/PSVI is separately inventoried and deferred unless explicitly included.

## Schematron lane

`M8-sch-compile` resolves includes/abstract patterns, phases, rule contexts, lets, assertions/reports and diagnostics for the chosen ISO edition/XPath 1.0 binding. `M8-sch-validate` evaluates with M4 expressions and emits structured results; reporting task adds the declared report serialization profile. Unsupported query bindings are compile errors. No XSLT-generated evaluator.

RELAX NG, XSD, and Schematron compilation lanes are independent after the model. Validator tasks wait for their own compiler and shared datatypes as specified. A schema integration task composes CLI commands and cross-validator resolver/limit tests only after all individual conformance tasks pass. Resource failures and invalid instances must remain distinguishable.
