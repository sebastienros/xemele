# Complete XML/resource freeze

Owner M2-contracts. Normative executable declarations live only in
`tests/Contracts/Fixtures/M2/`; independent explicit-source projects live in
`docs/reports/M2-contracts/Projects/`. They are nonpackaged models. Grammar/core/
recovery method bodies are declarations, never exercised or evidence of parser
features. Framework-only constructor/snapshot/disposal checks are model evidence.
Existing foundation/M1 files, runtime, API222 and default values remain unchanged.

## Profiles and additive API

The M2 strict profile targets XML 1.0 Fifth Edition and Namespaces 1.0 Third
Edition, as a nonvalidating processor. Prohibit remains the default; DOCTYPE
rejects before external work. Parse processes the internal subset and, when
present, the external subset with a caller grant. Resolver=null denies external
opens, including declarations needed by parameter entities. No silent partial
DTD success: a declared required external subset/entity that cannot be supplied
rejects with XML.ResourceDenied. Parse never implies network/file permission.
No global default resolver, URI fetcher, cache or encoding-provider mutation.

Recover=false remains default. Strict Parse/Load/TryParse reject Recover=true
with ArgumentException before input or resolver work; only the distinct recovery
family admits it. ParseWithDiagnostics/LoadWithDiagnostics with Recover=false
return strict structured failure as Value=null, IsComplete=false,
IsWellFormed=false and owned diagnostics, without throwing for grammar failure.
With Recover=true they admit the bounded repair policy below. Async parser APIs,
DTD validation, catalogs and inclusion are later milestones, not M2 switches.
String/char entry points never fabricate encoding or byte offsets.

Recovery can close open elements at physical EOF or before an ancestor closing
tag, discard an unmatched closing tag, keep the first duplicate expanded
attribute, and discard content outside the single root. Each repair records an
Error diagnostic at the offending source location. Recovery never invents names,
namespace bindings, entity values, a missing root or replacement characters.
Malformed start tags, namespace errors other than duplicate attributes, malformed
references/DTD, prohibited DTD, decoder failures, denied resources, cycles and
quota failures stop recovery. A validated start tag is committed atomically.
Partial Value exists only after a real root was committed and remains a legal
editable tree. No normal strict callback stream resumes after a fatal error;
recovery uses its separate engine/DOM composition.

IsComplete means physical EOF was reached and the operation finished its grammar
or permitted repair finalization, rather than early termination. A recovered
complete tree can be IsComplete=true, IsWellFormed=false. A root committed before
an unrecoverable error can yield a partial Value with both flags false. No root
means null Value. IsWellFormed describes original input, never repaired output;
true requires complete nonnull Value and no error/fatal diagnostic. Warnings
alone need not invalidate it. Cancellation, I/O, argument and resolver/user
callback exceptions propagate unchanged even from recovery and TryParse.
Recovery constructors preserve the existing generic result/snapshot invariant;
entry points enforce these additional diagnostic semantics.

## Independent DTD grammar and model

Dtd.cs.fixture uses common limits/locations only: no resolver, input decoder,
DOM or core implementation dependency. Internal immutable DtdModel owns ordered
read-only snapshots of element, attribute, general/parameter entity and notation
declarations. Particles own recursively immutable child snapshots with explicit
sequence/choice/mixed/EMPTY/ANY kinds and occurrence. Names remain lexical XML
names (DTDs are not namespace-aware). Entities distinguish internal replacement
text, external system/public identifiers and unparsed notation; source locations
and IsExternal retain declaration origin. Attribute enumeration/notation token
sets and default mode/value are retained. Entity replacement text preserves the
XML declaration-processing distinction: character references are resolved during
entity declaration processing, general references remain for use-site expansion;
attribute defaults are normalized when applied with the effective entity table.

Grammar covers ELEMENT/ATTLIST/ENTITY/NOTATION, parameter entities, external
conditional INCLUDE/IGNORE and text declarations. Internal-subset parameter
entity placement restrictions and entity-boundary well-formedness apply. First
entity/attribute declaration wins; first element/notation is retained (duplicate
validity constraints are for the later validator). Internal subset precedes the
external subset, so its first declarations take precedence. Separate general
and parameter name spaces; entity recursion tracks active names even with null
EntityDepth. Predefined entity redeclarations must have XML-permitted replacement
text. Unparsed entities cannot be expanded as parsed content.

DtdParser accepts subset contents, excluding DOCTYPE delimiters; External permits
an initial text declaration for supplied character input; byte composition strips
the already validated text declaration through the framing protocol below. Append consumes submitted UTF-16 units only, retains
bounded token state, and borrows characters/mappings through return. Offset rules,
scalar locations, source finalization and fault/disposal/reentrancy mirror M1.
NeedInput consumes all input and is nonfinal; Rejected has a nonnull Diagnostic
and no Entity; NeedEntity has Entity only and identifies an external parameter
entity whose caller must supply. Completed has neither and publishes Model only
when the initial subset finishes. Model access before Completed throws
InvalidOperationException. Internal parameter entities expand within the parser.

The XmlLocation constructor overload initializes the scalar source cursor at the
first supplied subset unit. The DOCTYPE/core caller passes the position immediately
after '[' for an internal subset, with primary final URI, normalized scalar
line/column and original byte offset when known. No invented prefix or resetting
to 1:1 is allowed; constructor location line/column are positive (or both zero
for unknown), ByteOffset null/nonnegative. The standalone Uri overload starts
1:1; first byte mapping supplies its offset. IDtdBudget constructor also takes
initialLocation. CR/surrogate state at a delimiter boundary is already resolved
by core; '[' cannot split a scalar or preserve pending CR. The initial offset is
not a local reset: submitted byteOffsets stay absolute in the original resource.
CompleteByteSource for an interior subset is the absolute offset immediately
after that subset's last unit, supplied by the outer core, not a fabricated full
primary-source EOF. Grammar stop excludes closing ']' and DOCTYPE '>'; the outer
core owns those delimiters and primary cursor/accounting. DTD raw internal units
are already DocumentCharacters-charged by the outer operation, so the shared
budget constructor must not charge that primary quota again. Standalone grammar
charges local raw DocumentCharacters independently. Expansion/name charges are
shared once at their owning consumer boundary.

NeedEntity consumes the reference once; caller retains the parent's owner and
unconsumed suffix/final state. PushEntity is legal exactly once per pending
request, takes the final absolute resource URI,
and starts a new source mapping/EOF scope. Caller then Append-s decoded entity
chunks, calling CompleteByteSource for each byte source before final Append.
The pushed source's final exhaustion pops its scope, returns NeedInput for the
parent (even if the entity was final), and does not complete the initial subset.
Resume the parent's saved suffix and final flag; no reference replay. A nested
NeedEntity repeats this stack protocol. Empty entities are legal. Illegal push,
source-mode change or resuming terminal/faulted operations rejects. Declaration
locations retain final URI; parent locations restore on pop. No whole external
resource is buffered to satisfy this grammar interface.

The standalone constructor uses local aggregate limits. The internal IDtdBudget
constructor accepts operation accounting supplied by entity integration, enabling
one shared charge ledger without a dependency on resolver internals. Enter/Exit
track simultaneous expansion depth; ChargeExpandedCharacters charges scalars
before expansion; ChargeName charges only distinct retained ordinal names. Quota
rejections are structured terminal diagnostics; IDtdBudget implementations must
signal quota refusal through an internal owned XmlParseException, caught only at
these explicit budget-call boundaries, never around user callbacks. External
bytes/opens belong to resolution. Raw subset input/token limits are also enforced
locally. No resource is opened by DtdParser itself.

## External declaration framing before body decoding

Dtd.cs.fixture also fixes XmlTextDeclarationFramer, a common-only DTD-owned
character prolog component reusable for external subsets, external parameter
entities and external general entities. It has no decoder or resolver reference;
M2-dtd implements it, M2-entities supplies composition. A byte adapter requests at
most one new byte per Decode until framer decision, drains detection carry first,
and feeds corresponding written character/mapping slices to the framer. Detection
carry is at most four original bytes and never decodes an arbitrary body prefix.
No byte after the declaration or declaration-absence decision is decoded before
ValidateDeclaration runs. The decoder may have Completed on a short source;
M1 already permits validation after successful decoder completion.

TextDeclarationResult.Consumed counts this call's accepted UTF-16 units.
NeedInput consumes all submitted input and is nonfinal. Declared owns a nonnull
XmlTextDeclaration (version optional/only 1.0, encoding mandatory and nonempty,
no standalone); Absent has no declaration; Rejected owns Diagnostic only.
Declared/Absent are one-time terminal decisions. NextLocation reports normalized
scalar line/column immediately after the consumed prolog, with final source URI;
its byte offset may be null until the next decoded scalar mapping is known.
For Declared, composition initializes DtdParser at NextLocation. For Absent,
framer.PrefixLocation reports the first held prefix unit's original source cursor;
initialize grammar there before DrainPrefix. The decoder's original-byte mappings
remain absolute, including BOMs. Newline normalization occurs once in the framer
for declaration units and once in grammar for forwarded prefix/body; closing '>'
resolves pending CR/scalar state before the declaration handoff. Cancellation/fault/disposal,
source mapping/scalar location and bounded prolog TokenCharacters rules mirror
M1. A declaration is charged and parsed once, not reparsed by the DTD parser.
Composition calls decoder.ValidateDeclaration with the declaration EncodingName
and its final-URI Location, or empty encodingName on Absent. Rejection terminates
before grammar/body work; resolver/callback exceptions are never caught here.

Absent may have accepted at most the five-unit recognition prefix (or the
scalar-complete unit establishing absence). It owns that bounded prefix plus
original mappings. Only after successful encoding validation does composition
call DrainPrefix once with output capacity >=7 and matching mapping capacity
(empty mappings for character input); it returns the prefix count and clears its
owned copy. Repeated drain/Declared drain or premature drain throws
InvalidOperationException. Capacity errors throw before state change. Declaration
units are not passed to DtdParser; absent prefix units are forwarded exactly once,
then the submitted unconsumed suffix resumes. No prefix/source view survives an
Append return. This strips a text declaration without losing adjacent body units,
counting them twice or guessing encoding from body text. All declaration and
prefix original bytes still count ExternalBytes; declaration scalars count the
shared expansion budget once, and prefix/body scalars are charged by grammar.
Framer itself only enforces token/configuration limits; composer charges accepted
declaration scalars after its decision, prefix via grammar on Absent. Malformed
prolog rejection is early terminal and still charged for consumed source work.
General parsed entities use the same framer, then the regular content grammar;
no M3 reader or unmerged M2-encoding type is required for this composition.

## Core and metadata integration

Entities.cs.fixture fixes entity-table/default-attribute lookup hooks and the
additive constructor in Syntax.cs.fixture. The existing constructor, sink methods,
span/count/pause contract and callback order remain unchanged. The context returns
immutable declarations, not borrowed replacement views. Core expands internal
content entities as parsed markup, forbids external entities in attributes,
normalizes CDATA/non-CDATA defaults, merges defaults after explicit attributes
before namespace resolution, and checks duplicate expanded names afterward.
Default namespace declarations participate in resolution. Namespace declarations
count as attributes. Standalone=yes checks externally declared defaults, entity
use and whitespace/type dependencies required by XML; it never grants access.

M2-entities owns the context implementation and synchronous external entity
composition using resolver abstractions and verified M1 decoder. It owns bounded
source stacks, aggregate budgets and exactly-once resource cleanup. No unmerged
resolver/encoding code is required by the DTD lane. General-entity boundaries
cannot cross element/tag boundaries illegally; parameter boundaries obey DTD
rules. The same table feeds internal context lookups and document metadata.
XmlDocument.DocumentType is nullable immutable lexical DOCTYPE metadata separate
from node kind/storage; internal XmlDocument.Dtd stores the completed model.
No public mutable DTD editing API or synthetic DOCTYPE XmlNodeKind is introduced.
Existing tree mutation does not recompute DTD defaults or pretend to revalidate.
Metadata need not retain source/decode buffers. Serialization of DOCTYPE and
DTD validation are not silently promised by this parse metadata addition.

## Encoding scope

M2-encoding adds strict US-ASCII, ISO-8859-1 and Windows-1252 in addition to M1's
UTF forms. Supported case-insensitive labels are exactly US-ASCII, ISO-8859-1,
Windows-1252 and existing UTF labels; no alias/codepage fallback. UTF-32/EBCDIC and
other labels remain INPUT.UnsupportedEncoding, an explicit supported-encoding
profile rather than a claim to implement every optional XML encoding.
ASCII rejects >0x7F; Latin1 maps all bytes then XML scalar validation applies;
Windows-1252 rejects undefined bytes 81/8D/8F/90/9D. Strict decoding never replaces.

BOM/signature-selected UTF cannot be overridden; declaration mismatch retains
INPUT.EncodingMismatch. Without a Unicode signature, the bounded ASCII XML
prolog is decoded provisionally as UTF-8 and declaration selection occurs before
body bytes; a supported single-byte declaration selects its decoder at that
boundary. A missing declaration defaults UTF-8. Non-ASCII declaration/prolog
bytes before selection reject. This preserves all M1 successful inputs and early
error order. Each external parsed entity restarts detection independently and
uses text-declaration rules (encoding required, version optional, no standalone).
Byte mappings count original bytes through BOMs. No process-wide
Encoding.RegisterProvider, arbitrary Encoding option or runtime-dependent aliases.
The existing decoder interface remains; M2 Encoding.cs.fixture adds internal enum
cases only. Stateful legacy encodings and async transport remain later contracts.

## Complete limits and ownership

All twelve widths/defaults/null semantics remain the foundation table. Exactly
N succeeds, N+1 rejects with checked arithmetic. Null disables that quota only;
Int32 locations, arena slots, token arrays and other capacities still checked.
DocumentCharacters counts primary source scalars, including DOCTYPE/internal
subset markup, before newline normalization; never double-charge on resumption.
Depth counts simultaneous emitted elements across entity boundaries. Attributes
count namespace/default attributes once on the final start tag. TokenCharacters
bounds each normalized lexical token, including declarations/default/replacement
literal, before persistence. InternedNames bounds distinct retained name/namespace
strings across document and DTD, not per entity. EntityDepth counts simultaneous
general/parameter expansions, predefined/numeric references as one transient
expansion (M1 already charges ExpandedCharacters for them). ExpandedCharacters
counts each replacement scalar on every use, including external subset/parameter
input and entity markup; nested levels each charge their emitted replacement
work so nested amplification cannot evade aggregate work. Primary document bytes
are not ExternalBytes. ExternalBytes counts bytes read across successful external
resources, including BOM/prolog and repeated opens; the resolver's private work
before return is outside parser control. ExternalResources counts accepted
non-null successful opens even if later rejected or aliased to the same final URI.

The session checks remaining byte allowance before reads. At exactly N bytes,
it may probe one additional byte to distinguish EOF; a returned byte is charged
and rejected without decoding/persisting it. This explicitly bounds unavoidable
one-byte overshoot, while source streams remain nonseekable. Resource count is
checked before calling a resolver when exhausted. Active final absolute URIs are
cycle keys (ordinal AbsoluteUri, fragment removed); repeated inactive opens are
allowed and charged again. Redirect aliases resolving to an active URI reject
and dispose the returned wrapper. Entity-name recursion is a separate cycle
check. InclusionDepth/EvaluationWork remain inactive until their respective
modules; they do not enable syntax. Diagnostics includes every repair and
terminal quota/truncation diagnostic, reserves last slot when truncating, and
stops recovery if capacity is exhausted; N=1 then only terminal diagnostic.

Resources returned successfully transfer ownership; failed acquisitions remain
resolver-owned. Sync dispose or async dispose exactly once, including failed
final-URI validation, quota/cycle denial, callback exception and cancellation.
Primary caller streams remain open; wrappers must use deliberate non-owning
stream adapters for caller-owned content. Options/limits snapshot once, resolver
identity retained per operation; no global state. Cancellation at entry/before
external work and every <=4096 source/work steps; no spans cross awaits. User
resolver exceptions, including XmlParseException, propagate unchanged.

## Implementing owners and required shared repair

| Declaration/option/hook | Implementing task and owned location |
| --- | --- |
| Resolver/request/resource, request SystemId/PublicId, session budgets | M2-resolver: Resolution/Core, Options/Resources |
| DtdModel/particle/declarations, parser/result/request, IDtdBudget, XmlTextDeclarationFramer/result | M2-dtd: Parsing/Dtd |
| Decoder cases/text declaration detection/single-byte profiles | M2-encoding: Parsing/Input |
| DtdProcessing/Resolver options, entity context/core constructor/expansion, shared budget adapter | M2-entities: Options/XmlParseOptions.cs, Parsing/Entities, Parsing/Core |
| DocumentType and internal Dtd | M2-entities: Tree/DtdMetadata |
| Complete strict grammar/namespaces and standalone checks | M2-conformance: Parsing/Core and full XML tests |
| Recover option and diagnostics factories/repair policy | M2-recovery: Options/Recovery, Parsing/Recovery, Parsing/Dom/Recovery.cs |
| InclusionDepth | M6-XInclude; remains inactive in M2 |
| EvaluationWork | query/validation owners in later milestones; remains inactive in M2 |

Actual Options/Common.cs contains nonpartial XmlParseOptions. Coordinator must
perform a narrow ordered ownership repair before M2-entities: split its unchanged
Limits-only declaration to the already assigned Options/XmlParseOptions.cs and
make it partial, or make the existing declaration partial under an explicitly
owned repair. Its sealed record/public constructor/Limits metadata/defaults must
remain identical. Then entities/recovery add separate partial declarations in
their owned files. This contract does not perform that outside-path edit.

No new timing measurements or hot-path implementation here. Fixtures establish
syntax, model semantics and public baseline preservation only. Full XML parsing,
malicious-input limits, borrowed lifetimes, allocation evidence, cross-platform
and AOT checks remain the enforcing task/gate responsibilities.
