# Provisional M0 tree representation experiment

This executable implements two independent private prototypes; it does not add runtime APIs to Xemele. Requires the repository-pinned .NET 10 SDK and centrally pinned BenchmarkDotNet 0.15.8. No other implementation task is referenced.

```sh
dotnet build experiments/Tree/Tree.csproj -c Release
dotnet run --project experiments/Tree/Tree.csproj -c Release --no-build -- --self-check
dotnet run --project experiments/Tree/Tree.csproj -c Release --no-build -- --memory
dotnet run --project experiments/Tree/Tree.csproj -c Release --no-build -- --filter '*' --artifacts docs/reports/M0-tree-spike/raw
DOTNET_TieredCompilation=0 DOTNET_JitDisasm='Probes:LiteralMatches' dotnet run --project experiments/Tree/Tree.csproj -c Release --no-build -- --self-check
```

Acquire `/private/tmp/xemele-benchmark.lock` with atomic `mkdir` and record owner before timing; release only your own lock. The benchmark configuration uses one launch, two warmups, five 100 ms target measurement iterations. This is a bounded architecture spike, not a release qualification. BDN may choose more operations/pilot iterations to reach its target. Each Names invocation processes 1,024 tokens; mutation does two moves. BDN allocations are per invocation, not per token/move.

Fixtures are deterministic synthetic trees with 1,024/16,384 nodes and four children per parent (`(i-1)/4`). Each node has owned name and shared literal `payload` text. Names alternate ASCII and Japanese, with either eight repeated names or one distinct name per node. Fixture generation is outside timing. Longer name benchmarks pad to 48 characters; short names to eight. No XML parsing, namespace/attribute storage, normalization, or actual input decoding exists here.

Arena slots keep string references and five integer links in a growing List. Handles store owner plus slot index; no slot reuse/destruction is implemented. Object nodes keep owner, strings and five reference links. Both traversals walk links iteratively without work stacks; mutations relink a leaf from its parent to root and back. Construction measures name-table setup, persistent unseen-name copies and growing storage; object construction also measures a temporary node-reference index, released afterwards. It is intentionally not optimized away: it supports breadth-order input construction just as arena indices do.

Memory mode reports first-factory-call and next-call allocation counters, then five retained-heap samples using 16 independent documents per sample. Full GCs surround the samples; `HeapSizeBytes - FragmentedBytes` is used rather than `GetTotalMemory(true)`, which produced implausible object-tree estimates in the initial harness. The holding array is subtracted; estimates include prototype nodes/storage/names, exclude pre-generated token backing and released intern/construction tables. Results are approximate live managed bytes, not OS working set. Cold counters mean first call in this process, not startup cost or fully isolated CLR/JIT initialization; warm means next call, and BDN supplies steady-state measurements.

Text memory mode retains a 32-character slice from a newly allocated 1,048,576-character source or makes a durable copy. A weak-reference assertion verifies whether the source survives full GC. Harness boxing and weak references can contribute a small overhead. `RetainedView` borrows caller-owned storage; it is valid only while that source is owned and live. `Owned` promises persistence independent of the input owner.

Borrowed dictionary probing uses ordinal `GetAlternateLookup<ReadOnlySpan<char>>`. Hits allocate no temporary strings; misses materialize one persistent name. Temporary-string baselines preserve identical name semantics. The source is never retained by the table. These APIs do not validate XML name grammar; even isolated UTF-16 surrogates are treated as ordinal key data in the oracle. The experiment has no custom hashing, delimiter scan, pooling, chunk scanner or parser; those gates belong to their implementation tasks. A small exact UTF-8 literal probe exposes compiler/JIT lowering for inspection without claiming a scanner optimization.

Ergonomic examples are exercised by the self-check: arena `child.MoveTo(root)` and `child.Name` versus object `objectChild.MoveTo(objectRoot)` and `objectChild.Name`. Arena value identity requires an owner and stable slot; object reference identity is natural. Neither prototype is a final public API. Default arena handles currently fail through missing owner; final exception policy, enumeration versions, import/clone, attributes and generation policy must be decided by contracts.
