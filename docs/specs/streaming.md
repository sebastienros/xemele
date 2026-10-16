# SAX, push, pull, and asynchronous contracts

Compile-only declaration models are in `tests/Contracts/Fixtures/M3/`.
The [streaming internals](streaming-writer.md) specify shared metadata, resource
continuations, and pump behavior. All transports preserve XML parser invariants.

## Borrowed views and handlers

All public streaming types except the writer/DOM transports live in
`Xemele.Streaming`. `IXmlSaxHandler` has exactly sixteen void notifications:
StartDocument, EndDocument, StartElement, EndElement, Text, Comment,
ProcessingInstruction, StartCData, EndCData, Declaration, StartDtd,
DtdDeclaration, EndDtd, SkippedEntity, IncompleteAttributeDefault, Diagnostic.
The fixture gives every parameter type. XmlSaxHandler has a public virtual
no-op for each; a consumer may override only one. Sealed XmlSaxCallbacks has
init-only nullable On* properties with sixteen named delegate types and forwards
each override directly. Delegate properties never hide inherited method names.
Text/Comment take ReadOnlySpan<char>; PI takes target/data spans. Notification
exceptions propagate as the identical exception object, even XmlParseException;
adapters must distinguish callback failures from structured parser rejection.

XmlStartElement/XmlEndElement expose LocalName, Prefix, NamespaceUri and
Location; start also exposes IsEmptyElement, Attributes, NamespaceDeclarations.
Collections are readonly ref struct views, with Count, checked zero-based
indexer, GetEnumerator and mutable concrete ref struct Enumerator
(Current, MoveNext). No IEnumerable, boxing, interface enumerator or
ReadOnlySpan<ref-struct-item> is required. Default collection is empty; bad
indices throw ArgumentOutOfRangeException. Enumerator Current is valid only
after successful MoveNext. Items and all other callback views are readonly ref
structs with get-only properties and no public constructors or owned copies.
Consumers persist strings/parts explicitly during the callback.

XmlAttributeItem has LocalName/Prefix/NamespaceUri/Value, IsDefaulted,
IsValueComplete, ValueParts and Location. Complete Value is XML normalized text;
ValueParts is empty. For an unresolved attribute Value is empty,
IsValueComplete=false and ValueParts contains the ordered existing XML
XmlKnownAttributeText/XmlUnknownAttributeReference records. No lossy partial
concatenation is published as a complete value. ValueParts is a borrowed span
of immutable owned record references; copy its container to retain it.
IsDefaulted distinguishes authored attributes from injected DTD defaults even
when their normalized values coincide. Attributes exclude xmlns declarations;
namespace items expose Prefix, NamespaceUri, Location and only this tag's
local declarations (including injected declarations). Attribute order is authored
order followed by applied defaults in the effective XML model order. Namespace
order follows their order in that combined list. Names are expanded before delivery.

StartElement is emitted only after the complete tag, all attributes and local
namespace bindings are available. The internal adapter gathers individual
StartElement/NamespaceDeclaration/Attribute/IncompleteAttribute calls until
StartElementContent, then invokes the single public start callback. One tag's
scratch is bounded by XML quotas; no document-sized event list is allowed.
Empty markup emits a start with IsEmptyElement=true and a corresponding end;
there is no omission or combined empty-element token. End name is expanded
against the element's scope before it is popped. Location for generated defaults
is the containing element's opening location; unknown attribute parts keep XML
physical locations and lexical reference spelling.

Every callback span/view expires when that callback returns, including views
into a paused parser. No view is retained by a handler, stored on a class, or
used across await. Immutable XmlDiagnostic and XmlIncompleteAttributeDefault
are owned existing XML values and can be retained. Input chunks are borrowed
only during Append; adapters retain copied scratch/owners, never caller spans.

## Lexical, DTD, and incomplete-input notifications

StartDocument is first, emitted once on first parse/read/append/complete work;
construction/Create alone does not invoke a callback or read input. Declaration
is optional and follows StartDocument before other content. XmlDeclaration has
Version, EncodingName (empty when absent), nullable Standalone and Location.
It describes only the document declaration, not an external text declaration.
XML encoding/version checks still apply before successful declaration delivery.

Text is normalized, entity-expanded character data, split arbitrarily at scalar
boundaries; an empty Text notification is permitted at a lexical boundary.
CDATA emits StartCData, zero or more Text notifications, EndCData; CDATA is not
collapsed into ordinary Text. CDATA boundaries remain paired on success, even
empty CDATA. Comment and PI callbacks each deliver one complete lexical body,
excluding delimiters; PI also supplies its complete target. They may buffer one
bounded token, never a document. Comments/PIs in prolog/epilog are delivered.
No ignorable-whitespace distinction is added by a nonvalidating parser.
Expanded parsed entities contribute ordinary text/elements; no StartEntity or
EndEntity callbacks and no raw input slice fidelity are promised.

DtdProcessing=Prohibit rejects before any DTD notification. Parse enables only
XML's nonvalidating processing; external access still requires the resolver.
After the DOCTYPE and all required external processing succeed, emit StartDtd,
then DtdDeclaration notifications for the **effective merged model**, then
EndDtd, before document element events. StartDtd exposes Name, optional lexical
PublicId/SystemId as spans with HasPublicId/HasSystemId, and Location. Absence
is distinct from an explicitly empty identifier. Internal subset text is not
copied into this public streaming view. Callbacks do not promise declaration
source order, overridden duplicate declarations, parameter-entity expansion
boundaries, or DTD comments/PIs; the integrated XML sink does not expose them.

Projection order is Elements, Attributes, GeneralEntities, ParameterEntities,
Notations, preserving each effective XML collection's order. XmlDtdDeclaration
has Kind (Element=0, Attribute=1, Entity=2, Notation=3), Name, ElementName,
ContentModel, AttributeType, DefaultKind, Value/HasValue, SystemId/HasSystemId,
PublicId/HasPublicId, NotationName, IsParameter, nullable IsExternal, Location.
Unused span fields are empty, Has* false, DefaultKind=None, IsParameter=false.
Element uses Name/ContentModel; attribute uses Name/ElementName/AttributeType
and default fields; entity uses Name, replacement Value when internal or
identifier fields when external, plus NotationName for unparsed entities;
notation uses Name and identifier fields. IsExternal means declaration provenance,
not whether the entity has an external identifier; XML knows it for element,
attribute and entity declarations. Notation provenance is unavailable and is null,
never fabricated from URI comparisons. Location is always the original XML
model location; no synthetic lexical offset is invented.

ContentModel is a deterministic model projection: EMPTY/ANY, a name plus its
occurrence suffix (?/*/+ or none), parenthesized comma-separated sequence or
pipe-separated choice plus suffix; mixed content is (#PCDATA) or
(#PCDATA|name|...)* in particle order. AttributeType is CDATA, ID, IDREF,
IDREFS, ENTITY, ENTITIES, NMTOKEN, NMTOKENS, a parenthesized pipe-separated
enumeration, or NOTATION followed by a space and that enumeration. DefaultKind
is None=0 (nonattribute), Implied=1, Required=2, Fixed=3, Value=4. Value is the
XML model value, not original quote/whitespace spelling; Fixed/Value have
HasValue=true, Implied/Required false. Model projection may build one bounded
declaration value, but must traverse content particles iteratively.

SkippedEntity represents the integrated XML unresolved **content** reference:
Name, exact LexicalReference, IsParameter=false, Location. It occurs at that
position between surrounding Text, never substitutes empty text for the unknown
value. Unknown attribute references are represented only by ValueParts.
IncompleteAttributeDefault delivers the existing owned XML declaration snapshot,
including Sequence, once before root start in XML's sequence order. Skipped
parameter declarations/unread external subsets have no fabricated per-entity
notification. Resolver denial, malformed entities and quota failures remain
structured rejection, not successful SkippedEntity. Namespaces depending on
unresolved values remain rejected. XML's permitted unresolved nonvalidating
content can still be well formed; missing knowledge alone is not a recovery edit.

## Entry points, recovery, faults and cancellation

Synchronous SAX Parse overloads accept string, ReadOnlySpan<byte>, Stream;
all have handler, optional XmlParseOptions and CancellationToken. Strict APIs
reject Recover=true at entry. ParseWithDiagnostics has the same three overloads
and returns XmlParseResult<XmlSaxCompletion>; XmlSaxCompletion is a sealed empty
owned record indicating successful/partial streaming work rather than a DOM.
It accepts Recover exactly as XML diagnostic DOM factories do. Async stream
counterparts return Task and Task<XmlParseResult<XmlSaxCompletion>>.
Result diagnostics are immutable snapshots of Diagnostic notifications in order;
notifications occur before the result/structured exception is returned. All
fatal parser/decoder/limit diagnostics are delivered once before strict
XmlParseException; callback-thrown exceptions never generate new diagnostics.

EndDocument is emitted once only when EOF was consumed successfully and no
recovery edit/terminal syntax failure occurred. A recovered but complete result
has IsComplete=true, IsWellFormed=false and no EndDocument. An incomplete repaired
result has IsComplete=false. Value is a completion marker iff input completed or
Recover=true allowed a partial result, otherwise null; IsWellFormed implies
complete and a nonnull marker. With no repair, permitted unresolved references
retain XML well-formedness. Cancellation/I/O/callback exceptions produce neither a
result nor EndDocument and propagate unchanged. Recovery never attempts to repair
an argument error, resource quota, denial, invalid encoding or callback failure.
Callbacks from a prefix may already have happened; no rollback is promised.

Push constructor takes handler/options/operation CancellationToken. It is
strict-only; Recover=true is rejected. States: created → accepting → completed,
or faulted; Dispose from any idle state moves to disposed. Empty chunks are legal,
Append parses immediately, Complete signals EOF once and drains pending content.
An empty input Complete still performs work and rejects missing root. Append
owns no caller bytes after return. Append after completed, duplicate Complete,
and operations on faulted instances throw InvalidOperationException. Disposed
operations throw ObjectDisposedException. Dispose is idempotent and aborts,
never silently completes. Concurrent/reentrant Append/Complete/Dispose reject
with InvalidOperationException; callbacks cannot operate on their own context.
A rejected competing call does not independently fault an active operation;
if its exception escapes that callback, the outer operation faults. Syntax,
callback, I/O and cancellation failures fault the operation permanently. Cleanup
preserves the primary failure and releases owned resources exactly once.

Snapshot/validate options and limits before input or notifications. Null handlers,
string/stream arguments use ArgumentNullException, nonreadable streams/options
use ArgumentException. Caller streams stay open on all outcomes; contexts own
only scratch and resolver-transferred resources. Cancellation is checked at entry,
before I/O/resolver work and at the existing bounded XML work intervals. Callback
execution is sequential/synchronous, never async void, may change thread after
an await, and carries no synchronization-context/thread-affinity guarantee.
Different contexts may run concurrently; a single context may not.

## Reader token and lifetime table

XmlReader.Create(Stream, options?, cancellationToken=default) snapshots the
operation token without consuming input; reader is strict-only. Read returns
true for one current token, false after successful exhaustion; repeated EOF
reads return false. ReadAsync(token=default) combines operation and call tokens
for that call, with cancellation of either permanently faulting the context.
No Read/ReadAsync/dispose overlap is allowed. Synchronous disposal uses IDisposable;
asynchronous transport also supports ReadAsync and IAsyncDisposable.
Dispose/DisposeAsync abort and are idempotent, leave caller stream open, and
invalidate all current views. No spans are carried through suspended I/O.

The exact numbered XmlTokenKind list (None=0 through EndDocument=16) is in Sync.
None is initial/EOF/disposed state. EndDocument is itself one true Read result;
the following read yields false/None. Structural reader ordering is the same
sixteen-notification SAX ordering. ValueSpan is Text or complete Comment/PI data;
Target is the PI target. Text is split at scalar boundaries; comment/PI is a
complete quota-bounded token. StartCData/EndCData surround Text tokens.

| Current kind | Kind-specific accessors |
| --- | --- |
| StartElement | LocalName, Prefix, NamespaceUri, Attributes, NamespaceDeclarations, IsEmptyElement |
| EndElement | LocalName, Prefix, NamespaceUri |
| Declaration | Declaration |
| StartDtd | DocumentType |
| DtdDeclaration | DtdDeclaration |
| SkippedEntity | SkippedEntity |
| IncompleteAttributeDefault | IncompleteAttributeDefault |
| Diagnostic | Diagnostic |
| Text, Comment | ValueSpan |
| ProcessingInstruction | Target, ValueSpan |

Location is the current notification's original opening/fragment location for
all kinds (boundary tokens use their lexical delimiter); unknown remains XML
unknown. Nonapplicable spans/collections/views are empty/default, booleans false,
owned nullable accessors null. Current data is valid until **invocation** of the
next Read/ReadAsync or disposal, including when that call suspends/fails. No
thread-safe concurrent accessor/read contract is offered. A fatal structured
rejection throws from Read/ReadAsync immediately (not a delayed fatal token);
views are invalidated, TokenKind=None. Diagnostic tokens represent any nonfatal
Diagnostic notifications only. The exception retains owned terminal diagnostics.
Repeated reads on a faulted context throw InvalidOperationException.

## Internal pump boundary

[Pump.cs.fixture](../../tests/Contracts/Fixtures/M3/Pump.cs.fixture) fixes an
internal target interface, not a public runtime stub. Supply takes
ReadOnlyMemory<byte> plus sticky EOF; Advance takes a synchronous sixteen-method
bool sink and returns NeedInput, Paused, Completed, Rejected or NeedResource, with an owned
terminal Diagnostic. Async extends with AdvanceResourceAsync, AdvanceAsync and async disposal.
In async resource mode Advance returns NeedResource **before** invoking a resolver
or reading a resource. Its consumed input is already reflected by the retained
cursor; no resupply or replay is allowed. AdvanceResourceAsync performs exactly
one pending open/frame/read continuation and resumes the same context.
AdvanceAsync loops Advance/await AdvanceResourceAsync until a public pause,
NeedInput or terminal state. Sync mode services the same continuation synchronously
and never exposes NeedResource to a public synchronous reader/push/SAX caller.
A false sink result accepts that event and pauses; it is never replayed.
Supply may replace input only after NeedInput; the memory owner remains alive
and unmodified until then/disposal. Pump retains owners/indices, not spans.
Advance resumes queued *bounded* token state before consuming more source.
EOF is sticky and may require many paused calls to drain. Rejected has a terminal
diagnostic; exceptions fault rather than becoming that result. NeedInput means
all supplied input consumed; Completed means successful EOF and terminal events
accepted. Supply after EOF/terminal/fault is invalid. Sink identity is fixed
for the context. Public push never exposes pause; reader sink pauses on one
public token and resumes the unconsumed decoded/source tail on the next read.

A composite tag or effective DTD model may contain multiple queued public
notifications. Its adapter must retain owned scalar strings/model and integer
cursor, never callback views, between pauses. Tag Attributes and Namespaces are
one bounded retained tag; DTD declarations are projected one at a time. Source
transport must not refill/reuse its buffer while pump still needs it. Async
resolver work suspends before constructing a view, invokes XML ResolveAsync and
ReadAsync/DisposeAsync, and shares aggregate quotas with primary input. It must
not use Task.Run, sync-over-async, or prefetch whole external resources.

Required runtime checks remain ordering/namespaces/defaults, all chunk splits,
early notifications, identity of exceptions, cancellation/reentrancy, sync/async
equality, and bounded 1 GiB memory from the performance goals.
