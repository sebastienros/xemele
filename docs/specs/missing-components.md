# Missing schema components

Internal availability representation and incomplete-assessment behavior.

The pinned [Second Edition Structures](https://www.w3.org/TR/2004/REC-xmlschema-1-20041028/#noss)
allows retained unresolved references and distinguishes missing required components
from ordinary invalidity. The interpretation here is narrow. Sections 3.3.1–3.3.2
are the reference for element typing; section 5.3 informs nonassessment. Mapping
nonassessment to the project result bits below is a project contract, not a W3C
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

The availability model defines immutable entries and closure edges:
reference ID; owner kind (element declaration, simple type or general type
definition)/expanded name/stable declaration ID where applicable; reference
kind; symbol space; authored expanded QName; original source location; and
Resolved/Unavailable state. ComponentAvailability snapshots entries and closure
edges into private arrays exposed through read-only wrappers. Entries are sealed,
get-only values. ComponentIdentity/DependencyEdge are readonly values. Edges name
the consumer and trigger (type assessment, substitution selection, schema value
constraint or derivation), rather than treating all references as unconditional
failure. Runtime validates IDs and charges traversal, retained entries and edges
**before** allocation/growth. No contract hot-path allocation claim is made.

Component lists retain the charged SchemaSnapshots behavior; a compile-time
model does not replace runtime snapshots.

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
After xsi:type selects the actual type, its TypeDefinition availability closure
must also be checked before consuming any deferred content program, even for an
undeclared element with no declaration ID. A complex type C with choice h/m can
have indeterminate substitution overlap and be selected solely through xsi:type;
that case must stop before the program is consumed, while an unrelated assessable
root remains valid. This complements the pre-replacement required declaration
check; xsi:type cannot rescue an unavailable declared type.
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
IDs, files and hashes in the original corpus: missing001.xsd,
missing002.xsd (g1's schema test is misleadingly named missing001.xsd),
missing003.xsd, missing006.xsd and their original i0 inputs. Their expectations are preserved independently of implementation status.
Four i1 cases are separate nonassessment controls and earn zero original/gate
pass credit. g0/g2/g5 original INVALID stays INVALID; g1/i1 original VALID stays
VALID, pending separate unresolved-head mapping adjudication. Do not claim twelve
passes, rewrite the oracle, or create gate exceptions. Corpus expectations remain independent of runtime output.
