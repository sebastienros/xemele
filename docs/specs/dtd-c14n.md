# DTD validation and canonicalization — M5

Decision owner: M5-contracts. The [frozen decisions](../decisions/M5/validation-canonicalization.md)
resolve association, outcome construction, finite budgets, cancellation, diagnostics,
node-set membership, all three algorithms and error/lifetime semantics. Exact compiled
API declarations and baselines live in `tests/Contracts/Fixtures/M5/`; these are isolated,
nonpackaged contract models, not shipped stubs.

Validation uses `Xemele.Validation.DtdSchema.Compile(string, resolver?, limits?, cancellationToken)`
or `FromDocument(XmlDocument)`, and `Validate(XmlDocument, ValidationOptions?)`.
Shared `ValidationResult` owns a diagnostic snapshot and distinguishes complete invalid
from incomplete quota/provenance outcomes. Shared `ValidationOptions` has Limits,
nullable positive MaxOperations (10M default), and CancellationToken. M5-validation-model
owns their implementation; they contain no M8 schemas. DTD lanes consume the verified M2
parsed model and current DOM, without silent augmentation or expansion.

Canonicalization uses `Xemele.Serialization.XmlCanonicalizer.Canonicalize(XmlDocument, Stream, options?)`
and the independent `IReadOnlyList<Xemele.XPath.XPathItem>` overload. Modes are Inclusive10,
Inclusive11, Exclusive10. Options specify comments, exclusive inclusive-prefix list,
finite shared Limits/output-byte cap and cancellation. Tree input and M4 identity/model
are the only required feature dependencies. Selection is exact membership, not subtree
closure. Streams remain caller-owned; byte output follows the frozen W3C algorithms.

An explicit ordered repair is required for retained standalone/provenance metadata,
as documented in the decisions and handoff. Contract fixtures do not claim that repair
exists. No M6 catalogs/XInclude, M7 HTML or M8 schema types are needed. Implementing
lanes prove their acceptance independently, then the M5 gate verifies combined behavior
and preservation of all earlier milestones.
