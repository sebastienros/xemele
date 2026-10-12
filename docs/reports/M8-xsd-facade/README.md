# Public XSD composition

The owned `XmlSchema.cs` seals the verified declaration graph with an executable
`XmlDatatypeRegistry`, prepares every retained datatype definition, compiles
content programs for all retained types, finalizes declarations and restricted
identity XPath, and then publishes the immutable public schema. Validations
supply the real identity factory/program even when the schema has no constraints;
per-call evaluators, cursors, namespace maps, effective values, IDs and identity
tables are independent. Published state retains no source document/node handles.

Before publication, checks cover unused and anonymous definitions, retained
old/new redefine carriers, attribute declaration and binding contexts, typed fixed
value equality, invalid defaults/fixed values, ID constraints and multiple ID
attribute uses, simple or emptiable mixed element constraints, and effective
NOTATION enumerations/declarations. Composite list/union enumeration checks
preserve member selection. Runtime default assessment still uses the canonical
representation against the actual instance type; value-only facet checking does
not replace lexical assessment.

String and stream compilation preserve the single supplied compile operation
through principal parsing and subsequent compilation. Resolver requests retain
the actual referring source while `xml:base` independently controls resolution;
returned resources are disposed exactly once. Caller streams stay open. The owned
validation input adapter uses the verified M2 parser paths with structured syntax
refusal and preserves exact caller exception identity. It transfers the remaining
aggregate work allowance to the verified DOM evaluator; no shared contract/source
changes were required. Default/fixed application never changes the caller DOM.

## Source checks and correction history

Source commits: `d95512f515ed8ee44d3d736515e8c95dde4fda64` and
`c27273d4adc5a1c05a5793873015dd598b141cde`.
`python3 docs/reports/M8-xsd-facade/check.py` pins SDK 10.0.400/runtime 10.0.11,
Release, offline restore, disabled build servers/shared compilation, and serial
correctness checks. It excludes every `WarmBorrowed` allocation-counter method
and never invokes measurement code. Raw XML and build outputs stay ignored under
`artifacts/M8-xsd-facade/`.

The source driver passed 1,018 affected cases (70 owned public, 948 dependency
cases), and safe resolver42. Zero errors, failures, skipped or not-run cases.
Compiled public API comparison matched 1,417 rows; the facade fragment matched
seven approved rows; frozen evaluator/compiler/identity/content seams matched
2/257/24/9 rows. Both normal and true XML-only consumers built with zero warnings
or errors and executed successfully; XML-only sources contain no HTML/CSS/XSLT
lane dependency. Planning validates 182 tasks. Separate tests demonstrate all
public input forms, concurrent validations, unchanged source/instance inputs,
content/derivation/substitution, facets, attribute/wildcard policies, defaults and
fixed values, xsi:type/nil, IDs and key/unique/keyref, malformed inputs,
resource refusal/callback identity, redirect provenance, and redefine retention.

Original failures are preserved honestly:

- Initial test build failed four xUnit1051 analyzer checks for omitted test-context
  cancellation tokens. Added those tokens; subsequent builds pass.
- First complete public run had 64/65 passing: the owned attribute-group witness
  incorrectly expected wildcard union. Corrected it to the verified XSD group
  intersection with independently accepted/rejected namespace witnesses. No runtime
  behavior was changed to satisfy the incorrect oracle.
- Composite NOTATION self-audit added retained dependency checks and five public
  cases. The new union witness expected lexical `1` to fail enumeration `true`,
  overlooking its valid boolean member alternative. Corrected the invalid witness
  to `2`; 1,018/1,018 affected checks then passed.

Source review acceptance and allocation evidence remain pending. No counters,
BenchmarkDotNet or timing measurements have been run by this worker. Build/test
runner elapsed output is not performance evidence. Full W3C corpus and cumulative
milestone verification remain with their owning tasks; this report does not claim
release conformance, x64 execution or performance.
