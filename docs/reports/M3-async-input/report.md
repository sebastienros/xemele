# Async SAX, reader, and DOM input

Runtime implementation revision: `3679687dcbd2ffbe84bcc13c9c74b0899c53baf7`.
Final probe-only revision: `9e9da61896bb185981825e28f306158745388f3a`.
SDK 10.0.400, .NET 10.0.11, macOS 15.8.1 Arm64.

SAX uses the verified streaming pump's asynchronous resource mode, calling only
`Stream.ReadAsync` on a bounded 4096-byte input buffer. Handler notifications
remain synchronous. The reader holds its existing guard across every await,
invalidates current data at invocation, links operation and call tokens only
when both differ, and selects the verified guarded resource mode for each read.
Synchronous Read has exactly the approved idle mode selection insertion.
Async disposal awaits owned resources; synchronous disposal retains the verified
once-only synchronous abort. Caller input stays open.

DOM uses a task-owned bounded decoder/core transport and the existing DomSink.
Its retained owners/indices survive actual `AdvanceResourceAsync` suspension;
decoder scratch is not reused until the same core consumed cursor has resumed.
This preserves complete DTD models, incomplete-value metadata, normalized scalar
locations, logical text boundaries, and earlier recovery semantics without
projecting away information through public SAX views. Primary exceptions and
structured diagnostic failures take precedence over cleanup failures.

The API fragment adds exactly the six frozen asynchronous methods. The reader
type's existing fragment gains only IAsyncDisposable. The approved extractor
compared 712 compiled members against the complete duplicate-checked current
fragment union; both IDs and member identities are unique. Run
`python3 docs/reports/M3-async-input/check-api.py` to reproduce the comparison.
The official integrated corpus inventory remains the coordinator's check.

## Correctness and preparation

Postcommit single-node Release builds of tests, probe, and sample passed with
zero warnings/errors (`--no-restore -m:1 /nodeReuse:false /p:UseSharedCompilation=false`).
The first local `--no-restore` attempt needed assets; restore then succeeded.
Precommit test/sample compilation mistakes and a SAX assertion incorrectly
assuming unsplit text were corrected before the frozen revision.

`preservation.xml` records 339 passing cases, zero failed/errors/skipped/not-run:
31 own cases plus earlier SAX, reader, streaming pump, async resources, resource
mode, entity, recovery, and DOM checks. The unchanged inherited allocation-only
methods `DomTests.WarmBorrowedNameHitsAllocateZeroAndUseOwnedInternedStrings`
and `ContextTests.BorrowedLookupsHaveNoWarmAllocationAndCannotBeMutated` were
excluded because measurement requires the coordinator's lease. Their prior
evidence is preserved; no allocation assertion was weakened.

Own checks cover genuinely awaited async-only short transport and resource
streams; every primary chunk size 1–32; exact SAX events, full current-reader
token fields/locations, DOM logical-node/DTD equality; recovery diagnostics and
completion flags; long text and incomplete metadata; validation and quotas;
operation/call cancellation and token rebinding; mixed sync/async live resource
cursors; invocation-time current invalidation, concurrency/reentrancy, repeated
EOF/disposal; callback/primary I/O exception identity and awaited once-only
resource disposal. The compiled sample executes every new API and leaves its
caller stream open.

`python3 tools/planning/tasks.py validate` passed all 147 task records.
The counters-disabled `control.json` passed at the final probe revision with all
measurement counters disabled; it is preparation, not allocation evidence.
Its generated source has no whole-document byte array and consumed 8,396,803
bytes while retaining the active reader. Runtime/probe hashes are recorded.

## Measurement scope

The standalone report-owned probe explicitly includes the owned `.cs.fixture`
outside parent benchmark globs. Warm buffered ReadAsync measures actual current
text dispatch with the verified pump and retained primary tail. A second stream
forces genuine one-byte asynchronous suspension, distinguishing state-machine
and scheduler allocation from the completed fast path. Cold setup, no-op and
positive controls are separate. Process-wide allocation counters are used;
per-thread counters cannot establish suspended async behavior. Generated 1 MiB
and 8 MiB active-reader retention include 32 full-GC live samples, each 256 KiB,
with reader kept alive; the maximum delta is gated at 128 KiB. The probe requires
zero buffered/no-op allocation, positive-control allocation, exact suspended
character/read/suspension consumption and positive bounded suspension allocation.
Raw values are written before any failure is thrown. DOM/name/attribute
persistence and unique-name interning remain owned
allocation boundaries and are not claimed allocation-free.

BenchmarkDotNet has exactly one fixed InProcess job and four cases: synchronous
buffered text, asynchronous buffered text, genuinely suspended asynchronous text,
and no-op. Each uses 1024 invocations, unroll one, one launch, three warmup and
three measurement iterations. BenchmarkConverter rejects extra cases/jobs before
running; discovery-only mode validates that guard without observations. The
runner also rejects failed cases, missing statistics or nonzero exits after run.
Shared-host timings are informational;
no optimization/regression or x64 qualification claim is made. Full 1 GiB
qualification remains the coordinator's corpus check.

## Actual allocation and retention results

Coordinator lease `m3-async-input-9e9da61-measure` authorized the exact prebuilt
serial sequence at the final probe revision. Allocation exited 0 in 1.278 s
(pid 86791); benchmark exited 0 in 0.684 s (pid 86794). The wrapper reaped both
direct children and confirmed their process groups absent. The lease was released
before any report inspection or evidence edits. No compilation, retries, option
changes, or parallel workloads occurred during the window.

| Boundary | Actual managed allocation |
| --- | ---: |
| Cold reader/decoder/core/current setup and first header | 57,336 B |
| 4,096 warm buffered ReadAsync text calls / characters | 0 B total |
| 1,024 forced one-byte suspended reads / characters / suspensions | 327,680 B (320 B/call) |
| No-op control, checksum 2,048 | 0 B |
| Positive 4,096-byte array | 4,120 B |

All semantic, zero-allocation, positive-control, suspended-consumption and
bounded-active-memory gates passed (`allocation.json`, `valid=true`). Across
32 active-reader samples every 256 KiB through 8 MiB, live growth peaked at
8,576 B, identical to both 1 MiB and 8 MiB endpoints, below the frozen 128 KiB
bound. The generated stream consumed 8,396,803 bytes and never owned a
document-sized array. Full 1 GiB and x64 qualification were not run in this task.

| Fixed BDN case | Mean | MemoryDiagnoser allocation |
| --- | ---: | ---: |
| Synchronous buffered text | 341.539 ns | 0 B/op |
| Asynchronous buffered text | 598.565 ns | 2 B/op |
| Genuinely suspended async text | 2,724.935 ns | 320 B/op |
| No-op | 0.163 ns | 0 B/op |

All four cases completed with statistics and successful exits in the single
fixed job; raw logs and CSV/HTML/Markdown exports are retained. The buffered
async BDN result is **2 B/op**, while the direct warmed process-wide probe
observed **0 B total** over 4,096 calls. Their observed boundaries differ; no
unproven attribution for the discrepancy or BDN-zero claim is made. The fixed
short job produced MinIterationTime and no-op ZeroMeasurement warnings. Processor
sysctl was sandbox-denied (unknown processor), not a failed benchmark. Wide
confidence intervals and short iterations make timing informational only.
