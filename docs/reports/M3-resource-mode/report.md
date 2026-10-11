# M3 resource mode bridge

The internal `SetAsyncResources(bool)` methods select resource I/O for the next
operation while holding the pump/core gates. Selection does not advance parsing
or replace an owner, cursor, model, resolution session, cancellation token or
quota counter. Pending work, suspended async DTD parsing/request/source cleanup,
and deferred DOCTYPE completion reject selection before either flag changes.
Both existing constructor modes and manual `NeedResource` service remain intact.
Future M3 async input adapters select the mode while their own context guard is
held; this task adds no public APIs or adapter dependency.

Synchronous disposal is an explicit synchronous abort after either mode. It
detaches and attempts every owned async DTD source/disposal, releases its parser,
then invokes the existing general-source/session cleanup. The first cleanup
exception retains its identity, while an existing primary failure takes
precedence. The core always releases scratch and depth charges; the pump drops
its borrowed primary owner and token/model references. `DisposeAsync` continues
to await owned source disposal even after a sync mode was selected. No blocking
async bridge or background cleanup is introduced. An owner whose synchronous
disposal throws is attempted once, and remaining owners still receive cleanup.

Tests cover delayed async-only resolver/read/disposal from default sync creation,
all primary split points, alternating nested sources with matching notifications
and locations, model/session/scratch identity and every quota, resource/depth
limit rejection, pending open/parameter/DTD/DOCTYPE rejection, suspended gates,
token rebinding/cancellation, callback identity, throwing synchronous cleanup,
caller primary lifetime, and genuine awaited async abort after mode selection.

The standalone probe project is under this report's `Probe/` directory; its
explicit source fixture is under the owned benchmark path. Setup/scratch
allocation is separate from warmed idle mode selection. A process-wide counter
probe measures core/pump setters and a no-op control after 128 warmup batches,
with 1024 measured batches of 1024 calls and a 4096-byte positive control.
Counters-disabled `--control` performs the same semantic checks without running
measurement counters. Three fixed BenchmarkDotNet MemoryDiagnoser cases report
no-op, core setter and paused pump setter latency/allocation; only one CLI
in-process job is configured. Timing on a shared host is informational and
supports no optimization or regression claim. End-to-end mixed reader input
measurement belongs to M3-async-input after integration of this bridge.

Actual check revisions, commands and outcomes will be recorded after the
coordinator's active measurement lease is released and this source is tested.
