# CSS selector measurement evidence

Measured source/probe revision: `12f4cb51102e5551dc981b7ea41fc91ce717724c`.
Runtime behavior reviewed at `92c8756b03fd1d666414b88cc415e1bcb1b0bac8`;
12f4cb5 changes only measurement setup to cache supplied nodes outside timing.
Exclusive grant: main `e15cc82`, lease `m7-css-12f4cb5`; durable release:
main `821e429`. Exactly one elevated sole-command invocation ran:
`python3 docs/reports/M7-css/run_measurements.py --exclusive-grant`.
No raw results were opened until release.

Prepared manifest SHA-256:
`988486264e234199bad00b47253b7008761ca780ecb329b3d9cd842fca14d4e4`.
Identity covers 3681 source/generated/corpus files, 43 binaries and 3852 SDK,
hostfxr and runtime files. It includes all 3386 source/destination SHA-verified
local W3C manifest resources, actual compiler-generated code, complete product
sources and the actual probe. Complete effective child environment SHA-256:
`60d9dcefb514fb59f21e94d53033066b7fcbbc8d8f309d3d9581a5e2a65b37ca`.
Opaque hashes cover every environment key, including CODEX_THREAD_ID; no secret
values are reported. SHLVL=1; tiered compilation/PGO/ReadyToRun=0; roll-forward
Disabled; SDK 10.0.400 and runtime 10.0.11. Host: Apple M4 Pro, Mac16,8,
14 cores, 51,539,607,552 bytes RAM, macOS 15.8.1 / Darwin 24.6.0, Arm64.

Exact-wrapper identity-only controls: positive and final positive each report
initial=0, final=0, exit=0, no_child=true and no product runs. An unknown effective
environment key is rejected: initial=1, final=1, exit=1, no_child=true and no product
runs. Measurement lifecycle reports initial=0, allocation=0, benchmark=0, final=0,
wrapper=0, no_child=true. All owned identity/product process groups were cleaned
and reaped, including the final identity child. The 1100-second total includes all
phase maxima and 70 seconds conservative cleanup; 29 cases completed in 205.26 s.

## Allocation boundaries

After 1024 warm operations per selector, 352 batches covering 11 selectors and
both actual graph types each measured 4096 supplied-node Matches calls: all
1,441,792 calls allocated 0 B. Six production class-token inputs add 24,576
operations at 0 B. Across 358 batches: 1,466,368 measured operations, no nonzero
record. This domain includes current graph/name/value/link access inside Matches;
owner root access and graph construction are fixture setup outside the counter.
It does not claim owner-root lookup, compilation, query output or DOM creation
is allocation-free. The positive-control 1024-byte array allocated 1048 B.

| Owned domain | Numerator / denominator | Bytes per operation |
| --- | --- | ---: |
| Cold CSS compile and initialization | 2616 / 1 | 2616 |
| Cold scanner comparison fixtures, SearchValues, reflection delegate binding | 9976 / 1 | 9976 |
| Warm compilation | 238400 / 100 | 2384 |
| XML query plus immutable result | 140800 / 100 | 1408 |
| HTML query plus immutable result | 79200 / 100 | 792 |

The scanner fixture initialization belongs to benchmark setup, not production
CSS compilation. Owned query figures include transient List growth, copied arrays
and read-only wrappers for 17 results over 65-element actual trees. Retained
snapshots intentionally retain permanent owners: 100 fresh XML/HTML graph pairs
with both result snapshots measured an inclusive whole-heap delta of 8,952,320 B
(before 414,352; after 9,366,672), or 89,523.2 B per pair. This includes complete
owners, graph data, tuple/retaining-array overhead and runtime roots; it is not an
isolated per-node or snapshot-size estimate and is subject to GC noise.

## Borrowed scanner comparison

Same CSS-whitespace semantics, consumed boolean results, cached fixture creation
outside timing, oracle equality at setup. The production loop is the actual private
ContainsToken implementation bound once by a cached delegate. The alternatives
are allocating Split, cached SearchValues and span IndexOfAny of the five CSS
whitespace characters. Each BDN class uses MemoryDiagnoser and ShortRun; all 29
cases use InProcessNoEmit, 1 launch, 3 warmups and 3 iterations, with no builds or
code generation during the lease.

| Scenario | Split ns / B | Production loop ns / B | Cached SearchValues ns / B | Span IndexOfAny ns / B |
| --- | ---: | ---: | ---: | ---: |
| Short final hit | 31.232 / 104 | 12.778 / 0 | 8.157 / 0 | 7.472 / 0 |
| Long middle hit | 326.342 / 1160 | 248.574 / 0 | 11.279 / 0 | 21.911 / 0 |
| Multilingual long miss, NBSP | 126.105 / 432 | 139.272 / 0 | 15.900 / 0 | 24.076 / 0 |
| Long early hit | 569.994 / 2152 | 10.486 / 0 | 4.166 / 0 | 4.029 / 0 |
| Short miss | 29.359 / 104 | 11.608 / 0 | 6.439 / 0 | 5.769 / 0 |
| Dense delimiters, middle hit | 1664.170 / 4240 | 314.356 / 0 | 2569.691 / 0 | 1607.041 / 0 |

Retain the simple production loop for this deliverable: it removes Split's owned
token allocations and handles dense runs efficiently. Cached searches win sparse
long scans on this host but lose the dense case substantially; no repeatable,
representative blanket gain or validated threshold justifies a production switch.
No custom SIMD/hash optimization was introduced. A later adaptive search decision
requires its own correctness/composition and repeatability evidence.

## Actual two-graph composition

Selector: `root > item.selected:nth-child(2n+1):not([data=absent]), #hit`.
The shared compiled program selects 17 unique preorder results from 64 item children,
with actual current XML and HTML names/attributes, structural positions and negation.

| Operation | Mean | Managed bytes/op |
| --- | ---: | ---: |
| Supplied XmlNode Matches | 380.5 ns | 0 |
| Supplied HtmlNode Matches | 317.4 ns | 0 |
| XML Select plus immutable snapshot | 10.6311 us | 1408 |
| HTML Select plus immutable snapshot | 8.4339 us | 792 |
| Owned Compile | 688.8 ns | 2384 |

These timings are informational ShortRun results on one Arm64 host. High-priority
setup was denied by the OS; confidence intervals are broad in several scanner
cases, especially dense IndexOfAny. No x64, cross-host or release-level speed
claim is made. The repeatable allocation counters and MemoryDiagnoser agree on
the declared warmed borrowed/owned boundaries. Raw BDN logs/CSV/HTML and the
identity manifest remain ignored under artifacts/M7-css; they are not committed.
