# Legacy parser measurements

Measured checkout: `9661cf26b7be08a17a12d2de94a1745e112d80ad` (runtime source `918f52d3ac68e3c4632f776d0b0572f55586ffd8`). Exclusive grant `0813121`, release `4816130`, lease `m7-legacy-9661cf2`. The sole exact wrapper command ran once; initial/allocation/benchmark/final exits were 0 and owned cleanup reported `no_child=true`. No builds, retries, tests, edits or raw inspection occurred inside the lease. Results were inspected after durable release.

Prepared/final manifest SHA256 `d83aa22d6130bfba338704a68b4c20e64db6e5de0baeb2d78b69c4c5d41dcc1a`: 3651 source/corpus/generated entries, 43 binaries, 3852 SDK/runtime/host entries. Identity includes complete effective child environment hash and per-key hashes, SHLVL=1 and session identity. Exact positive/final-positive controls accepted; unknown environment negative rejected. Root independently compared the readable closure, with only the opaque executor thread identifier differing.

Apple M4 Pro arm64, macOS 15.8.1, SDK 10.0.400, actual runtime 10.0.11; TieredCompilation/TieredPGO/ReadyToRun=0 and roll forward disabled. BenchmarkDotNet 0.15.8 used ShortRun (3 warmups, 3 measured iterations, 1 launch), MemoryDiagnoser and InProcessNoEmit. All 30 reports succeeded in 254.18 seconds. Fixture generation and stream byte construction occur in setup. BenchmarkDotNet could not raise process priority (permission denied). Dense loop is 1.063 ± 1.8816 ns and short-miss cached search is 2.491 ± 2.5684 ns (reported mean ± error); these intervals exceed the means, so dense/short ratios are provisional and establish no repeatable speed advantage; timing is informational from one host/run, and x64/cross-host repeatability were not tested.

## Allocation domains

| Domain | Numerator bytes | Denominator | Interpretation |
|---|---:|---:|---|
| cold-parse-owned-setup-and-dom | 70256 | 1 operations | Initialization and first owned DOM |
| positive-control | 1048 | 1 operations | Owned persistence/setup; no zero-allocation claim |
| owned-dom-repeated | 3412864 | 100 operations | Owned persistence/setup; no zero-allocation claim |
| owned-dom-distinct | 4443512 | 100 operations | Owned persistence/setup; no zero-allocation claim |
| owned-dom-multilingual | 2877672 | 100 operations | Owned persistence/setup; no zero-allocation claim |
| owned-dom-malformed | 1316864 | 100 operations | Owned persistence/setup; no zero-allocation claim |
| owned-new-name-storage-and-dictionary-growth | 14912 | 100 operations | Owned persistence/setup; no zero-allocation claim |
| retained-whole-heap-inclusive-doc-array-runtime-roots | 6000944 | 100 retained documents | Whole heap including runtime/JIT/probe roots; not exclusive DOM size |

Each of three steady borrowed-name/loop-scan/charset-label cases passed 16 batches × 4096 operations with exactly 0 current-thread allocated bytes: 196608 combined operations. Warm-up and the preallocated operation-local name table are outside that domain; new names and DOM persistence are measured separately. The positive byte-array allocation control observed 1048 B. Cold first parse measured 70256 B and is not hidden in the warm budget. Retained heap before/after was 425480/6426424 B; the 6000944 B delta is deliberately inclusive and cannot be divided into a precise per-node storage claim.

## LegacyScanBenchmarks

| Method | Case | Mean | Error | Allocated |
|---|---|---|---|---|
| Loop | dense | 1.063 ns | 1.8816 ns | 0 B |
| CachedSearch | dense | 2.061 ns | 0.1166 ns | 0 B |
| Loop | long-hit | 4,148.345 ns | 2,127.3253 ns | 0 B |
| CachedSearch | long-hit | 232.878 ns | 10.2097 ns | 0 B |
| Loop | long-miss | 3,997.511 ns | 637.0837 ns | 0 B |
| CachedSearch | long-miss | 231.141 ns | 29.3851 ns | 0 B |
| Loop | multilingual | 2,028.373 ns | 420.7916 ns | 0 B |
| CachedSearch | multilingual | 119.023 ns | 1.7717 ns | 0 B |
| Loop | short-hit | 2.334 ns | 0.1226 ns | 0 B |
| CachedSearch | short-hit | 2.636 ns | 0.3346 ns | 0 B |
| Loop | short-miss | 1.862 ns | 1.4709 ns | 0 B |
| CachedSearch | short-miss | 2.491 ns | 2.5684 ns | 0 B |

## LegacyNameBenchmarks

| Method | Input | Mean | Error | Allocated |
|---|---|---|---|---|
| AllocatedLookup | data-name | 11.104 ns | 0.4734 ns | 40 B |
| BorrowedLookup | data-name | 9.605 ns | 4.0017 ns | 0 B |
| AllocatedLookup | p | 10.417 ns | 0.4638 ns | 24 B |
| BorrowedLookup | p | 8.891 ns | 3.1037 ns | 0 B |
| AllocatedLookup | unkno(...)-name [27] | 14.187 ns | 1.1453 ns | 80 B |
| BorrowedLookup | unkno(...)-name [27] | 11.880 ns | 0.8101 ns | 0 B |

## LegacyCompositionBenchmarks

| Method | Case | Mean | Error | Allocated |
|---|---|---|---|---|
| Characters | distinct | 27.070 μs | 2.1680 μs | 43.39 KB |
| Bytes | distinct | 29.800 μs | 1.3000 μs | 43.39 KB |
| ChunkStream | distinct | 31.028 μs | 2.5502 μs | 47.45 KB |
| Characters | malformed | 3.382 μs | 0.3753 μs | 12.86 KB |
| Bytes | malformed | 3.473 μs | 0.6920 μs | 12.86 KB |
| ChunkStream | malformed | 3.684 μs | 0.2515 μs | 16.92 KB |
| Characters | multilingual | 19.252 μs | 1.3983 μs | 28.1 KB |
| Bytes | multilingual | 21.628 μs | 1.6095 μs | 28.1 KB |
| ChunkStream | multilingual | 22.525 μs | 0.3169 μs | 32.16 KB |
| Characters | repeated | 26.229 μs | 1.0408 μs | 33.33 KB |
| Bytes | repeated | 29.246 μs | 2.1775 μs | 33.33 KB |
| ChunkStream | repeated | 30.354 μs | 3.4868 μs | 37.38 KB |

## Decision and ownership

Keep borrowed alternate dictionary name probes: BDN reports 0 B versus 24/40/80 B for allocating lookup strings, with local means 8.891/9.605/11.880 ns versus 10.417/11.104/14.187 ns. Unique name storage/dictionary growth remains explicitly owned by the operation and measured (14912 B for 100 names).

Retain the scalar delimiter path for this native buffering/recovery implementation. The cached SearchValues candidate helps long contiguous spans (~17× in these microbenchmarks) with provisional dense/short ratios of 1.13–1.95× that do not establish repeatable superiority; native scalar validation, quota/location accounting and 64/32-byte growth boundaries remain required. This is a measured reason to retain the simpler current stateful path, not a claim that cached SearchValues is generally inferior. No scanner change is inferred from a microbenchmark without a matching parser composition comparison.

Owned DOM creation is not allocation-free: composition memory diagnostics report 12.86–43.39 KB for character/byte inputs, and the chunk-stream path adds ~4.06 KB transport/stream setup. Returned DOM nodes, names, values, attributes and child lists are persistence allocations; input transport/lookahead/scratch/name tables and duplicate-attribute bookkeeping have operation lifetime and never survive in returned documents. Byte spans are borrowed; no whole-input UTF8/string conversion is used to share entry points.

Raw logs and BDN outputs remain ignored in artifacts/M7-legacy; only summary tables and analysis are committed. SHA256 of the existing raw allocation log: `7d212cfdb856ff38ed7d1790d1a04ebb106ae567e5821abfcc59e842c0e9bd95`; benchmark log: `7e2a559a391ed15cf4eb8a6204b75fccaca0ec782a342d2c400e12da60aeee0c`.
