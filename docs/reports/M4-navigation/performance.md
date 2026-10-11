# Navigation measurement summary — provisional runtime mismatch

Measured implementation/probe revision: d79ffea3ca933f38227a8cfc74b31d49454832b4.
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
