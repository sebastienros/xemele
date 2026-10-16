# Streaming and writer internals

- Sixteen matching handler methods/delegates/On* properties; minimal subclass
  and lambda styles remain equivalent. Borrowed collections use concrete
  enumerators over ordinary owned rows, not spans of ref structs.
- Aggregate one start tag, after namespace expansion/default processing. Incomplete
  attribute values retain XML parts; skipped content references remain explicit.
- DTD events project the effective merged XML model in collection order, after
  the DOCTYPE completes. Lexical source order/DTD comments/entity boundaries
  are not available. Notation provenance is nullable because XML does not store it.
- Strict void SAX stays strict. Diagnostics SAX uses XmlParseResult of an owned
  empty completion marker, and repaired input never emits successful EndDocument.
  Reader/push stay strict, with XML's nonvalidating unresolved values preserved.
- Sync and async declarations compile in separate layers. Async pump uses owner
  memory plus integer cursors, calls synchronous borrowed sinks only after await,
  and suspends external resource work. No await crosses a span/view's use.
- Reader pauses after one accepted notification; false means accepted, no replay.
  Tag/model iteration retains bounded owner state, not a list of a chunk's events.
- Forward writer seals pending tags on content/end/flush. EndDocument drains but
  does not caller-flush; Flush explicitly does. Disposal aborts. Async Write*
  methods take memory rather than spans. Forward indentation commits a parent
  to mixed or element-only mode; DOM serialization retains full XML subset lookahead.
- Separate XmlAsyncSerializer avoids modifying the nonpartial XML subset serializer.
  DOM async wrappers are additive partial files. No later module APIs needed.

## Attribute metadata

Complete attribute events carry explicit `IsDefaulted` provenance, propagated
from default application through pending delivery. Authored attributes are false;
injected defaults are true. Incomplete events carry DefaultSequence. Public
adapters must not infer provenance from value or source-location equality.

## Asynchronous resources

An optional asynchronous resource mode uses `NeedResource` and
`AdvanceResourceAsync` with exact consumed input and resumable request/read/framing
continuations. Retain source owners, DTD/model cursors, and consumed offsets.
Await `XmlResolutionSession.OpenAsync` and `XmlResourceHandle.ReadAsync/DisposeAsync`.
Resolution/read cancellation, aggregate quotas, source URI/byte mappings, and
exactly-once disposal match synchronous behavior. Synchronous entry points remain
synchronous. NeedInput/Paused must neither lose a continuation nor replay an event.
No full-resource cache, blocking bridge, or Task.Run substitute.

## Shared streaming pump

SAX, push, and reader adapters use a common byte-decoding/offset/EOF transport,
notification/tag aggregation, DTD model projection cursors, incomplete values,
diagnostics, owned token storage, and pause/resume pump. Do not duplicate parsers.
Test split decoding, one-token backpressure, no replay/loss, bounded tag/model
retention, final EOF draining, source maps, and exactly-once cleanup.
