# Local BenchmarkDotNet results

All 30 cases completed successfully in 388.06 seconds on 2026-10-15, before
the repository history was squashed. Only documentation
had uncommitted edits before the run; runtime code, benchmark code and fixture
bytes were unchanged during measurement. Qualification: 43/43 checks passed,
including assertions of seven documented compatibility differences; no unexpected
failures.

Apple M4 Pro, 14 physical/logical cores; macOS Sequoia 15.8.1 (24H32), Darwin
24.6.0; 48 GiB RAM; SDK 10.0.400; .NET 10.0.11, Arm64 RyuJIT armv8.0-a;
BenchmarkDotNet 0.15.8, AngleSharp 1.8.2 net10.0. ShortRun was overridden to use
one launch, three warmups and 10 measurement iterations, concurrent workstation GC.
Default outlier removal left 8-10 samples per case. The host denied high-priority
scheduling. This was a local desktop run on AC power in the normal power mode,
with no explicit CPU affinity or tiering/PGO/GC environment overrides, not
dedicated hardware or x64 verification.

Times below are microseconds/op, mean ± BDN's 99.9% confidence-interval half-width.
Ratios divide Xemele's unrounded mean time or allocation by AngleSharp's;
lower is better. Allocations are inclusive managed bytes/op (KiB = 1024 bytes),
not retained DOM size or peak process memory. These are fixture-specific results;
one desktop rerun does not establish a runtime regression against the earlier run.

| Operation | Fixture/context | AngleSharp μs ± error | Xemele μs ± error | Time ratio | AngleSharp allocation | Xemele allocation | Allocation ratio |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| String parse | Article | 316.959 ± 8.991 | 145.027 ± 1.406 | 0.46 | 468.05 KiB | 338.99 KiB | 0.72 |
| String parse | RecoveryForeign | 14.336 ± 0.621 | 6.377 ± 0.137 | 0.44 | 22.15 KiB | 15.33 KiB | 0.69 |
| String parse | Small | 6.055 ± 0.069 | 2.350 ± 0.077 | 0.39 | 11.52 KiB | 6.04 KiB | 0.52 |
| UTF-8 stream parse | Article | 338.629 ± 4.687 | 150.774 ± 0.699 | 0.45 | 540.10 KiB | 339.28 KiB | 0.63 |
| UTF-8 stream parse | RecoveryForeign | 15.498 ± 0.217 | 6.172 ± 0.104 | 0.40 | 41.44 KiB | 15.52 KiB | 0.37 |
| UTF-8 stream parse | Small | 7.287 ± 0.176 | 2.674 ± 0.039 | 0.37 | 29.69 KiB | 6.23 KiB | 0.21 |
| Contextual fragment | Table | 2.687 ± 0.034 | 0.889 ± 0.024 | 0.33 | 8.20 KiB | 3.75 KiB | 0.46 |
| Contextual fragment | Template | 4.347 ± 0.038 | 1.233 ± 0.037 | 0.28 | 11.11 KiB | 4.70 KiB | 0.42 |
| Contextual fragment | Textarea | 1.684 ± 0.104 | 0.619 ± 0.025 | 0.37 | 6.75 KiB | 2.32 KiB | 0.34 |
| String-selector query | Article | 17.736 ± 0.145 | 6.882 ± 0.076 | 0.39 | 1256 B | 1168 B | 0.93 |
| String-selector query | RecoveryForeign | 0.387 ± 0.004 | 0.193 ± 0.003 | 0.50 | 200 B | 112 B | 0.56 |
| String-selector query | Small | 0.205 ± 0.003 | 0.083 ± 0.002 | 0.41 | 200 B | 112 B | 0.56 |
| HTML string output | Article | 102.901 ± 2.012 | 29.841 ± 0.345 | 0.29 | 181.53 KiB | 90.14 KiB | 0.50 |
| HTML string output | RecoveryForeign | 4.173 ± 0.083 | 0.865 ± 0.074 | 0.21 | 8.34 KiB | 3.62 KiB | 0.43 |
| HTML string output | Small | 1.427 ± 0.041 | 0.318 ± 0.004 | 0.22 | 4.42 KiB | 1.38 KiB | 0.31 |

Across all 15 paired workloads, Xemele is faster and allocates less than
AngleSharp. On the 128-article page, Xemele takes 0.46× AngleSharp's string
parse time and 0.45× its stream parse time, allocating 0.72× and 0.63×
respectively. Stream parsing uses pooled `ArrayPool` buffers and skips per-unit
offset tracking when source locations are not requested, reducing small-page
stream allocation to 0.21× AngleSharp's (6.23 versus 29.69 KiB).

The string-selector workload takes 0.39–0.50× AngleSharp's time and 0.56–0.93×
its allocation. Both libraries use their default internal selector caching over
public string-selector calls.

Xemele's fragment means are 0.28–0.37× AngleSharp's, with 0.34–0.46× allocations.
HTML string output takes 0.21–0.29× the time (roughly 3.4–4.8× faster by mean)
and 0.31–0.50× the allocations. Exact strings match on all three output
fixtures; this is not a general escaping/serialization conformance claim.
Allocations are unchanged at the displayed precision from the earlier
three-iteration run.

The useful next investigations are parsing allocation profiles on the article
fixture, stream decoder/setup costs, and separate compiled-selector versus
compile-plus-select measurements. No runtime optimization or new public API is
part of this comparison. Full conformance, cold initialization, async throughput,
network/file I/O, retained memory, larger real-site corpora and stable cross-machine
regression gates were not measured.

The corpus is deterministic and generated outside timing. UTF-8 byte sizes and
SHA-256 digests (the same inputs are used for both libraries):

| Fixture | UTF-8 bytes | SHA-256 |
| --- | ---: | --- |
| Small | 229 | `ac599bfd54f3b04024b12c3613bd27425148b77243dccd0e8334d75f2bfda8f0` |
| Article | 22507 | `d23ad2bab41a91794e39fda4849a8fb3eb39f6decdf1f588745a755a2da1137b` |
| RecoveryForeign | 424 | `1b2dc4466b9304dcdefb8b1ae986a96073acf92c4aa70b01d1d462e162420fb7` |

[Reproduction commands and workload definitions](README.md).
Raw BDN logs, CSV/HTML and full JSON remain ignored under
`artifacts/readme-html-20261015-rerun/` in the measurement checkout; only summary
tables and analysis are committed.
