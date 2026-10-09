# DTD validation and canonicalization — M5

Decision owner: `M5-contracts`. These are independent implementation lanes after the contract. DTD validation consumes the M2 parsed model; canonicalization consumes the tree and M4 node-set model.

```csharp
namespace Xemele.Validation;
public sealed class DtdSchema
{
    public static DtdSchema Compile(string dtd, XmlResourceResolver? resolver = null);
    public ValidationResult Validate(Document document, ValidationOptions? options = null);
}
public sealed record ValidationResult
{
    public bool IsValid { get; }
    public IReadOnlyList<XmlDiagnostic> Diagnostics { get; }
}
public sealed record ValidationOptions
{
    public CancellationToken CancellationToken { get; init; }
    public long MaxOperations { get; init; }
}

namespace Xemele.Serialization;
public enum XmlCanonicalizationMode { Inclusive10, Inclusive11, Exclusive10 }
public sealed record XmlCanonicalizationOptions
{
    public XmlCanonicalizationMode Mode { get; init; }
    public bool IncludeComments { get; init; }
    public IReadOnlyList<string> InclusivePrefixes { get; init; }
}
public static class XmlCanonicalizer
{
    public static void Canonicalize(Document document, Stream output,
        XmlCanonicalizationOptions? options = null);
    public static void Canonicalize(IReadOnlyList<Xemele.XPath.XPathItem> nodes, Stream output,
        XmlCanonicalizationOptions? options = null);
}
```

These sketches are in separate files/namespaces. Contract review fixes schema/document DTD association, multiple-source diagnostics, and result construction. `ValidationResult`/`ValidationOptions` are implemented by `M5-validation-model` and reused by M8; they contain no XSD/RELAX NG references.

DTD validation checks declared elements/attributes, content models, required/fixed/default semantics, enumerations, ID uniqueness and IDREF resolution, entities/notations, and standalone constraints where applicable. Parsing/expansion remains in M2. Validation must not silently rewrite the DOM; default-attribute augmentation is an explicit separate operation if exposed.

Canonicalization emits exact UTF-8 bytes without BOM/declaration according to each algorithm. Tests cover document and selected-node contexts, inherited namespace/XML attributes, comments outside root, inclusive-prefix lists, normalization/escaping, sorting, and C14N 1.1 differences. Invalid or mixed-document node sets are rejected. No normal pretty-printer reuse that changes canonical rules.

Implement content-model compilation and DTD instance validation in ordered tasks. C14N has its own engine and corpus tasks, parallel with DTD after M5 contracts. Final gate runs both suites plus earlier milestones. Neither lane needs catalogs, XInclude, HTML, or M8 schemas.
