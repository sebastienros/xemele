# Serialization measurements

## First run: preserved partial evidence

Measured revision `a3722a5cf2a0106f0ea26bfdc8105b5bced3bb9c`, runtime source
`52be6bd6f9558ed9385ba2cef842820c489347e0`, probe `e021177b87a92ba8805d27db72b32a43c2274883`.
The old preparation report describes an earlier ungranted checkpoint, not this run.
Manifest SHA256 `76750d45d31ed53eb66da3c669c77fd1e0736b18dd2ca860b3ea5dc48cc456e5`:
3942 source/generated/input,150 binaries,3852 SDK/runtime files, actual inputs,
host and opaque complete effective child environment, explicit SHLVL1/session.
Fresh exact-wrapper controls passed0/unknown-environment1/0 before grant.

Coordinator granted `m7-serialize-a3722a5` after main/worker PARK and independent
closure/inventory. Sole wrapper execution, session51322, exited124 at the750s BDN
phase limit. Initial/final identity checks0, allocation0, no_child=true, unchanged
manifest. Coordinator recorded durable RELEASE before either party read results.
Two outbound lifecycle messages were rejected by automatic approval review;
the direct human answer in this worker chat authorized M7 coordination messages,
and the normal tool then accepted the lifecycle message. No denied action was
executed indirectly and no workload was retried.

Pinned SDK10.0.400, actual .NET10.0.11(10.0.1126.37416), Arm64 RyuJIT armv8.0-a,
Apple M4 Pro14logical/physical cores, macOS Sequoia15.8.1(24H32)/Darwin24.6.0;
concurrent workstation GC. Tiering/PGO/ReadyToRun disabled. BDN0.15.8,
InProcessNoEmit, ShortRun3warmup/3actual/1launch; high-priority setup was denied.
Raw logs do not print hardware timer frequency: no timer precision is claimed.
Actual per-iteration nanoseconds and operation counts, N3 variability and99.9%CI
are preserved in ignored raw logs; the committed summary table records all87
completed identities/statistics and GC numerator/denominator, not raw logs.

Allocation:231 rows,214 warm borrowed rows at0B over213760 operations
(192 scan/escape/matcher batches*1024,16 invalidUTF16/NUL/surrogate-boundary
rows*1024,6 tree compositions*128). Cold first DOM/result/runtime6696B/1;
positive byte-array control1048B/1; cold first UTF8 stream/transport/runtime2360B/1;
cold benchmark SearchValues/name-table288B/1. None are hidden in warm0B claims.

| Owned domain | Html5 bytes /128 operations | Legacy bytes /128 operations |
| --- | ---: | ---: |
| Small result strings |180224|267264|
| Large result strings |81675264|85578752|
| Multilingual result strings |6798336|6924288|
| Small UTF8 transport |296960|296960|
| Large UTF8 transport |694272|694272|
| Multilingual UTF8 transport |694272|694272|

Retained inclusive results/array/runtime roots:360600B before,599456B after,
238856B delta for128 owned results. This whole-heap delta is not a primitive
allocation count or an isolated string-size claim.

Timing:88 started,87 completed. All60 escape/scanner and18 output CSV rows
completed, plus9 boolean raw results. Escape elapsed552.2s; output109.59s.
Complete Boolean Dispatch/Linear pairs:empty,checked,CHECKED,noresize; Dispatch
selecte. Timeout during Linear(selecte), WorkloadWarmup2:67108864 operations,
677856750ns. Missing7:Linear(selecte) and both methods for selectedx,unknown,
and Unicode long-s ſelected. No Boolean CSV/final94-report assertion completed.

Borrowed tree BDN reports large output1B/op: raw fixed4656B/4096operations with
no GC, versus direct counter0B/128. The same4656B numerator occurs at65536,
2097152 and4194304 operations for smaller compositions. Boolean raw overhead
is4656B or3648B over67108864..268435456 operations with no GC. Report these
fixed harness costs separately; rounded0B/1B columns do not establish intrinsic
allocation behavior. See `first-measurement-summary.json` for exact denominators.

Historical candidate evidence: cached text search26ns versus scalar906..947ns
on1024-character misses/late hits, and13ns versus425..446ns on multilingual
late hits. Dense first-stop scalar0.89..0.95ns versus cached1.98..2.29ns;
short-hit scalar2.00..2.09ns versus cached2.23..2.26ns. Short-miss differences
overlap noisy three-iteration confidence intervals. These are first-source
controls, not final runtime timing or cross-architecture claims.

## Reviewed owning correction and final preparation

Coordinator independently inspected first evidence and approved one choice:
text scanning keeps a16-character scalar prefix, then two static state-specific
SearchValues sets. Attribute scans retain scalar behavior. No threshold sweep,
custom SIMD, API/shared-file edit or autonomous tuning. Explicit type initializer
keeps product cache setup within first-use cold measurement; caches live for
the process and their ownership is distinct from operation allocations.

Final selection is49 cases:12 production text scans,12 changed borrowed escapes,
18 changed output compositions,7 missing Boolean cases. Original scalar remains
an explicit reference; completed baseline/candidate and9 Boolean cases are
preserved rather than rerun unchanged. Full allocation sequence must run again
for changed product source. Plan maximum1550s:identity45/allocation150/BDN1200/
identity45. The130s reserve includes final identity45 and owned cleanup;
initial45+allocation150+BDN1200+reserve130=1525s fits1550s. No final grant or result is
claimed by this preparation section.

## Preparatory process cleanup correction

Before the first grant, coordinator inventory found a stale preparatory shell
PID26204/PGID26204, its find26205 and head26206 children, parent92249. Worker
had previously printed only exec output and missed its running-session handle.
The earlier broad no-child statement was therefore incomplete. Coordinator
aborted/released the pregrant PARK; there was no measurement lease. Worker
verified those exact PID/PPID/commands and sent SIGTERM only to PGID26204;
final scoped inventory was empty. Parent/shared services were untouched, no
SIGKILL was used in this preparatory cleanup. Fresh PARK/inventory preceded
the real grant. No source/binary/environment identity changed in that cleanup.

Corrected source/probe/test revision `a7022f44247f51d7201bf930e45856fcf2acadb3`:
all1622 focused combined tests pass (916serializer,0errors/failures/skips/notrun),
including10 prefix/chunk-boundary state theories and all prior selected tests.
Official1253API,3388fixtureSHA/negativecontrols and publicsample pass; all three
offline Release builds0warnings/errors. Correctness-only probe and actualinputs
pass; discovery exactly49 reviewed identities:24Escape/18Output/7Boolean. No
final measurement, counters or identity controls have run at this checkpoint.

## Final measured source

Frozen measured revision `a80a097dc529e3f63a0d71ea8c5085fadfd4a86c` contains
exactly the passing corrected runtime/test/probe source at `a7022f4`.
Manifest SHA256 `cb260f4313fa991ff3ba777131bfd9da1f30126602c95bc7f64b9ad984ea87e0`:
3944 source/generated/input,150 binaries,3852 SDK/runtime files plus full host,
actual inputs and opaque complete effective child environment/session. Default
zsh login/escalated exact-wrapper controls0/unknown-environment1/0 all reported
no_child=true/runs=[]; negative initial/final1, positives initial/final0.
Initial default-sandbox process inventory was denied; a premature empty-inventory
message was corrected immediately. Actual scoped escalated inventory then passed
empty before fresh root/main/worker PARK and independent closure review.

Grant `m7-serialize-a80a097`: sole wrapper session71694, no retries, total1550s
maximum. Initial0/allocation0/49BDN0/final0/wrapper0/no_child=true, unchanged
manifest. Root recorded durable RELEASE before final raw inspection. Benchmark
elapsed408.33s:24 escape/scanner230.75s,18 output112.39s,7 Boolean64.28s.
All49 selected report-success/statistics assertions passed. Final correctness,
API and sample checks remain the actual a7022f4 results; source did not change.

All231 allocation rows completed;214 warm borrowed rows again0B over213760
operations with the same batch/domain denominators as the first run. Cold first
DOM/result/runtime **including product cache initialization**6856B/1, versus
earlier6696B/1. The160B aggregate difference is not an isolated cache-size
measurement. Two process-lived immutable product sets are documented ownership,
not operation allocations. Cold first UTF8 transport2360B/1, independent
benchmark candidates/table288B/1 and positive1048B/1. All12 owned string/transport
rows exactly match the table above. Retained inclusive before336840B,
after575696B, delta238856B/128results, including array/runtime roots.

| Production text scan | Html5 mean ns | Legacy mean ns |
| --- | ---: | ---: |
| Dense immediate stop |1.297|1.273|
|1024-character late hit |46.740|49.048|
|1024-character miss |48.701|49.067|
| Multilingual late hit |32.367|34.091|
| Short hit |2.382|1.997|
| Short miss |3.575|3.427|

These support the one reviewed long-text correction while tiny/immediate inputs
remain scalar. Historical comparisons are across separate leases, not paired
same-run ratios. No threshold sweep or universal speedup is claimed. End-to-end
workloads mostly contain short text/attributes: large borrowed output means
197219ns Legacy/140159ns Html5 with99.9%CI half-widths101181ns/77840ns; no
composition improvement or regression conclusion is justified by those noisy
three-iteration timings. See exact mean/StdDev/CI tables for all49 identities.

Final primitive BDN rows round0B but raw numerator includes0..2976B over
16777216..268435456 operations, no GC. Borrowed tree raw costs:large4656B/4096,
multilingual4656B/65536,smallLegacy3984B/2097152,smallHtml54656B/4194304, no GC;
the direct borrowed counter is0B for each128-operation composition. Final Boolean
rows include4368B or4656B over67108864..268435456operations, no GC. Owned large
BDN inclusive figures differ (669050B/638554B strings,5429B transport),
while direct counters report668584B/638088B strings and5424B transport per op.
Large owned-result raw GC counts are204/204/204 at1370214512B/2048ops Legacy
and1307758704B/2048ops Html5; UTF8 raw5558832B/1024ops has no GC. The inclusive
BDN/per-thread-counter difference is not attributed solely to product allocations.
The summary preserves exact GC numerator/denominator for every case.

The7 missing Boolean cases now complete; combined with9 unchanged first-phase
cases they cover all16 identities without rerunning the completed methods. Length
dispatch remains faster in measured means for these fixed-name controls; narrow
or noisy intervals are reported rather than promoted to architecture guarantees.
Actual pinned host/runtime/GC/settings match the first run, high-priority setup
was still denied, hardware timer frequency still absent. x64 validation remains
the later release/performance owner's responsibility; no x64 claim is made.

`final-measurement-summary.json` holds concise domain/statistics tables and raw
hashes. Every preserved first raw hash and first manifest still matches. Root independently acknowledged all231 rows,49 CSV identities and49 GC
numerator/denominator rows. Raw allocation/BDN/CSV logs remain ignored and
untouched; cleanup waits integration and durable final-report review.
No runtime/test/probe source changed after this successful frozen measurement.

Terminal evidence validation: `python3 tools/planning/tasks.py validate` validates
169 tasks; `git diff --check` passes; all37 branch paths are task-owned relative
to claim5af29be. Runtime/tests/fixtures/probe/sample/API are byte-identical to
measured a80a097. Evidence-only handoff closure changes require no rerun. No
owned exec remains active; successful wrapper final no_child=true is retained.
