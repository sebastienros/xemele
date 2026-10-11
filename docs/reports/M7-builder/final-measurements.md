# Released final measurement: 9abeeb9

Implementation revision `9abeeb9c8e21c47ace9f949b76b14de372271f39`, branch `codex/m7-builder-continuation`. Root granted `m7-builder-9abeeb9` after source/probe review, fresh PARK and empty workload inventory; durable RELEASE preceded all raw inspection. Sole exact wrapper `python3 -B docs/reports/M7-builder/run_measurements.py --exclusive-grant` exited 0. Initial identity, allocation, affected owned BDN, and final identity each exited 0; `no_child=true`. No builds, source edits or additional workloads occurred during the lease.

Manifest SHA256 `a9ae60f09d887d26645cde614ff5fb2c4771bcc48146bb96ea86aabbf5713976`: 3905 source, 145 binary, 3852 runtime files, actual host and complete effective environment unchanged. Identity-only controls positive 0 / unknown-environment negative 1 / final positive 0; no runs and no children in every control. Plan: 1150 s total, initial/final 45 s, allocation 150 s, affected BDN 750 s, 130 s closure reserve.

Actual .NET SDK 10.0.400 / runtime 10.0.11, Apple M4 Pro Arm64, macOS Sequoia 15.8.1. TieredCompilation=0, TieredPGO=0, ReadyToRun=0, roll-forward disabled. BenchmarkDotNet 0.15.8, MemoryDiagnoser, ShortRun, NoEmit, 3 warmups / 3 actual iterations / 1 launch. All 24 affected cases passed in 142.84 s. Timings are informational on one Arm64 host; priority elevation was denied and confidence intervals can be wide. No x64 or release performance claim.

The original 72-case run at `5d4de9951572e1b79d5480b7d1af8c820d19d6e6` exposed quadratic resumed foster-text copying. Its failure and original allocation/timing interpretation remain in `measurements.md`; it is not used to claim corrected tree-buffer growth. Forty-eight unchanged primitive timing rows are retained below with exact source-hash proof.

## Corrected text ownership and growth

Pending text is operation-owned. Resuming an existing text node creates one lazy append buffer seeded from its owned value, then appends runs without copying the whole growing string at each table-cell/body location switch. Exact subtree observation, move, copy and removal boundaries materialize affected values; EOF materializes all remaining buffers. Correctness witnesses cover repeated foster/adoption, adoption subtree moves, selectedcontent copies and processing-instruction patch removals.

| Domain | N | Total B | Parses | B/parse | Ratio to previous N |
|---|---:|---:|---:|---:|---:|
| foster | 256 | 4026944 | 8 | 503368.000 | — |
| foster | 512 | 7972928 | 8 | 996616.000 | 1.9799 |
| foster | 1024 | 15863360 | 8 | 1982920.000 | 1.9897 |
| foster | 2048 | 31643456 | 8 | 3955432.000 | 1.9948 |
| adoption | 256 | 4861440 | 8 | 607680.000 | — |
| adoption | 512 | 9662912 | 8 | 1207864.000 | 1.9877 |
| adoption | 1024 | 19265160 | 8 | 2408145.000 | 1.9937 |
| adoption | 2048 | 38468936 | 8 | 4808617.000 | 1.9968 |

Foster ratios 1.980/1.990/1.995 and adoption ratios 1.988/1.994/1.997 demonstrate approximately linear allocated-byte growth over these fixed N/2N fixtures. Foster N=2048 allocation fell from 66,136,896 B to 31,643,456 B for eight parses (52.15% reduction). These are allocation-scaling observations, not timed growth or a proof for arbitrary graphs.

## Allocation boundaries

All 96 batches of 1024 borrowed prescan + byte/char label + cached scan operations allocated 0 B (98,304 consumed operations). Positive control allocated 1048 B. Cold first owned parse including static initialization allocated 28,896 B; cached SearchValues/class initialization 48 B; prepared linear-oracle JSON/owned fixtures 152,120 B. Cold values are observations, not a claimed initialization speedup. Fixture generation, strings/bytes/context and measurement closures are outside counters. No async claim.

The added nullable resumed-buffer field increases parser object size: affected document/byte/stream/fragment allocation counters each rise by 1024 B across 128 parses, or 8 B/parse, on these fixtures. The dictionary and append buffers are allocated only for resumed nodes and have parser-operation lifetime. Owned names/text, decoded buffers, tree stacks, stream transport and output nodes remain inclusive explicit ownership costs.

| Fixture | Character total B / 128 | Byte total B / 128 | Caller stream total B / 128 | Fragment total B / 128 | Deep clone total B / 128 |
|---|---:|---:|---:|---:|---:|
| short-hit | 998400 | 6368000 | 7427840 | 990208 | 216064 |
| short-miss | 1004544 | 6374144 | 7433984 | 996352 | 216064 |
| long-late-hit | 5389312 | 10827264 | 11887104 | 5381120 | 293888 |
| long-miss | 5212160 | 10582528 | 11641864 | 5172224 | 171008 |
| dense | 3540736 | 8869632 | 9929472 | 3524608 | 1281024 |
| multilingual | 1058560 | 6487808 | 7547648 | 1049600 | 234496 |

Inclusive retained heap for 128 documents: before 949,760 B, after 3,927,272 B, delta 2,977,512 B, including array/model/tokenizer/runtime roots. This equals the released first-run retained observation; it is not isolated node storage.

Byte and stream inputs contain a UTF-8 meta declaration absent from character/fragment inputs. Comparisons between API lanes describe their ownership costs; they are not equivalent-input parser speedup claims. Caller stream setup is included in that ownership lane; the caller retains stream ownership.

## Affected owned parser timing summary

| Method | Fixture | Mean | 99.9% CI half-width | StdDev | Managed allocation/op |
|---|---|---:|---:|---:|---:|
| CharacterDocument | dense | 15.232 μs | 0.4585 μs | 0.0251 μs | 27.01 KB |
| ByteDocument | dense | 18.804 μs | 1.8820 μs | 0.1032 μs | 67.67 KB |
| CallerStreamDocument | dense | 19.557 μs | 15.6580 μs | 0.8583 μs | 75.76 KB |
| ContextualFragment | dense | 15.251 μs | 1.0084 μs | 0.0553 μs | 26.89 KB |
| CharacterDocument | long-late-hit | 130.582 μs | 11.9871 μs | 0.6571 μs | 41.12 KB |
| ByteDocument | long-late-hit | 140.137 μs | 27.1556 μs | 1.4885 μs | 82.6 KB |
| CallerStreamDocument | long-late-hit | 146.364 μs | 7.3693 μs | 0.4039 μs | 90.69 KB |
| ContextualFragment | long-late-hit | 122.412 μs | 36.4380 μs | 1.9973 μs | 41.06 KB |
| CharacterDocument | long-miss | 105.201 μs | 8.9347 μs | 0.4897 μs | 39.77 KB |
| ByteDocument | long-miss | 147.605 μs | 34.4572 μs | 1.8887 μs | 80.73 KB |
| CallerStreamDocument | long-miss | 129.887 μs | 14.7730 μs | 0.8098 μs | 88.82 KB |
| ContextualFragment | long-miss | 105.170 μs | 14.3069 μs | 0.7842 μs | 39.46 KB |
| CharacterDocument | multilingual | 2.211 μs | 0.0915 μs | 0.0050 μs | 8.07 KB |
| ByteDocument | multilingual | 3.801 μs | 0.2763 μs | 0.0151 μs | 49.5 KB |
| CallerStreamDocument | multilingual | 4.062 μs | 0.0463 μs | 0.0025 μs | 57.58 KB |
| ContextualFragment | multilingual | 2.216 μs | 0.2888 μs | 0.0158 μs | 8.01 KB |
| CharacterDocument | short-hit | 1.255 μs | 0.1284 μs | 0.0070 μs | 7.62 KB |
| ByteDocument | short-hit | 2.854 μs | 1.4405 μs | 0.0790 μs | 48.58 KB |
| CallerStreamDocument | short-hit | 3.173 μs | 3.3991 μs | 0.1863 μs | 56.67 KB |
| ContextualFragment | short-hit | 1.136 μs | 0.0377 μs | 0.0021 μs | 7.56 KB |
| CharacterDocument | short-miss | 1.873 μs | 0.0430 μs | 0.0024 μs | 7.67 KB |
| ByteDocument | short-miss | 3.536 μs | 0.3801 μs | 0.0208 μs | 48.63 KB |
| CallerStreamDocument | short-miss | 3.790 μs | 0.6414 μs | 0.0352 μs | 56.72 KB |
| ContextualFragment | short-miss | 1.766 μs | 0.2129 μs | 0.0117 μs | 7.6 KB |

## Retained unchanged primitive evidence

The following six files have identical SHA256 in both frozen manifests and current source. Root independently verified this proof before granting the narrower run. Probe routing changed only to select the affected 24 owned rows; no primitive implementation, oracle, parameter fixture or timed method changed. Primitive rows retain their original checkpoint provenance and were not rerun.

| Source | SHA256 |
|---|---|
| `benchmarks/Xemele.Benchmarks/M7-builder/BuilderBenchmarks.cs` | `bf6de722fc610a965df76fe0b23171c023d3dbdc10200553db49191ab3f166eb` |
| `benchmarks/Xemele.Benchmarks/M7-builder/AllocatingPrescanBaseline.cs` | `96e7ef4f82613d8cdca1bb07a7dd214c6c2e90b52e31d1a99f948bde5cffff60` |
| `src/Xemele/Html5/TreeConstruction/HtmlAscii.cs` | `238e9bb8140a3e295f65ae93f624cf22ef367731f3ec8de547e6fc92c4a5c3ab` |
| `src/Xemele/Html5/TreeConstruction/HtmlByteInput.cs` | `732f1f1c9bc3bad6921123b607aa0881ec7c5b983839c1b578fa01e0ba9476a9` |
| `src/Xemele/Html5/TreeConstruction/HtmlByteDecoder.cs` | `98c09f2d20ab79d0c5ec458f3b59fdc6ddd2d748deaaaf6debb12f512682fff3` |
| `src/Xemele/Html5/TreeConstruction/HtmlEncodingTables.Generated.cs` | `03b23daa3cacca0c3e0d7a59a8ae018d8256ace4b90b91bb1f6dd3427f110026` |

| Method | Fixture | Mean | 99.9% CI half-width | StdDev | Managed allocation/op |
|---|---|---:|---:|---:|---:|
| LinearByteAndCharLabels | dense | 375.5977 ns | 80.3549 ns | 4.4045 ns | 0 B |
| GeneratedByteAndCharLabels | dense | 248.2040 ns | 45.3623 ns | 2.4865 ns | 0 B |
| LinearByteAndCharLabels | long-late-hit | 415.6973 ns | 34.2593 ns | 1.8779 ns | 0 B |
| GeneratedByteAndCharLabels | long-late-hit | 324.9998 ns | 74.2030 ns | 4.0673 ns | 0 B |
| LinearByteAndCharLabels | long-miss | 431.3568 ns | 220.1041 ns | 12.0647 ns | 0 B |
| GeneratedByteAndCharLabels | long-miss | 323.3937 ns | 11.4789 ns | 0.6292 ns | 0 B |
| LinearByteAndCharLabels | multilingual | 619.4109 ns | 45.1314 ns | 2.4738 ns | 0 B |
| GeneratedByteAndCharLabels | multilingual | 363.0236 ns | 29.3372 ns | 1.6081 ns | 0 B |
| LinearByteAndCharLabels | short-hit | 20.5578 ns | 3.7290 ns | 0.2044 ns | 0 B |
| GeneratedByteAndCharLabels | short-hit | 15.6837 ns | 1.2177 ns | 0.0667 ns | 0 B |
| LinearByteAndCharLabels | short-miss | 458.3111 ns | 37.7295 ns | 2.0681 ns | 0 B |
| GeneratedByteAndCharLabels | short-miss | 331.2640 ns | 65.8566 ns | 3.6098 ns | 0 B |
| AllocatingBaseline | dense | 2,387.4719 ns | 178.5960 ns | 9.7895 ns | 4240 B |
| BorrowedPrescan | dense | 1,462.5605 ns | 279.6589 ns | 15.3290 ns | 0 B |
| AllocatingBaseline | long-late-hit | 888.1389 ns | 3,579.5044 ns | 196.2047 ns | 248 B |
| BorrowedPrescan | long-late-hit | 1,056.1615 ns | 1,959.3450 ns | 107.3983 ns | 0 B |
| AllocatingBaseline | long-miss | 502.2759 ns | 211.3486 ns | 11.5847 ns | 0 B |
| BorrowedPrescan | long-miss | 565.7580 ns | 102.5363 ns | 5.6204 ns | 0 B |
| AllocatingBaseline | multilingual | 172.2331 ns | 25.3832 ns | 1.3913 ns | 408 B |
| BorrowedPrescan | multilingual | 177.8566 ns | 13.0955 ns | 0.7178 ns | 0 B |
| AllocatingBaseline | short-hit | 78.7888 ns | 8.8529 ns | 0.4853 ns | 248 B |
| BorrowedPrescan | short-hit | 112.1162 ns | 23.4297 ns | 1.2843 ns | 0 B |
| AllocatingBaseline | short-miss | 252.7074 ns | 28.7865 ns | 1.5779 ns | 256 B |
| BorrowedPrescan | short-miss | 287.5898 ns | 48.5343 ns | 2.6603 ns | 0 B |
| Scalar | dense | 0.7186 ns | 0.1340 ns | 0.0073 ns | 0 B |
| IndexOf | dense | 0.9965 ns | 0.0294 ns | 0.0016 ns | 0 B |
| SmallSet | dense | 1.2356 ns | 0.0198 ns | 0.0011 ns | 0 B |
| CachedSearchValues | dense | 1.0325 ns | 0.1527 ns | 0.0084 ns | 0 B |
| Scalar | long-late-hit | 230.7125 ns | 143.8728 ns | 7.8862 ns | 0 B |
| IndexOf | long-late-hit | 16.1750 ns | 2.0804 ns | 0.1140 ns | 0 B |
| SmallSet | long-late-hit | 17.9000 ns | 1.3921 ns | 0.0763 ns | 0 B |
| CachedSearchValues | long-late-hit | 16.3683 ns | 1.2530 ns | 0.0687 ns | 0 B |
| Scalar | long-miss | 237.5849 ns | 31.8943 ns | 1.7482 ns | 0 B |
| IndexOf | long-miss | 16.9616 ns | 1.0057 ns | 0.0551 ns | 0 B |
| SmallSet | long-miss | 18.2593 ns | 0.8146 ns | 0.0447 ns | 0 B |
| CachedSearchValues | long-miss | 17.1107 ns | 1.3117 ns | 0.0719 ns | 0 B |
| Scalar | multilingual | 2.5564 ns | 0.9475 ns | 0.0519 ns | 0 B |
| IndexOf | multilingual | 0.9966 ns | 0.0396 ns | 0.0022 ns | 0 B |
| SmallSet | multilingual | 1.2374 ns | 0.2389 ns | 0.0131 ns | 0 B |
| CachedSearchValues | multilingual | 1.0342 ns | 0.0501 ns | 0.0027 ns | 0 B |
| Scalar | short-hit | 0.7470 ns | 0.1350 ns | 0.0074 ns | 0 B |
| IndexOf | short-hit | 1.0135 ns | 0.0457 ns | 0.0025 ns | 0 B |
| SmallSet | short-hit | 1.2403 ns | 0.1393 ns | 0.0076 ns | 0 B |
| CachedSearchValues | short-hit | 1.0220 ns | 0.0159 ns | 0.0009 ns | 0 B |
| Scalar | short-miss | 0.7536 ns | 0.7149 ns | 0.0392 ns | 0 B |
| IndexOf | short-miss | 1.0121 ns | 0.0169 ns | 0.0009 ns | 0 B |
| SmallSet | short-miss | 1.3053 ns | 0.1776 ns | 0.0097 ns | 0 B |
| CachedSearchValues | short-miss | 1.0185 ns | 0.0461 ns | 0.0025 ns | 0 B |

Borrowed prescan removes transient baseline allocations on applicable matching inputs, with mixed timing results (short-hit slower, dense faster). Generated length-dispatched literals perform complete equality checks on both byte/char paths; all 228 original labels match the linear oracle, with unknown/prefix/non-ASCII/malformed cases covered by tests. Scalar scan wins at first-byte markup; simple IndexOf and cached SearchValues are similar on long inputs. Retain simpler production matching; no hashing/custom SIMD change.

Compiled Release getter inspection with retained local dotnet-inspect 0.24.0 confirms both large `big5` and small `windows_1252` tables use `ldtoken <PrivateImplementationDetails>` followed by `RuntimeHelpers.CreateSpan<int>(RuntimeFieldHandle)` and `ret`; neither getter contains array construction. This checks representative actual .NET 10 compiler lowering, alongside generated-source reproducibility and measured primitive allocation controls.

Complete raw logs/CSV/manifests/frozen first-run probe binaries are retained under ignored `artifacts/M7-builder/run1/`; corrected raw logs/CSV/manifest are retained under ignored `artifacts/M7-builder/run2/`. Only summaries, analysis and source-hash proof are tracked.
