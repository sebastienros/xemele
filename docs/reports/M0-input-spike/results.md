# Results and architecture recommendation

**Keep string callers on borrowed character input, and preserve bounded incremental decoding for byte callers.** Both character-core and byte-core candidates meet the experiment's warmed allocation and input-retention requirements. These are candidates for M0-contracts/M1 validation, not a parser architecture freeze. Byte framing before callback decoding does not establish an end-to-end advantage in this synthetic workload; decoder-first character scanning is a viable simpler candidate. Do not round-trip a string through an owned UTF-8 array simply to share an implementation.

## Revisions and validity

- Corrected scanner/checks: `87d101c7e9b09167b2d114f1f33ea5974680617d`; 56 input and 6 rejection benchmark cases, plus all final correctness, allocation, compiler and build checks.
- Unchanged primitive suites: `662b15bb24ce340c301389e037277fd68d9f60ab`; 24 chunk matches, 20 literal/lowering cases, 96 matches and 80 scans. Their implementation files are unchanged by the scanner correction.
- Original full run executed all 282 cases in 656.44 seconds. Review then found repeated full-tail scanning per 64-unit fragment, which could become quadratic on long chunks. Search now examines at most quantum+1 units (65 by default), preserving delimiter lookahead, consumed counts, final flush and pauses. Corrected input/rejection rerun executed all 62 cases in 98.28 seconds. The original 62 affected cases are explicitly **superseded** and kept in `raw/superseded/`; they are not evidence for the corrected scanner.

Every active case has three measured workload iterations and nonempty statistics. [Suite revisions](raw/suite-revisions.json) and [matrix summary](matrix-summary.md) identify the exact revisions; each raw JSON preserves workload/overhead measurements, runtime metadata, statistics and MemoryDiagnoser metrics. CSV and GitHub-formatted BDN tables are beside it. Compressed original/corrected console logs preserve setup, warnings and execution counts.

## Host and measurement limits

Apple M4 Pro, arm64, 14 physical/logical cores; macOS Sequoia 15.8.1 (24H32), Darwin 24.6.0. SDK 10.0.400, runtime .NET 10.0.11 (10.0.1126.37416), RyuJIT armv8.0-a, ArmBase/AdvSimd/AES/CRC32/DP/RDM/SHA1/SHA256, 128-bit vectors. Release build, workstation GC, interactive latency, concurrent GC. BenchmarkDotNet 0.15.8, one launch, two warmups, three 100 ms target iterations, default tiering/PGO; fixture generation and owned context setup outside timing. No debugger attached. Timing slots were serialized with the coordinator and an atomic lock directory. Process high-priority setup was denied; no scheduling guarantees are claimed.

Three short iterations produce broad confidence intervals. Near-empty/subnanosecond operations cannot be ranked reliably; aggregate ratios omit denominator means below 1 ns, while retaining **every** raw case. Input `Chunk` counts bytes on byte transport and UTF-16 units on character transport; multilingual inputs therefore require different numbers of transport steps. This is provisional single-host evidence; x64/arm64 repeatability and actual XML parser composition remain required before release claims.

## Conversion and suspension

All five borrowed/scratch/paused paths reported **0 B/op** under MemoryDiagnoser and the separate synchronous allocation regression. Owned whole-input UTF-8 arrays allocated 56–6,168 B/op; decoded whole strings allocated 80–7,704 B/op. Invalid UTF-8 rejection costs 1,160–1,184 B/op and approximately 2.87–3.03 microseconds, including exceptions; it is not part of the no-error allocation budget.

A representative corrected multilingual long-input case (128 repeated token pairs; transport chunk 256 units):

| Path | Mean ns/op | Managed B/op |
|---|---:|---:|
| String → character core | 3,996.73 | 0 |
| Byte → byte-framed core → character callback | 6,319.38 | 0 |
| Byte → bounded character scratch → character core | 5,537.45 | 0 |
| String → paused character core | 3,918.76 | 0 |
| Byte → paused byte-framed core | 6,001.05 | 0 |
| Byte → owned whole string → character core | 7,377.91 | 7,704 |
| String → owned whole UTF-8 array → byte core | 8,625.64 | 6,168 |

Pause timings sometimes fall below unpaused timings within short-run uncertainty; this is not evidence that pausing is free or faster. Other fixture/chunk combinations make a whole-string decode faster than incremental scanning, at the cost of allocation proportional to the complete input. Choose based on the actual bounded-memory/latency contract, then remeasure a composed parser.

`correctness.txt` records **83,095 assertions**: every short two-part byte/character split, chunks 1..32, pauses after each unit, independent checksum/token oracle, malformed UTF-8/UTF-16, exact/ASCII-insensitive matching, deliberate collisions, 1,000 deterministic random unknowns, culture independence, bounded candidate overflow, and single-chunk tokens up to 1 MiB with exact boundary/progress accounting. Source inspection establishes search bounded to the fragment window; each iteration cannot scan the full remaining tail.

A paused 4 MiB source is collectible while scanner/sink remain alive. Scanner stores no input references; UTF-8 carry is bounded to three bytes, character validation to one high surrogate, callback scratch to 128 UTF-16 units. A 64 MiB token from one reusable 64 KiB chunk completes with one token and **0 B processing allocation**. These show bounded input retention, not complete parser memory or process RSS.

## Scan and matcher choices

For five-stop long/late scans, cached SearchValues strongly beats simple scalar loops on this host. One 1,024-unit late-hit fixture measured byte cached/simple at 22.86/410.49 ns and character cached/simple at 30.77/397.46 ns, all 0 B/op. Across meaningful same-parameter sets, cached/simple median ratios were 0.055 byte and 0.076 character. Small-set IndexOfAny was also measured; the representative late case measured 43.58 ns byte and 96.03 ns char. Tiny/early cases approach harness overhead. Retain simple state-specific operations when their actual workload gives no repeatable advantage; the synthetic scanner uses a single-delimiter IndexOf, whose separate controls cannot substitute for five-stop validation scans.

For this 12-token vocabulary, retain length-dispatched equality. The measured collision-rich hash/table candidate does not justify extra complexity: hash/length median ratios across meaningful cases were 1.97 byte and 2.21 character; chunked matching medians were 1.64/1.63. Both allocate 0 B/op after initialization. All hash hits verify length and full equality. Non-ASCII, ASCII case rules, punctuation, shared prefixes, max+1 overflow and a same-length `xml`/`xnm` collision reject correctly. This does not evaluate an optimized generated perfect-hash implementation or a larger vocabulary; those need separate evidence.

## Cold allocation and lowering

Fresh default-tier diagnostics report 7,992 B for explicit primitive initialization (cached SearchValues, arrays/vocabulary, hash metadata, encoded tokens and temporary setup LINQ objects), and 496 B for both scanner/sink contexts and owned encoding/decoders. These are **allocated setup bytes**, not all retained bytes; static metadata has process lifetime, contexts operation lifetime, setup temporaries are collectible. Counter fixtures and delegates are outside measured warm intervals. All normal borrowed operations, local collections and static literals assert zero allocation across 1,000 measured calls after 100 warm calls.

IL inspection in `lowering-il.txt` shows UTF-8/constant byte spans referencing compiler static data, character spans borrowing interned literals, constant int spans using RuntimeHelpers.CreateSpan, and local runtime-valued/mutable collections using inline arrays. Returned runtime-valued/mutable collection expressions are rejected with CS9203, reproduced by `check_escape.py`; the successful expected-rejection output is in `escape-check.txt`.

Explicit array-backed return controls contain `newarr`. In default short counter batches, readonly/mutable returns allocated 32/104 B/op. With `DOTNET_TieredCompilation=0`, they allocated 32/32 B/op; cold setup was 7,848/360 B. Captured FullOpts getter JIT output (`lowering-getters-jit.txt`) includes `CORINFO_HELP_NEWARR_1_VC`, and optimized diagnostic consumer output (`lowering-jit.txt`) shows constant static-literal extraction folding to a constant. BDN's specialized consumers reported 0 B/op for these controls and timings near empty-harness overhead, consistent with different inlining/optimization context; the captured diagnostic code does not prove the specific BDN elimination mechanism. **A span return type or one optimized consumer does not establish general allocation freedom.** Prefer explicit immutable static spans for lifetime and cold allocation reasons; no reliable literal-versus-once-owned-array timing ranking follows here.

## Checks and follow-ups

Experiment Release build and ten-project solution Release build passed with zero warnings/errors. An initial solution build stalled on reused MSBuild nodes and was canceled (exit 130); retry used `--disable-build-servers -m:1 -p:UseSharedCompilation=false` and passed. The compiler's two expected CS9203 failures are successful negative-test evidence, not a successful source build. No native dependency, shared-file change, later milestone, parser API or DOM behavior is introduced.

M0-contracts must review consumed counts, pause/EOF semantics and borrowing/decoder ownership alongside the tree lane. M1-input/M1-syntax/M1-dom must validate actual XML Unicode/location/normalization/lexical behavior and remeasure end-to-end workloads, distinct/repeated names and token limits. Cross-architecture and longer repeated performance runs remain release follow-ups. Coordinator integration and verification are still required; this worker reports `implemented`, never `verified`.
