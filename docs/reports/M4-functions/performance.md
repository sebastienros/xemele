# M4-functions allocation and provisional latency evidence

The coordinator granted one exclusive prepared sequence at evidence revision 4f0e27859955b737539711dc091f79a2123852f2, whose runtime source is approved b7d3a2e. The wrapper ran control, discovery, allocation and then (only on success) all 13 benchmark cases exactly once. All phase/direct wrapper exits were zero. Initial/final freeze passed 108 source/config/generated files, 40 actual binary closure files and 195 pinned runtime files, manifest SHA256 210b27c4d87e339dde145dfc9bcaa18f9450a7eb92290788c5443c67e034cc4f. No build, restore, generation, executable retry, source edit or option change occurred during the lease. The lease was released before raw inspection. The first read-only process scan was sandbox-denied; an authorized read-only escalation actually confirmed no wrapper/driver/BDN children. The first release packet incorrectly stated the initial scan passed; an immediate correction preserved that distinction. No executable measurement was repeated.

Actual allocation FrameworkDescription is **.NET 10.0.11**, Arm64, macOS 15.8.1. All BDN host reports independently identify .NET 10.0.11 (10.0.1126.37416), SDK 10.0.400, Arm64 RyuJIT armv8.0-a. The minimum runtimeconfig is 10.0.0 with default roll-forward; it actually selected 10.0.11, consistent with the frozen maps. Product Xemele.dll SHA256 is 15EC35A89ECA9EB1F87C3EAFA8B005252A72E2FC0393B14FB8A38914F8715E8D.

## Exact standalone allocation domain

A group consumes seven production borrowed invocations with preallocated arguments/focus/navigator/shared budget: short decimal conversion, 400-digit overflow conversion, invalid number conversion, 1025-scalar multilingual length, ordinal contains, boolean and round. There are 128 batches of 256 groups per warm/measured phase: 32768 groups, 229376 invocations. Both phases consume exactly 33374208. The full measured batch loop and distribution stores count toward the strict zero gate; no tolerance, rounded display or attribution exclusion is used. Setup is outside this interval. No async work occurs.

| Domain | Operations | Exact allocated bytes | Bytes/op |
|---|---:|---:|---:|
| Cold fixture/library/metadata/argument setup | 1 | 9176 | 9176 |
| Warm-up primitive groups | 32768 | 0 | 0 |
| Entire measured primitive groups | 32768 | 0 | 0 |
| Positive 1024-byte array control | 1 | 1048 | 1048 |
| Owned minimum-subnormal number string | 256 | 348160 | 1360 |
| Owned scalar translate output | 256 | 8192 | 32 |
| Owned normalized ID result set | 256 | 79872 | 312 |

All 128 warm-up batch samples and all 128 measured batch samples are exactly zero. Owned outputs are separately warmed before their counters. Number formatting includes its capped char-array scratch for a decimal expansion exceeding 256 units; this is an explicit persistence/intermediate allocation, not a borrowed primitive claim. ID counts include token strings, scratch membership and final immutable membership ownership. Fixture setup deliberately includes initialization; its figure is not a claim about dispatch alone. The stateless library retains no caller input/result globally. Owned result lifetimes and fixed static metadata are separate from transient scratch; retained heap size was not measured.

## Thirteen successful ShortRun cases

All three classes declare exactly [MemoryDiagnoser, ShortRunJob]. One toolchain-only mutator uses InProcessNoEmit while preserving three warmups, three measured iterations and one launch. No CLI job flags or generated child benchmark builds. BDN reports 13 successful cases, global benchmark elapsed 94.29 seconds. CPU identity is reported as unknown and attempts to set High/restore Normal process priorities were denied. These timing figures are provisional on this single Arm64 machine; broad error intervals and a single ShortRun are not repeatability or release hardware evidence.

Borrowed/OwnedCopy pairs invoke the same production function with identical input semantics; OwnedCopy explicitly materializes input string and argument array, so their allocation difference demonstrates ownership, not a new scanning algorithm. Long decimal is overflow, Invalid is rejected +1e3, Short is XML-S -12.5. Scalar Unicode is 1024 CJK scalars plus one supplementary scalar. Semantic controls ran before allocation and in each benchmark setup, including numeric edge/long input oracles and invalid Unicode rejection.

| Function/input | Borrowed mean | Owned-copy mean | BDN borrowed allocation display | Owned-copy allocation |
|---|---:|---:|---:|---:|
| number invalid | 105.3 ns | 114.7 ns | – | 88 B |
| number long | 3170.9 ns | 3578.6 ns | – | 880 B |
| number short | 170.0 ns | 180.8 ns | – | 96 B |
| string-length ASCII | 90.45 ns | 101.42 ns | – | 88 B |
| string-length Unicode | 3942.59 ns | 4080.36 ns | – | 2136 B |

| Owned result | Mean | BDN allocated/op |
|---|---:|---:|
| Minimum-subnormal number string | 1212.1 ns | 1360 B |
| Scalar translate | 198.0 ns | 32 B |
| ID snapshot result | 178.4 ns | 312 B |

## Inclusive BDN diagnostics versus rounded display

BDN's dash allocation display does not establish exact zero in every diagnostic interval. The inclusive MemoryDiagnoser totals below are preserved and divided by its operation counts without subtracting any overhead. Several borrowed cases include small nonzero totals of unknown origin; the independent exact-zero standalone domain above is a separate interval/harness. No failure is hidden and no repeat was requested to erase these differences. Owned result standalone values match the rounded BDN per-operation displays while inclusive diagnostic totals retain their small additional bytes.

| BDN case | Inclusive diagnostic bytes | Diagnostic operations | Inclusive bytes/op |
|---|---:|---:|---:|
| NumericBenchmarks.Borrowed Invalid | 0 | 8388608 | 0.000000000 |
| NumericBenchmarks.OwnedCopy Invalid | 738200144 | 8388608 | 88.000314713 |
| NumericBenchmarks.Borrowed Long | 2640 | 262144 | 0.010070801 |
| NumericBenchmarks.OwnedCopy Long | 230689360 | 262144 | 880.010070801 |
| NumericBenchmarks.Borrowed Short | 2352 | 4194304 | 0.000560760 |
| NumericBenchmarks.OwnedCopy Short | 402655200 | 4194304 | 96.000480652 |
| ScalarBenchmarks.Borrowed Ascii | 3312 | 8388608 | 0.000394821 |
| ScalarBenchmarks.OwnedCopy Ascii | 738200528 | 8388608 | 88.000360489 |
| ScalarBenchmarks.Borrowed Unicode | 3312 | 131072 | 0.025268555 |
| ScalarBenchmarks.OwnedCopy Unicode | 279973104 | 131072 | 2136.025268555 |
| OwnedBenchmarks.NumberString | 713034992 | 524288 | 1360.006317139 |
| OwnedBenchmarks.Translate | 134221040 | 4194304 | 32.000789642 |
| OwnedBenchmarks.IdSet | 1308626160 | 4194304 | 312.000789642 |

No SearchValues, token hash or custom SIMD optimization is claimed; the charged straightforward ordinal/scalar loops are retained. Correctness, allocation and ownership evidence apply only to this internal function lane with approved snapshot fakes. Actual navigator ID inference, compiler/evaluator composition and end-to-end query performance remain later owners. M9 must confirm x64/Arm64 release hardware behavior. Raw logs, JSON and BDN result files remain only in /private/tmp/m4-functions-4f0e278-measurements; this file contains derived summary tables and analysis only.
