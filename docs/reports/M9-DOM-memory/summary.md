# DOM storage repair

The unchanged frozen ten-fixture aggregate retained-DOM target passes at **0.687098**, below the required **0.75**. The original failure was **0.935969**. Summed candidate medians fell from 170,475,112 to 125,145,240 bytes (26.59%); current BCL summed medians are 182,135,936 bytes. All original fixtures and failures remain preserved.

| Fixture | Original / BCL | Repaired / BCL | Repaired median bytes |
| --- | ---: | ---: | ---: |
| feed-small | 1.937633 | 1.190751 | 31,312 |
| svg-small | 1.301003 | 0.808054 | 143,032 |
| deep-medium | 1.222904 | 1.022536 | 817,464 |
| entity-medium | 1.221590 | 0.851915 | 2,043,520 |
| xhtml-medium | 0.801326 | 0.590884 | 2,356,464 |
| build-medium | 0.803857 | 0.584066 | 4,263,976 |
| soap-large | 1.175049 | 0.948341 | 12,703,024 |
| configuration-large | 0.804682 | 0.565490 | 16,707,800 |
| business-large | 0.923064 | 0.575170 | 52,351,992 |
| text-large | 1.006061 | 1.007211 | 33,726,656 |

The target uses the predeclared ratio of summed fixture medians. Individual small, deep, and text-dominant fixtures remain above 0.75; no fixture was excluded. Each engine ran three held/released full-GC cycles in its own process, verified the frozen semantic digest, and detected the same 9 MiB positive retention control. Source allocation, whole-heap live retention, and process RSS remain separate.

Immutable document-owned name triples replace three string references per arena slot. Only creation caches new triples; prefix replacement probes existing triples and otherwise owns a replacement without growing the triple cache. Lexical interning and preflight quota accounting retain their prior behavior. Source coordinates use dense stable-slot sidecars, full-width line/column/offset fields, presence flags, and reference-preserving URI indices; mutations and incomplete input retain the original invalidation rules.

| Allocation workload | Original KB/op | Repaired KB/op | Change |
| --- | ---: | ---: | ---: |
| Distinct names | 1770.40 | 1688.87 | -4.61% |
| Multilingual repeated names | 1493.61 | 1217.11 | -18.51% |
| Repeated names | 1477.98 | 1201.49 | -18.71% |
| Small document | 54.20 | 54.62 | +0.77% |

Both BenchmarkDotNet workers use the identical fixture setup, `[MemoryDiagnoser, ShortRunJob]`, SDK 10.0.400, .NET 10.0.11, Apple M4 Pro arm64, and the recorded disabled tiering/PGO/ReadyToRun/background-GC settings. The frozen original source is 68c7523bf. One baseline run passed; a candidate project-discovery failure executed zero benchmarks and was retained; only the missing candidate was repeated after proving source equivalence.

Local ShortRun mean times were 2871→3077 µs (distinct), 3116→3185 µs (multilingual), 2477→2529 µs (repeated), and 5.291→5.658 µs (small). These observations include increases and wide ShortRun uncertainty. They confer no controlled throughput or regression pass: dedicated-machine timing on both architectures remains required. The repeatable retained-memory benefit is the justification for this storage repair.

Eight focused storage regressions and all earlier unit tests passed: 10,749 total, zero failures/errors/skips/not-run. Initial build/report-option and runtime-provenance failures remain failed records; no credit was assigned until the test configuration was rebuilt to pin both parent and children to 10.0.11.

Affected conformance, differential and compiled API checks are recorded separately. Earlier 44,287-input whole-source conformance reuse no longer applies verbatim after these two runtime files changed. Original XSD disposition evidence remains historical until the owning robustness lane reassesses current applicability. Fuzzing remains explicitly deferred.

Affected suites passed: 920 conformance and 618 differential tests, zero failures/errors/skips/not-run. The approved M9 union and metadata extractor exactly match all 1,430 public members across 55 fragments and all 222 first-preview members; missing/changed controls refuse. The generic fragment-discovery rejection remains an unsuccessful attempt.
