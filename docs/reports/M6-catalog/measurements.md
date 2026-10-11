# Catalog measurement evidence

Tested source: `d5de67996156d18a61c8d65abbe8991886365a32`; prepared revision:
`c1c62b88a4b375a7d184934bc7691e24438d7f20`. The exact frozen manifest SHA256 was
`c0e0ee9d8a0d3b6dcff92569b55a87ec431fb6fd7214e0d2096e27560fe6ea8d`
(226 source, 43 binary, 3,852 runtime/SDK/host files, complete effective environment).
Both wrapper identity controls passed in the intended elevated context: the positive
control matched and the unknown-variable negative control rejected, without measured phases.

Coordinator lease `m6-catalog-c1c62b8` was granted at main `d2814b2` and released
at `9b0b905`. The sole-command `python3 docs/reports/M6-catalog/run_measurements.py
--exclusive-grant` ran once. Initial/final identity, allocation and benchmark phases
all exited 0; no child survived cleanup. Boundaries were initial 35s, allocation 60s,
benchmark 900s, final 35s, total 1,050s. Raw allocation and all eight BDN cases were
inspected after release and independently confirmed by the coordinator. Ignored
raw outputs remain in `artifacts/M6-catalog/` for independent review; none is committed.

## Allocation boundaries

Actual .NET 10.0.11, Arm64. Each borrowed domain used 16 batches of 4,096 operations;
every batch allocated exactly 0 bytes (327,680 operations across five domains).
Fixture/request setup and graph construction precede these counters. Hits consume
stored URI references; misses consume null. Deep traversal uses an already-warmed
pool and a 49-catalog graph.

| Domain | Inclusive bytes | Operations |
|---|---:|---:|
| Borrowed 128-entry last-key exact hit | 0 | 65,536 |
| Borrowed 128-entry miss | 0 | 65,536 |
| Borrowed already-normalized public ID hit | 0 | 65,536 |
| Borrowed URI-kind hit | 0 | 65,536 |
| Borrowed deep graph with pooled frames | 0 | 65,536 |
| Positive new byte[1024] control | 1,048 | 1 |
| Cold fixture and first graph | 194,200 | 1 |
| Owned deep graph setup | 2,842,544 | 1 |
| Cold pooled lookup frame storage | 2,272 | 1 |
| Owned multilingual rewrite result | 47,280 | 100 |
| Owned public ID normalization | 12,088 | 100 |
| Owned mapped request, refusing fallback | 5,600 | 100 |
| Owned 128-entry graph construction | 14,705,600 | 100 |

Cold figures include runtime/static/pool effects, and do not attribute bytes to an
individual internal object. Deep setup includes 48 caller-resolver resources, bytes,
parsing and graph ownership. Mapped refusal measures request composition and one
null fallback, with no original-request retry. Async behavior and exception/cancellation/
quota denial paths have correctness coverage but no allocation or timing claim.

Whole-heap retained measurement rose from 418,376 to 5,939,640 bytes: inclusive delta
5,521,264 bytes for 100 retained graphs (55,212.64 bytes/graph). This includes the
retention array, graphs and runtime roots; it is not an exact graph object-size result.

## Informational timing

BenchmarkDotNet 0.15.8, Apple M4 Pro, macOS Sequoia 15.8.1, SDK 10.0.400,
actual .NET 10.0.11 Arm64 RyuJIT. All eight cases used attributed MemoryDiagnoser and
ShortRunJob, InProcessNoEmitToolchain, three warmups, three measured iterations and
one launch. All means and three workload results per case were nonzero. No CLI job
flags were used; frozen tiering, PGO and ReadyToRun settings were disabled.

| Case | Entries | Mean ns | Error ns (99.9% CI half-width) | StdDev ns | BDN bytes/op |
|---|---:|---:|---:|---:|---:|
| StraightforwardExact | 8 | 11.64 | 0.318 | 0.017 | 0 displayed |
| CatalogExact | 8 | 29.96 | 29.935 | 1.641 | 0 displayed |
| CatalogMiss | 8 | 52.21 | 2.415 | 0.132 | 0 displayed |
| OwnedParse | 8 | 28,410.45 | 1,921.134 | 105.304 | 26,536 |
| StraightforwardExact | 128 | 92.37 | 0.409 | 0.022 | 0 displayed |
| CatalogExact | 128 | 261.39 | 105.703 | 5.794 | 0 displayed |
| CatalogMiss | 128 | 672.32 | 349.253 | 19.144 | 0 displayed |
| OwnedParse | 128 | 291,962.73 | 6,097.148 | 334.205 | 147,057 |

The baseline is a BCL-backed ordinal linear KeyValuePair array exact scan with the
same fixture key/result, not another catalog implementation or full-feature parser.
Catalog exact lookup was 2.57x/2.83x that narrow baseline; no speedup is claimed.
OwnedParse consumes a newly parsed immutable graph, with its input fixture outside
timing. Short-run confidence intervals are wide; timing supports no optimization,
release portability or x64 claim. Ordinal equality/prefix comparisons remain.

BDN rounds tiny inclusive scaffold allocations to zero: raw GC numerators for the
six matching cases were 0, 2,560, 2,272, 2,304, 2,560 and 2,560 bytes respectively
across millions of operations. Strict zero borrowed evidence comes from the separate
per-thread allocation batches above, not rounded BDN output.

Functional evidence remains 32 focused passing controls, public offline sample,
972 compiled API union members (951 inherited + 21 catalog), eight-case discovery
and planning validation of 162 task records. Integration and the combined inherited
suite belong to the coordinator; this branch is implemented, not verified.
