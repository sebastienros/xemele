# XML performance lane — working checkpoint

The lane is **in progress**. Allocation targets pass in the current local screening run; latency targets do not. Geometric mean Xemele/BCL latency is 2.33×, allocation is 0.88×, maximum allocation ratio is 1.22×. Summed retained managed DOM heap is 0.902× BCL. No default features were removed. These ARM64 shared-host observations are informational, not controlled release or cross-platform evidence.

Apple M4 Pro, macOS 15.8.1, SDK 10.0.400, runtime 10.0.11; TieredCompilation/PGO/ReadyToRun disabled consistently for these managed screenings. Native references: libxml2 2.15.4, commit 96498992efa48d52b0e8b83058bd88dbdaf153c1; xmloxide 0.5.0, commit 555055f1ea6bf9944488fd96415b6f03b5b43696, Rust 1.95.0. GC is concurrent workstation; no claim that concurrent GC was disabled.

Prepared UTF-8 bytes, DOM materialization, namespace processing, whitespace, internal DTD/entity expansion, no external resource access. All ten frozen managed semantic digests and twenty native/Rust qualifications pass. Reference parse timings exclude explicit free/drop; lifecycle timings are recorded separately. Managed timing includes eventual GC amortization. Seven alternating 200 ms screening rounds are medians, not BenchmarkDotNet confidence estimates. The unmodified baseline and earlier optimization phases also have full ShortRun BenchmarkDotNet memory diagnostics.

| Fixture | Xemele ms | BCL ms | Time ratio | Allocation ratio | Retained ratio | libxml2 parse ms | xmloxide parse ms |
|---|---:|---:|---:|---:|---:|---:|---:|
| feed-small | 0.065 | 0.016 | 3.99 | 1.02 | 1.17 | 0.019 | 0.019 |
| svg-small | 0.227 | 0.104 | 2.18 | 0.72 | 0.71 | 0.100 | 0.099 |
| deep-medium | 0.707 | 0.263 | 2.69 | 1.02 | 1.05 | 0.222 | 0.245 |
| entity-medium | 7.749 | 1.773 | 4.37 | 0.71 | 1.03 | 1.082 | 5.434 |
| xhtml-medium | 3.794 | 1.403 | 2.70 | 0.97 | 0.98 | 1.481 | 1.586 |
| build-medium | 4.230 | 2.485 | 1.70 | 0.66 | 0.66 | 2.329 | 2.074 |
| soap-large | 17.830 | 5.575 | 3.20 | 1.22 | 1.23 | 5.086 | 6.935 |
| configuration-large | 21.925 | 14.311 | 1.53 | 0.70 | 0.70 | 10.280 | 9.416 |
| business-large | 79.466 | 44.229 | 1.80 | 0.91 | 0.91 | 26.385 | 27.420 |
| text-large | 19.578 | 17.229 | 1.14 | 1.00 | 1.01 | 20.485 | 19.793 |

The current library preserves strict Unicode validation, scalar columns, byte offsets, durable per-node/member locations, quotas, DTD defaults/normalization, unknown-reference provenance and recovery. General callback/pause contracts remain on their original event path. Private DOM batching does not expose borrowed source views or retain the parser in the document.

Changes so far: bounded UTF-8/ASCII conversion; bulk ordinary text and predefined references; complete ASCII tags with guarded fallback; fused QName validation; owned-name DOM handoff and bounded canonical caches; conditional DTD interpretation scratch for plain primary literals; synchronous private DOM tag batching; pooled operation scratch; 256-slot segmented owned arenas; lossless 12-byte common source coordinates with sparse full-width/URI exceptions; cached quota ceilings.

The latest cumulative checks are 10,778 tests passed, including zero-byte warmed borrowed text and repeated-tag dispatch regressions. Task validation passes. Broader conformance, differential, API, large-stream requalification and final BenchmarkDotNet checks are still required before integration. Failed attempts remain in ignored artifacts; no stale-binary result is credited.

## Source findings and the referenced parser study

[BCL XmlLoader](https://github.com/dotnet/runtime/blob/v10.0.11/src/libraries/System.Private.Xml/src/System/Xml/Dom/XmlLoader.cs) passes owned names into the DOM; [DomNameTable](https://github.com/dotnet/runtime/blob/v10.0.11/src/libraries/System.Private.Xml/src/System/Xml/Dom/DomNameTable.cs) probes shared names before interning on misses. We adopted the owned boundary. We retained a full ordinal randomized tuple dictionary rather than its local-name-only bucket hash, which collides predictably across many namespace/prefix triples sharing one local name.

[libxml2 parser.c](https://github.com/GNOME/libxml2/blob/96498992efa48d52b0e8b83058bd88dbdaf153c1/parser.c) scans ordinary character data in runs and updates positions in batches. [xmloxide input.rs](https://github.com/jonwiggins/xmloxide/blob/555055f1ea6bf9944488fd96415b6f03b5b43696/src/parser/input.rs) and its XML parser use contiguous runs and isolate normalization. These ideas motivated the bulk scans and private DOM handoff. Native/Rust character coordinate semantics and durable location storage differ from Xemele; fixture equivalence is not a claim of equivalent failure/provenance contracts for arbitrary XML.

The user-referenced chat **Benchmark fast C# parser patterns** has local measured findings at `/Users/sebastienros/github/parsers/docs/FINDINGS.md` and conditional rules at `docs/RULES.md`. E01 shows cached searches losing on 3-character delimiter runs but winning on long runs; identifier scans had different thresholds. This motivated direct dispatch at dense XML boundaries and our XML-specific scan comparisons. E02 favored direct span matching over AsciiHash for a small fixed vocabulary: predefined references stay direct comparisons. E03 favored a small switch over a transition table: the fallback state machine stays a switch. E06 showed that node pooling can remove allocations without improving time, and that arena overcapacity matters: only operation scratch is pooled and owned DOM pages have bounded slack. E07 rejected a universal UTF-8 advantage: byte-to-character decoding stays an explicit measured stage.

Retained managed memory uses held/released `HeapSizeBytes - FragmentedBytes` after full compacting collections and an independently traversed rooted object graph. 32 KiB SOH and 9 MiB LOH controls pass. `GC.GetTotalMemory` overcounts some SOH cases on this runtime and is preserved only as forensic data, never used for the ratio. Native sidecars count requested allocator payload bytes with matching positive/release controls; allocator metadata/RSS is excluded. They are separate binaries and do not perturb timing workers. Rust/native byte strings and features differ from managed UTF-16 DOMs; their memory figures are descriptive, not the managed parity gate.

Raw logs, binaries, per-round data, layout graphs and failed attempts live under ignored `artifacts/M9-xml-performance/`. Reproduction tools are adjacent to this report. The stricter latency acceptance remains pending; no fixture is excluded to claim parity.
