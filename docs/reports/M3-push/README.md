# M3-push implementation and checks

`XmlPushParser` implements the frozen strict byte-only constructor, Append,
Complete and IDisposable surface. It uses only the verified callback models and
synchronous streaming pump. Its task-owned forwarding sink does not depend on
SAX implementation visibility. Earlier APIs are not changed.

Append copies borrowed bytes through one 4096-byte owned scratch buffer, supplying
and draining each segment before reuse. Empty appends are real work. Complete
supplies sticky EOF and requires successful terminal completion. Caller memory
is never retained. The pump retains quota-bounded lexical/tag/resource state;
there is no whole-document event list. Completion/fault/disposal drops references
to the pump, handler sink and byte scratch.

A nonblocking atomic gate rejects concurrent or callback-reentrant Append,
Complete and Dispose. A caught competing-call error does not fault the active
operation. Exceptions escaping active parsing permanently fault the context;
cleanup is attempted once without replacing the primary exception. Disposed
operations reject with ObjectDisposedException and repeated idle disposal is
idempotent. Construction validates strict options/limits without callbacks.

There is no caller-stream input overload or resource leaveOpen constructor in
the frozen API: byte spans are borrowed, whereas successful XmlResolvedResource
objects transfer ownership and close their content exactly once.

## Verification plan

Focused tests compare complete notification content against verified SAX for
every two-chunk byte split and one-byte chunks, including UTF-8 scalars,
namespaces, CDATA, comments, PI, declaration, effective DTD/default projection,
unknown attribute parts and skipped content. Lifecycle, immediate prefix delivery,
mutated caller bytes, reentrancy and competing threads, cancellation, callback
identity including XmlParseException, terminal diagnostic identity, resource I/O
and cleanup failures are covered. A capacity assertion checks that large caller
chunks and a growing body do not enlarge transport scratch.

The standalone probe project is here under Probe; its entry point uses a fixture
extension to avoid the parent benchmark project's default source globs. It links
the single task-owned benchmark source. --control checks behavior without GC
allocation or retention observations. --allocation separates cold owned setup,
warm continuous text, positive allocation control, 1 GiB retained-memory stress
and owned end-to-end setup. --benchmark runs exactly two chunk-size jobs and one
representative end-to-end job using BenchmarkDotNet MemoryDiagnoser. Fixtures are
prepared outside timing; assertions consume callback counts. No scanning or
matching algorithm changes are introduced by the adapter.

All commands, observed outcomes, known tested revision and measurement limits
will be recorded after execution. Allocation/BDN observations require the
coordinator's bounded serial grant. Cross-architecture conclusions are not made
from one local host.
