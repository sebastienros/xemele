# Navigation measurement summaries — target-runtime evidence and preserved history

The corrected separately granted sequence at bee37f2 passed on actual10.0.11;
its summary follows the preserved first-attempt history below.

Initial informational implementation/probe revision: d79ffea3ca933f38227a8cfc74b31d49454832b4.
One authorized exclusive sequence only. Allocation and timing each exited 0;
ten timing cases completed in 71.36 seconds. No retries, builds, generation,
restores, option changes, tests or source edits occurred during the sequence.

**These results are actual .NET 10.0.0 results, not the required .NET 10.0.11
performance evidence.** SDK 10.0.400; macOS 15.8.1 Arm64; concurrent workstation
GC; BDN 0.15.8. CPU model was reported unknown. The project property requested
10.0.11 but the preparation build with `--no-restore` left the generated
runtimeconfig requesting 10.0.0. `DOTNET_ROLL_FORWARD=Disable` therefore selected
10.0.0, as both FrameworkDescription and BDN reported. The worker failed to check
the effective runtimeconfig before freezing. The frozen 10.0.11 directory map
was not the loaded runtime closure. Target-runtime performance remains pending;
no second attempt was authorized. These numbers cannot be relabeled as 10.0.11.

The frozen 117 source/config/generated files and 40 binaries matched before and
after (manifest SHA256 4b81cae69cfa04f18f2594673c65bd9d9e58c625e2e034b04672fd613164a2e3).
The nominal 195-file runtime map also matched but refers to 10.0.11, not the actual
loaded 10.0.0 libraries. Xemele.dll SHA256 was
8724aa686b1daae41ba6fc9dd232006a7b1b4800f8616b433a9792e66de994d0;
probe DLL SHA256 was 01ff96566b952b16e6a012cdacccf2497a71692831bd15a6ea17d49289f517d6.

## Exact allocation totals

64-child bilingual/repeated-name fixture: 389 logical input items and 6,123
aggregate UTF-16 field characters. Fixtures, arrays and control distributions
are prepared outside primitive measurements. Cold capture excludes tree setup.
Warm and measured phases each contain 128 batches of 512 primitive groups; each
consumes 1,703,936. The exact whole-loop counters include batch sampling/stores.
Each group performs rebinding (element, namespace, attribute), metadata reads,
language lookup, indexed order, equality and hashing.

| Boundary | Samples | Exact total B | Derived B/sample |
| --- | ---: | ---: | ---: |
| Warm primitive groups | 65,536 | 0 | 0 |
| Measured primitive groups | 65,536 | 0 | 0 |
| Positive 1,024-byte array control | 1 | 1,048 | 1,048 |
| Cold first snapshot | 1 | 165,224 | 165,224 |
| Owned snapshot after warmup | 128 | 16,449,040 | 128,508.125 |
| Materialized descendant axis | 128 | 3,670,304 | 28,674.25 |
| Normalization only (five inputs/four unique) | 128 | 48,128 | 376 |
| Normalization plus immutable XPathValue storage | 128 | 89,088 | 696 |

All 128 warm and 128 measured batch distributions have min=max=sum=0. The cold
capture includes first-use/JIT-related setup and is distinct from warmed owned
storage. Owned counters intentionally include List/Dictionary geometric capacity,
metadata, aggregated strings, result wrappers and normalization scratch. Results
are not claimed allocation-free. Counts/string/input limits and operation quotas
preflight growth/copying as established by focused tests.

A separate `GC.GetTotalMemory(true)` observation while retaining 128 snapshots
reported a process-heap delta of 27,469,400 B (214,604.6875 B per retained snapshot).
That estimate exceeds the corresponding thread allocation total; it is **not**
a reliable attributable retained-size measurement and is not a pass criterion.
Runtime/background/heap-estimation effects were not isolated. Preserve the actual
number as a diagnostic; do not infer a retained-size bound from it or silently
replace it with the owned-allocation number. A later properly isolated retained
measurement remains necessary where an exact retained-memory claim is required.

## Timing summaries

Every benchmark class declares `[MemoryDiagnoser, ShortRunJob]`. Discovery and
actual run use one ShortRun job, InProcessNoEmit toolchain-only mutator, three
warmups, three iterations, one launch; ten cases. No job CLI overrides. Linear
and indexed order baselines operate on the same captured identities, with setup
semantic controls; identity comparison/search work is charged. Storage cases
measure owned boundaries without implying a zero-allocation alternative.

| Operation | Children | Mean ns | StdDev ns | BDN reported B/op |
| --- | ---: | ---: | ---: | ---: |
| Linear captured identity order | 4 | 31.25 | 0.322 | 0 |
| Indexed captured identity order | 4 | 25.11 | 0.211 | 0 |
| Linear captured identity order | 64 | 441.09 | 7.270 | 0 |
| Indexed captured identity order | 64 | 24.93 | 0.102 | 0 |
| Owned snapshot | 4 | 2,803.8 | 24.79 | 10,264 |
| Materialized axis | 4 | 237.0 | 2.37 | 1,696 |
| Normalized node-set with value storage | 4 | 176.2 | 0.76 | 696 |
| Owned snapshot | 64 | 40,922.0 | 526.97 | 128,508 |
| Materialized axis | 64 | 3,071.1 | 41.19 | 28,673 |
| Normalized node-set with value storage | 64 | 175.2 | 0.30 | 696 |

BDN summary allocations are rounded/averaged and are not substituted for exact
thread totals. Its raw GC diagnostics included small nonzero harness totals for
three order cases (2,304/2,640/2,640 B over tens of millions of operations), while
summary cells rounded to zero; the independently bounded primitive loops were
exactly zero. Axis BDN 28,673 differs from exact probe 28,674.25; snapshot BDN
128,508 differs from exact 128,508.125. These differences remain visible.

Small single-Arm64 synthetic timing results are informational; no cross-platform
release or end-to-end expression-evaluation claim. The dictionary index avoids
linear full-snapshot order lookup; no speculative scan/token optimization was
introduced. Full scans, repeated aggregate text and ordinal insertion sorts
remain operation-budgeted; more diverse/deep/wide composition and x64 evidence
belong to integrated follow-ups.

## Lifecycle diagnostics

Exact command: `python3 docs/reports/M4-navigation/run_measurements.py --exclusive-grant`.
Grant maximum 960 seconds; actual allocation=0, benchmark=0, initial freeze=0,
final freeze=0. Wrapper exit was **1**, phase complete, cleanup_error
**PermissionError**, initially no_child=false (unconfirmed): the sandbox denied
process-group access and subsequently denied read-only `ps`. This cleanup failure
is separate from the successful measurement processes and unchanged source/binary
identity. Lease RELEASED was reported before raw inspection. An authorized
read-only escalated pid/ppid/pgid/command inventory then confirmed **zero scoped
navigation driver/measurement processes**, without signaling any processes or
touching unrelated services. Root was informed before raw inspection. No second
run or cleanup-only executable change occurred.

Raw logs/counters/BDN result files remain ignored under artifacts/M4-navigation.
Only these derived summaries and honest limitations are committed.

## Authorized corrected target-runtime sequence

Measured revision: bee37f243c67cda55d6d6b17df66ee1c8e372d68. After the prior
sequence released, one bounded preparation correction added explicit
`--fx-version 10.0.11` before the unchanged probe DLL in both plan arrays.
The same counters-disabled control asserted its actual FrameworkDescription
`.NET 10.0.11` before freeze. Runtimeconfig minimum 10.0.0 remains disclosed;
explicit host selection overrides it. No product/probe DLL, runtime source or
correctness/API/package evidence changed. A separate new exclusive grant
then authorized exactly one target-runtime sequence; no retries.

Fresh allocation FrameworkDescription and all ten BDN host records confirm
**.NET 10.0.11**, build 10.0.1126.37416, Arm64 RyuJIT armv8.0-a; SDK 10.0.400,
Apple M4 Pro (14 logical/physical cores), macOS 15.8.1, concurrent workstation GC.
The actual intended source/binary/runtime closure (117/40/195 files) matched
before and after; manifest SHA256
375169cc6e34d01bc04a206457970d7aef7f8292fc3294b0d80866d6c770b9f2.
Product and probe DLL hashes remain the same as recorded above.

Both direct phases, initial identity, final identity and wrapper exited **0**;
phase complete; no_child=true. Authorized escalated execution allowed the
bounded process-group lifecycle checks. RELEASED was sent before raw inspection.
No builds, generation, restores, edits, tests, option changes or retries occurred
inside the measurement grant. Ten ShortRun cases completed in 71.49 seconds.
The first attempt's wrapper1/PermissionError and target-runtime mismatch remain
historical failed evidence, not erased or substituted by this successful attempt.

| Target-runtime allocation boundary | Samples | Exact total B | Derived B/sample |
| --- | ---: | ---: | ---: |
| Warm primitive groups | 65,536 | 0 | 0 |
| Measured primitive groups | 65,536 | 0 | 0 |
| Positive array control | 1 | 1,048 | 1,048 |
| Cold first snapshot | 1 | 165,224 | 165,224 |
| Warmed owned snapshot | 128 | 16,449,040 | 128,508.125 |
| Materialized descendant axis | 128 | 3,670,304 | 28,674.25 |
| Normalization only | 128 | 48,128 | 376 |
| Normalization plus XPathValue storage | 128 | 89,088 | 696 |

Again min=max=sum=0 for each of the 128 warm and 128 measured batch arrays;
consumed=1,703,936 for each phase; fixture input389/6123. These independently
bounded counters establish the warmed borrowed primitive zero-allocation result
on the intended runtime, distinct from cold initialization and owned outputs.

| Target-runtime operation | Children | Mean ns | StdDev ns | BDN B/op |
| --- | ---: | ---: | ---: | ---: |
| Linear captured identity order | 4 | 30.60 | 0.061 | 0 |
| Indexed captured identity order | 4 | 23.63 | 0.124 | 0 |
| Linear captured identity order | 64 | 435.73 | 2.859 | 0 |
| Indexed captured identity order | 64 | 24.92 | 0.136 | 0 |
| Owned snapshot | 4 | 2,872.1 | 39.46 | 10,264 |
| Materialized axis | 4 | 246.8 | 1.51 | 1,696 |
| Normalized node-set with value storage | 4 | 178.2 | 0.09 | 696 |
| Owned snapshot | 64 | 41,182.6 | 317.12 | 128,508 |
| Materialized axis | 64 | 3,143.0 | 63.98 | 28,673 |
| Normalized node-set with value storage | 64 | 177.3 | 1.67 | 696 |

Raw order-case GC diagnostics were 0/2,640/2,640/2,304 total bytes over millions
of operations; BDN summary rounding to zero does not erase those totals. Axis
and snapshot summary-vs-exact-counter differences remain as shown above.
Retained process-heap observation was 29,309,120 B for 128 snapshots
(228,977.5 B/snapshot), again exceeding attributed thread allocations. It remains
a noisy diagnostic rather than an exact retained-memory result or bounded-size
claim. Logical input/node-set/string growth limits and operation ceilings are
proven by focused checks/code preflight, not inferred from that heap estimate.

This completes the branch's required provisional single-Arm64 navigation
allocation/timing evidence. Cross-architecture release and end-to-end evaluator
composition measurements remain future integration/gate work; exact retained
size needs an appropriately isolated measurement if claimed. The original raw
paths were reused by this explicitly authorized target-runtime sequence; the
first attempt's derived observations/history are preserved above. No raw result
files are committed.
