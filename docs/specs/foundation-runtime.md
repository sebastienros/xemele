# Runtime foundation

Storage, decoding, lifetimes, errors, and limits shared by the XML runtime.

## Storage, identity and lifetime

Select a document-owned arena and public `readonly struct XmlNode` handles.
Each handle stores an owner reference and signed 32-bit slot identifier. Slot
zero is valid; internal missing links use -1. At most Int32.MaxValue live or
previously allocated slots exist (indices 0..Int32.MaxValue-1). Checked arithmetic
and allocation failures apply even if input limits are explicitly unlimited.
Never recycle slots during the owner's lifetime; no generation field is needed.
Deletion/detachment unlinks a node, leaves its handle valid, and retains its slot.
No public IDisposable document or pool-backed durable DOM storage is selected.
Any live node retains its owner and all arena storage, including detached nodes.
This is an intentional retention tradeoff that callers must understand.

Equality and hashing use owner identity plus slot, independently of parentage,
content, or arena growth. Default handles compare equal to each other, expose
`IsDefault == true`, and throw InvalidOperationException on navigation/value/
mutation access. A node never silently changes document. Foreign-owner insertion
throws ArgumentException; explicit ImportNode copies into the target document
and creates new identity. Clone has new identity even within the same owner.
Cycles, moving the document root, or structurally invalid insertions throw
ArgumentException without partial mutation. Detached nodes remain editable.

Names/text are owned at persistence boundaries, not views into the source or
rented decode storage. Document-local ordinal interning probes borrowed spans
and owns strings only for misses; table limits apply. Dynamic attacker-controlled
keys use the runtime dictionary with bounded counts and collision defenses.
Public navigation returns handles; child traversal uses a struct enumerator with
0 B warm traversal as a composition target. Structural mutations increment a
checked 64-bit version; live enumerators throw InvalidOperationException after
such mutation, including mutations on detached nodes of that owner. Reaching
version overflow rejects mutation before applying it. Value changes do not
invalidate structural enumeration. Documents are not thread safe; simultaneous
read-only access requires no mutation. No internal refs/spans into movable arena
slots escape. This contract does not freeze a physical List/array slot layout.

## Input and scanner boundary

Select character grammar scanning for borrowed string/ReadOnlySpan<char> input.
Byte inputs use bounded strict incremental decoding into operation-owned scratch,
then the same character grammar; do not allocate a whole decoded string or turn
strings into owned UTF-8 arrays. Keep byte framing/ASCII recognition as an
optional measured internal optimization rather than a second required grammar.
XML subset supports UTF-8 and BOM/signature-selected UTF-16LE/BE; malformed sequences,
unpaired surrogates, incomplete EOF and unsupported/conflicting declarations
are syntax diagnostics, never replacement decoding. A decoder/input adapter owns
its decoder, BOM/prolog carry and scratch; no retained input span or borrowed
memory survives Append return. Stream/file handling is separate from scanning.

The exact internal boundary is `ScanResult(int Consumed, ScanStep Step)` with
`NeedInput`, `Paused`, `Completed`; `ICharacterSink.Fragment(ReadOnlySpan<char>,
bool tokenEnded)` returns false to pause. `Append<TSink>(ReadOnlySpan<char>,
bool final, TSink sink, CancellationToken)` uses `where TSink : class, ICharacterSink`.
Callback text is a borrowed fragment, not necessarily a whole token. A consumer
persists data only by explicit copying; no callback span may survive return.
Production lexical event kinds are specified by the parser contract. The reference sink is explicitly operation-owned so changes are not lost in
value copies. A future internal struct-sink overload must pass it by ref, use
constrained dispatch and demonstrate no boxing; this fixture does not approve
a by-value mutable struct callback contract.

Consumed counts submitted UTF-16 units for character input; a byte adapter's
Consumed counts original bytes, not scalar columns. Always 0..input.Length.
Advance consumption before returning Paused so delivered events are not replayed.
NeedInput consumes the full submitted slice; incomplete scalars/lexical state
are retained in bounded owned state, not as a source view. No partial-progress
result may repeatedly scan an unbounded remaining tail. A pause may consume all
input. Final EOF emits its closing event exactly once; if that callback pauses,
resume with empty final input to finish without repeating it. A pause before EOF
requires the unconsumed suffix (possibly empty) with final still true. Completed
is terminal; a subsequent Append throws InvalidOperationException. Append never
returns NeedInput after final exhaustion; malformed/truncated input rejects.

Reentrant/concurrent use of the same scanner is InvalidOperationException.
Cancellation, I/O and user callback exceptions propagate unchanged and fault the
operation; continuing it is InvalidOperationException. Syntax rejection uses
structured diagnostics, rather than catching user-thrown XmlParseException.
CancellationToken is an operation argument, not retained in options. Check it at
entry, before reads/resolution/callbacks and at least every 4,096 source units or
work steps; no unbounded synchronous step evades checking. Async operations
retain owners and integer offsets, reconstructing spans only in synchronous
sections. If decoding runs ahead of a pause, remaining decoded scratch is owned
by the operation and processed before refill; byte offsets track original bytes.
Final decoder flush occurs once. Carry is bounded (UTF-8 at most three bytes,
UTF-16 at most an odd byte plus high surrogate); normalization may retain a CR.
Scratch target is 4,096 UTF-16 units, with checked bounded growth only for explicit
owned token results. No stackalloc proportional to input or depth. Bounded
prolog detection is charged to TokenCharacters; a declared encoding is resolved
before body decoding. Stream buffering/pool setup is separately accounted.

## Errors and limits

Exact common declarations and constructor semantics are in Common.cs.fixture.
XmlParseOptions defaults to Recover=false, DtdProcessing=Prohibit, and
Resolver=null, with finite limits. Prohibited DTD syntax is rejected with a stable
module diagnostic.
No shipped interface gains required abstract members. The XML resolver is an
abstract class with abstract sync Resolve and a virtual async fallback; async-only
providers can override both with an explicit sync rejection. Enabling Parse
permits syntax, never network/file access. Null resolution means denied/missing;
resolver exceptions are callback failures and propagate. Require absolute request
and result URIs; returned SourceUri identifies actual resource locations.

XmlResolvedResource transfers its readable Stream's ownership to the operation.
Dispose/DisposeAsync happens exactly once even on rejection/cancellation; the
parser owns the wrapper after successful resolver return. A resolver cleans up
its own unsuccessful acquisitions; it must not return a caller-owned stream
without a deliberate non-owning wrapper. Caller input streams stay open; file
entry points close streams they open. Cancellation and aggregate limits span
all nested resources; resolver content cannot bypass external byte/count quotas.

Strict Parse/Load produce a complete well-formed value or XmlParseException with
an owned nonempty bounded diagnostic list. TryParse returns false for syntax/
quota/unsupported feature rejection and provides diagnostics through its approved
module shape; it never catches argument, I/O, cancellation or callback failures.
Recovery is a separate XML ParseWithDiagnostics surface: Value can be null, complete
is physical/grammar completion, well-formed implies complete and nonnull Value.
Partial recovered trees are not well-formed. Constructors snapshot diagnostics
in read-only owned arrays; options/limits are immutable records snapshotted and
validated once before work. Diagnostics own messages and stable module string
codes. Unknown line/column is zero, unknown byte offset null. Line/column are
one-based after XML newline normalization; column counts Unicode scalars (tab
counts one). Byte offset is zero-based original bytes, at the first byte of the
reported scalar; CRLF normalization refers to its CR. String input has no byte
offset. Locations retain the resource URI; counters use checked arithmetic.
Int32 line/column overflow is a resource-limit rejection, not wraparound.

Defaults and units (all values nullable, null explicitly opts out of that quota;
zero/negative invalid, no sentinel -1; unlimited never removes capacity bounds):

| Property | Width | Default | Unit / scope |
| --- | --- | --- | --- |
| DocumentCharacters | Int64? | 16,777,216 | Unicode scalars consumed from primary input, including markup |
| Depth | Int32? | 256 | simultaneous element nesting, root element depth 1 |
| AttributesPerElement | Int32? | 256 | attributes including namespace declarations, on one start tag |
| TokenCharacters | Int32? | 1,048,576 | normalized scalar units in one lexical token, before persistence |
| InternedNames | Int32? | 65,536 | distinct document-owned namespace/name strings |
| EntityDepth | Int32? | 32 | simultaneous general/parameter entity expansion depth |
| ExpandedCharacters | Int64? | 16,777,216 | scalar expansion output cumulatively, including repeated references |
| ExternalBytes | Int64? | 16,777,216 | bytes actually read across all external resources |
| ExternalResources | Int32? | 64 | successful external opens, including repeats |
| InclusionDepth | Int32? | 32 | simultaneous include nesting |
| Diagnostics | Int32? | 128 | total recorded diagnostics including terminal truncation/limit entry |
| EvaluationWork | Int64? | 10,000,000 | one candidate-node visit or expression/automaton transition |

The common limits record can expose all quotas before later consumers exist;
unavailable syntax remains rejected and inactive limits do not enable it.
Exactly N is allowed, N+1 rejects before the extra persistent allocation/read/
work; increment/check using checked arithmetic. Primary and expansion quotas are
separate so entity expansion cannot evade either appropriate scope. Scalar
accounting validates surrogate pairs across fragments. For diagnostics limit N,
reserve the final slot for the terminal limit diagnostic when more than N messages
would be generated; N=1 produces only that terminal entry when truncated. Limits
are aggregate per operation, not reset per entity/include or recursive call.
All representational capacities/stream lengths/allocation sizes remain checked
under null limits. Location overflow and capacity failure have stable owned
module diagnostics where possible; OutOfMemoryException is not converted into
a syntax result. Test exact/one-over enforcement, invalid configuration, and nullable opt-out.

## Allocation boundaries

Approve borrowed ordinal intern lookup: 0 B hits in the tree spike versus
40,960–122,880 B/1,024 temporary-string probes. New names still allocate; all-unique
interning was slightly slower with the borrow path. Persist owned text: retaining
a 32-character view held ~2,097,520 B, versus ~112 B with an owned copy.

Use cached state-specific SearchValues for repeated multi-stop long/late scans
where composed evidence confirms value; keep IndexOf for single delimiters and
simple small-set paths for short/early states. The measured 1,024-unit five-stop
late case was 22.86 vs 410.49 ns byte and 30.77 vs 397.46 ns char (cached vs
scalar, all 0 B). Do not adopt a generic SearchValues path for every token.
Keep length-dispatched full equality for the measured 12-token vocabulary;
collision-rich hash candidates were slower (median hash/length ratios ~1.97 byte,
~2.21 char). Larger/generated vocabulary matchers require new benchmarks and
unknown/prefix/collision/case/non-ASCII oracles. No Redis/generator dependency.

Prefer UTF-8 literals and compiler-backed constant spans; inspected IL/JIT shows
static storage for the tested forms. Runtime-valued/mutable returned span
collections failed escape checks; explicit returned arrays allocated 32/32 B
under FullOpts despite 0 B in specialized BDN consumers. Span signatures alone
are not allocation evidence. Static setup/context costs were 7,992 B/496 B in
one cold diagnostic run (not all retained); don't erase startup costs.
Warm primitive scan/match/slice and borrowed callback targets: 0 B/op, after
explicit bounded state initialization. DOM creation, unique interning, owned
results/diagnostics, decode/stream setup, pool misses/growth and retained memory
are distinct budgets measured by their implementation owners. Invalid UTF-8
exception paths measured 1,160–1,184 B/op and are bounded nonzero-error budgets.
One Arm64 synthetic short run is provisional. Longer repeats on x64 and Arm64,
plus composed XML/query/output workloads, are required before release claims.
