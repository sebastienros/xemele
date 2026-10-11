# Tokenizer allocation and benchmark evidence

Measured checkpoint `6f8945d5532f10d084ab1b972c39bbd73fdde20b`; runtime source
`c8b6a34568a786fb1d0fd86d097a947be5a77583`, probe source
`52d6b4dda76dda218486e44dd5bccbd72af0a022`.
Prepared manifest SHA256
`ed355598e0c2010e75995969c6cae913e29187c5ae2e0bdca9ef30fc88d766a6`.
Exact positive/unknown-env-negative/final-positive wrapper controls passed.
Root grant `6699dcb`, lease `m7-tokenizer-6f8945d`, durable release `adcdb15`.
The sequence executed exactly once, without builds/restores/generation or retries.
Allocation and benchmark exits, initial/final identity checks all 0; no_child=true.
Raw inspection occurred only after durable release. Closure: 3,670 source/input
identities (including all original tokenizer/encoding and immutable XML resources),
43 binaries, 3,852 SDK/runtime/host files, host, full opaque effective environment
SHA and per-key digests, SHLVL=1 and retained session identity. Subsequent changes
are evidence/task metadata only; measured runtime/probe source remains unchanged.

Actual .NET10.0.11 Arm64 RyuJIT, SDK10.0.400, Apple M4 Pro Mac16,8, 14 CPUs,
48 GiB, macOS Darwin24.6.0. Tiering/PGO/ReadyToRun disabled. BDN uses
MemoryDiagnoser + ShortRunJob, 1 launch/3 warmups/3 iterations, InProcessNoEmit,
24 completed cases. High process priority was denied. These single-host short-run
timings are provisional; several long whole-input confidence intervals exceed
their means. No repeatable speedup or architecture-independent result is claimed.

## Allocation domains

Cold shared reference table initialization: 35,744 bytes / 1 operation.
Positive byte-array control: 1,048 bytes / 1 operation.
Borrowed scalar-digit + shared named-reference classification: 96 warmed batches,
1,024 operations each, all exactly 0 managed bytes (98,304 operations total).
Dedicated reference inputs include short/maximum-length/two-scalar/prefix hits and
unknown/long misses. This is a borrowed primitive claim; tokenization persists
owned strings/tokens and allocates operation state. No tokenizer-wide 0 B claim.

Owned totals below include operation construction and all owned lexical snapshots;
each cell is an actual numerator in bytes for exactly 128 operations. Callback
cost includes its captured consumer; list costs additionally retain the result
collection. Per-thread counters apply to this synchronous probe only.

| Fixture | Owned callback bytes/128 | Owned list bytes/128 | Single-unit chunks bytes/128 |
| --- | ---: | ---: | ---: |
| short-hit | 497800 | 509056 | 509088 |
| short-miss | 500880 | 512136 | 512160 |
| long-late-hit | 1597584 | 1620624 | 1620136 |
| long-miss | 1597576 | 1620096 | 1620128 |
| dense | 730240 | 772216 | 772248 |
| multilingual | 557200 | 568456 | 568488 |

Inclusive retained whole heap: 413,800 before, 881,800 after, delta 468,000 bytes
for 128 retained token lists. This includes the list array and runtime roots;
it is not isolated retained bytes per token/document.

## Informational comparisons

Primitive baseline is char.IsAsciiHexDigit plus ASCII value conversion; the
range implementation also serves base-10/base-16 scalar state correctness.
It was slower on these inputs and remains a straightforward grammar utility,
not an adopted speed optimization. No hashing/SIMD/SearchValues scan is adopted;
state-specific scalar handling preserves NUL, normalization, quotas and locations.
Contiguous versus one-UTF16-unit input retains equivalent owned lexical tokens.

| Fixture | Framework mean | Range mean | Range ratio | Whole mean | Chunk mean | Whole allocated | Chunk allocated |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| short-hit | 3.234 ns | 5.974 ns | 1.85 | 487.595 ns | 483.802 ns | 3976 B | 3976 B |
| short-miss | 3.166 ns | 5.287 ns | 1.67 | 1,134.238 ns | 1,121.784 ns | 4000 B | 4000 B |
| long-late-hit | 4,073.946 ns | 4,268.176 ns | 1.05 | 123,692.108 ns | 109,172.287 ns | 12658 B | 12657 B |
| long-miss | 3,992.539 ns | 4,254.179 ns | 1.07 | 173,167.399 ns | 130,132.063 ns | 12658 B | 12657 B |
| dense | 3,181.088 ns | 3,984.018 ns | 1.25 | 4,446.959 ns | 4,460.732 ns | 6032 B | 6032 B |
| multilingual | 5.786 ns | 11.979 ns | 2.07 | 1,382.197 ns | 1,267.513 ns | 4440 B | 4440 B |

Builder/corpus composition must reconfirm end-to-end allocations and throughput.
Release verification must cover x64 and Arm64. No cumulative suite ran here.
Ignored raw identities (logs/results are not committed):

- `artifacts/M7-tokenizer/allocation.log` SHA256 `25a7f9c3898128e2c264fe1e85b8efda15f3e33453a2ac99374e204ebf6c0cd5`.
- `artifacts/M7-tokenizer/benchmark.log` SHA256 `65d96bc15b2044a8fbda68f837e1f2569100fd688d275485934bdbdb808fe6c5`.
- `artifacts/M7-tokenizer/bdn/results/Xemele.Benchmarks.HtmlTokenization.TokenizerBenchmarks-report.csv` SHA256 `3385adca51a85068e3a5989b36af7292887cce65e652ea0221f5da41f84f7ab2`.
