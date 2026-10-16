# Performance goals

These are product targets, not claims of achieved performance. Changes require a documented rationale, not silent relaxation. Reproducible local comparisons are under `benchmarks/XmlComparison/` and `benchmarks/HtmlComparison/`.

Implementation changes follow the [string/byte performance rules](performance-coding.md), including cached SearchValues experiments, static span literals, collision-checked ASCII token matching, ownership, and focused allocation measurements. These apply throughout development, not only before release.

## Comparisons and release targets

Define throughput ratio as `Xemele throughput / reference throughput`; higher is better. Compare equivalent operations and options on the same hardware. For DOM parsing, also report full lifecycle cost including native/Rust destruction and managed allocation/GC, so differing lifetime models are visible.

| Workload | Initial stable-release target | Stretch target |
| --- | --- | --- |
| UTF-8 XML → owned DOM | Geometric mean throughput ≥ 0.80× each pinned native/Rust reference baseline; ≥ 1.00× BCL `XmlDocument` on common supported semantics | ≥ 1.00× both native/Rust references |
| Forward XML read/SAX | ≥ 1.00× BCL `XmlReader` throughput on equivalent byte-stream workloads | ≥ 1.25× BCL |
| DOM → UTF-8 XML | Geometric mean throughput ≥ 0.90× each native/Rust reference | ≥ 1.10× each |
| Compiled XPath evaluation | Geometric mean throughput ≥ 0.80× each native/Rust reference on a fixed representative suite | ≥ 1.00× each |
| DOM retained memory | ≤ 0.75× BCL `XmlDocument` on the frozen representative XML corpus, measured as managed retained bytes | ≤ 0.50× BCL |
| Streaming working memory | ≤ 8 MiB parser-owned live memory on 1 GiB generated input with depth ≤ 64, fixed vocabulary, and tokens ≤ 64 KiB, excluding source/sink buffers | ≤ 2 MiB |
| Traversal | 0 B managed allocations per operation for steady-state concrete child/sibling enumeration | Same for common filtered enumerators |

Apply throughput targets to a frozen balanced corpus of roughly 4 KiB–10 MiB fixtures, with separate small-input latency and very-large-input reports. No individual agreed primary fixture should fall below 0.50× its reference without an explicit release exception. A good geometric mean must not conceal a pathological case.

HTML, CSS, canonicalization, and each validator receive equivalent workload baselines when their first vertical slice exists. Their initial gate is no statistically confirmed regression beyond the policy below; absolute comparative targets are chosen from measured feasibility before those modules stabilize. Do not invent a credible MB/s target without specifying hardware and semantics.

## Workload matrix

- Sizes: tiny (< 1 KiB), small (4–16 KiB), medium (100–500 KiB), large (1–10 MiB), and generated 100 MiB–1 GiB streams.
- Shapes: shallow/wide, deep, text-heavy, attribute-heavy, namespace-heavy, many repeated names, many unique names, entity-heavy, and malformed/recovery inputs.
- Encodings: UTF-8 ASCII, multilingual UTF-8, UTF-16, and selected supported legacy encodings.
- Realistic fixtures: feeds, SVG, XHTML, build/project files, SOAP, configuration, and business documents, each with documented origin and license.
- Operations: decode/scan, strict/recovery parse, DOM build, navigation/mutation, serialization, reader/SAX/push, XPath compile versus evaluate, schema compile versus validate, HTML parse, CSS selection, canonicalization, and cold process startup.
- API costs: string versus UTF-8 input, memory versus stream, sync versus async, and tree/diagnostic options. Never charge only one implementation for an input conversion.

Record latency, MiB/s, operations/s, managed allocation bytes/op, GC counts, retained managed memory, and peak process RSS. RSS/native allocator usage and managed retained bytes are different measurements and must not be presented as interchangeable.

## Measurement protocol

Use BenchmarkDotNet for managed workloads following its [benchmarking practices](https://benchmarkdotnet.org/articles/guides/good-practices.html): Release builds, no debugger, consumed results, and isolated execution. Native and Rust runners perform repeated in-process operations using the same bytes and report their own timing; process startup and IPC are excluded from steady-state comparison. Measure end-to-end CLI separately.

Pin SDK/runtime, GC mode, tiering/PGO settings, native compiler flags, Rust toolchain/profile, reference revisions, corpus hashes, and all options. Record OS, architecture, CPU, memory, power settings, and affinity policy. Run reference and candidate on the same machine in alternating order. Publish raw results and confidence intervals, not just a favorable aggregate.

Preload fixtures for compute benchmarks. Include file I/O only in named I/O workloads. Keep parsing out of XPath-evaluation setup and schema compilation out of validation timing, then add separate end-to-end cases. Verify output checksums/semantic summaries before accepting a speedup.

DOM memory tests keep results alive during retained-memory measurement; repeated lifecycle tests account for cleanup. Streaming tests use a generated stream and a sink that retains no events. Measure live parser-owned state separately from cumulative transient allocations and peak RSS.

## Regression policy

- Shared CI produces informational benchmark reports; stable dedicated machines provide release evidence.
- Investigate a repeated slowdown greater than 5% or managed allocation increase greater than 5% versus the checked-in baseline. Require repeat runs and confidence evidence before calling it a regression.
- A confirmed regression greater than 10% on a primary workload blocks release unless an explicit, documented correctness/feature tradeoff is accepted.
- Memory-bound violations, newly unbounded input retention, or superlinear behavior under fixed-profile adversarial tests block release regardless of average throughput.
- Store environment metadata, raw machine-readable measurements, and reviewed summaries in versioned baseline records. Do not compare measurements across unrelated hardware as a regression test.

## Optimization order

First remove duplicated parsing, avoid hidden DOM creation, and choose suitable storage. Then optimize bulk scanning, name interning, buffer growth, transcoding, traversal, query compilation, and serialization. Add SIMD or unsafe internals only after profiles identify a bottleneck and differential tests protect the behavior. Public APIs stay safe and managed.
