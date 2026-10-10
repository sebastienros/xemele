# Syntax allocation and provisional ShortRun evidence

Source revision: `d4de9b394cec42fe8caa5c8deacc7c31ecc7b4b0`.
Runtime source is unchanged since reviewed `7ea67f77a7d878f510f2ea29e1d30bb187439c2a`.
The final verifier process 42388 exited 0 before benchmark process 7185 began.
The coordinator explicitly reserved the host for M1-syntax's 21-case ShortRun;
serialization was parked and the coordinator reported no heavy checks. No other
builds/tests were started by this worker during timing. BenchmarkDotNet's own
startup restore/build completed before timed workload processes began.
The outer process exited 0 and cleanup finished before window release was reported.

Command (SDK root/PATH pinned, DOTNET_CLI_USE_MSBUILD_SERVER=0):

```
dotnet run --project docs/reports/M1-syntax/Xemele.SyntaxBenchmarks.csproj -c Release --no-build --no-restore -- --filter '*' --artifacts artifacts/M1-syntax/bdn
```

Host: Apple M4 Pro, Arm64, macOS Sequoia 15.8.1 (24H32), .NET SDK 10.0.400,
.NET 10.0.11 Arm64 RyuJIT, concurrent workstation GC, 128-bit vectors,
BenchmarkDotNet 0.15.8. ShortRun: one launch, three warmup and three measurement
iterations. Normal process priority: BDN's high-priority request was denied.
Raw console: ignored artifacts/M1-syntax/benchmark-console.txt; raw BDN log/results
are beside it under artifacts/M1-syntax/bdn. Durable CSV/Markdown tables are in
this report's bdn/ directory. All 21 child workload processes exited 0; all cases
have three WorkloadResult records (63 total), nonmissing/non-NaN mean and memory
results. No harness or quota failures occurred during this window.

## Grammar ownership boundaries

| Case | Mean | Allocated |
| --- | ---: | ---: |
| WarmRepeatedTagAndUnicode | 1,012.4 ns | 0 B/op |
| ColdDocument | 1,327.6 ns | 7,248 B/op |
| StructuredRejection | 472.8 ns | 6,688 B/op |

Warm dispatch reuses one operation with an open root and primed name/scope/scratch
capacity. Each append contains a repeated empty tag with ordinary/namespaced
attributes and namespace declaration, alpha/astral Unicode, an amp reference and CRLF.
DocumentCharacters/TokenCharacters are explicitly unlimited for the continuous
synthetic operation; other finite quotas remain active and no quota fails.
Every result must be NeedInput; the sink consumes text units. Construction,
new names and fixture creation are outside this measured steady-state case.
The separate synchronous regression and allocation mode both observe 0 B across
10,000 warm appends. It is character/sink parsing, not decoder/tree composition.

ColdDocument creates and disposes a new core per operation and parses a complete
small character document with attributes, text/references, CDATA, comment and PI,
requiring Completed. Its static/JIT environment is warm; it measures cold
operation ownership, not fresh-process startup. The 7,248 B includes core/setup,
scratch/maps, first scopes and distinct owned names/table growth, with no DOM.
StructuredRejection creates/disposes a new core and requires Rejected for
`<r a='&unknown;'/>`; 6,688 B includes setup and a single owned terminal diagnostic
(72 B beyond the measured 6,616 B warm setup). No exception is constructed or
caught to turn callback failures into syntax rejection, and messages are static.

Separate fresh-process allocation mode at the tested source observes:

- Fresh setup 8,608 B, including this harness's first managed initialization.
- Warm repeated construction/disposal 6,616 B/op.
- Warm dispatch 0 B across 10,000 appends (50,500 emitted text UTF-16 units).
- Retained one-open-root heap-minus-fragmentation estimate 6,872 B/op, excluding
  the preallocated reference array. GC retention estimates are informational.

Bounded scratch growth and new distinct intern keys are explicit persistence
allocations; they are not hidden inside the 0 B warm claim. Input owners/views are
not retained. The long-text regression emits 524,288 units before finalization,
without increasing initial lexical scratch capacity. Tag scratch can grow up to
its configured normalized-scalar/capacity bounds; namespace/attribute/depth/name
counts are separately enforced. Cold/retained measurements use the documented
small corpus and do not claim a universal retained size for maximal legal tags.

## Delimiter primitive comparisons

The 18 candidates compare first matching UTF-16 index in the identical immutable
`<&\r\n]` set: a direct loop, small span IndexOfAny and cached SearchValues.
Setup consumes/compares all three outputs before timing. Corpora cover short
hit/miss, long hit at the last position/miss (4,096 units), dense early delimiters,
and multilingual/astral data. All candidates allocate 0 B/op in steady state;
fixture creation and cached SearchValues initialization are excluded and are not
claimed allocation-free. The runtime core does not instantiate SearchValues.

| Corpus | Direct loop ns | Small-set ns | Cached SearchValues ns |
| --- | ---: | ---: | ---: |
| dense | below useful resolution (0.0057) | 0.5219 | 0.9918 |
| long-hit | 1,665.0009 | 348.3692 | 138.8726 |
| long-miss | 1,554.5419 | 334.1267 | 139.2247 |
| multilingual | 5.8627 | 0.9987 | 1.2419 |
| short-hit | 0.9546 | 0.8336 | 1.2768 |
| short-miss | 2.1937 | 2.8962 | 2.4984 |

The dense baseline is indistinguishable from overhead; its ratios are unusable.
ShortRun intervals for several tiny cases are wide. Long delimiter-only candidates
show a useful direction for future bulk scanning on this host, but these primitives
do not validate XML/scalars, quotas, normalized source locations or pause state.
They therefore do not prove a whole-parser improvement. The core retains its
straightforward scalar state machine; no hashing/SIMD/scanner optimization is
adopted from an unrelated semantic workload. A semantically complete future bulk
path requires its own oracle/setup/retention/composition evidence before adoption.

These timings are provisional single-Arm64 character/sink evidence, not a public
performance victory. Decoder + DOM composition belongs to M1-dom; x64/Arm64
release confirmation and longer repeated performance runs remain release work,
not dependencies secretly added to this independent syntax implementation.
