# M0 input and dispatch experiment

This is a **provisional architecture experiment**, not an XML parser, contract freeze, public API, or release performance claim. It depends only on integrated, verified `M0-build`. Production scanner/decoder composition belongs to M1 and must remeasure with XML validation, locations, normalization and realistic event lifetimes. This host supplies arm64 evidence only; x64/arm64 release validation remains required.

## Reproduce

From the repository root with pinned SDK 10.0.400 and central BenchmarkDotNet 0.15.8:

```sh
dotnet build experiments/Input/Input.csproj -c Release
dotnet run --project experiments/Input/Input.csproj -c Release --no-build -- check
dotnet run --project experiments/Input/Input.csproj -c Release --no-build -- allocations
dotnet run --project experiments/Input/Input.csproj -c Release --no-build -- lowering
dotnet build Xemele.slnx -c Release
python3 tools/planning/tasks.py validate
```

Run timing in an exclusive coordinator-granted slot. Atomically create `/private/tmp/xemele-benchmark.lock`; do not run if another owner holds it. Release the lock on completion. The full benchmark command is:

```sh
dotnet run --project experiments/Input/Input.csproj -c Release --no-build -- benchmark --filter '*' --artifacts artifacts/M0-input-spike/benchmarks --exporters json csv
```

`Program.cs` defines one launch, two warmups and three 100 ms target iterations. These intentionally short exploratory jobs identify tradeoffs; their confidence intervals and near-zero timing warnings limit conclusions. Fixture generation and context initialization are outside timing; MemoryDiagnoser measures warmed operations. Cold allocation and retained-input diagnostics run separately in a fresh process. All benchmarks consume their results. No concurrent sustained benchmark work is authorized. Select a suite with `--filter '*InputBenchmarks*'`, etc., for a focused reproduction.

## Model and ownership

`Scanner<T,TUnits>.Append` accepts borrowed byte/character spans, reports exact consumed units and `NeedInput`, `Paused` or `Completed`. A synthetic `|` delimiter ends a token; payload fragments have a maximum dispatch quantum of 64 units. An unfinished token may be arbitrarily long because its fragments are streamed. Delimiter search is bounded to the next quantum plus one lookahead unit (65 units at the default); each payload is searched once with only a bounded boundary overlap, rather than repeatedly searching a long remaining tail. The state stores two booleans, no input or token data. This demonstrates suspension **inside** a token: the caller slices off `Consumed`, preserves the unconsumed caller-owned chunk, and resumes. A final callback pause requires an empty final resume. Empty chunks are legal, appending after completion is rejected. This is a scanner/sink mechanics model; it does not settle parser faulting, cancellation, namespace or lexical-event semantics.

`CharSink` consumes borrowed UTF-16 and carries only one pending high surrogate. `Utf8Sink` owns a strict incremental decoder and a character sink; it decodes into a bounded 128-character stack buffer and carries only decoder scalar state (at most three pending UTF-8 bytes). Callback spans expire on return. The UTF-8 byte core recognizes ASCII framing before decoding payload; the alternative decodes byte chunks into bounded scratch before feeding the character core. Byte/string adapters that allocate a complete converted string/byte array serve as explicit conversion baselines. No round-trip conversion is hidden inside a supposedly native string path.

The simplified sinks compute the same UTF-16 checksum and token count. All short two-part splits and chunks 1..32 are checked against a separate whole-string oracle; pauses can occur after every code unit or scalar byte. Invalid UTF-8 (overlong, surrogate, out-of-range, truncated, isolated continuation) and UTF-16 surrogates are rejected. The bounded matcher copies only a maximum ten-byte/character candidate across chunks, detects overflow, and requires an explicit token end before accepting; shared prefixes and max+1 inputs cannot be truncated into valid tokens.

## Benchmark matrix

- **Input:** ASCII/multilingual, short/128-repeat inputs, 7/256-unit transport chunks; native character caller, byte core with callback decoding, byte-to-character scratch, allocating byte-to-string and string-to-byte baselines; byte/character callback suspension. Separate malformed-input exception benchmarks account for error-path allocations.
- **Scanning:** byte/char five-stop cached SearchValues vs identical simple scans vs small-set IndexOfAny; short/long hit at beginning/middle/end, misses, dense delimiters, Unicode. Single-delimiter IndexOf is a separately labelled control with different semantics.
- **Matching:** 12-token fixed ASCII vocabulary; length-dispatched full equality vs a deliberately collision-rich five-bit ASCII hash with precomputed metadata, length and full equality checks. Both byte/char and exact/ASCII-insensitive rules; short/long hits, shared-prefix misses, case variants, punctuation and non-ASCII. `xnm` collides with `xml` and must reject. The hash candidate scans a small metadata table; it is not an evaluation of a production generated perfect-hash system.
- **Chunk matching:** byte/char length/hash matching for short/long tokens, cases, non-ASCII and max+1 input split across two chunks; fixed capacity prevents source retention.
- **Literals/lowering:** UTF-8 and character literals versus once-owned arrays, hits/misses; constant byte/int collection spans, runtime-valued readonly and mutable *local* collections, explicit array-backed escaping controls. Local collection forms are distinct from returning a borrowed local span. IL verifies actual static data/inline-array/heap-array lowering.

The allocation command measures explicit primitive initialization and context construction before warming. Warm counters assert zero processing allocation for the borrowed pipelines, suspended callbacks, scans, fixed-token matchers and local/static literal cases. It separately reports complete conversion and deliberate escaping array allocations. A paused 4 MiB byte source must become collectible while its scanner/sink remain alive. A 64 MiB single token is processed from one reusable 64 KiB chunk with no document buffer or processing allocation. These checks establish bounded **input retention**, not general process RSS or complete parser memory behavior.

Measured results and interpretation are in [results.md](results.md), with [matrix-summary.md](matrix-summary.md), durable CSV/full JSON under `raw/active/`, and [suite-revisions.json](raw/suite-revisions.json). Original scanner/rejection results are preserved under `raw/superseded/`; they do not describe the corrected bounded-search implementation. Initial build experimentation found CS9203 when returning runtime-valued or mutable span collection expressions: the local forms compile and use inline arrays; explicit heap-backed returns are separate controls whose allocations depend on the consuming call site and JIT settings; the captured diagnostic getters allocate, while BDN specialized consumers report zero allocation. The reproducible negative-compile check records this compiler constraint.
