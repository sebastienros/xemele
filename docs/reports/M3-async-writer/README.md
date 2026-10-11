# Async forward writer evidence

Worker root: `/Users/sebastienros/.codex/worktrees/705c/xemele`, branch `codex/m3-async-writer`.
Base `827fde5f82dd202abb53c300d3a4a59b7cd3eda0`, coordinated claim
`755cd5cf5a87bb30d507d042fbabe8e03512f058`. Imported narrow API type-row ownership
grant `a1b76e16aa4dfeaeada2c69f21123cb596e4d2d0` before modifying the row.
The fourteen existing synchronous API rows and their IDs are preserved; the
writer type now implements both `IAsyncDisposable` and `IDisposable`.

The async partial emits all markup, escaping, namespace declarations, BOM and
encoder completion through actual bounded `Stream.WriteAsync` calls. Only
retained memory and integer offsets cross awaits; the rented 8192-byte encoder
owner stays live until the awaited write completes. No complete output is built,
no synchronous writer emission/transport helper is called, and caller streams
are never disposed. The shared operation guard spans awaits and rejects sync,
async and disposal reentrancy. Both tokens are combined only for the current
call; linked token setup is an explicit allocation boundary when both distinct
tokens are cancellable. State validation and scope semantics match the verified
writer. Caller memory and emitted attribute cursors are cleared on completion.

## Preparation history

* First runtime Release build passed without warnings.
* Initial test build failed xUnit1051 because tests omitted the test-context
  cancellation token. Calls now pass it, except intentionally controlled
  cancellation tests, and the subsequent build passed without warnings.
* A stale pre-test binary was invoked following that failed build and discovered
  zero tests. This is not passing runtime evidence.
* Seventeen focused delayed async-only transport cases passed. Reentrant callback
  and caller-owner tests extended the lane to nineteen cases.
* Immediate GC inside the completing continuation cascade reported a live owner
  (18/19 passed). Clearing supplier references and explicit runtime memory
  parameters alone did not change that immediate-boundary result.
* Coordinator's bounded control adds exactly one `await Task.Yield()` after the
  supplying operation completes, with no intervening writer operation/disposal,
  then collects and keeps the writer alive. Both 1 MiB caller text and attribute
  owners are dead. The focused retention control passed 1/1. This distinguishes
  transient stack roots from idle writer retention; no acceptance was removed.
* Standalone immediate/delayed async-only probe preparation passed with counters
  disabled. No allocation or timing measurements were run during preparation.

Final revision-pinned checks and authorized allocation/BDN results are recorded
in the task JSON and the adjacent raw reports when completed.

## Prepared identity and scope

Runtime/retention/API type-row commit:
`80119207072d0eb292f9cb6bf186bc8cf2bdb3fd`.
Final prebuilt measurement source/harness revision:
`83edfe8c6b2701587b1ccaf3d680ad21da43f22a`.
The probe project was moved into this report directory, compiling its owned
`.cs.fixture` source explicitly, because a nested benchmark project generated
assembly attribute files that the parent benchmark project's SDK glob included.
The first parent benchmark build failed CS0579; after isolation it passed with
zero warnings/errors. Shared build/project files were not edited.

Pinned executable:
`/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet`.
Prepared SHA-256:

* runtime: `2ed69f4e6737f74f307662f29d7362e76de4f97ccb7f9d8a9fb4b191d3c8fff6`
* probe: `b10047007a157e503d16d156ec8fb6da860686f9df90fa0376c1ddc30ce02b81`
* BDN: `b16fa9f287637339572e428de09d8c141a566bc3c253db89c346c8d6eeacc388`

Each buffered primitive group performs five completed `ValueTask` calls:
two adjacent escaped text writes, start element, attribute, and empty end element.
The measured group is checked to perform no I/O; flushes are outside its counter
scope. Its positive control allocates a 256-byte array per group and consumes it.
Distinct operation/call token linking is measured separately as per-call setup.
Cold/warm writer creation includes owned state and pooled scratch acquisition;
options/corpus generation belongs to harness setup. Delayed document measurements
use process-wide counters to account for cross-thread continuations, include
writer setup, and exclude corpus creation. Retained scratch is inspected while
the writer is alive; pending tag strings and namespace/depth state are separate
contract-required ownership, not an entire output buffer. Timing is informational
on this shared Arm64 host, and x64/release hardware checks remain follow-ups.

## Authorized allocation attempts (failed; not acceptance evidence)

First lease at `83edfe8`: probe exited 1, original current-thread exact-zero
counter reported 128 B over 16,000 buffered five-call groups (0.008 B/group).
The no-I/O check passed. Positive control: 280 B/group, cold setup: 10,816 B,
warm setup: 1,056.216 B/writer, distinct linked-token setup: 80 B/call.
Retained scratch: 8,192 B. Suspended composition: 44,321.6 B/document using
precise process-wide counters. The gated sync writer and BDN steps did not run.
`allocation-initial-failed.json` preserves the raw result.

The coordinator approved one bounded harness-only correction: initialize the
identical 16-group buffered batch shape, record warm-up allocations separately,
and preallocate per-batch distributions outside all counter brackets. Current-
thread exact-zero and no-I/O gates remain unchanged; added process-wide buffered
counters are diagnostic context, while suspended composition still uses precise
process-wide counters. The counters-disabled control also executes the actual
buffered batch shape before measurement preparation is accepted.

Second source/harness revision:
`3a0e4070ac696655a624fbd8b32fb76a8fd42dc9`.
Runtime source remains `8011920`; rebuilt SDK Git/assembly revision metadata
changes the binary hashes without changing the runtime source closure.
Actual second prepared SHA-256:

* runtime: `b5443d5d119eed97527cf735f437d1f91c8acb45c44c1c5761c3c4e4e5f4bece`
* probe: `ce6ae509f2e04ac58bf733b8cc2509b27e51ed2c7d72bee7c81b5b4493cc3d9f`
* BDN: `94fc34c1d72dbbe8378cf69bb7f2fc9ca8d5823f48c1633042696228e9a75725`

Second lease was allocation-only, bounded 40 s, with unrelated host CPU work
explicitly excluding timing. Probe exited 1; all children completed, and the
lease was immediately released before inspection. Warm-up batch 0 allocated
344 B, all other 199 warm batches allocated zero. Steady batch 577 allocated
128 B, all other 999 steady batches allocated zero. Both current-thread and
precise process-wide counters observed those same values. Exact-zero remains
failed: 128 B / 16,000 groups. Positive control, no-I/O, linked-token setup and
scratch bound passed. Suspended composition reported 44,245.6 B/document and
maximum write length 2,049 B. `allocation-second-failed.json` preserves all raw
batch distributions. Original failure was never mislabeled process-wide.

No additional allocation/timing reruns followed the two proving cycles.
Coordinator root attribution/resolution is required; sync writer checks were
not run by this worker due only-pass gating, and BDN was not authorized in the
second lease. No timing success or API global-discovery success is claimed.
