# Missing components: approved internal representation, source review candidate

Task: M8-xsd-missing-components-contract. Starting claim: d12be860e118b8ce97d383a0511ff1167f8c4bab.
Human choice: `Allow the internal nullable-type migration`, coordinator reply
call_506dc1f8825e4bd09a41087a0e685ae8, recorded in
`docs/orchestration/M8-xsd-missing-components-contract-pending.json` (approval
record ade1e7aa3). This freezes a candidate for review. Restore, compilation,
metadata extraction, lifecycle controls and runtime checks are **UNRUN**. The
contract task stays in_progress; no conformance or allocation credit exists.

The pinned [Second Edition Structures](https://www.w3.org/TR/2004/REC-xmlschema-1-20041028/#noss)
allows retained unresolved references and distinguishes missing required components
from ordinary invalidity. The interpretation here is narrow. Sections 3.3.1–3.3.2
are the reference for element typing; section 5.3 informs nonassessment. Mapping
nonassessment to the project result bits below is a project decision, not a W3C
requirement for an `IsComplete` property. Unresolved-head g1/i1 remains a separate
standards adjudication, with its original VALID expectation unchanged.

## Exact internal change and representation

Change only the third positional field of the sealed internal
`XsdElementDeclaration` record: `SchemaName Type` becomes `SchemaName? Type`.
`SchemaName` is a readonly value record: this is `Nullable<SchemaName>`, an actual
CLR constructor/deconstruct signature change. It is not merely nullable-reference
annotation. The property ID is stable; its return type changes. Constructor and
Deconstruct IDs change. Every other positional field, record member, dense
Elements index, GlobalElements ID and XsdElementTerm ID stays unchanged.
`XsdCompiledSchema` and all frozen IXsd* interface signatures stay unchanged.
The public XmlSchema surface stays unchanged. Availability is carried explicitly
by new concrete internal joins; it is not hidden global/cache/operation state.

| Authored element state | Candidate Type | Retained availability reference |
| --- | --- | --- |
| Explicit type resolves | Authored expanded QName | Resolved explicit type |
| Explicit type absent | Authored expanded QName | Unavailable explicit type, type symbol space |
| Inline type | Actual owned anonymous type QName | Its real dependency closure |
| No type, inline type or substitutionGroup | xs:anyType | Ordinary default, no absent inference |
| No type/inline type; resolved head | Actual inferred type QName | Head plus transitive type dependencies |
| No type/inline type; absent head or unresolved inferred chain | null | Unavailable inference, head element QName, original reference location |
| Explicit/inline type plus absent substitution head | Actual selected type QName | Separate missing affiliation; no null type or invented head type |

A direct list keeps its authored ItemType QName even when unresolved. Do not
borrow the missing head's element QName as a type QName, emit default(SchemaName),
create synthetic missing definitions, drop declarations/IDs, or fill anyType into
an unavailable inferred type. Resolve all permitted include/import/composition
inputs and declaration order before sealing availability. Publication never
retains XmlDocument/XmlNode, source namespace maps, resolver, operation, mutable
cache or executable stand-in. Resolved wrong-kind definitions remain errors.

The owned Availability.cs.fixture freezes immutable entries and closure edges:
reference ID; owner kind/name/stable declaration ID where applicable; reference
kind; symbol space; authored expanded QName; original source location; and
Resolved/Unavailable state. ComponentAvailability snapshots entries and closure
edges into private arrays exposed through read-only wrappers. Entries are sealed,
get-only values. ComponentIdentity/DependencyEdge are readonly values. Edges name
the consumer and trigger (type assessment, substitution selection, schema value
constraint or derivation), rather than treating all references as unconditional
failure. Runtime validates IDs and charges traversal, retained entries and edges
**before** allocation/growth. No contract hot-path allocation claim is made.

The snapshot fixture is deliberately nonpackaged and compile-only. Runtime must
retain existing charged SchemaSnapshots behavior for component lists. The copy of
the original positional fixture does not replace runtime snapshots.

## Consumption and independent checks

A consumed required unavailable definition stops assessment with IsComplete=false,
IsValid=false, one Fatal `XSD.MissingComponent`, through existing Refuse/diagnostic
machinery. Primary location is the original instance consumption location; bounded
explanatory data identifies the retained schema reference kind/QName/location.
Do not fabricate unknown line/column/byte coordinates. Poll before lookup and
before successful publication; charge every closure inspection/edge even with
null work/component caps. Existing quota/cancellation refusal takes precedence.
Preserve exactly-at-cap/one-over diagnostic behavior and primary exceptions.

The availability check precedes selecting a content cursor, parsing a typed value,
applying default/fixed constraints, xsi:type replacement, xsi:nil bypass and any
identity success hook. Explicit unresolved type and unresolved inferred type are
required for direct/local/ref and known strict/lax declarations. A lax wildcard
may skip an actually absent declaration; it must not mistake a present unavailable
declaration for absence. A true skip wildcard does not consume that declaration's
type and remains skipped; independent instance processing/quotas still apply.
An explicitly typed declaration with absent affiliation can assess its direct
root if assessment never uses affiliation. Substitution/derivation consumption
must consult the separate affiliation dependency. No runtime resolver call,
recompilation, source mutation or fallback definition may discharge absence.

Schema compilation still completes all independently decidable checks, including
unused, anonymous and displaced redefine carriers. Missing base types are not in
this deferral: preserve missing004/missing005. Preserve syntax, QName/NCName and
unbound-prefix checks, local import authorization, mandatory redefine, cycles,
wrong-kind, final/block, valid known derivations, required attributes, independently
invalid facet grammar/regex/default constraints, UPA, EDC and identity syntax/known
keyref checks. Do not skip an entire declaration or datatype family. A known bad
construct next to a missing reference must still reject compilation.

A dependent check is recorded as a specific immutable closure edge and deferred
only if its required operand really is unavailable. Examples: list item atomicity
and item final need a resolved item; typed enumeration/default parsing needs the
actual datatype; a substitution type relation needs both endpoints; EDC needs
actual type identity when inference is unknown. Equal nulls are not proof of EDC.
Name/particle overlap UPA checks still run when independent of unknown substitution
membership. If an unavailable affiliation makes expanded content choices or a
restriction relation indeterminate, retain that exact program/check dependency and
refuse when the instance consumes it. Do not manufacture a narrower program and
pretend its checks established the complete relation. Existing complete-graph
interface lanes remain strict unless their concrete overload explicitly carries
availability; a caller cannot pass the partial graph alone and obtain assessment.
Implementation must demonstrate these joins before expanding beyond the narrow
explicit-element/list-item/head-inference families.

Denied/Unavailable resource outcomes and missing components are distinct. A
permitted optional missing resource records no document/cache entry and cannot
relax namespace permission. Denials, malformed resources, caller/resolver/I/O/
mutation exceptions retain their existing handling, cancellation and exactly-once
resource disposal. Caller streams stay open. Publication copies provenance and
contexts; validation results remain isolated under concurrent schema reuse.

## Original corpus boundary

The unchanged schema/good-instance pairs g0, g1, g2 and g5 under
`saxonMeta/Missing.testSet` are eight future positive controls. Preserve original
IDs, files and hashes from the pending approved record: missing001.xsd,
missing002.xsd (g1's schema test is misleadingly named missing001.xsd),
missing003.xsd, missing006.xsd and their original i0 inputs. They are UNRUN here.
Four i1 cases are separate nonassessment controls and earn zero original/gate
pass credit. g0/g2/g5 original INVALID stays INVALID; g1/i1 original VALID stays
VALID, pending separate unresolved-head mapping adjudication. Do not claim twelve
passes, rewrite the oracle, or create gate exceptions. Corpus observation/status
records remain owned by M8-xsd-corpus/coordinator.

## Ordered later implementation and checks

The exact source assessment/ownership requests are in runtime-migration.json.
Facet Build currently resolves a list item before CompileFacets; blanket skipping
ValidateDefinition is therefore insufficient. Request exact Definitions.cs and
Evaluator.cs ownership through an ordered repair, or prove an owned new concrete
independent-check extension sufficient. No facet/runtime path is automatically
authorized, and no partial executable FacetPlan may be published.
The coordinator must create a new runtime task depending on **verified integrated**
M8-xsd-import-visibility (including its current annotation repair), this contract,
and M8-xsd-instance-hint-ordering. Existing import ordering follows optional
composition/particle/facade repairs; hint ordering follows content failure. Do not
start from or import an unmerged peer. Reassess those tasks on current main when
claiming; this starting revision only supports read-only scope assessment.

Runtime must prove normal product and true XML-only consumers; every affected
frozen/internal/additive/public projection; direct/global/local/ref strict/lax/skip;
substitution selection, xsi:type/nil, facets/default/fixed and identity hooks;
partial closure stop; order permutations and permitted later resolution; unrelated
valid root; independent syntax/namespace/regex/facet/UPA/EDC/cycle/derivation/final/
block/default/identity negatives; original eight positives/four nonassessment;
missing004/005; quota exact/one-under and independent counted edges; cancellation
in long null-cap walks; diagnostic caps/precedence; provenance/disposal/source
mutation/concurrency; and zero validation resolver attempts. All are UNRUN.

Allocation checks belong only to the later owned compile/availability/diagnostic/
result paths. Require cold initialization, disabled and positive controls,
consumed outputs/retained objects, actual independent counters and reviewed finite
supervision. Preserve historical benchmark counters without claiming they measure
the new behavior. No timing claim, speculative borrowed-zero, or PARK/isolation
shortcut. Root handles actual counters after ordered ownership and source review.

Contract runner check.py stages isolated outputs under ignored task artifacts,
uses pinned 10.0.400/10.0.11 offline restore, compiles old/candidate/negative usage
and runs only the copied metadata extractor. No fixture methods execute. It must
prove exactly three changed emitted members, identical public/interface rows and
reject missing-member/nullability controls. Its source/lifecycle requires review
and release before any execution. See the report README for hard deadlines,
whole-family/parent-death cleanup and remaining lifecycle controls.
