# Input and parser contracts — M1

The additive nonvalidating unresolved-content-reference contract is frozen in
[M2 undeclared references](../decisions/M2/undeclared-references.md). It preserves
the existing sink/views and distinguishes VC-only identity provenance from
replacement character events and terminal diagnostics. This is a contract for
an ordered implementation, not evidence of shipped support.

Owner M1-contracts. Exact internal declarations are
[Input.cs.fixture](../../tests/Contracts/Fixtures/M1/Input.cs.fixture) and
[Syntax.cs.fixture](../../tests/Contracts/Fixtures/M1/Syntax.cs.fixture).
M1-input depends on common only; M1-syntax depends on common only, not input,
tree or DOM. Composition in M1-dom calls both. Namespace Xemele.Parsing.Input
contains IXmlInputDecoder/XmlInputDecoder; Xemele.Parsing.Syntax contains
XmlParserCore/IXmlEventSink. Nonpackaged declarations compile independently.
The approved M0 character scanning, ownership and pause rules remain binding.

## Byte decoder

XmlInputDecoder(XmlParseOptions options, Uri? sourceUri = null) validates/snapshots
options and owns bounded decoding state; sourceUri is null or absolute.
IXmlInputDecoder extends IDisposable. Decode takes borrowed byte input, writable
UTF-16 output, writable Int64 original-byte offsets, final flag and operation
cancellation token. Offsets span length must equal output span length; output
must have capacity at least two to permit one scalar pair. Bad capacities are
ArgumentException before state changes. Dispose is idempotent and releases scratch;
use after disposal throws ObjectDisposedException. State is single-operation,
not resettable or safe for concurrent/reentrant calls.

DecodeResult has ConsumedBytes, WrittenCharacters, DecodeStep and nullable
XmlDiagnostic. Counts refer only to this submitted call: original bytes consumed
and UTF-16 code units written, never scalars/normalized characters. Bounds are
0..input.Length and 0..output.Length. Each emitted UTF-16 unit gets its original
source scalar's first-byte offset; both surrogate halves share that offset.
Offsets are absolute from the start of the primary byte source, include skipped
BOM bytes in counting, are monotonic (duplicates allowed), and are meaningful
only in the written output slice. Owned incomplete carry keeps its original
position even when output appears in a later call. BOM yields no output units.
The decoder validates strict Unicode encoding but does not normalize CR/CRLF,
expand references, validate XML names/grammar or produce DOM storage.

| Step | Meaning / mandatory progress |
| --- | --- |
| NeedInput | Entire input consumed, nonfinal; retain only bounded unfinished encoding/detection state. |
| NeedOutput | Output cannot hold next whole scalar; input may have an unconsumed suffix. Resume with suffix and same final flag, fresh output. Must have consumed or written something, except when a caller supplies insufficient scalar capacity (rejected at entry). |
| Completed | Final source exhausted, carry flushed exactly once; terminal. |
| InvalidInput | Nonnull owned diagnostic, terminal rejection; no replacement output. |

Diagnostic is null for all other steps. Final flag is sticky once seen: resume
suffix/final=true until terminal, never final=false. NeedInput cannot follow
final exhaustion. Completed/InvalidInput followed by Decode throws
InvalidOperationException. A final zero-length source is legal and completes
UTF-8 detection; grammar later rejects no root. Error consumption stops before
an offending sequence when it is in the submitted slice, or counts its bytes
already copied into carry; counts never require negative rollback. Already
written valid prefix remains available but no success may be inferred. Invalid
encoding locations identify the first byte of the invalid sequence (incomplete
EOF identifies its start), using tracked normalized scalar line/column through
the valid prefix; they must not mislabel UTF-16 units as scalar columns.

Detection begins Undetermined. BOMs select UTF-8/UTF-16LE/UTF-16BE; without BOM,
XML initial `<`/`<?` UTF-16 byte signatures select LE/BE, otherwise UTF-8.
Recognition requires bounded at most four-byte prefix carry, even when split.
Unsupported UTF-32/EBCDIC signatures reject rather than appearing as UTF-8 text.
Encoding never changes after selection. UTF carry after detection is at most
three UTF-8 bytes, or odd UTF-16 byte plus high surrogate; a complete scalar is
not partly emitted. UTF-8 overlong, surrogate encodings, >U+10FFFF, invalid
continuations and incomplete EOF reject; UTF-16 unmatched surrogate and odd EOF
reject. XML's allowed scalar range is enforced by syntax even for character input.

ValidateDeclaration receives the encoding-name borrowed span and declaration
location, after lexical parsing but before any body event. It returns null or an
owned diagnostic; a nonnull result marks the decoder rejected and the caller
treats it as terminal parse rejection. ValidateDeclaration is allowed after
encoding detection, including successfully Completed decoding, but not after
rejection/disposal; it consumes no bytes and emits no callbacks. A declaration
may have been lexically buffered until decoder completion in a short source;
validation still precedes forwarding any body callbacks.
Empty encodingName means no declaration encoding. M1 accepts ASCII-case-insensitive
UTF-8, UTF-16, UTF-16LE and UTF-16BE only, with full equality (no alias/codepage
fallback). UTF-8 must match selected UTF-8; explicit LE/BE must match selected order;
generic UTF-16 must match selected UTF-16 and have a BOM. Signature-selected
UTF-16 without BOM requires a matching explicit LE/BE declaration. No UTF-16
BOM may silently switch based on a contradictory declaration. Non-UTF labels
use INPUT.UnsupportedEncoding; conflict uses INPUT.EncodingMismatch; malformed
bytes use INPUT.InvalidEncoding. These are M1 scope choices informed by
[XML encoding rules](https://www.w3.org/TR/REC-xml/#charencoding).
Composition buffers only bounded prolog data charged to TokenCharacters and
checks the declaration before decoding body bytes; no whole-source decoding.
The DOM byte adapter initially submits at most one new original byte per Decode,
draining any detection carry output first, and feeds the core the corresponding
characters/mappings. It retains at most the initial <?xml recognition prefix to
recognize declaration absence; once absence is known it validates empty encodingName
before further decoding. If a declaration is present, it continues byte-at-a-time
until the core Declaration callback validates it, then switches to bulk scratch
decoding. This simple bounded prolog path prevents a bulk Decode from rejecting
body bytes under an unvalidated declaration or reporting the wrong first error.
A future faster framing path requires equivalent boundary/error-order evidence.
Composition also calls ValidateDeclaration with empty encodingName when no
declaration is present, before the first non-declaration lexical/body event
(or at EOF for empty/BOM-only input); this rejects BOM-less generic UTF-16.
No declaration validation is done against a fabricated encoding for strings.

## Core append and source mapping

XmlParserCore(XmlParseOptions options, Uri? sourceUri = null) validates/snapshots
configuration and retains bounded unfinished tokens/open-element state.
Append<TSink>(ReadOnlySpan<char> input, ReadOnlySpan<long> byteOffsets,
bool isFinalBlock, TSink sink, CancellationToken cancellationToken) constrains
TSink to class, IXmlEventSink. No by-value mutable struct sink is admitted.
byteOffsets is empty for character input or exactly input.Length entries for
byte input. A stream/decoder adapter passes corresponding written slices.
Source mode cannot change within one core operation. SourceUri is null or absolute.
Negative/decreasing offsets reject as ArgumentException; paired-surrogate offsets
must be identical; duplicate offsets also accommodate bounded decoder carry.
The source owner, decoding counts and unconsumed suffix are the adapter's concern.
The core does not reference an input implementation or invent byte lengths.

ScanResult contains Consumed (UTF-16 units in the submitted slice), ScanStep
NeedInput/Paused/Completed/Rejected and optional XmlDiagnostic (nonnull exactly
for Rejected). Structured syntax/quota/DTD rejection is a result, not a catch of
XmlParseException; DOM converts it to a strict exception or TryParse diagnostics.
NeedInput consumes all submitted input and is forbidden after final exhaustion.
Paused counts input already accepted, even when callbacks remain for that accepted
token. Resume the unconsumed suffix (possibly empty) with final still true if it
was true, and the same sink identity. Never replay a delivered callback. Pending
callbacks from a bounded token are drained before consuming new source units.
A start tag is fully validated/namespaces resolved before its first callback;
its owned temporary token data is bounded by TokenCharacters/AttributesPerElement.
No partial malformed start tag is exposed as a successful element event.
Completed/Rejected are terminal; later Append throws InvalidOperationException.
Dispose is idempotent; use after disposal throws ObjectDisposedException.

The core validates pairs across character chunks, retaining at most one high
surrogate. It normalizes literal CRLF/CR to LF across chunks; a pending CR is
bounded state. Locations are one-based normalized Unicode-scalar line/columns,
not UTF-16 units; tabs count one, CRLF reports CR's byte offset. Each event location
is the opening delimiter/name or first source scalar of a fragment; expanded
reference fragments point at the &. EndDocument points at EOF (next scalar
position, original byte length for byte input). For an initial empty byte input,
EOF offset is zero; for BOM-only input it follows the BOM. Because no emitted
unit can supply EOF after a trailing BOM/carry, composition must provide the
exact original byte count through the separate CompleteByteSource method
specified below. Unknown offsets are null for character input. All counters are
checked; location overflow is XML.LocationLimit. DocumentCharacters counts source
scalars including markup before newline normalization; ExpandedCharacters counts
emitted reference expansion scalars. TokenCharacters counts normalized scalars.

CompleteByteSource(long byteLength) is called exactly once by composition after
successful decoder completion and before final=true Append for byte source.
It accepts a nonnegative absolute source length at least the last mapping offset,
retains a scalar byte count rather than source memory, and never emits callbacks.
It is also valid before the first Append for empty/BOM-only source. It is forbidden
for a known character-mode source or after final/terminal/disposed state.
No core completion is permitted for byte source without this marker; misuse is
InvalidOperationException. Character finalization needs no marker. This explicit
marker prevents inferring UTF-8/UTF-16 EOF from the last scalar's starting offset.

## Sink ordering and borrowed lifetime

All methods return bool: true continues, false pauses immediately after that
callback and before the next callback, including zero-input events. Ref-struct
views and ReadOnlySpan<char> are borrowed through callback return only; consumers
persist by explicitly copying. There is no ReadOnlyMemory or retained rented
array promise. No callback occurs after a structured rejection.

For a successful source, the sequence is StartDocument once, optional Declaration
once (VersionNum as specified below, lexical order version/encoding/standalone), prolog lexical
content, root/content, epilog lexical content, EndDocument once. StartDocument
can pause before consuming any input. Declaration gives borrowed version,
encodingName (empty if absent), nullable standalone and location; it does not
create a tree declaration node. Byte composition performs ValidateDeclaration
inside its forwarding callback before allowing body callbacks.

A start tag emits StartElement(resolved name, isEmptyElement, location), then
NamespaceDeclaration for explicit declarations in source order, then Attribute
for ordinary attributes in source order, then StartElementContent. Namespace
views are this tag's declarations, not inherited scope; element and attribute
views use the fully resolved scope, including declarations after a lexical
attribute. Default namespace applies to unprefixed elements, never ordinary
unprefixed attributes. The xml binding is implicit; reserved/duplicate bindings
and duplicate expanded attribute names reject. Namespace declarations count
against AttributesPerElement, are never Attribute callbacks, and are retained
by DOM separately. No required attribute-array allocation is imposed by the ABI.
An empty tag additionally emits EndElement, even if StartElementContent paused;
there is no synthetic empty Text. EndElement uses the matching resolved name
and closing-tag location (empty tags use the opening-tag location).

StartText(kind, target, location) begins one logical text/CDATA/comment/PI token;
target is nonempty only for PI. TextFragment can split arbitrarily at scalar
boundaries and finishes that token with tokenEnded=true (an empty final fragment
is allowed). Fragments may split references from adjacent text but cannot split
one scalar. Comment/PI/CDATA boundaries remain visible; text references are
expanded to predefined or numeric scalars. Literal attribute whitespace becomes
space after newline normalization; character references preserve referenced
TAB/LF/CR. M1 has no DTD types/defaults or attribute collapsing. Outer-root
whitespace is permitted and delivered as Text; DOM ignores it as document-node
children because it is outside the root, but preserves element whitespace.
A text token cannot be followed by a different token/event before its final
fragment, including pauses at any callback.

EndDocument is emitted after all final grammar/carry checks, exactly once. If it
returns false, next Append(empty, empty offsets, final=true, same sink, token)
returns Completed without another callback. Cancellation is checked before that
completion too. A pause can consume the entire input; it does not imply NeedInput
or completion. Callback exceptions (even XmlParseException), cancellation and
I/O propagate unchanged and fault their operation; later calls reject. Reentrant
or concurrent same-core use is InvalidOperationException; if thrown through a
callback it faults the outer operation. Cancellation checks occur at entry,
before callbacks/reads, and at most every 4096 source units or bounded work steps.
Changing sink identity or final flag after final=true is rejected without replay.

## Declaration version contract — M2 correction

[XML 1.0 Fifth Edition section 2.8, productions 23–26](https://www.w3.org/TR/REC-xml/#sec-prolog-dtd)
defines VersionNum as literal ASCII `1.[0-9]+`. A document declaration requires
VersionInfo first. Compatible `1.1`, `1.7`, `1.00`, `1.10` and longer digit
sequences are accepted under the effective XML 1.0 Fifth Edition / Namespaces
1.0 Third Edition profile; their lexical value does not activate XML 1.1.
The specification recommends `1.0` for authors; that recommendation does not
change the processor's acceptance grammar. Numeric conversion, fixed length,
Unicode digits, signs, whitespace inside the value, references and normalization
of leading zeros are forbidden. XML 1.1-only content remains rejected under
XML 1.0 rules, including references to forbidden control characters and prefixed
namespace undeclarations. NEL/U+2028 do not gain XML 1.1 newline normalization.

[Section 4.3.1, production 77](https://www.w3.org/TR/REC-xml/#sec-TextDecl)
uses the same VersionInfo optionally in external parsed-entity text declarations:
version, when present, precedes mandatory encoding; standalone is forbidden.
The effective profile also applies to external subsets and general/parameter
entities. A missing text-declaration version remains null, not an invented `1.0`.
XmlDeclarationView.Version borrows the exact literal lexical value through
callback return; XmlTextDeclaration.Version owns that exact string when present.
No ABI shape changes. Existing declaration placement, whitespace, matching quotes,
encoding validation, standalone, event order, byte mapping, cancellation,
TokenCharacters/DocumentCharacters/ExpandedCharacters accounting and bounded
unfinished-token lifetime rules remain binding. Long versions are limited by
the existing quotas before retention/delivery, not by a new version-specific cap.

Corpus VERSION and source-purpose metadata describe original test intent; they
alone do not determine acceptance under this effective profile. Evaluate both
the declaration grammar and content against the selected normative editions.
Independent models under `tests/Contracts/Fixtures/M2/Version/` demonstrate this
contract, not parser implementation or corpus conformance. M2-version-declarations
owns runtime guard and historical rejection-test transitions; this correction
supersedes the earlier exact-`1.0` wording without rewriting historical evidence.

## Supported slice and ownership

One root, XML 1.0 Fifth Edition declarations, namespaces, predefined/numeric references, text,
comments, CDATA, PIs, normalization and strict Unicode. `<!DOCTYPE` rejects with
XML.DtdProhibited at its opening delimiter, without resolving/accessing anything.
Unknown general entity uses XML.UndeclaredEntity. Generic grammar rejection is
XML.Syntax, reserved/binding/expanded duplicate failure XML.Namespace; quota
rejection XML.ResourceLimit. M1 has no DtdModel, recovery, resolver, public push
reader, or forward-writer type. Module code strings remain extensible.

Input adapter owns 4096-unit decode scratch and parallel Int64 mapping scratch;
pool/setup and growth allocations are reported separately. It processes decoded
unconsumed scratch before refill and retains owners plus offsets across future
awaits. The core keeps only bounded unfinished tokens and open scopes, chunks
long text, and has no DOM allocation. Names/attributes crossing chunks may use
bounded owned token scratch; a borrow never survives Append return. M2 extends
through an ordered contract; no absent M2 hook/type is needed to compile M1.

Runtime evidence belongs to enforcing tasks: every short split, scalar/surrogate/
BOM/signature/declaration/CRLF/EOF split, mapping and byte/UTF-16/scalar distinction,
all-callback pauses including start/empty-tag/end-document, invalid bytes/names/
bindings/duplicate attributes, exact/one-over quotas, cancellation/fault/reentrancy,
borrowed source release and zero-error allocation budgets. Full XML conformance
cases remain explicit later-milestone failures, never hidden exclusions.

## Additive M2 contract

The [complete XML/resource decision](../decisions/M2/xml-resources.md) fixes the
independent internal DtdParser/DtdModel and declaration/particle types,
NeedEntity/PushEntity source stack, explicit shared IDtdBudget adapter, core
entity lookup/default hooks and document metadata. DTD grammar references only
common types, so it compiles without a resolver implementation. Entity composition
joins verified resolver/DTD lanes and retains all M1 callback/count/lifetime rules.

M2 decoder declarations preserve IXmlInputDecoder, DecodeResult and declaration
validation, adding explicit single-byte enum cases and entity text-declaration
policy. M2 strict XML/namespaces processing remains nonvalidating. Distinct
ParseWithDiagnostics and LoadWithDiagnostics factories return the existing
XmlParseResult<XmlDocument>; bounded recovery never masquerades as strict success.
Exact signatures live in tests/Contracts/Fixtures/M2; declarations are models,
not shipped APIs or full XML conformance evidence. The decision maps each hook,
option and quota to its implementing task and specifies all unrecoverable errors.

## Incomplete attribute/default values — M2 ordered addition

[M2 incomplete attributes](../decisions/M2/undeclared-references.md#incomplete-attributedefault-values--m2-additive-contract)
freezes the exact nonpackaged optional two-callback sink, symbolic stage-one
parts, immutable declaration/applied-member provenance, guarded existing
string getters and owner-wide loss refusal. No complete normalized value is
inferred across unknown parts. Applied incomplete namespace bindings reject
before start-tag callbacks with a representation diagnostic. Existing sink/views,
DTD models and all current public signatures remain frozen. EntityDeclared
applicability uses physical origins, actual PE references, first-unread processing
and declaration order. Runtime implementation is a required ordered owning
prerequisite of the conformance join; independently compiled models are not
runtime acceptance evidence or a shipped API baseline.
