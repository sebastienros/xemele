# Nonvalidating unresolved content references

Owner: M2-undeclared-contract. Execution evidence is recorded separately in the
task/report summary. This document freezes a subsequent implementation
contract, not a claim that these capabilities are shipped.

## Applicability

Normative basis: [XML 1.0 Fifth Edition 4.1](https://www.w3.org/TR/2008/REC-xml-20081126/#sec-references),
[3.1](https://www.w3.org/TR/2008/REC-xml-20081126/#sec-starttags),
[4.4.3](https://www.w3.org/TR/2008/REC-xml-20081126/#included-if-validating),
and [5.1](https://www.w3.org/TR/2008/REC-xml-20081126/#proc-types).

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
`XmlLocation Location`. Name is exact ordinal XML Name (not QName resolution);
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
Mutation, clone and import do not rewrite or copy this provenance. Detached handles
remain valid within their owner; new/imported documents begin with empty provenance.
This explicitly retains owners just as existing XmlNode handles do.

For `<foo>a&ent2;b</foo>`, children are Text(a), Text(b), occurrence boundary=1,
previous=Text(a), parent=foo. TextContent returns `ab`, the known character content,
and the document list exposes the gap rather than asserting a replacement was
empty. Consumers requiring complete expanded content must inspect the list.
No serialization/roundtrip promise is introduced for this parse provenance.
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

Existing `src/Xemele/Parsing/Events/Views.cs`, `Parsing/Dom/Factories.cs`,
`Parsing/Dom/ByteDomAdapter.cs`, public node enum/handles and existing API fragments
must retain their shapes. Runtime tests must exercise originals, all source splits,
byte mappings, pauses, cancellation, actual borrowed lifetime, old sink refusal,
mutation provenance and quotas. Contract model execution proves none of these
parser behaviors. Runtime acceptance/allocation/full conformance remain later
ordered-task evidence, not a dependency of this contract's model checks.
