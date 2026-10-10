# M0 tree representation evidence

Implementation measured: `24a0499f1d7c891a803bd8a370951f2a50e9a751`. Task-local prototypes only; no Xemele runtime API was added. Verified dependency: M0-build, integrated at `c7ecb084050b8c4cc014e7bc3f4b57f37f34413e`, present in base `5cb0e4eae1a0835a7455580bac25ee05ade7c463`.

## Interpretation for contract review

Both representations remain candidates. The arena approximately halves node-storage live memory in this model, but the straightforward List-of-structs traversal and mutation are slower. Large construction favors the arena in this run; small construction does not establish a winner. Choose with actual parser/query/serialization workloads and lifetime requirements, rather than treating these microbenchmarks as a final representation decision. No release hardware or repeatability claim is made.

Use borrowed ordinal name probes for existing names where possible: they remove per-hit temporary allocations in every tested distribution. Unique-name persistence still requires owned strings and table storage; the borrowed-intern path is slightly slower than the temporary baseline for all-unique names here. A tiny retained text view can keep a large input alive, so durable DOM text should own materialized storage at its persistence boundary unless an explicit document-owned backing strategy measures and accepts that retention.

## Reproduction and environment

See `../../../experiments/Tree/README.md` for exact commands, fixture semantics, cold/warm definitions and boundaries. Actual captured environment is in `environment.json` and `dotnet-info.txt`: Apple M4 Pro, Arm64, 14 logical CPUs, 48 GiB RAM, macOS 15.8.1, SDK 10.0.400, runtime .NET 10.0.11, BDN 0.15.8, concurrent workstation GC. BDN reports CPU unknown under the sandbox; a read-only `sysctl` probe supplied its identity. BDN could not set elevated process priority. The atomic shared-host timing lock was held for the full run and released immediately after the last case.

BDN ran all 36 cases, one launch each, two warmups, five measurement iterations with a 100 ms target. Normal runtime tiering/PGO applies to timing; the separate literal inspection disables tiering. Synthetic fixture generation occurs outside timing. Each name operation handles 1,024 tokens. A mutation operation means two moves. Raw full JSON includes individual measurements, statistics, allocations, GC counts and environment; CSV/Markdown/HTML summaries and complete console logs are retained in `raw/` and `benchmark-console.txt`. Default BDN outlier handling applies, and short iteration warnings and outliers remain visible. These are single-run informational timings, with especially broad confidence intervals for some construction cases.

## Representation measurements

Means below are microseconds per operation except mutation (nanoseconds). All traversal/mutation cases report 0 B per operation.

| Workload | Nodes | Names | Objects | Arena | Notes |
|---|---:|---|---:|---:|---|
| Construction | 1,024 | repeated | 16.73 μs | 20.57 μs | arena slower |
| Construction | 1,024 | unique | 46.10 μs | 45.13 μs | arena CI ±56.26 μs: inconclusive |
| Construction | 16,384 | repeated | 1,181.49 μs | 538.58 μs | object CI ±504.84 μs |
| Construction | 16,384 | unique | 2,305.70 μs | 1,521.45 μs | arena CI ±594.86 μs |
| Traversal | 1,024 | repeated | 1.234 μs | 3.242 μs | arena ~2.63× time |
| Traversal | 16,384 | repeated | 24.889 μs | 52.058 μs | arena ~2.09× time |
| Move roundtrip | 1,024 | shared literal | 10.63 ns | 33.23 ns | shallow parent/root move |
| Move roundtrip | 16,384 | shared literal | 10.46 ns | 33.20 ns | ~3.17× time |

Construction measures full graph creation, document-local intern table setup, unseen name ownership and storage growth. Both use identical names and shared literal text. Arena List growth allocates successively larger arrays; object construction allocates individual nodes plus a temporary breadth-order index. Neither includes parsing, validation, decoding, attributes, namespaces or text copying; these are parse-like storage construction costs only.

`memory.json` reports synchronous first-factory-call/next-call allocations, separate from BDN steady-state diagnostics. “Cold” here means first factory call in that process, not isolated process startup, CLR/JIT cost, or a guaranteed uncached OS state. Five full-GC samples retain 16 independent documents each; median live managed bytes uses `GC.GetGCMemoryInfo().HeapSizeBytes - FragmentedBytes`, subtracting the holding array. Released construction indices and intern tables are excluded, pre-generated token backing is excluded, persistent graph names are included. No pool exists. The initial uncommitted harness using `GC.GetTotalMemory(true)` yielded implausible object estimates; it was corrected before the measured implementation commit. Samples are approximate live heap deltas, not RSS.

| 16,384-node graph | First-call bytes | Next-call bytes | Retained median bytes/tree |
|---|---:|---:|---:|
| Objects, repeated names | 1,443,152 | 1,443,152 | 1,311,082 |
| Arena, repeated names | 1,312,240 | 1,312,240 | 655,778 |
| Objects, unique names | 3,039,208 | 3,039,208 | 1,966,122 |
| Arena, unique names | 2,908,296 | 2,908,296 | 1,310,818 |

Steady-state BDN construction allocation is close to these counters but includes workload/GC harness effects; use its raw values when comparing its timing. Repeated-name node storage is roughly 80 bytes/object versus a 40-byte arena slot, consistent with the live heap difference. This layout excludes future attributes and source metadata.

## Name ownership and text materialization

ASCII/Japanese input alternates deterministically, with eight repeated names or 1,024 unique names. Ordinal semantics are identical. Borrowed lookup uses the runtime's alternate dictionary lookup; no custom hash/matcher is added. New tables in intern benchmarks grow naturally and own one string per unique name. Temporary baseline strings on hits are discarded. All returned checksums are consumed by BDN and compared by the self-check.

| Names | Length | Temporary lookup μs / B | Borrowed lookup μs / B | Temporary intern μs / B | Borrowed intern μs / B |
|---|---:|---:|---:|---:|---:|
| repeated | 8 | 8.850 / 40,960 | 4.365 / 0 | 8.841 / 41,872 | 6.031 / 1,312 |
| repeated | 48 | 20.382 / 122,880 | 10.831 / 0 | 20.803 / 123,792 | 14.741 / 1,952 |
| unique | 8 | 11.562 / 40,960 | 5.565 / 0 | 22.975 / 143,176 | 25.377 / 143,176 |
| unique | 48 | 23.789 / 122,880 | 15.673 / 0 | 38.172 / 225,096 | 40.763 / 225,096 |

Memory mode separately records 134,920 B for creating the repeated short-name fixture plus populated lookup table, and 0 B for its next 1,024 borrowed lookups. Setup allocation includes fixture builder, source and range array; it is not a pure dictionary-initialization estimate. Miss/unique-name ownership is represented by new-table intern workloads rather than falsely claiming a zero-allocation cold DOM.

Owned 32-character text takes 5.363 ns and 88 B; owned 8,192-character text takes 527.02 ns and 16,408 B. Borrowed `ReadOnlyMemory<char>` construction reports 0 B and timing indistinguishable from BDN's empty-method overhead (displayed as 0 ns; this is not a physical zero-latency claim). The borrow benchmarks exclude setup backing storage, so the separate retention test is essential: a 32-character view keeps a 1,048,576-character source alive (~2,097,520 retained bytes including harness overhead), whereas an owned copy lets the source die (~112 retained bytes including harness overhead). Weak-reference assertions enforce this source-lifetime distinction after full GCs. The copied value is equivalent to the view.

## Identity, ownership and ergonomics

- Object identity is natural reference equality. A node's owner and parent links keep its connected tree reachable. Each node incurs an object header and reference links; stable references survive growth without copying node state.
- Arena handles carry document reference and slot index. `child == new Handle(arena, id)` remains true across List growth and moves, and another owner with the same index differs. Any live handle retains the owner's entire slot storage, including detached entries in a future implementation. Slots are never reused in this spike, so generation checking is unnecessary here; any future reuse requires a generation policy.
- Arena operations resolve an index and copy/write slot structs; the measured cost is relevant to this implementation, not a bound on optimized arena designs. Object relinking updates references directly. Struct versus class names alone do not predict speed.
- Both exercise `child.MoveTo(root)`/`child.Name`-style usage, explicit owner rejection and cycle rejection. The arena needs an owner/index pair, while object references are easier to inspect. Default arena handles do not implement a final exception policy. The contract owner must freeze invalid/default behavior, equality, generation/lifetime policy, enumeration versions, import/clone and cross-document ownership.
- Interned names and text are owned at persistence boundaries; borrowed spans do not survive construction/probing. The tree does not retain the input source. Document tables must be bounded and use collision-safe dictionary behavior in actual parsing. No source-slice optimization is selected for durable DOM text by this experiment.

## Correctness, lowering and remaining composition checks

The self-check covers eight construction fixtures; equivalent checksums; growth-stable handle identity; foreign-owner, root-move and cycle rejection; sibling relinking; 50,000-depth iterative traversal; ordinal ASCII/Japanese/Turkish/empty/isolated-surrogate keys, unknown and prefix misses; equivalent temporary/borrowed lookup and interning; text equivalence; and a 10,000-hit exact zero-allocation borrowed lookup regression. Isolated surrogates are key-data tests, not accepted XML grammar. These prototypes deliberately do not implement scanners, chunk/pool lifetime handling, namespace/name validation or fixed vocabulary hashes; corresponding parser requirements cannot be claimed here.

`literal-jit.txt` records the actual .NET 10 Arm64 generated code for `Probes.LiteralMatches`: length guard followed by 32-bit loads and equality, with no array construction/allocation helper. This confirms the four-byte UTF-8 literal lowering in this probe only, and does not establish a scanning or hashing optimization. No SearchValues/token-hash adoption is proposed.

Before release, the composition owners must rerun representative parse-to-DOM, traversal/query, mutation and serialization with actual attributes/namespaces/text, short and adversarial documents, cold/warm initialization, document table limits and chunk/source lifetime rules. Repeat on x64 and Arm64 with more launches and a stable performance host. M0-contracts decides representation using this evidence and the input spike; later tree/parser/query/output owners validate end-to-end effects. These are follow-ups for composition that does not exist yet, not missing M0 acceptance criteria.

## Executed checks

- Focused experiment Release build: passed, zero warnings/errors (`build.txt`).
- Focused self-check: passed (`self-check.txt`).
- Memory/lifetime/zero-allocation mode: passed (`memory.json`).
- Literal JIT inspection plus self-check: passed (`literal-jit.txt`); command uses `Probes:LiteralMatches` (global helper class), correcting the original README namespace typo without changing source.
- BDN: 36/36 completed, process exit 0; raw JSON contains statistics and memory diagnostics for every case.
- Default `dotnet build Xemele.slnx -c Release`: stalled without output for over 90 seconds; only this worker's identified build process was terminated. No global build server or peer process was stopped. This attempt is not a passing check.
- `dotnet build Xemele.slnx -c Release --disable-build-servers -m:1 -p:UseSharedCompilation=false`: passed in 8.03 seconds, zero warnings/errors (`solution-build.txt`).
- `python3 tools/planning/tasks.py validate`: passed, 93 tasks (`task-validation.txt`), rerun after evidence update.

All implementation measurements/checks above used the committed source at the revision at the top. The evidence commit adds raw outputs, this report, task metadata and the command spelling correction only. Integration verification belongs to the coordinator; `integration_revision` remains unset.
