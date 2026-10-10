# Benchmark class configuration

Implementation source revision: `e1ded29c1eeaa11b00707b1c625aec2e2bd9bfb6`.
Verifier correction revision: `1ca7de90fc18aa16a389337dda4b396851a679bb`.
Claim: `1246381a72839c7d6b77e05416985fa407baad6b`.
Integrated prerequisite base: `6d3326fce6dcc979d243a6bdddc92f9ae2e1897e`.

Sixteen benchmark classes now carry `[MemoryDiagnoser, ShortRunJob]`. Six
launchers use those class configurations directly. Tree retains its full JSON
exporter; main help and argument forwarding, experiment commands, allocation
commands, and the internal contract comparison retain their behavior. Benchmark
workload files differ only in the class attribute lists; no runtime, public API,
tests, project configuration, or historical evidence was modified.

## Compiled discovery

`python3 docs/reports/M1-benchmark-class-config/verify.py baseline 1246381a72839c7d6b77e05416985fa407baad6b`
captured original compiled class workloads with their prior launcher job settings.
The script temporarily substitutes only five owned class files from the claim,
builds the real harness assemblies, and restores those files in a `finally` block.
Launchers are not invoked for this baseline: the converter receives their prior
job configuration directly. The baseline snapshots are committed alongside the
checker, before verification of the changed source.

`python3 docs/reports/M1-benchmark-class-config/verify.py current` passed at
`e1ded29c1eeaa11b00707b1c625aec2e2bd9bfb6`: all six harnesses and the checker
built with zero warnings/errors. `BenchmarkConverter.TypeToBenchmarks` discovers
real compiled methods and expands their parameter combinations. Each class and
each resulting case has exactly one job with launch/warmup/iteration counts
`1/3/3`, and exactly one memory diagnoser. Sorted method/parameter inventories,
job counts/settings and diagnoser identities match the original compiled
snapshots byte for byte.

| Harness | Classes | Cases |
| --- | ---: | ---: |
| Tree experiment | 5 | 36 |
| Input experiment | 6 | 282 |
| Main benchmark harness (affected classes) | 5 | 33 |
| Common report harness | 1 | 5 |
| Input report harness | 2 | 8 |
| Serializer report harness | 2 | 20 |

This checks 21 class occurrences and 384 cases across six compiled assemblies;
the five main-harness classes also appear in the report harnesses. There are
16 distinct affected classes and 351 distinct workload cases. Previously
annotated Tree/Syntax/builder benchmark sources remain untouched. Main harness
build and CLI discovery also include its other existing benchmark classes.

`baseline.log`, `current.log`, and the six pairs of JSON inventories retain the
actual discovery output. `ConfigCheck` never calls a benchmark runner or executes
benchmark methods. No timing run, dry run, explicit job argument, or new
performance claim was made.

## Launcher modes and API

`python3 docs/reports/M1-benchmark-class-config/modes.py` passed at
`1ca7de90fc18aa16a389337dda4b396851a679bb`. It executes `--list flat` on all six
launchers (Input uses its `benchmark` command), main help, Tree self-check/memory,
Input help/check/allocations/lowering, Common setup allocation, Input allocation,
Serializer allocation, and Input contract comparison against the approved
`M1Input.dll` model. The contract driver's relative output goes into a disposable
working directory and matches all 41 historical rows byte for byte. `modes.log`
contains every exact command and outcome.

Tree self-check passed eight construction fixtures plus ownership/growth/mutation
checks and the warm borrowed lookup regression. Input self-check passed 83,095
assertions and its allocation/collectability regressions, including 0 B processing
for a reusable-chunk 64 MiB synthetic token. Common escaping construction was
704.00032 B/op; Input cold/warm decoder setup was 2808 B / 816 B/op with an
informational retained estimate of 648 B/op. Serializer's three borrowed scanners
allocated 0 B across 1000 repetitions for each of four corpora; stream allocation
was 1392 B/op at both text lengths (16 and 1,000,000), and the temporary document
owner was collectible. These are existing synchronous allocation modes, not
new latency measurements or async/all-platform allocation claims. Retained-heap
estimates remain informational.

`dotnet build docs/reports/M0-contracts/ApiCheck/ApiCheck.csproj -c Release --disable-build-servers -m:1 -p:UseSharedCompilation=false`
and `python3 -B tools/ApiBaseline/compare.py --report docs/reports/M1-benchmark-class-config/runtime-api.json`
passed at `e1ded29c1eeaa11b00707b1c625aec2e2bd9bfb6`; all 215 compiled public
members matched. The later correction changed only the model path in the owned
verification script. The runtime built as part of the affected harness builds.
No broad runtime suite, fresh package test, or new timing was required or run.

All builds use the pinned SDK 10.0.400, Release, disabled build servers,
single-process MSBuild and disabled shared compilation. `environment.txt` records
the actual host/runtime; this is local verification, not a cross-platform claim.

## Failures and final audit

The first checker build failed NU1900 because sandbox networking could not reach
NuGet vulnerability metadata. The same command with authorized network access
restored successfully and built with zero warnings/errors; subsequent builds
passed. The installed dotnet-inspect version rejected the skill's `--oneline`
option; querying the pinned converter/config APIs without that option succeeded.

The first mode verifier mistakenly used the older foundation model, whose Input
namespace is empty; the contract command rejected that model. Other modes had
passed. `modes-wrong-model.log` preserves that failure. The verifier was corrected
to use the previously verified Input model and all modes passed on the corrected
committed revision. Neither runtime nor historical evidence was changed to
resolve the verifier mistake.

`python3 tools/planning/tasks.py validate` passed for 103 tasks; `git diff --check`
passed. `audit.log` records every changed/untracked path, five byte-identical
workload-body comparisons, the 5/6/1/2/2 annotation inventory, and unchanged
runtime/API/tests/historical evidence. Acceptance is implemented on the worker
branch; integration and integrated verification belong to the coordinator.
