# XML performance lane — working checkpoint

The lane is **in progress**. Allocation targets pass in the current local screening run; latency targets do not. The final 20-case BenchmarkDotNet ShortRun at source revision `6e534cdc183f864b26c905740a44fba88ef34a23` gives geometric mean Xemele/BCL latency 2.22×, allocation 0.88×, maximum allocation ratio 1.22×. Summed retained managed DOM heap is 0.902× BCL. No default features were removed. These ARM64 shared-host observations are informational, not controlled release or cross-platform evidence.

Apple M4 Pro, macOS 15.8.1, SDK 10.0.400, runtime 10.0.11; TieredCompilation/PGO/ReadyToRun disabled consistently for these managed screenings. Native references: libxml2 2.15.4, commit 96498992efa48d52b0e8b83058bd88dbdaf153c1; xmloxide 0.5.0, commit 555055f1ea6bf9944488fd96415b6f03b5b43696, Rust 1.95.0. GC is concurrent workstation; no claim that concurrent GC was disabled.

Prepared UTF-8 bytes, DOM materialization, namespace processing, whitespace, internal DTD/entity expansion, no external resource access. All ten frozen managed semantic digests and twenty native/Rust qualifications pass. Reference parse timings exclude explicit free/drop; lifecycle timings are recorded separately. Managed timing includes eventual GC amortization. The managed table uses the final ShortRun means (one launch, three warmups and three 250 ms iterations). Native columns are earlier seven-round parse medians; the binaries are unchanged, but the measurement windows differ. Uncertainty and raw samples are retained; these timings do not establish small native/managed wins. Earlier seven-round screenings are medians, not BenchmarkDotNet confidence estimates.

| Fixture | Xemele ms | BCL ms | Time ratio | Allocation ratio | Retained ratio | libxml2 parse ms | xmloxide parse ms |
|---|---:|---:|---:|---:|---:|---:|---:|
| feed-small | 0.061 | 0.017 | 3.68 | 1.02 | 1.17 | 0.019 | 0.019 |
| svg-small | 0.228 | 0.104 | 2.19 | 0.72 | 0.71 | 0.100 | 0.099 |
| deep-medium | 0.664 | 0.249 | 2.66 | 1.02 | 1.05 | 0.222 | 0.245 |
| entity-medium | 6.496 | 1.779 | 3.65 | 0.71 | 1.03 | 1.082 | 5.434 |
| xhtml-medium | 3.584 | 1.345 | 2.66 | 0.97 | 0.98 | 1.481 | 1.586 |
| build-medium | 3.931 | 2.399 | 1.64 | 0.66 | 0.66 | 2.329 | 2.074 |
| soap-large | 17.195 | 5.541 | 3.10 | 1.22 | 1.23 | 5.086 | 6.935 |
| configuration-large | 21.038 | 14.471 | 1.45 | 0.70 | 0.70 | 10.280 | 9.416 |
| business-large | 73.775 | 44.731 | 1.65 | 0.91 | 0.91 | 26.385 | 27.420 |
| text-large | 19.326 | 16.875 | 1.15 | 1.00 | 1.01 | 20.485 | 19.793 |

The current library preserves strict Unicode validation, scalar columns, byte offsets, durable per-node/member locations, quotas, DTD defaults/normalization, unknown-reference provenance and recovery. General callback/pause contracts remain on their original event path. Private DOM batching does not expose borrowed source views or retain the parser in the document.

Changes so far: bounded UTF-8/ASCII conversion; bulk ordinary text and predefined references; complete ASCII tags including primary DTD tags with guarded fallback; cached ASCII identifier scan plus fused QName validation; owned-name DOM handoff and bounded canonical caches; conditional DTD interpretation scratch for plain primary literals; synchronous private DOM tag batching; pooled operation scratch; 256-slot segmented owned arenas; lossless 12-byte common source coordinates with sparse full-width/URI exceptions; cached quota ceilings.

The latest cumulative checks are 10,778 unit tests passed, including zero-byte warmed borrowed text and repeated-tag dispatch regressions. The final broader checks pass: 920 conformance and 618 differential cases; compiled API comparison is identical. Exact commands and source revisions are in the task JSON. All five 1 GiB streaming modes pass the corrected compacted live-heap oracle (whole-heap peaks 133,296–149,552 bytes), and a retained 9 MiB negative control fails that oracle as intended. These streaming binaries precede the last DOM-only changes; decoder, quota and streaming paths are unchanged. Failed attempts remain in ignored artifacts; no stale-binary result is credited. The lane remains isolated because the latency criterion fails; it is not marked implemented or verified.

## Source findings and the referenced parser study

[BCL XmlLoader](https://github.com/dotnet/runtime/blob/v10.0.11/src/libraries/System.Private.Xml/src/System/Xml/Dom/XmlLoader.cs) passes owned names into the DOM; [DomNameTable](https://github.com/dotnet/runtime/blob/v10.0.11/src/libraries/System.Private.Xml/src/System/Xml/Dom/DomNameTable.cs) probes shared names before interning on misses. We adopted the owned boundary. We retained a full ordinal randomized tuple dictionary rather than its local-name-only bucket hash, which collides predictably across many namespace/prefix triples sharing one local name.

[libxml2 parser.c](https://github.com/GNOME/libxml2/blob/96498992efa48d52b0e8b83058bd88dbdaf153c1/parser.c) scans ordinary character data in runs and updates positions in batches. [xmloxide input.rs](https://github.com/jonwiggins/xmloxide/blob/555055f1ea6bf9944488fd96415b6f03b5b43696/src/parser/input.rs) and its XML parser use contiguous runs and isolate normalization. These ideas motivated the bulk scans and private DOM handoff. Native/Rust character coordinate semantics and durable location storage differ from Xemele; fixture equivalence is not a claim of equivalent failure/provenance contracts for arbitrary XML.

The user-referenced chat **Benchmark fast C# parser patterns** has local measured findings at `/Users/sebastienros/github/parsers/docs/FINDINGS.md` and conditional rules at `docs/RULES.md`. E01 shows cached searches losing on 3-character delimiter runs but winning on long runs; identifier scans had different thresholds. This motivated direct dispatch at dense XML boundaries and our XML-specific scan comparisons. E02 favored direct span matching over AsciiHash for a small fixed vocabulary: predefined references stay direct comparisons. E03 favored a small switch over a transition table: the fallback state machine stays a switch. E06 showed that node pooling can remove allocations without improving time, and that arena overcapacity matters: only operation scratch is pooled and owned DOM pages have bounded slack. E07 rejected a universal UTF-8 advantage: byte-to-character decoding stays an explicit measured stage.

Retained managed memory uses held/released `HeapSizeBytes - FragmentedBytes` after full compacting collections and an independently traversed rooted object graph. 32 KiB SOH and 9 MiB LOH controls pass. `GC.GetTotalMemory` overcounts some SOH cases on this runtime and is preserved only as forensic data, never used for the ratio. In raw worker JSON, use `graphEstimate` (`heldHeap - releasedHeap`); the legacy `retainedBytes` field is forensic `GetTotalMemory` output. Native sidecars count requested allocator payload bytes with matching positive/release controls; allocator metadata/RSS is excluded. They are separate binaries and do not perturb timing workers. Rust/native byte strings and features differ from managed UTF-16 DOMs; their memory figures are descriptive, not the managed parity gate.

Raw logs, binaries, per-round data, layout graphs and failed attempts live under ignored `artifacts/M9-xml-performance/`. Reproduction tools are adjacent to this report. The stricter latency acceptance remains pending; no fixture is excluded to claim parity.

## Remaining causes and constraints

The original main ratios ranged from 9.27× to 34.21× BCL latency; current ratios range from 1.15× to 3.68×. This is a large improvement, but structural and entity-heavy documents still fail parity. Ordinary large-text scanning is close to BCL; replacing more scans alone is unlikely to close the structural gap.

Measured costs and code evidence must be distinguished. [The feature experiment](feature-costs.md) measures durable coordinates at about 5–12% of structural latency, insufficient to explain the full gap. Whole primary ASCII tag batching reduced the entity fixture from 7.54 ms in the prior full ShortRun to 6.50 ms in the final one; unchanged allocation and broader semantic tests support keeping it, though these are separate short measurement windows. Removing duplicate canonical-name probes reduced work at the DOM boundary without changing retained storage.

Source inspection points to remaining per-tag costs: namespace and attribute resolution, repeated operation checks, quota/provenance bookkeeping, and DTD interpretation that must preserve origin information. These are optimization candidates, not profiler-derived percentages. CPU traces obtained in this lane did not resolve reliable JIT attribution and are not used to claim an exact cost breakdown. Native/Rust source coordinates, failure diagnostics, DTD provenance and limits are not equivalent to all Xemele contracts. Dropping those features by default would change the product; source-coordinate opt-out and a more compact conditional member arena require explicit contract/storage follow-ups.

The public surface extractor finds 1,430 members identical to the baseline (SHA-256 `afd84a4e070ffffa0b2e31db5c089cd57ed87dfb6900dfc9556ac00514d566d9`). The standard API-baseline script is blocked by the pre-existing empty `docs/api/M8-datatypes.txt`; this is an owning-task request, not a passing pipeline check.
