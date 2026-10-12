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

## Consolidated source correction batch 1

Root's two independent source reviews identified three owned and three shared
publication/composition gaps. Owned corrections are committed at
`cfba0a33e24a970075b4035f3b57defcdc7972af`:

- Canonical prepared constraint text is revalidated through the declared type
  with the captured context and same registry/operation, before publication.
  The authored `DeclaredValue` remains available for typed comparisons.
- NOTATION token scans charge only actual inspected UTF-16 units, with cancellation
  polling at every unit. Prefix/local/namespace matching uses borrowed slices and
  existing owned schema names; no token lookup strings are materialized. A private
  `a b c` scan examines five units for three tokens. Long-token and long prefix/local
  controls cancel inside their scans/comparisons. This is a straightforward loop,
  with no speedup claim; primitive and end-to-end allocation evidence remain gated.
- The finished private program dictionary is wrapped read-only without copying,
  and final cancellation/source-revision checks occur after constructing that
  wrapper/schema. Existing concurrent/source-detachment witnesses pass.

Actual public run at that source commit: 93 cases, 84 pass, nine fail, zero
errors/skips/not-run. All 12 owned correction cases and earlier 70 cases pass;
valid numeric/QName particle fixed-value controls also pass. Expected shared
failures remain active in `PendingJoinTests`:

- Three unused group obligations: local referenced fixed QName mismatch, two ID
  attribute uses, and inconsistent same-name local element types.
- Four principal/external aggregate quota witnesses: DocumentCharacters and
  ExpandedCharacters, each through string and caller-stream public compilation.
- Two mismatched typed particle fixed values: numeric 1/2 and QName v/other.

The full affected run reports 1,041 cases, 1,032 pass and the same nine failures.
The check driver continues independent checks while retaining its failed exit:
resolver42, public1417/facade7/frozen2/257/24/9, both normal/XML-only consumers,
planning182 and whitespace checks all pass with zero build warnings/errors.
No expected outcome was relaxed and no failing case was excluded. Root is assigning
owning compiler and content repairs; this worker made no outside-owned source edit
and added no unverified repair dependency. Integration/join awaits root's exact
verified checkpoint. Earlier passing source evidence above is historical and is
not claimed to establish the new public obligations.

Source review acceptance and allocation evidence remain pending. No counters,
BenchmarkDotNet or timing measurements have been run by this worker. Build/test
runner elapsed output is not performance evidence. Full W3C corpus and cumulative
milestone verification remain with their owning tasks; this report does not claim
release conformance, x64 execution or performance.

## Verified repair join

Coordinator-authorized verified `main` checkpoint
`6eb332dd944472229c376ed942af76a32b2e83a2` was merged into the isolated branch.
Both repair records are `verified` and have been added to this task's dependencies.
Minimal facade source join commit:
`a75d851db90277e41a631e1e79eb1b4326c7b6af`.
The compiler now consumes `Compile(source, context)`, retaining the principal
parser's loader/ledgers, and content compilation uses its complete
`Compile(compiled, type, context.Operation, facets)` overload with the sealed
executable registry and shared facet evaluator. Existing all-retained-type and
binding traversal finalizes the new unused/displaced group carriers as well.
No original public witness oracle changed and no shared source was manually edited.

Actual `python3 docs/reports/M8-xsd-facade/check.py` at that source commit passed
1,110 affected cases, including all 93 public facade cases and 69 repair cases,
plus safe resolver42. Zero failures/errors/skips/not-run; public1417/facade7 and
frozen evaluator/compiler/identity/content2/257/24/9 matched; normal and true
XML-only consumers passed; all builds had zero warnings/errors; planning184 and
whitespace checks passed. All nine previously active shared-join failures now pass.
Final bounded seam review and allocation build/control/freeze remain pending;
no counters or timing measurements are claimed.

## Final worker handback

Both coordinator-reported bounded final seam reviewers accepted sourcea75d851d.
Final allocation build/control/freeze and actual root-granted execution passed;
see allocation-plan.md, allocation-preflight.json and allocations.md for exact
identities, lifecycle, numerators/denominators and ownership limitations.
Task is `implemented`; `integration_revision` remains null for the coordinator.
Root independently reports1152combined checks at9b2816007f9e5aa39cdb85752f8c416524dbfa40.
Accepted runtime/tests/API/sample/check/probe/supervisor inputs remain unchanged.
All raw outputs stay transient/ignored. No worker criteria or outside-source
requests remain. This worker has zero owned active jobs and relinquishes all
assignment/task/source/test/report/benchmark/sample/API/branch writes after its
final handoff commit. Coordinator alone integrates/verifies and cleans up.
