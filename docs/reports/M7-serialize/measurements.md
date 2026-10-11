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
identity45 with130s cleanup reserve fits the total. No final grant or result is
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
