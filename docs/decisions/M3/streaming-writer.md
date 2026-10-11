# M3 streaming/writer freeze

Exact public member baseline: task-owned M3 *.cs.fixture files. They are excluded
from runtime compilation and packaging. Sync/Async consumer projects reference
only the integrated M2 runtime; the DOM partial project compiles the same runtime
source plus its additive declaration fixture. Fixture bodies simulate consumers,
not an implementation, and must never be copied as shipped stubs.

Decisions:

- Sixteen matching handler methods/delegates/On* properties; minimal subclass
  and lambda styles remain equivalent. Borrowed collections use concrete
  enumerators over ordinary owned rows, not spans of ref structs.
- Aggregate one start tag, after namespace expansion/default processing. Incomplete
  attribute values retain M2 parts; skipped content references remain explicit.
- DTD events project the effective merged M2 model in collection order, after
  the DOCTYPE completes. Lexical source order/DTD comments/entity boundaries
  are not available. Notation provenance is nullable because M2 does not store it.
- Strict void SAX stays strict. Diagnostics SAX uses XmlParseResult of an owned
  empty completion marker, and repaired input never emits successful EndDocument.
  Reader/push stay strict, with M2's nonvalidating unresolved values preserved.
- Sync and async declarations compile in separate layers. Async pump uses owner
  memory plus integer cursors, calls synchronous borrowed sinks only after await,
  and suspends external resource work. No await crosses a span/view's use.
- Reader pauses after one accepted notification; false means accepted, no replay.
  Tag/model iteration retains bounded owner state, not a list of a chunk's events.
- Forward writer seals pending tags on content/end/flush. EndDocument drains but
  does not caller-flush; Flush explicitly does. Disposal aborts. Async Write*
  methods take memory rather than spans. Forward indentation commits a parent
  to mixed or element-only mode; DOM serialization retains full M1 lookahead.
- Separate XmlAsyncSerializer avoids modifying the nonpartial M1 serializer.
  DOM async wrappers are additive partial files. No later milestone APIs needed.

## Required owning repairs before runtime feature acceptance

These are target boundaries, not completed runtime behavior. The coordinator
accepted recording repairs while passing the declaration/fake/lifetime criterion.
Affected workers must wait for integrated, verified owning repairs or receive
explicit ordered ownership of the boundary; they cannot edit a peer's core.

1. Coordinator owning task `M3-attribute-metadata`: `src/Xemele/Parsing/Events/Views.cs`,
   `src/Xemele/Parsing/Core/XmlParserCore.cs`,
   `src/Xemele/Parsing/Core/XmlParserCore.Attributes.cs`:
   add explicit internal IsDefaulted provenance to complete attribute events and
   propagate it from ApplyDefaultAttributes through Drain; authored attributes
   false, injected defaults true. Incomplete events already carry DefaultSequence.
   Public adapter must not infer provenance from value equality or location
   equality. Preserve earlier event consumers with an optional internal constructor
   argument/default or coordinated adapter updates. Needed by callbacks/reader.
2. Coordinator owning task `M3-async-resources`: `src/Xemele/Parsing/Core/XmlParserCore.Resources.cs`,
   `src/Xemele/Parsing/Core/XmlParserCore.Entities.cs`,
   `src/Xemele/Parsing/Entities/Runtime/ExternalXmlSource.cs`, and the owning
   core delivery boundary in `src/Xemele/Parsing/Core/XmlParserCore.cs`:
   introduce an optional async resource mode, explicit NeedResource and
   AdvanceResourceAsync with exact consumed input, and resumable request/read/framing continuations before
   external work; retain source owners, DTD/model cursor and consumed offsets.
   Async transport must await existing XmlResolutionSession.OpenAsync and
   XmlResourceHandle.ReadAsync/DisposeAsync. Resolve/read cancellation, aggregate
   quotas, source URI/byte mappings and exactly-once disposal must match sync.
   Existing synchronous entry points remain synchronous and work independently.
   NeedInput/Paused cannot lose a resource continuation or replay a notification.
   No full-resource cache, blocking bridge or Task.Run substitute. Required before
   M3-async external-resource equality/async-only resolver acceptance.

3. Coordinator should create an explicit `M3-streaming-pump` owner, ordered after
   callbacks, attribute metadata and async resource repair, for
   `src/Xemele/Streaming/Core/`, corresponding focused tests and report paths.
   That task implements the common byte-decoding/offset/EOF transport plus full
   notification/tag aggregation, DTD model projection cursors, incomplete values,
   diagnostic delivery, owned token storage and pause/resume pump. SAX, push and
   reader depend on its integrated verification and consume it through thin
   task-owned adapters; none owns a duplicated parser or edits another adapter's
   core. Async transport extends it in explicitly owned partial files. Tests must
   cover split decoding, one-token backpressure, no replay/loss, bounded tag/model
   retention, final EOF draining, source maps and exactly-once cleanup. This is
   implementation ownership work, not a runtime change by M3-contracts.

Compiled fakes prove the proposed sink/pump and language lifetime are feasible;
they do not prove runtime integration, chunk splits, quotas, performance or
real asynchronous external resource behavior. Runtime workers own those checks.
No runtime hot path was changed; allocation benchmarks are not applicable here.
