# Evaluator ownership and performance evidence

Exactly one coordinator-granted frozen sequence ran at 07487607b3df13cba3b8bcd523e47d544da589be. Runtime/test candidate 83dac3a and prepared fixture/binary inputs were unchanged. Direct control/discovery/allocation/benchmark exits 0, wrapper 0, initial/final identity 0, no_child=true after scoped cleanup. Report and lease release preceded raw inspection. Independent root inspection confirmed all 148 source/generated, 43 binary and 3852 full SDK/runtime/host mappings and actual allocation/benchmark data. No builds/restores/tests/source changes/retries or repeated measurements occurred during the grant.

Pinned SDK 10.0.400, explicitly selected .NET 10.0.11, native Arm64 RyuJIT on Apple M4 Pro, 14 cores, 48 GiB, macOS 15.8.1. BenchmarkDotNet 0.15.8; exact [MemoryDiagnoser, ShortRunJob], one InProcessNoEmit toolchain-only job,3 warmups / 3 iterations / 1 launch, eight successful cases. Global benchmark time 50.79 seconds. Exact argv/env/process/deadline bounds are frozen in measurement-plan.json; lifecycle evidence uses the robust checked own-group cleanup/final identity/no-child wrapper. The effective runtime-environment projection and exact plan overrides avoid unrelated volatile shell-state differences; the actual pre-grant whole-environment mismatch is retained in preparation.md/checks.md.

CompileSmall compiles /r/a[1]; CompileLong compiles 128 left-associative literal terms. Whole Scalar evaluates 1+2*3; Path materializes 128 /r/a members; Predicate materializes 64 even-position members; Comparison evaluates existential /r/a != /r/a; Extension returns 6 then adds 1 using the captured dynamic context; Unicode counts 1024 BMP plus one supplementary scalar. The shared document contains 128 alternating 64-character text elements. Fixture generation, retained source document and compiled programs precede evaluation timing, but every Evaluate still captures its own whole document and owns its contexts, continuation frames, scratch and results. This is actual query composition through verified compiler/navigation/functions, separate from primitive lane gates. No new scanner/matcher optimization or zero-allocation whole-query claim is made.

## Standalone owned allocation distributions

Cold fixture setup is 223,968 B, inclusive of parsing, fixture-owned compilation and static initialization reached during that setup. First-evaluation cold initialization is not separately counted. Each case warms 16 operations, then 64 measured operations. The positive 1024-byte array control allocates 1,048 B. The complete per-operation distribution is preserved as an exact frequency summary below; raw 64 entries were inspected independently before cleanup. Each numerator counts the whole 64-operation interval, including the driver's consumption and distribution stores. Every operation produces a consumed result; all owned costs are positive.

| Case | Inclusive bytes/operations | B/op | Exact per-operation frequency distribution | Consumed |
|---|---:|---:|---|---:|
| compile-small | 237,056/64 | 3704.000000 | 3704 B × 64 | 64 |
| compile-long | 4,595,488/64 | 71804.500000 | 71800 B × 63, 72088 B × 1 | 64 |
| scalar | 12,560,912/64 | 196264.250000 | 196256 B × 62, 196520 B × 2 | 448 |
| path | 18,187,632/64 | 284181.750000 | 284160 B × 61, 284448 B × 1, 284712 B × 2 | 8192 |
| predicate | 38,494,920/64 | 601483.125000 | 601440 B × 59, 601992 B × 5 | 4096 |
| comparison | 24,025,720/64 | 375401.875000 | 375376 B × 60, 375640 B × 1, 375664 B × 1, 375928 B × 2 | 64 |
| extension | 12,487,944/64 | 195124.125000 | 195120 B × 63, 195384 B × 1 | 448 |
| unicode | 743,712/64 | 11620.500000 | 11616 B × 63, 11904 B × 1 | 65600 |

The retained fixture remains alive. Sixteen long compiled expressions plus sixteen 128-member path results are retained across full live-heap collections: 474,352→1,994,016 B, observed delta 1,519,664 B. This is an informational aggregate live-heap delta, not a precise deep-size or per-result retention proof. Compiled ASTs and owned item metadata/result arrays persist with their owners; exposed node handles retain the live input document. No global evaluation cache retains results.

## Benchmark timing and inclusive memory totals

| Case | Mean us | Actual GC allocated-byte numerator/operations | Inclusive B/op | Actual Gen0/Gen1/Gen2 collections |
|---|---:|---:|---:|---:|
| CompileSmall | 1.169 | 1,908,534,728/524,288 | 3640.241104126 | 228/2/0 |
| CompileLong | 20.185 | 2,350,726,464/32,768 | 71738.478515625 | 281/50/0 |
| Scalar | 43.069 | 3,214,775,480/16,384 | 196214.323730469 | 384/127/0 |
| Path | 314.424 | 581,901,984/2,048 | 284131.828125000 | 69/34/0 |
| Predicate | 223.839 | 2,463,466,440/4,096 | 601432.236328125 | 294/98/0 |
| Comparison | 578.071 | 384,363,232/1,024 | 375354.718750000 | 45/14/0 |
| Extension | 42.126 | 3,196,948,896/16,384 | 195126.275390625 | 381/127/0 |
| Unicode | 5.146 | 1,516,343,040/131,072 | 11568.779296875 | 181/5/0 |

These exact allocation numerators/operation denominators preserve the actual MemoryDiagnoser inclusive observations even though the human BDN table rounds to KB/op. The GC operation denominator is its diagnostic batch, not the sum of three WorkloadActual timing iterations. All Gen2 counts were zero. The standalone counters and BDN report have different observed per-operation amounts; occasional standalone distribution spikes and those differing amounts are disclosed without attribution. No overhead subtraction or rounding creates an exact-zero claim.

Timing is informational on this single Arm64 host and ShortRun's three iterations; this report makes no regression/speedup or cross-architecture claim. M4-gate owns cumulative conformance integration; M9 owns x64/Arm64 release validation. Whole evaluation allocations intentionally include cold document capture and owned results. Borrowed primitive zero gates remain the independently verified model/compiler/navigation/functions responsibilities; this evaluator introduces no borrowed primitive promised to allocate zero.

Only durable tables and analysis are committed. The coordinator acknowledged actual raw measurement and prior check inspection; raw outputs may be removed after this summary/task commit. Ignored preparation cache/closure artifacts are handled separately from raw output.
