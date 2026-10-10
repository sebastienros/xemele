# Nonvalidating unresolved content references

Owner: M2-undeclared-contract. Execution evidence is recorded separately in the
task/report summary. This document freezes a subsequent implementation
contract, not a claim that these capabilities are shipped.

## Applicability

Normative basis: [XML 1.0 Fifth Edition 4.1](https://www.w3.org/TR/2008/REC-xml-20081126/#sec-references),
[3.1](https://www.w3.org/TR/2008/REC-xml-20081126/#sec-starttags),
[4.4.3](https://www.w3.org/TR/2008/REC-xml-20081126/#included-if-validating),
and [5.1](https://www.w3.org/TR/2008/REC-xml-20081126/#proc-types).
The frozen namespace profile also applies
[Namespaces 1.0 Third Edition section 7](https://www.w3.org/TR/2009/REC-xml-names-20091208/#Conformance):
entity names are NCNames, excluding every colon even in a VC-only context.

For references outside an external subset or parameter entity, EntityDeclared
is a WFC when there is no DTD, only an internal subset without PE references,
or standalone=yes. Otherwise external subsets/PE references plus standalone=no
or absent make it a VC. Reading every declaration does not turn that VC into a
WFC. The five predefined entities are exempt. General declarations must precede
their use in attribute defaults; PE declarations precede PE use. Declaration
processing uses every declaration read through the first unread PE; afterward
entity/attribute-list declarations are not processed unless standalone=yes.
Direct/indirect external attribute references, unparsed content references and
recursion remain prohibited. Notification for a recognized unread external
parsed entity does not establish notification for an unknown name.

| Document context; content reference in document entity | Missing declaration |
| --- | --- |
| No DTD, any standalone value | EntityDeclared WFC rejection |
| Internal subset, no PE reference, standalone absent/no | WFC rejection |
| Internal subset, PE reference, standalone absent/no | VC-only unresolved occurrence |
| External subset, standalone absent/no, whether read or unread | VC-only unresolved occurrence |
| Any subset/PE configuration, standalone=yes | WFC rejection; external-markup dependency checks remain |

A PE declaration alone is not a PE reference. Track actual lexical references,
not whether expansion successfully produced an entity declaration. References
inside external subsets/PEs are outside the first WFC sentence's origin condition;
do not infer their classification from this document-content table. Track
declaration/reference origin and the first unread-PE boundary explicitly, including
internal PEs. Processed declarations retain first-declaration precedence and
standalone external-markup provenance. DTD prohibition, acquisition denial, quotas,
malformed reference/name/scalar, illegal nesting and cancellation keep their
existing outcomes. This contract grants no resource access or DTD validation.

Only unresolved *content* general references receive the representation below.
Attribute/default values cannot use content events or invented normalized values.
Their existing required WFC errors stay errors; a VC-only unknown attribute value
is a separate unsupported representation issue for the ordered implementation
owner, never evidence of an EntityDeclared WFC. Resolve that issue without silently
substituting empty text before claiming full corpus coverage.

## Exact original E13

The independently read local original is
`/private/tmp/xemele-corpus-cache/w3c-xml-20130923/resources/xmlconf/eduni/errata-3e/E13.xml`.
It declares internal `%pe` whose replacement declares `ent1`, references `%pe;`,
declares `foo ANY`, and uses `&ent2;` in `foo` content. There is no XML declaration
or external subset. Thus standalone is absent and the internal PE reference makes
missing `ent2` validity-only even though the PE was read. `ent1` must be processed;
it cannot supply `ent2`. The leading comment is not a declaration or exemption.
The original case `xmlconf/eduni/errata-3e/errata3e.xml#rmt-e3e-13` must remain
accepted in the existing applicability ledger, with an owned unresolved `ent2`
occurrence. No corpus exclusions, edits or expectation changes are authorized.
Independently verified SHA-256 (252 bytes):
`40470ff5f2281476ed4521e066d85122e9cc240f1fa916f97bd095644540fa14`.
Original case identity/accepted ledger expectation also match. The reference
begins at byte offset 239, line 7, scalar column 6, with source URI
`https://xemele.invalid/corpus/xmlconf/eduni/errata-3e/E13.xml`.
This is source/identity assessment, not runtime execution of the original case.

## Concrete event contract

Keep IXmlEventSink, XmlTextKind, ScanResult and the declaration/name/attribute/
namespace view ABI exactly unchanged. Add the internal optional interface
`IXmlUnresolvedReferenceSink` with one method
`bool UnresolvedReference(XmlUnresolvedReferenceView reference)`.
The internal `readonly ref struct XmlUnresolvedReferenceView` has get-only
`ReadOnlySpan<char> Name`, `ReadOnlySpan<char> LexicalReference`, and
`XmlLocation Location`. Name is exact ordinal NCName (no colon/QName resolution);
LexicalReference is the complete exact `&Name;`, and Location identifies its `&`.
Location uses the active source's URI/scalar line/column/original byte mapping;
character sources have null byte offset. No guessed enclosing-document location
for a reference physically read in another resource.

Accept and validate the complete reference before any callback. If a logical text
token precedes it, end that token first, even when that final callback pauses.
Deliver exactly one unresolved callback, then later text starts a new token. Never
emit StartText/TextFragment for the reference itself, even empty fragments. For
`a&x;&y;b`, observable order is completed text a, unresolved x, unresolved y,
completed text b. Splits through `&`, Name, surrogate pairs or `;` do not alter
identity/order. Pending reference storage is owned, bounded by TokenCharacters
and checked capacity, released after delivery; caller source spans are never held.

Borrowed views are valid through callback return only. Retention explicitly
copies Name/LexicalReference and the value location. A false return pauses after
the single delivery, counts submitted UTF-16 units already consumed, and drains
pending callbacks before new input. Resume requires the same sink, suffix and
sticky final flag. Cancellation is checked before callbacks/completion and at
the existing bounded work cadence; exceptions fault the operation, and disposal
clears pending owners. Cancellation after a paused delivery never replays it.

Old sinks compile and run unchanged for all previously representable inputs.
If this new occurrence reaches an old sink, reject with terminal Error diagnostic
`XML.UnresolvedReferenceSinkRequired` at `&`, before any invented replacement
event. This is an application representation capability failure, not a WFC or
validity report. Capable sinks accept the VC-only occurrence with **no diagnostic**.
Known declared entities still expand; rejected resources never become unresolved.

## DOM representation and proposed additive public surface

Use immutable parse provenance, separate from the mutable node arena. No new
XmlNodeKind or altered XmlNode/attribute/namespace representation is needed.
Proposed public types, modeled only in task-owned fixtures:

```csharp
public sealed record XmlUnresolvedReference(
    string Name, string LexicalReference, XmlLocation Location,
    XmlNode Parent, XmlNode PreviousSibling, int ChildBoundary, int Sequence);
// additive member on the existing partial document:
public IReadOnlyList<XmlUnresolvedReference> UnresolvedReferences { get; }
```

Every parsed DOM document exposes a non-null read-only list, empty ordinarily;
each occurrence owns its strings and anchors to the containing element and the
last completed child (default when none). ChildBoundary is the number of completed
direct children at delivery, not a UTF-16 offset. Sequence is the zero-based global
occurrence order with checked Int32 capacity. Consecutive occurrences may share
one boundary; Sequence distinguishes them. Text before/after a reference must
remain distinct children so no gap is lost to coalescing. Parent and PreviousSibling
are stable document handles; location and boundary describe the **original parse**.
Mutation does not rewrite provenance. Detached handles remain valid within their
owner; newly constructed documents begin with empty provenance. Copy and output
operations follow the explicit refusal policy below, so they cannot discard gaps.
This explicitly retains owners just as existing XmlNode handles do.

For `<foo>a&ent2;b</foo>`, children are Text(a), Text(b), occurrence boundary=1,
previous=Text(a), parent=foo. The known text nodes retain values `a` and `b`; no
reference is represented as empty replacement text. Aggregate text projection
and output/copy operations follow the refusal policy below. Consumers can inspect
the list and individual known nodes without a fabricated expansion.
Persisted names participate in InternedNames; reference tokens in TokenCharacters;
original primary scalars in DocumentCharacters; no unknown replacement scalars
are charged to ExpandedCharacters. Occurrence capacity is checked before copying;
quota/capacity refusal is terminal and does not expose partial success.

The DOM sink implements the optional callback and copies before returning. Normal
Parse/TryParse/Load accepts these occurrences and TryParse returns true/document
with empty diagnostics. Factories.Finish stays unchanged: its current any-diagnostic
failure remains correct because no warning is used as provenance. ByteDomAdapter
also needs no warning transport or new successful diagnostic channel. Existing
fatal/recovery behavior and XmlParseResult invariants remain untouched.

## Lossy-operation refusal policy

One conservative, owner-wide rule applies whenever the source owner document's
UnresolvedReferences.Count is nonzero: XmlSerializer.Serialize (string or Stream),
XmlDocument.ToXmlString/Save, XmlNode.GetTextContent, XmlNode.Clone (deep true or
false, including DocumentNode), and XmlDocument.ImportNode refuse with
InvalidOperationException, message `Document contains unresolved entity references.`
The refusal happens after existing argument validation/cancellation at entry and
before output/BOM writes, destination mutation, copy allocations or traversal.
Import checks the **source owner**, not the destination. A clean source can still
be imported into an unresolved destination, whose provenance remains intact and
whose later output remains guarded. Cloning even an unrelated/detached child of
an unresolved owner refuses; this deliberate conservative rule avoids uncertain
scope inference after mutation. Removing/replacing text or elements never clears
parse provenance or implicitly enables output. No lossy opt-in is introduced.

Fully represented source documents retain their existing serialization, shallow/
deep clone/import and GetTextContent behavior. Inspecting known leaf Value and
the provenance list is allowed; those explicitly expose known data plus gaps,
not complete expanded content. No successful serializer may emit
`<foo>ab</foo>` from the unresolved example. Clone/import must not produce an
apparently complete tree by erasing its provenance. The policy needs no public
signature, node-kind, options or baseline changes beyond the proposed provenance.
It is an implementation requirement in this milestone, not a deferred serializer
feature. Scripted contract controls prove the refusal rule only; runtime guards
must be tested at their true output/copy entry points by the subsequent owner.

## Ordered implementation request

Before runtime edits the coordinator must approve the exact additive public
record/member fixture and create an ordered task owning these paths:

- `src/Xemele/Parsing/Events/UnresolvedReferences.cs`: optional internal ABI.
- `src/Xemele/Parsing/Core/`: applicability/origin/read-state integration,
  unresolved pending callbacks, text boundaries, quotas and source locations.
- `src/Xemele/Parsing/Entities/` and `src/Xemele/Parsing/Dtd/`: declaration-read
  provenance and PE-reference tracking, without changing frozen model shapes.
- `src/Xemele/Tree/UnresolvedReferences/`: additive document provenance and DOM
  partial callback in Parsing.Dom, using current private parent state.
- Focused runtime tests and allocated/performance evidence under that task's
  assigned paths; contract fixture synchronization and one additive API fragment
  under their explicit owners. No current baseline entry is changed here.

The precise existing runtime files needing review/ordered ownership are
`src/Xemele/Parsing/Core/XmlParserCore.cs` (reference/delivery states),
`src/Xemele/Parsing/Core/XmlParserCore.Doctype.cs` (subset applicability),
`src/Xemele/Parsing/Core/XmlParserCore.Entities.cs` (active-source provenance),
`src/Xemele/Parsing/Dtd/Grammar/Parser/DtdParser.cs` and `Composition.cs`
(actual PE reference/read tracking), and
`src/Xemele/Parsing/Entities/Runtime/XmlEntityContext.cs` (processed-declaration
provenance). Prefer additive internal state/hooks over frozen model changes.
New public source is `src/Xemele/Tree/UnresolvedReferences/XmlUnresolvedReference.cs`
and `XmlDocument.UnresolvedReferences.cs`; the DOM partial is
`src/Xemele/Tree/UnresolvedReferences/DomSink.UnresolvedReferences.cs`.
Assign runtime tests `tests/Xemele.Tests/Xml/UndeclaredReferences/`, benchmark
sources `benchmarks/Xemele.Benchmarks/UndeclaredReferences/` and approved additive
baseline `docs/api/M2-undeclared-content.txt` only after exact fixture review.
These are ownership requests, not edits made by this contract task.
Also assign `src/Xemele/Serialization/Xml/XmlSerializer.cs` (Prepare before Output),
`src/Xemele/Tree/Document.cs` (TextContent, Clone, ImportNode/Copy preflight), and
tests for forwarding `src/Xemele/Serialization/Document/Save.cs` and
`src/Xemele/Tree/Node.cs` APIs. The forwarding files need no signature edit;
implementation can centralize their refusal in current callees. Add scoped
stream-no-write and destination-no-mutation tests to the ordered runtime task.

Existing `src/Xemele/Parsing/Events/Views.cs`, `Parsing/Dom/Factories.cs`,
`Parsing/Dom/ByteDomAdapter.cs`, public node enum/handles and existing API fragments
must retain their shapes. Runtime tests must exercise originals, all source splits,
byte mappings, pauses, cancellation, actual borrowed lifetime, old sink refusal,
mutation provenance and quotas. Contract model execution proves none of these
parser behaviors. Runtime acceptance/allocation/full conformance remain later
ordered-task evidence, not a dependency of this contract's model checks.

## Incomplete attribute/default values — M2 additive contract

Owner: M2-undeclared-attribute-contract. Exact declarations are the nonpackaged
[proposal](../../../tests/Contracts/Fixtures/M2/UndeclaredAttributes/Proposal.cs.fixture).
This section resolves the representation obligation above; it neither ships an
API nor establishes parser conformance. Existing content events/provenance and
all current public signatures, node kinds, sink/views and DtdModel/declaration
shapes remain unchanged. No placeholder reaches the library or API baseline.

### Applicability and declaration processing

Apply XML 1.0 Fifth Edition sections 4.1, 3.3.3 and 5.1 to the **physical origin
of each reference**, not merely the enclosing start tag. For a missing general
reference outside an external subset/PE: no DTD, an internal-only subset with
no actual lexical PE reference, or standalone=yes requires EntityDeclared WFC
rejection. An external subset (read or unread) or actual PE reference with
standalone absent/no makes missing declaration VC-only. A PE declaration is
insufficient. Reading all sources does not promote VC to WFC. References inside
external subsets/PEs are excluded from the first WFC sentence even if the caller
is standalone=yes; do not reuse a document-attribute table for these origins.
Standalone external dependency checks apply when a declaration is actually used.
Predefined/numeric references keep their specified handling.

Process declarations read before the first unread PE, with first-declaration
precedence and internal-before-external processing. Without standalone=yes,
entity/ATTLIST declarations after that boundary never enter the effective table,
produce default events, allocate provenance, or resolve previously unknown names;
lexical grammar/WFC/resource checking still applies. Standalone=yes continues
processing as required, retaining origin/external dependency checks. Actual PE
reference/read state is tracked independently of whether any declaration results.
Unread external sources never grant permission to acquire denied resources.

Direct general references in attribute defaults must have preceding declarations
(section 4.1 WFC's separate order sentence); a declaration actually read later
cannot retroactively satisfy that order. Reject an actual direct forward
reference with DTD.InvalidDefault, including VC-only missing-name contexts.
Indirect general-reference and PE-declaration ordering also has the explicit VC
sentence: in a VC-only regime preserve an unresolved indirect use rather than
using a later declaration to pretend normalization was complete. Where the WFC
applies, reject unknown references. An absent, unread, or suppressed declaration
in a VC-only context is **not proof of a known forward declaration**: preserve
its unknown identity. Validate the effective declarations even if an element
never uses a default or an explicit attribute overrides it. Do not defer all
checks to a final merged entity table. The owning implementation must retain
reference/declaration order and physical source mapping through internal sidecars;
no frozen DTD public/model constructor changes are needed.

The seven owned UTF-8 fixtures preserve authored assessment identities
`unknown-attribute-0` through `-6`. Rows 0/1/3 are required WFC controls; 2/4/5/6
are explicit/DTD-default VC-only capability gaps. Rows 5/6 use exactly the owned
`urn:subset` resource text/hash in authored-identities.json. Historical product
`1.0.0+588ae1cbcd74b2a585b8f779221baf88db389c5f` rejected all seven with
XML.UndeclaredEntity; only the three WFC rows are correct expectations. These
are authored inputs, not original corpus IDs. No accepted original attribute gap
was identified in the separate nine-gap corpus assessment. The 2585 original
cases, 3386 resource identities and applicability ledger remain unchanged.

Malformed reference/name/scalar, literal `<`, direct/indirect external or unparsed
attribute entities, recursion, resource denial, DTD prohibition, quotas and
cancellation retain terminal outcomes. An unknown part never short-circuits the
remaining literal or known replacement graph: later forbidden/scalar/cycle errors
still reject before callbacks for that tag. Known values retain existing complete
CDATA/tokenized normalization. NCName entity-name restrictions still apply.

### Owned symbolic value and exact internal event

The proposed `XmlAttributeValuePart` union has sealed known-text and unknown-
reference records. Known Text is **stage-one attribute normalization** (literal
whitespace mapped to spaces, numeric character-reference whitespace retained,
known internal replacements expanded with their normal rules). It is not an
independently tokenized normalized string. Ordered unknown parts own exact Name,
`&Name;` LexicalReference and source-mapped opening `&` Location. LexicalReference
is the exact recognized reference token in the active replacement stream; when
entity-declaration numeric/PE processing assembled that token, it need not be
a contiguous physical source substring. Preserve its opening scalar's original
source mapping (for a numeric-produced ampersand, that numeric reference's `&`),
rather than inventing a physical `&Name;` range. IsTokenized records the
processed declaration's non-CDATA type. Tokenized trim/collapse is deferred for
the **whole** value until all gaps are known: never trim/collapse each island or
join islands across gaps. There is no complete-normalized-value member.
For `a='  A &x; B&#x9;'`, known parts are `  A ` and ` B\t`; the gap is x,
and no aggregate string can be inferred for either CDATA or tokenized form.
Adjacent unknowns remain separate parts; empty known parts need not be stored.

LexicalValue owns the entire quote-excluded literal **as physically authored**,
before newline/character/entity-reference normalization; quote delimiter style
is not promised. Nested known entities may introduce gaps not spelled in that
outer literal. Part locations identify their physical declaration/replacement
source, with correct URI/line/scalar column/byte offset or null for character
input; never borrow the enclosing attribute location as an invented origin.
Known runs split at physical origin changes. For normalized CRLF use the CR's
location; for numeric/predefined expansion use its `&`. DTD lexer sidecars retain
exact literal spelling and source mapping, including PE/entity declaration paths,
before their normal model transformations. The applied-default ValueLocation and
LexicalValue remain those of its declaration, not the element's opening tag.

Add only optional internal `IXmlIncompleteAttributeSink` with two bool callbacks:
`IncompleteAttributeDefault(XmlIncompleteAttributeDefaultView)` and
`IncompleteAttribute(XmlIncompleteAttributeView)`. The first announces each
**effective incomplete default declaration once**, in processing order, after
complete DTD/effective-default validation and before the first element callback;
it also covers unused/overridden defaults. DeclarationSequence is checked,
zero-based order among incomplete defaults. No callback is promised for a
suppressed/losing duplicate declaration. The second replaces exactly one ordinary
Attribute callback for that same lexical attribute position; no empty/partial
XmlAttributeView.Value event accompanies it. Name is fully namespace resolved.
DefaultDeclarationSequence is null for explicit values, otherwise identifies a
previously delivered incomplete-default event. Its parts are the immutable
same declaration interpretation, not a second expansion or quota charge.

Validate the complete start tag, namespace bindings, duplicate expanded names,
known replacement graph, quotas and optional sink capability **before** its
StartElement callback. Known explicit ordinary attributes retain source order;
merged default attributes follow in effective declaration order; each position
emits exactly one complete or incomplete attribute callback. Namespace callbacks
remain first, then ordinary callbacks, then StartElementContent. Default events
occur at the DTD boundary without inserting content/text events. An old sink
encountering either new event rejects with terminal Error
XML.IncompleteAttributeSinkRequired at the first unknown part's `&`, before
partial callbacks for that affected tag/default batch. This is representation
capability, not WFC/validity. A capable sink accepts ordinary gaps without
successful diagnostics; TryParse/Finish/result invariants remain unchanged.

Both views are readonly ref structs. Name/LexicalValue and Parts spans borrow
only through callback return. Parts refer to immutable, already owned record
strings and locations; retaining the span/array is forbidden, but copying the
part sequence and retaining those immutable records is safe. No ReadOnlyMemory
or rented-owner lifetime promise is added. The allocation cost of owned symbolic
parts is explicit; no zero-allocation runtime claim is made here. Pending event
owners are bounded and released on delivery/fault/disposal. A false return pauses
after one delivery; next Append drains pending callbacks before new source,
using the same sink/unconsumed suffix/sticky final flag. Cancellation before
callbacks/completion and at existing bounded work cadence never replays a paused
event. Callback exceptions fault the operation. Every source split, including
CRLF/surrogates/reference delimiters and nested declaration boundaries, must
preserve identical identity/order/locations. Complete-default and content events
keep their existing semantics.

### Namespace participation and DOM guards

Processed complete defaults, including xmlns, participate before name resolution
as today. Any **applied** incomplete xmlns/default binding rejects with Error
XML.UnresolvedNamespaceValue at its first unknown reference before StartElement;
it is a namespace representation capability failure, not EntityDeclared WFC.
No invented URI, inherited-binding fallback or partially resolved element/ordinary
attribute event is allowed. An overridden/unused incomplete namespace default
can still be retained as declaration provenance without establishing a binding.
Other duplicate/reserved/unbound prefix errors retain their existing diagnostics;
no namespace-disabled mode or new parse option is introduced.

Every DOM document exposes two nonnull read-only provenance snapshots:
IncompleteAttributeDefaults and IncompleteAttributeValues, ordinarily empty.
The former owns lexical declaration names/value, type/external flags, declaration
and literal locations, immutable part snapshot and declaration sequence.
The latter anchors each applied ordinary value to a real XmlAttribute handle and
its Parent, owns its lexical value/location/parts and checked zero-based global
applied-value Sequence, and links Default to the exact declaration record when
applicable. Explicit values have null Default. Ordinary attributes still count
and enumerate with exact names/order/owner/attachment and participate in duplicate
checking; no omitted member or fake empty value is permitted. Internal member
storage may hold null exclusively for incomplete values with matching sidecar
provenance; existing public string-valued shapes remain unchanged.

XmlAttribute.Value getter and XmlNode.GetAttribute for an incomplete member throw
InvalidOperationException with message `Attribute value contains unresolved entity
references.` Known unrelated attribute getters keep their values. Setters/removal
keep original immutable parse provenance, even after detachment/replacement;
conservatively a handle recorded as incomplete keeps refusing its Value getter.
Consumers inspect the symbolic parts/provenance instead. Newly built documents
have empty lists. Anchors retain owner lifetime like existing handles. The DOM
sink copies spans/part sequence before callback return, never fabricates normalized
Value and never promotes unknown data into IDs/type-dependent lookup.

Extend the current centralized **owner-wide** output/copy/text refusal to a source
owner with content gaps OR either incomplete-attribute list nonempty (including
unused default declarations). Preserve its current exception/message
`Document contains unresolved entity references.` and argument/cancellation-first
entry behavior: XmlSerializer.Serialize(string/Stream), ToXmlString/Save,
GetTextContent, Clone(shallow/deep/DocumentNode) and ImportNode must refuse before
BOM/output writes, traversal, copy allocations or destination mutation. Inspecting
known leaf/node/complete attribute values and provenance is allowed. Clean sources
can still import into an incomplete destination; its provenance/output guard
persists. Removing/mutating all anchors never enables output. No lossy opt-in,
serializer lexical forwarding, or apparent complete copy is introduced. Existing
forwarders use their actual centralized callees; tests must exercise those callees.

TokenCharacters bounds physical literal/pending symbolic retained text and each
expanded attribute's logical stage-one size before copying, with checked UTF-16
capacity as well as scalar quotas; unknown lexical reference scalars participate
in bounded storage but contribute no invented replacement to ExpandedCharacters.
DocumentCharacters charges primary authored scalars once; actual known expansion
work uses existing aggregate expansion/depth budgets once at its owner boundary.
Static validation alone uses no replacement-output expansion charge. Constructing
known parts of an incomplete default is actual bounded expansion work, charged
once at declaration interpretation even if unused; applying its immutable
interpretation neither reruns nor recharges that expansion. Fully known default
values retain existing use-site accounting. Persisted names
use InternedNames; explicit and applied default members use AttributesPerElement
once (including namespaces). Default/part/provenance collections and Sequence
have checked capacity; refusal is XML.ResourceLimit before retention/delivery,
not successful partial output. Runtime allocation evidence must measure retained
part/literal/location sidecars and empty-list ordinary paths explicitly.

### Required ordered runtime owner

Coordinator must create an owning implementation prerequisite of M2-conformance
join. This contract alone never fulfills capability or conformance acceptance.
Start runtime work only from the integrated verified contract and current Core
standalone/DTD repair; source-only grammar assessment is no peer dependency.
Order: (1) applicability/order/origin and sidecars; (2) bounded normalization and
pending optional events; (3) DOM members/provenance/guards; (4) real transport,
forwarding/negative/quota/allocation checks plus additive API/fixture integration;
(5) verify/integrate, then the final grammar/conformance join. Grammar boundary
repairs must be integrated before that join; if both touch DTD lexer/value files,
serialize ownership on verified revisions. No recursive worker or later milestone.

Smallest concrete requested runtime ownership (requests, no edits here):

- New src/Xemele/Parsing/Events/IncompleteAttributes.cs; Core/XmlParserCore.cs,
  XmlParserCore.Attributes.cs, XmlParserCore.AttributeExpansion.cs,
  XmlParserCore.Doctype.cs, XmlParserCore.Resources.cs, XmlParserCore.Entities.cs
  for pending tag/default delivery and effective provenance integration.
- Dtd/Grammar/Parser/DtdParser.cs, Lexer.cs, Declarations.cs,
  DefaultReferences.cs (literal processing is in Declarations.cs/Lexer.cs), and
  Entities/Runtime/XmlEntityContext.cs for internal sidecars/reference ordering.
  Keep DtdModel/DtdAttributeDeclaration/event views frozen.
- New Tree/IncompleteAttributes/XmlIncompleteAttributeValues.cs,
  XmlDocument.IncompleteAttributes.cs and DomSink.IncompleteAttributes.cs;
  Tree/XmlAttribute.cs and Tree/Document.cs for nullable internal backing/guarded
  getter and centralized RefuseUnresolvedReferences extension; Tree/Node.cs solely
  for GetAttribute, which currently reads AttributeSlots[i].Value directly.
  Serialization/Xml/XmlSerializer.cs requires only review if the existing
  centralized guard reaches both real output entry points; no redundant write
  ownership requested. Serialization/Document/Save.cs stays forwarding.
- tests/Xemele.Tests/Xml/IncompleteAttributes/, benchmarks/Xemele.Benchmarks/
  IncompleteAttributes/, docs/reports/M2-undeclared-attributes-runtime/,
  samples/M2-undeclared-attributes-runtime/; approved new docs/api/
  M2-undeclared-attributes.txt and fixture synchronization explicitly owned then.

Required runtime tests include seven authored inputs, origin/order/first-unread
matrix, unused/overridden defaults, direct/indirect gaps and later forbidden
errors, exact known normalization (literal versus numeric whitespace), multiple
adjacent gaps, namespace complete default/applicable refusal, enumeration/getters,
all character/byte/entity splits and mappings, every callback pause/cancel/fault,
old sinks, exact/one-over quotas, mutation/detachment, real no-write stream/BOM
and no-mutation copy/refusal entry points. Existing 290-member ABI and content
behavior remain passing. Model checks below do not execute any of these runtime
paths. No incompatible public/ABI decision is required by this additive proposal.
