# Applicable independent compile and evaluate evidence

M4-corpus reuses the verified evaluator's separate compile/evaluate evidence in `docs/reports/M4-evaluate/performance.md`. It adds no hot-path implementation, benchmark fixture or optimization and makes no new measurement or speedup claim. The frozen source profile requires independent compile/evaluate measurements; the approved evaluator's two compile and six evaluation cases satisfy that scope. Repeating those unchanged workloads would add no material corpus evidence.

Exact measured revision07487607b3df13cba3b8bcd523e47d544da589be, runtime candidate83dac3a8813c6a482b0e46242334f68bf3c420c0. At clean corpus candidate a5159eb, `git diff --exit-code` confirmed no changes to runtime source, build configuration or evaluator benchmark fixture against that measured revision. The corpus base contains the verified evaluator integration and all its dependencies. This comparison establishes unchanged source applicability; it does not claim current binaries were remeasured.

The original one-grant closure had148 source/generated mappings,43 binary mappings and3852 complete SDK/runtime/host mappings, all independently inspected by the coordinator. It used SDK10.0.400, explicit actual.NET10.0.11, native Arm64 AppleM4Pro/macOS15.8.1 and BenchmarkDotNet0.15.8. Both benchmark classes used `[MemoryDiagnoser, ShortRunJob]`, one InProcessNoEmit toolchain-only job,3 warmups/3 iterations/1 launch, no CLI job override. All eight cases succeeded; checked direct/wrapper/initial/final identity exits were0 and own-group cleanup left no child process. Lease release preceded raw inspection.

| Independent workload | Mean us | Inclusive standalone owned B/op |
| --- | ---: | ---: |
| CompileSmall (`/r/a[1]`) | 1.169 | 3704.000000 |
| CompileLong (128 literal terms) | 20.185 | 71804.500000 |
| Evaluate scalar | 43.069 | 196264.250000 |
| Evaluate128-member path | 314.424 | 284181.750000 |
| Evaluate64-member predicate | 223.839 | 601483.125000 |
| Evaluate existential comparison | 578.071 | 375401.875000 |
| Evaluate extension | 42.126 | 195124.125000 |
| Evaluate Unicode function | 5.146 | 11620.500000 |

Compilation precedes evaluation timing; every Evaluate includes actual entry contexts, whole-document capture, frames, scratch and owned results. These are positive whole-query ownership costs, not borrowed zero-allocation claims. Original cold setup223968B, positive array control1048B, and observed retained live-heap delta1519664B are preserved in the verified report. That report retains all64-operation allocation frequency distributions and exact BDN allocated-byte numerators/operation denominators/GC collection counts; this applicability table does not replace them. Occasional distribution spikes and standalone-versus-BDN differences remain disclosed without attribution.

ShortRun timings are informational on one host. No regression/speedup or x64 release claim is made. Functional grammar/order/Unicode/mutation/quota regressions are the new corpus deliverable. No new allocation/timing run or exclusive host lease occurred in this task.
