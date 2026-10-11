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

## Bounded attribution (separate from normal acceptance)

Attribution source/probe revision `824470cb8320b73c0cd134bc7a785fd2ff913531`:
actual runtime SHA-256
`ca1c3a9c66f653b2ced0d4793ee8eda5f8cdf75927cfe0863736145c6b5fe810`, probe
`8f9622da96bff376671e22dd64082e2d956121f283c3cc66222d9fea12d32ac4`.
Runtime source remains `8011920`; hashes include generated Git/assembly stamps.

The coordinator authorized one default-runtime `--diagnose` process, bounded
20 s, with preallocated records for 200 warm and 1000 steady batches, 16 groups
of the same five writer calls. Every call has current-thread and precise process
counter deltas plus an empty bracket with the same four reads. Pre-acquired
reflection-derived compiled getters capture buffered/steps/depth outside both
per-call and empty brackets; whole batch counts explicitly include diagnostic
outside-bracket overhead. Sparse distribution defaults every omitted counter
to zero, including when no steady nonzero call exists.

`allocation-attribution.json`: all 80,000 steady calls, all empty controls, and
all 1000 steady whole batches reported zero in both scopes. Warm batch0/group0
Attribute allocated288B, buffer18/steps17/depth2 ->18/19/2; EndElement56B,
18/19/2 ->28/22/1. Warmbatch0 total344B; warmbatch96 total152B occurred entirely
outside the writer/control brackets. Program exit0 means diagnostic completion,
not a global acceptance pass; historical normal helper-loop128B remains intact.

Operational correction: a `write_stdin` yield still containing session4760 was
mistakenly reported as completed/released. That report was immediately corrected
before worker inspection/metadata. The coordinator had already released peers
and read raw JSON in another process. The actual final poll then returned
exit_code0 without session_id and explicit child-complete stdout; confirmed
release preceded worker inspection. Other processes cannot alter own-process or
current-thread allocation counters; no timing conclusion or reporting-error
rerun was taken. No children remained after final confirmation.

Source-only local IL inspection is documented in `il-review.md`: no explicit
managed factory in successful Primitive/Complete, identical direct-switch
arguments/calls, and getters/reflection setup outside per-call brackets.
This alone cannot identify the historical CLR allocation source.

The coordinator then authorized one unchanged normal-mode probe process with
process-local `DOTNET_TieredCompilation=0`, bounded20s; no build/source/options
other than this named attribution setting, no retry or timing. Actual exit0/no
session_id and child-complete stdout were confirmed before release/inspection.
`allocation-tiering-control.json`: warm0=344B, all1000steadybatch counters0 in
both scopes; positive280B/group, linkedtokens80B/call, scratch8192, no-I/O passed;
suspended precise process44285.6B/document. This supports runtime/tiering
initialization or OSR interaction in the historical helper-loop observation but
does not identify the exact allocation site. It is not substituted for default-
runtime acceptance. Coordinator adjudication remains explicit.

## Coordinator bounded adjudication

The coordinator accepted the mandatory actual default-runtime primitive
observation from the raw 80,000 steady writer calls **and** entire 1000 steady
batches reporting zero in both scopes, with no I/O, together with independently
preserved positive/setup/linked/scratch and genuine suspended-process counts.
This evaluates actual writer deltas; diagnostic exit0 still denotes completion.
The original helper-loop128B failures remain failed historical scopes, with the
exact CLR allocation site unproven. The named tiering-disabled control supports
an initialization/OSR inference, not a retroactive default-runtime pass. No gate,
runtime, criterion, or failure was changed to manufacture zero; no further probe
or harness correction retries were approved.

Remaining prebuilt correctness/BDN checks reuse revision3a copies with runtime
source801 unchanged. Actual SHA-256:

* tests DLL: `1eeb1f150d28c415b196cb3491a9fdfa551e17c5c14a9c0942a25799235355a0`
* benchmark DLL: `94fc34c1d72dbbe8378cf69bb7f2fc9ca8d5823f48c1633042696228e9a75725`
* each test/benchmark local runtime DLL: `b5443d5d119eed97527cf735f437d1f91c8acb45c44c1c5761c3c4e4e5f4bece`

These differ from the latest diagnostic runtime-directory824 Git-stamped hash;
no unnecessary rebuild or repeats of passing69/API/sample checks are planned.
The prior process-local tiering override was never persisted in the parent or
applied to these default-environment checks. A3 overall remains pending until
the 19 sync writer and six informational in-process benchmark cases complete.

## Sync preservation and benchmark discovery correction

Authorized default-environment prebuilt sync writer lane at3a passed all19,
including its existing allocation regression. Earlier50serialization and19async
remain passing at runtime source801, for88focused source cases in total. No
passing tests/API/sample/allocation evidence was repeated for a benchmark fix.

Initial BDN discovery reported12 cases instead of authorized6. The wrapper
immediately killed the processgroup before measurement; childexit-9, wrapper124,
no session/children remained. Its shared stop() incorrectly labeled the
intentional scope kill as timedoutTrue; this was not an elapsed timeout.
`bdn-scope-failed.txt` preserves that initial result with no measuredcase claim.
Cached source-only BDN0.15.8 IL (`inprocess-attribute-il.txt`,
`job-attribute-il.txt`) proves `[InProcess]` adds its own job via
JobConfigBaseAttribute/ManualConfig.AddJob. The CLI's Dry InProcess job added a
second: twojobs*threecorpora*twomethods=12.

Coordinator authorized one benchmark-only correction, committed
`7f99de9ab0683a1ce581e6f4f902a02353389de3`: remove the redundant job attribute,
keep MemoryDiagnoser, methods/corpora and the sole explicit CLI Dry InProcess
job. Build passed0warnings/errors with BuildProjectReferences=false to avoid
unneeded runtime rebuilding; flatlisting showed the two methods. Actual frozen
benchmarkSHA `c035149aa2a5a1b9a4752504ec581f0770c19c1f01b9549b3874f8461cd4345f`,
local runtime copied from existing824 build:
`ca1c3a9c66f653b2ced0d4793ee8eda5f8cdf75927cfe0863736145c6b5fe810`, source801.
A six-case discovery/InProcess hardguard remains; corrected run is pending.

The coordinator explicitly permits only informational contended timings while
unowned host Silex.Jepsen work is active (observed CPU407). Raw latencies are
**unusable for speedup, regression or repeatability claims**. Own-process
MemoryDiagnoser and actual allocation observations remain distinct. No optimized
scan claim is made; M9 dedicated repeatablex64/Arm64 qualification remains a
release follow-up. External user processes were not touched.

## Final six-case branch benchmark check

Corrected source revision `7f99de9ab0683a1ce581e6f4f902a02353389de3`, exact
prepared command with filter AsyncWriterBenchmarks, Dry + InProcess,
iteration3/warm1/launch1/invocation1/unroll1. Coordinator lease bounded110s;
actualexit_code0, no session_id, discovery6 twice, no timeout/invalidscope,
child-complete stdout and nochildren confirmed before prompt release/inspection.
No builds, generation, source/options edits or retries occurred during the lease.

Exactly six cases ran in one `InProcessEmitToolchain` job. MemoryDiagnoser's
inclusive Dry-operation allocations (full writer/setup/framework/runner scope):

| Corpus | Synchronous | ImmediateAsync |
| --- | ---: | ---: |
| long-ascii | 9.67 KB | 13.63 KB |
| multilingual | 11.27 KB | 13.35 KB |
| short | 13.56 KB | 13.63 KB |

These are not isolated primitive costs or a steady writer-only allocation
attribution; use the separately scoped standalone evidence for those claims.
Raw CSV/Markdown/HTML/log/console results are preserved. BDN emitted small-
iteration warnings, and host contention makes **all latency ratios and speedup,
regression or repeatability conclusions unusable**. No comparative claim or
invented threshold is applied. The source-only scalar escaping baseline remains;
no unmeasured SearchValues optimization was added.

All task acceptance criteria are implemented on this branch:12frozenmembers,
19async+19sync+50earlier source cases, API651, sample, actualdefault bufferedzero
adjudicated from measured deltas, allocationpositive/setup/linked/continuation/
scratch evidence, and six informational BDN cases. Historical failures and
operational corrections remain visible above; none was retroactively passed.
Task status is implemented with integration_revision=null. Coordinator combined
verification, private offlinecorpus provisioning and M9 repeatablex64/Arm64
release qualification remain the explicitly scoped follow-ups.
