# Streaming allocation boundary

Current state: **in_progress, unverified**. Independent integration failed the
owned primitive at3,448B. Prior branch passes below are not a complete fix.

Only the streaming allocation Fact/helper/imports and owned diagnostic/report
files change. Production source, default JIT/GC/test parallelism, exact 0 B,
100 warm iterations and 10,000 measured iterations remain unchanged. Each
iteration checks NeedInput, full UTF-16 consumption and null Diagnostic, using
the same core, sink, chunk and cancellation token. Measurement must consume
50,000 text units and 120,000 callbacks, then complete the document.

## Preserved observation and bounded comparison

`history/` contains byte-for-byte raw copies from the PI parent's
`source-9286cd6-attempt1` report (source
`9286cd6cac00df0ccd5f4627d4c29e5046c16af3`, evidence
`3e9bd43179ee3599b7c4881455857dfde95e4ae8`). That cumulative MTP run failed
Streaming with exactly 224 B; its separate direct runs passed 247/255/299 tests
(801 total). These are historical observations, not verification of this branch.
No unmerged PI implementation is imported.

`experiment-f71cee2-attempt1` retains one focused MTP, one cumulative MTP and one
focused direct XML run of instrumented test source
`f71cee2` (full hash obtainable from Git). Counters nested inside the checked
NoInlining helper and outside its return were compared before the original
Assert expression, with output after the assertion. All 801 cumulative tests
passed; focused direct XML records primitive=0, surrounding=0, 50,500 total text
units and 121,202 callbacks before finalization. Instrumentation changes the
caller scope; this does **not** establish the historical 224 B cause.
The diagnostic-build/original/equivalent logs in this first directory are a
preliminary, uncommitted diagnostic prototype that selected outer versus inner
snapshots around the same loop. They are retained as exploratory output only,
not known-source criterion evidence.

`experiment-95006d1-attempt1` compares two fresh-process modes at committed source
`95006d1`: original NoInlining checked helper with caller counters, versus
NoInlining checked helper that returns internally captured counters. Both retain
100 warm/10,000 measured iterations and the identical checked Append workload.
Both measured 0 B. Independently reported scopes: counter-only 0 B; cold options/
core/sink 6,816 B; root first use 312 B; first chunk 432 B; second core/root setup
7,128 B; warm100 432 B; completion 0 B. Measured work is 50,000 UTF-16 text units
and 120,000 callbacks; final totals 50,500 units and 121,205 callbacks with exactly
one EndDocument. No timing or retained-memory claim is made.

## Chosen boundary and limitations

The final test asserts the exact result returned by the shared synchronous
checked helper. Both counters now execute inside that same NoInlining helper;
caller dispatch, test context access, assertions, validation output, cold setup
and completion are outside the primitive scope. Both warm and measured phases
use the same helper and Append call site. It additionally verifies callback/
unit totals, consumption, Diagnostic and completion. The independent executable
links this actual helper source and retains a separate original-counter control.
There is no filter, extra warm-up, threshold change, production repair, retry
until success, or runtime configuration adjustment.

The controlled experiments do not demonstrate framework/Assert/JIT causality
for the historical 224 B. They establish a meaningful allocation-free checked
primitive and make its scope explicit. Test context remains outside the
synchronous counter scope; no broader framework or async allocation claim is
made. If genuine steady primitive allocation appears, request the runtime owner;
do not relax this test. Original-versus-equivalent equality is useful evidence
but is not a claim that the old failure must reproduce in every environment.

## Independent source/integration checks

Run `verify.py` at a committed source revision with unique `--report-prefix`,
explicit `--expected-branch`, `--audit-base`, and all expected count arguments.
It builds Release with build servers disabled; preserves fresh BCL output before
solution build; runs both diagnostic processes, focused and cumulative MTP,
separate full direct runtime/conformance/differential suites, compiled API,
discovery, reference provenance, planning regression/validation and ownership
checks. It retains every outcome, including an unrelated PI failure if present,
then returns failure when any combined check fails. Counted direct XML reports
include the actual streaming allocation/unit/callback output. Final summary
records full source revision, SDK/runtime information and source hashes.

A passing child run does not discharge the original PI parent's mandatory
integrated combination criterion. That parent still requires full allPass
verification after composition before encoding/resolver/framing consumers can
be verified. Child status is `implemented` until independent integration checks.

## Reopened after independent integration failure

At integration `f573a6c7ee8cb9ca122d2f3e260617ddd162800d`, focused MTP1,
cumulative MTP801, independent original/equivalent0B and API/reference/planning
checks passed, but the standalone runtime247 suite failed this owned Streaming
Fact with exactly3,448B returned from inside the helper. The other direct255/299
suites passed. The byte-identical reports and SHA256 preservation record are
under `history/integration-f573a6c-attempt1/`. Prior branch passes remain recorded,
but this defeats a complete correction; the task is reopened and unverified.

The next controlled diagnostic asks whether allocations occur within one Append
(including its borrowed callbacks), after its progress checks, within the
nonallocating progress observation, or between iterations. No cause is presumed.
The actual helper accepts optional preallocated100/10000 value-type sample
arrays and a precreated progress observer. Each sample stores zero-based
iteration, Append/validation/observer/gap bytes, consumption/step, cumulative
sink units/calls and thread ID. Both phases use the same checked Append call
site. Returned total bytes are **never** adjusted/subtracted; exact0B still
fails for any allocation. Samples are serialized only after measurement.

The baseline has empty sample spans; the sampled test is enabled only by an
owned output path in `XEMELE_STREAMING_ALLOCATION_SAMPLE_OUTPUT`. This is a
logging choice, not a JIT/GC/parallelism override. Its arrays/delegate are
allocated before warm-up and their setup is separated in the independent
sampled diagnostic. Instrumentation may perturb runtime behavior, so a0B sampled
run cannot erase3,448B or establish a fix. A nonzero Append scope alone cannot
attribute an allocation to production rather than callbacks/runtime transitions;
stack evidence would be needed for that attribution. dotnet-trace is available
locally if a subsequent allocation observation warrants a bounded EventPipe
experiment. No full-solution retry is planned: one baseline direct247 process,
one sampled direct247 process and one independent sampled process will compare
contexts, with every result retained. Source/hypothesis is sent before execution.

The single announced comparison ran at source
`4854b87ba019fef3999bbfa275299b8f10538ec6`; immutable output is under
`sample-4854b87-attempt1/`. Both baseline and sampled standalone processes passed
247/247. Their owned test output records primitive0B/surrounding0B,50,000measured
units/120,000callbacks,50,500totalunits/121,205totalcallbacks and completion.
Both sampled contexts recorded exactly432B inside warm Append iteration0,0B
inside the remaining99warm calls, and0B in all10,000measured Append/validation/
observer/gap samples. All samples consumed40UTF-16units and returnedNeedInput.
Measured rows span units505..50,500 and callbacks1,214..121,202. Thread ID stayed
15 in the test and1 independently. The independent sampling arrays/delegate
setup was727,312B, reported separately before warm-up. Full100+10,000rows per
context are preserved compressed, not just a filtered summary.

The3,448B failure remains unexplained: these fresh processes and instrumented
source did not reproduce it, and per-Append counters alone cannot distinguish
production, callback, native/runtime/JIT causes. No further test retries or
EventPipe run were made after this one comparison. There is no observed measured
allocation here to attribute with stacks; there is also no evidence warranting
a production repair request or a new correction. A coordinator-approved next
experiment should target the preserved failing integration context with bounded
allocation stack attribution, acknowledging small allocation sampling limits.
The task remainsin_progress withA2failed; parent jointallPass remainsrequired.

## Package-free allocation-counter controls (source before execution)

The coordinator supplied [runtime issue134724](https://github.com/dotnet/runtime/issues/134724)
and [draftfix134855](https://github.com/dotnet/runtime/pull/134855). Read on2026-10-10:
the issue demonstrates positive empty-interval deltas during a bounded GC worker;
the draft PR describes allocation-context accounting and has an approval, but
remains unmerged. These sources guide the control, not an attribution of224B/3448B.

`CounterControl/` has no package or project references and empty local
Directory.Build imports. One .NET10.0.11 Release process runs32 baseline pairs
and192 pressure pairs. ZeroWork is NoInlining, only two counter reads and
SpinWait100000. PositiveWork allocates escapedbyte[37] (64B expected) and uses
the same spin. Storage/output/reflection remain outside counters. A separate
worker retains under28MiBlive, caps512 allocation/collection requests, and has
startup/shutdown5s and overall20s deadlines. Nonblocking generation2 requests
are explicit diagnostic pressure, not changed GC settings. Background memory
info indices outside intervals establish whether an actual background collection
was observed. Raw arrays, counts, IL and exact exit code are retained; an anomaly
returns1, baseline/controlerror2, workererror3. No subtraction or selected retries.

If this pinned runtime reproduces a counter anomaly with meaningful positive
controls and actual background collections, the next proposal may isolate the
whole unchanged synchronous primitive in a filtered standalone child process.
That is conditional and not yet implemented. Any such proposal must preserve
100warm/10000measured/exact0B/defaults/provenance, validate a same-workload escaped
sink-allocation positive child, and fail parent checks for child allocation,
crash, timeout or absent/malformed output. Historical in-process failures remain
unresolved, with no concurrent allocation guarantee.

The single package-free run atsource
`8e76f6ef620c6e4b16848e88bb314a95ae799083` reproduced a counter anomaly on
.NET10.0.11/Arm64/workstationGC. Baseline zero32/32=0B and positive32/32=64B.
Pressure zero173x0B,18x8,136B,1x7,848B; pressure positive171x64B,21x8,200B.
It made253requests, recorded256generation2collections and background-info
index2->256. No worker error occurred; the exact diagnostic exit1 is retained
under `control-8e76f6e-attempt1/`, along with fullarrays/IL/package-emptyassets/
runtimeconfig/assembly+sourceSHA. This is independent counter evidence, not
causal proof for historical parser deltas and not a passing exact-zero test.

A concrete conditional proposal was sent to the coordinator: a new owned
StreamingAllocationChild process/protocol helper launches the actual compiled
standalone test DLL with its existing-method filter and a recursion guard.
The real child preserves the whole original synchronous workload and exactzero;
a second positive child intentionally escapes byte[37] through the existing
sink Hook during that same workload and must measurepositive. Structured report,
oneFactXMLpass, runtime/compiled-source/module hashes/counts/completion would be
pinned and validated. Any realchildnonzero/crash/timeout/missing/malformedoutput
fails theparent, without retries/subtraction. Defaulttestparallelism and runtime
settings stayunchanged. Nothing is wired yet; assertionchanges await focused
coordinator go-ahead. Historical failures remainunresolved and claim staysnarrow.

## Approved conditional process isolation — source checkpoint

Coordinator approved the concrete proposal after inspecting the pinned controls.
A separate read-only reviewer found no control-source blockers; that is recorded
as review evidence, not a test/rebuild. The actual control exit1 staysfailed.

`StreamingAllocationChild.cs` is a narrow protocol for this existing Fact. The
parent runs real then positive once, using its verified current dotnet/apphost
or host beside the active runtime. Configured host inconsistencies fail. Every
child validates a32-digit capability/expectedmode/liveparent/ownedrequestpath,
exact standalone -method/-result-xml invocation and compiled provenance before
executing. An arbitrary mode environment variable cannot silently bypass the
canonical parent. Parent verifies oneFactXMLPass, PID/mode/capability/version,
iterations/units/callbacks/consumption/nullDiagnostic/completion, executable SHA,
test+product DLL SHA/sourceversions, active managed/native runtime SHA, actual
checkedhelper IL SHA and runtimeconfig SHA. Provenance hashing streams files
outside measurement, avoiding large temporary byte-array copies.

The real child retains exactAssert.Equal(0,allocated). The positive child installs
an escapednewbyte[37] sinkHook before root/warm-up and measures the same whole
checkedAppend workload, requiringpositivebytes and the finalescapedarray37.
Both still check100warm/10000measured and all original meaningful totals.
Positive doesnotwarm real: it runsafter real in a different process. Parent test
parallelism/defaultGC/JIT remain unchanged; no testcount/project/API changes.
Childtimeout30s kills theprocess tree; drain/termination bounds5s. Invocation,
stdout/stderr, exitmetadata and availableXML/report remain in uniqueownedignored
artifacts on every success/failure; missing/malformed output, nonzerochildexit,
provenance mismatch, timeout or realnonzero failsparent. No retry/subtraction.
Source is committed before focused/full checks; these changes are not yet
claimed tested. Historical in-process failures remainunresolved; only the
isolated synchronous borrowed primitive is promised, no concurrent guarantee.
