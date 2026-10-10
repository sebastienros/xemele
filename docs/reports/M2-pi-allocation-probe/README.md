# PI allocation probe repair

The only existing test changed is
`PiTargetTests.RepeatedBorrowedPiTargetsAllocateZeroAfterWarmup`. Both phases
now use one non-inlined helper checking NeedInput, complete chunk consumption,
and no diagnostic. It retains the same sink/cancellation snapshot, 100 warmups,
10,000 measured iterations, exact 0 B assertion, and default runtime/test settings.
Setup, root, warm callback assertions, completion, and final callback assertions
remain outside the allocation interval. Each chunk delivers three PI tokens,
ten callbacks, and four text units; measurement must deliver 100,000 callbacks
and 40,000 units, then consume the four-unit closing tag and complete.

`Diagnostics` is a separately compiled, synchronous replication using a simple
counting sink. It distinguishes options/core/sink setup, first root append,
first PI append, the other 99 warmups, measured 10,000 appends, and completion.
Splitting the warm scope allows first-use observation without increasing warmup.
It uses CancellationToken.None, rather than the test framework token, and its
counting sink lacks the test sink's unused pause/hook fields. Its original mode
replicates the old two-call-site/ignored-warm-results logic; equivalent mode
uses the checked helper. These bounded process-local observations cannot
identify causes of earlier process-local allocation failures. The actual test
is independently executed in focused and cumulative MTP and direct runtime.
No timing or retained-memory claim is made.

Historical reports copied byte-for-byte from preserved framing commit
`25d879f7db7a02adae2cdc8a2a267ab55c4f13aa` are under `history/`:

- checkpoint source `46104cecdce70170aea050612d13a77f1467251a`: cumulative
  MTP 2040 tests / one PI failure (1944 B); subsequent direct runtime 1490
  tests / all passed. `runtime-after-mtp.xml.gz` preserves that separate pass.
- bounded-checkpoint source `cea326c7ed59ac53a832af2f8cc4e528a118d89a`:
  MTP 2040 / all passed; subsequent direct 1490 / one PI failure (712 B).

These historical counts include unmerged framer tests. None are imported into
this branch's implementation or represented as current checks. No historical
failure is attributed to JIT, framework, parallelism, cold initialization, or a
runtime allocation without direct evidence.

Run `verify.py` with a new `--report-prefix`, `--expected-branch`, `--audit-base`,
and explicit `--runtime-count`, `--conformance-count`, `--differential-count`,
`--api-count`, `--fixture-count`, `--planning-count`, and `--task-count` values
for the integrated tree. Prefixes cannot be reused. The harness preserves
failures and independently continues checks after a check-level failure.
Prerequisite failures stop dependent execution. Runtime counters and XML
counts are actual observations, not inferred from task status. Reference runner
output is freshly built and copied in full before consumers; 242 records and
unchanged runner SHA are checked. Source, working, staged, untracked and
whitespace audits cover all owned paths. No thresholds, production code, shared
projects, or test parallelism settings change.

## Renewed claim and approved isolated synchronous scope

Renewed claim `b72c2a1bb2cf4f53c322b198f91923bfeed94b8f`, base
`f1415a8355fc9084e919cc877e3c4e6d50690bf9`, adds the independently verified
streaming allocation prerequisite (integration
`8a254d3c62aad297b88e2906f8b55e7bce04e30e`). Its package-free pinned .NET10.0.11
counter controls establish positive deltas in empty intervals during actual
background collections. That independent evidence is in the prerequisite's
`control-8e76f6e-attempt1/` report. It does not establish any historical PI or
streaming failure cause; the diagnostic anomaly remains failed evidence.

The coordinator approved equivalent narrow PI process isolation. The existing
Fact is the parent and the actual compiled child Fact. `PiAllocationChild` runs
real then positive once, with a capability/live-parent/exact-method invocation
guard. It verifies streamed SHA fingerprints of executable, test/product DLL,
compiled source versions, managed/native runtime, runtimeconfig and the actual
`PiAllocationProbe.AppendChecked` IL. Typed required-field/unknown-member JSON,
PID/mode/token, one Fact XML Pass, full callback/text counts, completion and
consumption are checked. Missing/malformed reports, nonzero allocation/exit,
crash or timeout fail the parent; no retry or subtraction. Timeout30s kills the
child tree; termination/drain bounds5s. Default JIT/GC/class parallelism remain
unchanged. The real child retains the exact `Assert.Equal(0, allocated)`.

Both counter snapshots now live inside the same NoInlining helper and enclose
the entire checked Append loop. Same core/chunk/sink/TestContext cancellation
snapshot,100warm and10000measured, NeedInput/full consumption/null Diagnostic
remain required. Measured counts are100000callbacks/40000textunits;
completion consumes4 and total counts are101005callbacks/40400units. Warm
callback delta is1000 (root's3 are reported separately). The positive child uses
the unchanged counting sink Hook to escape byte[37] per callback and must record
positive bytes and final array length37. It never warms the real child and runs
after real in another process. Expected positive allocation is6400000B on the
pinned runtime, with actual bytes reported. No production or peer helper edits.
The scope promises isolated synchronous primitive allocation only, with no
concurrent allocation guarantee.

Independent diagnostics now link the actual owned checked helper source, while
keeping the earlier old-shape two-call-site/ignored-warm-results comparison.
`primitiveBytes` is the exact equivalent whole-loop scope; `steadyBytes`
additionally reports surrounding caller dispatch. Setup/root/first-use/99warm/
completion remain separately recorded. Diagnostic simple sink/CancellationToken.None
limitations still apply; actual child Fact checks use the real counting sink and
test token. First PI plus99warm still equals100 warmups. Output happens afterward.

The renewed verifier preserves every child invocation/request/stdout/stderr/
XML/report/exit as compressed raw data **before** parsing any report, including
on prerequisite failure. Its meaningful malformed-first/later-raw archive
regression tests this failure preservation. Seven bounded actual compiled Fact
negative controls cover unknown/incomplete guard, missing-required/malformed/
unknown-member request JSON, provenance and host mismatch. Crash/timeout and
post-report tamper rejection are reviewed policy, not executed negative controls;
no passing run is claimed for them. The full fresh focused/cumulative/direct
joint suites, independent diagnostics,3real-positive pairs, API/discovery,
fresh242reference records, planning and all owned/diff checks must pass before
`implemented`; coordinator independently verifies integration.

Additional historical PI1024B failure is byte-identically preserved under
`history/encoding-integration-cd75317-attempt1/` with SHA256/source/path records:
source`cd753170601936520d1d8efadb949852bacd083f`, durable failure commit
`fc862e888a0089e6bd304b8c2586206353c94f66`. Direct255runtime/one PI1024B failure
followed a separate cumulative809/API222 pass. Subsequent directconformance/
differential/provenance/model/package/finalplanning checks were unrun in that
attempt. This is historical unmerged encoding context, not current code or cause
proof. Earlier PI1944/712/128B, streaming224B and callback603-correction records
remain untouched. Source checkpoint changes have not yet been checked.

## Single known-source check outcome

`isolated-f3e47be-attempt1/` records the announced one-shot check at
`f3e47be7911fd3805b3ae424000e83ccdce370c4`. Verifier exit0/all checks passed:
focused1, joint MTP801, separate direct247/255/299, compiled API222,
discovery3388fixtures,242freshreference records with source and unchanged runner
SHA, planning14/validation114, meaningful archive1regression and ownership/
unchanged-other-test-body audits. Release builds have0warnings/0errors.

Exactly3actualFact pairs (focusedMTP/cumulativeMTP/direct) measured real0B and
positive6400000B. Realwarm100=368B, positivewarm100=64368B; measured100000calls/
40000units, totals101005/40400, Completed4/nullDiagnostic. Apphost and direct
dotnet invocations both pass with pinned expected/actual provenance. Complete
raw artifacts are compressed under `child-runs/`. The independent linked helper
measured0B; old-shape fresh diagnostic reported128B, with separate setup6792B/
root312B/firstPI368B/remaining99warm0B/completion0B. No historical cause inferred.

`isolated-f3e47be-attempt1-protocol-negative/` retains seven actual compiled Fact
rejection controls, each with one intended failed Fact and exit1: unknown/
incomplete guard, missingrequired/malformed/unknownmember request JSON,
provenance and configured-host mismatch. They succeeded as rejection controls;
no allocation test was retried until success. Timeout/crash/post-reporttamper
negative execution remains unrun; policy is reviewed source only.

The task is implemented on this branch pending independent source/integration
review and a fresh full joint integrated allPass run. `integration_revision`
remains null; consumers must await coordinator verification. No further full
check or benchmark is requested by these passing observations. Earlier immutable
failed reports and the precise isolated synchronous scope remain preserved.
