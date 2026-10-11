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

## Actual branch evidence

Implementation revision: `ede12339ef78ca77d071a826ec48f2994dbfb422`.
Both pinned single-node Release tests/probe builds passed with zero warnings or
errors. Focused tests passed 218/218 (18 own, 29 earlier async resources, 33 earlier
pump, 78 entity and 60 resolution tests); no skips/errors/not-run. Two unchanged
inherited allocation-only methods were excluded from ordinary correctness to
keep counters under the serial lease. The approved extractor matched 655 public
members against the duplicate-checked 12-fragment canonical API union. Validator
passed 147 tasks; `git diff --check` passed. Exact commands and durable output
paths are recorded in the task JSON.

An initial uncommitted development run passed 12/14 tests and failed two fixture
assertions: a non-public MemoryStream buffer and a callback expectation before a
pending resource refill. Both fixtures were corrected; the final expanded 18
own cases passed on the known committed revision. No runtime failure is hidden
by those corrections or the allocation-only exclusions.

The counters-disabled prepared control passed before measurement. Its zero
fields are not allocation evidence. Frozen product SHA-256:
`440B688112DF866CC1A0BCA5190E2142DE887FC1E916F316E88B4F80DCE29A16`.
Frozen probe SHA-256:
`E10EF104BB7749A823D2E8588A763BB34CE80FAD6FBE065334DFA324C88BDBEF`.

The coordinator granted lease `m3-resource-mode-ede1233-measure`: one prepared
allocation child with a 30-second timeout, followed only on exit 0 by the same
DLL's fixed benchmark child with a 150-second timeout. No source/build/config
change, retry or unrelated process operation occurred during the lease.
Allocation child, benchmark child and sequence all exited 0. The tool session
completed and both children were reaped before reporting RELEASED, inspecting
results or updating this evidence.

Process-wide `GC.GetTotalAllocatedBytes(true)` measurements:

| Scope | Calls | Managed bytes |
| --- | ---: | ---: |
| Warm paused pump selection | 1,048,576 | 0 |
| Warm core selection | 1,048,576 | 0 |
| Warm no-op control | 1,048,576 | 0 |
| Positive 4096-byte array | 1 | 4,120 |
| Cold owned fixture/scratch setup | 1 | 61,040 |

Each warm path had 131,072 initialization/warmup calls before counting. The
semantic oracle confirmed the paused token/primary cursor had not advanced and
both modes remained selected. Setup costs are owned fixed parser/pump scratch,
not attributed to idle selection. This synchronous primitive uses process-wide
counters; it makes no allocation claim about awaited resolver work or readers.

BenchmarkDotNet 0.15.8's hard suite gate found exactly three cases with one
InProcessEmit job (3 warmups/3 iterations). All three completed in 21.75 seconds
with 0 B/op reported by MemoryDiagnoser. Informational means: no-op 0.5347 ns,
pump setter 4.2956 ns and core setter 2.4159 ns per operation (1024 calls/invoke).
These numbers are scope-specific microbenchmark observations, not an
optimization, comparative or regression claim. The host was shared with
unrelated Silex activity; three-iteration intervals are broad. macOS 15.8.1,
Arm64, SDK 10.0.400/runtime 10.0.11, Concurrent Workstation GC/Arm64 RyuJIT were
recorded; the CPU model was unavailable and high process priority was denied.
Raw allocation JSON, logs, CSV/HTML and GitHub benchmark tables are retained.

The branch is implemented, with integration revision unset. Independent source-only review approved the exact implementation with no
concrete A1/A2 blockers. Coordinator integrated verification remains separate. No later adapters were used;
composed Read/ReadAsync measurement belongs to M3-async-input after this bridge
is integrated.
