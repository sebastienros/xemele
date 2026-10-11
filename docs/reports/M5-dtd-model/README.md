# Internal DTD compilation

Task-owned runtime files are `src/Xemele/Validation/Dtd/Compilation/`.
The internal compiler consumes only the immutable verified M2 effective declaration
model and the verified shared `ValidationOperation`; it ships no public facade or
future schema stub. `samples/M5-dtd-model/DtdModelSample.csproj` is nonpackaged and
uses a task-owned friend assembly declaration. No public API baseline fragment is
needed for this internal deliverable.

## Frozen internal interface

- `DtdCompiler.Compile(DtdModel, ValidationOperation)` returns an immutable
  `CompiledDtdModel`. Declaration rejection throws `XmlParseException` with one
  fatal `DTD.*` diagnostic and original declaration location. Work/capacity failure
  throws `XmlParseException` with the operation's `VALIDATION.Limit` diagnostic;
  cancellation throws the original operation token. Argument exceptions propagate.
- `CompiledDtdModel.Source`, `Elements`, `Attributes` preserve M2 declaration
  snapshots and provenance. Collections are owned read-only wrappers.
  `FindElement`, `FindAttribute`, `HasNotation`, `HasUnparsedEntity` take borrowed
  ordinal character spans and an operation; they return null/false on exhausted
  work. A caller must stop if `operation.IsComplete` is false.
- `CompiledDtdElement.Declaration` and `.Content` expose each effective element.
  `DtdContentModel.Kind`, `InitialState` (0), `TryAdvance(ref int, span, operation)`,
  and `IsAccepting(int)` implement one-unambiguous position automata. Mismatch
  returns false and sets state -1 (sticky). Quota refusal returns false without
  changing the cursor. `ANY` accepts child names; `EMPTY` accepts no children;
  mixed content permits only listed child names and accepts the empty sequence.
  Text/whitespace/undeclared-child checks belong to the tree evaluator, which uses
  Kind to distinguish EMPTY, ANY, mixed and element-only content.
- `CompiledDtdAttribute.Declaration`, `NormalizedDefault`, and
  `IsValueValid(span, model, operation, location=default)` implement XML scalar,
  Name/Nmtoken, normalized token-list, enumeration, NOTATION, and unparsed ENTITY
  constraints. This method checks type, not REQUIRED/FIXED presence/equality or
  document-wide ID uniqueness/IDREF resolution. Those belong to the evaluator.
  Values are already normalized; repeated/leading/trailing spaces or tabs in
  non-CDATA token lists are rejected. Compilation uses the verified M2 default
  normalizer, retaining external declaration provenance and source locations.

## Bounded work and storage

Compilation uses an iterative postorder traversal and Glushkov first/last/follow
positions. Initial and every follow position set must have distinct lexical names;
repeated names in different deterministic positions (e.g. `(a,a)`) are allowed.
Occurrence and nullable groups contribute follow edges before publication. Every
visited particle, scalar/name comparison, union/edge, collection population and
normalizer entity probe is charged. Hash tables are compilation-only, with input
names/scalars charged before insertion. Warm lookup uses direct ordinal probes;
there is no fixed vocabulary search or SearchValues scanning to optimize here.

Operation caps remain aggregate, including default expansion across attributes.
The shared default budget meters each replacement scalar and each dictionary probe,
with aggregate ExpandedCharacters and EntityDepth. TokenCharacters counts Unicode
scalars. Compiler depth is the minimum of caller Depth and a finite implementation
4096; retained storage units have an aggregate hard maximum 1,048,576 even when
input/work caps are explicitly null. Units include compiled objects, stack entries,
position/edge/set entries, token references and normalized default UTF-16 units;
they are an implementation capacity, not an exact retained-byte estimate.
Successful comparisons charge each inspected UTF-16 code unit (a conservative
scalar-work charge for supplementary names), plus the comparison/transition step.
`IsAccepting` is a direct immutable array access; the evaluator charges its visited
node/member separately. No no-error diagnostic string formatting occurs.

## M2 boundary and follow-up

M2 intentionally publishes first bindings. Its parser discards duplicate element
and notation declarations, so their source-level uniqueness cannot be recovered
from the effective model. The compiler rejects duplicates in an explicitly supplied
internal model, but does not claim to detect information absent from M2. The
coordinator is creating an ordered M2 metadata repair owned outside this task for
source conflicts and cross-subset combination; the later facade will check that
retained evidence. Repeated attribute definitions remain M2 first-binding semantics.
No declaration source is reparsed or silently merged here.

## Validation and performance preparation

Focused correctness covers EMPTY/ANY/mixed/sequence/choice/all occurrences,
ambiguity, attribute/notation/default constraints, external default provenance,
borrowed ordinal matching, culture independence, malformed UTF-16, original
locations, aggregate/exact/one-over quotas, cancellation and 64 concurrent reuses.
An independent regular-expression language oracle exhaustively enumerates all
four-name sequences up to length five across at least ten deterministic models.

The Release sample `--control` verifies actual `.NET 10.0.11` on Arm64 and prints
`Demonstration=accepted 名,last; normalized default=one; unparsed entity=e; immutable schema reuse`.
Build/control/discovery run outside an exclusive lease. `[MemoryDiagnoser, ShortRunJob]`
benchmarks cover eight methods at NameLength 8/128/4096: transition/lookup hit and
last-character miss, enumeration hit/miss, owned compilation and composed model use.
`--measure` separately reports ten inclusive warm allocation numerators/100,000
operations, owned compilation/100, and inclusive whole-heap retention/100 schemas;
it makes no overhead subtraction. Warm allocation nonzero is an actual failure.
The task-owned NoEmit probe checks exactly 24 cases and the single ShortRun job
(3 warmup/3 measurement iterations, one launch); it never builds or generates under
the lease. The prepared plan bounds allocation at 60 s, benchmarks at 900 s, and
whole lifecycle at 1050 s with checked direct phase exits, scoped group cleanup,
initial/final identity and no-child proof. Freeze includes full runtime source,
probe/sample source/config/generated files, actual binary closure, entire pinned
SDK/runtime/host closure and stable effective environment.
No timing/allocation measurements have yet run; measurement requires coordinator
prepared/frozen lifecycle and exclusive lease. Composition with the future tree
validator, broader hardware, AOT/trimming and release checks remain later owners.
