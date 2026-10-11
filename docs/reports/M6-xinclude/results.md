# M6 XInclude branch evidence

Tested source: `d4396d91442e13ab378dd2140b4a1ca1a11d60dc`.
This is branch implementation evidence; integration verification remains with the coordinator.

## Correctness and API

The bounded fresh preparation completed all 13 phases with exit 0 and no remaining
children: offline-cache restores, forced nonincremental Release builds, 97 focused
checks (0 failed/errors/skipped/not-run), independent public sample, six-case
benchmark discovery, compiled API union and planning validation (162 tasks).
Execution used the pinned SDK 10.0.400 host with explicit `--fx-version 10.0.11`
and build/restore `RuntimeFrameworkVersion=10.0.11`. The sample and test runner
reported actual .NET 10.0.11. The sample resolver genuinely yields asynchronously;
focused checks additionally exercise asynchronous reads and DisposeAsync.

The compiled union matched 974 members: 951 inherited members (including the
three integrated resource-kind additions) plus this task's 23-member baseline.
The scoped approved API checker avoids unrelated corpus discovery. The inherited
full suite was not run by this worker; combined-tree regressions belong to the
coordinator. The module consumes the verified integrated shared-parser-budget
repair and has no catalog or unmerged peer dependency.

Focused coverage includes immutable original content/revision/source metadata,
owned result/diagnostic snapshots, incomplete metadata rejection and transformed
provenance invalidation, namespace/base/language fixup and root structure,
shorthand/element selection and captured-source identity, redirect aliases/cycles,
strict encoding, fallback eligibility, exception identity and cleanup, cancellation,
exact aggregate scalar/entity/DTD/resource quotas and one-over boundaries, and
4,096-resource sync/async chains with both inclusion-depth limits null.
Independent source review approved the explicit continuation frames and bounded
pointer/path cancellation; it was a source review, not an executable check.

## Allocation observation

The independent public sample uses process-wide `GC.GetTotalAllocatedBytes(true)`
so allocations on asynchronous continuations are included. Fixtures precede the
counter; warm averages follow 32 sync/async warmup pairs and use 128 operations.
These are owned document/result operations, with scratch, snapshots, wrappers and
DOM storage; no borrowed primitive has a zero-allocation claim.

| Observation | Bytes |
| --- | ---: |
| Cold owned XML operation | 68,344 |
| Warm owned sync XML operation, truncated mean | 66,956 |
| Warm owned true async XML operation, truncated mean | 67,772 |
| Retained one-result GC delta | 81,456 |
| Positive `byte[1024]` control | 1,048 |

Warm means are integer-divided bytes per operation, truncated toward zero. The
raw log does not contain total numerators; no exact inclusive totals are inferred
from these quotients. Cold allocation includes first-use costs in this sample process. Process-wide
counters may include runtime background activity; the exclusive lease excludes
competing managed measurements, not all runtime work. Retained GC delta is a
separate approximate host observation, not precise reachable graph size or an
allocation count. The positive control confirms a nonzero counter observation.

## Benchmark observation

BenchmarkDotNet 0.15.8 on Apple M4 Pro, macOS 15.8.1, Arm64 RyuJIT,
.NET SDK 10.0.400 and actual .NET 10.0.11, concurrent workstation GC.
All six cases retained `[MemoryDiagnoser, ShortRunJob]` with
InProcessNoEmitToolchain, 1 launch, 3 warmups and 3 measured iterations. No CLI job
flags, generated build or child benchmark build was used. Fixtures precede timing.
Error is half the 99.9% confidence interval; GC columns are collections per 1,000
operations and KB means 1,024 bytes.

| Case | Mean (us) | Error (us) | StdDev (us) | Gen0 | Gen1 | Allocated (KB/op) |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Short owned text | 1.961 | 0.0099 | 0.0005 | 1.6060 | 0.0381 | 13.15 |
| Multilingual owned text | 17.663 | 1.1386 | 0.0624 | 3.9978 | 0.3967 | 32.89 |
| Nested owned XML | 12.614 | 0.2800 | 0.0153 | 15.3503 | 1.9073 | 125.70 |
| Denial with owned fallback | 4.458 | 0.0401 | 0.0022 | 1.3504 | 0.0458 | 11.04 |
| Selected owned XML | 6.297 | 0.7948 | 0.0436 | 8.1024 | 1.0071 | 66.35 |
| True async owned XML | 24.017 | 0.4922 | 0.0270 | 15.8081 | 2.5635 | 127.04 |

All cases have nonzero raw managed allocation and Gen0/Gen1 statistics; Gen2 was
zero. Async raw threading statistics recorded 65,538 work items and 35 contention
observations over 32,768 operations. Different fixtures make the independent
sample's allocation numbers unsuitable for direct comparison with nested BDN
cases. The three-iteration ShortRun is a local descriptive observation, not a
cross-host threshold or optimization comparison. BDN could not raise process
priority (permission denied); measurement otherwise completed six cases, exit 0.

## Lease and identity provenance

The exact single execution used lease `m6-xinclude-d4396d9`, durably granted on
coordinator main `164429c` and released on `11a5178` before raw inspection.
Frozen manifest SHA-256:
`69570c31b4a54327ebb99e9f03066a8a984aa9396f7181460a6de0c3e110a196`.
Initial and final identities both passed, covering 210 source/config/generated
items, 50 binary items and all 3,852 SDK/runtime/host items plus the complete
effective child environment hash and readable runtime variables. The exact
wrapper positive identity-only control passed; the unknown-variable negative
control rejected both identities as expected. Both left no children. SHLVL=1
was explicit; capture and launch used the same elevated executor context.

The earlier 20830a5 identity-only control failed complete environment identity
because capture and wrapper executor contexts differed; no measured phase ran.
It remains recorded in README rather than described as a successful measurement.
The granted wrapper ran once with initial/final 35-second limits, allocation 60,
benchmark 720 and total 840 seconds. Allocation and benchmark exits were 0;
final wrapper exit was 0 and no children remained. Raw allocation, preparation,
identity and BDN artifacts were inspected after durable release and remain ignored
under `artifacts/M6-xinclude/` for coordinator inspection. Only this summary and
analysis are committed; no raw measurement logs are committed or deleted.
