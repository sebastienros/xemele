# XPath navigation and ordering repair

The original frozen entity-medium query now returns the expected 3,146 items within its unchanged 100-million-operation profile. A positive control rebuilt the exact pre-repair source at 2d38e67fc and reproduced its work-limit exception with the same fixture and worker. The rejected original command retains its failed receipt; the control confirms the regression rather than awarding a release pass.

Child/self/parent/member/descendant axes traverse captured structural intervals. Child traversal skips whole child subtrees. Sorted normalization is linear; unordered input uses iterative metered merge sort. Monotone merges avoid a uniqueness table, while reverse/overlapping merges use a bounded balanced integer tree with a poll and charge for every comparison. Every output append retains prior node-set preflight; foreign/default identities, reverse proximity and cancellation remain unchanged.

All 10,754 unit tests pass, including five new ordering/limit/cancellation cases. Large sorted/reversed duplicate normalization and 4,096-element descendant counting remain inside their explicit test work caps. Existing exact-budget accounting and all earlier tests pass.

| Count | Workload | Original allocated KB/op | Repaired allocated KB/op | Original local mean | Repaired local mean |
| ---: | --- | ---: | ---: | ---: | ---: |
| 128 | Children | 14.33 | 14.33 | 4.392 µs | 2.875 µs |
| 128 | Count descendants | 553.16 | 552.84 | 2692.524 µs | 175.251 µs |
| 128 | Normalize reverse | 15.32 | 15.21 | 19.735 µs | 6.522 µs |
| 128 | Normalize sorted | 15.32 | 14.69 | 18.024 µs | 4.430 µs |
| 1024 | Children | 112.41 | 112.41 | 33.048 µs | 21.934 µs |
| 1024 | Count descendants | 4491.90 | 4491.52 | 164545.579 µs | 2929.785 µs |
| 1024 | Normalize reverse | 120.46 | 120.28 | 879.523 µs | 56.420 µs |
| 1024 | Normalize sorted | 120.46 | 116.26 | 806.692 µs | 37.132 µs |
| 128 | Reverse/overlapping merge | 483.62 | 491.26 | 1247 µs | 206.8 µs |
| 1024 | Reverse/overlapping merge | 3904.56 | 3967.94 | 68531 µs | 2057.6 µs |

Both versions use identical `[MemoryDiagnoser, ShortRunJob]` workloads and pinned SDK10.0.400/.NET10.0.11 on Apple M4 Pro arm64, with the recorded tiering/PGO/ReadyToRun/background-GC settings disabled. Initialization fixtures and query compilation occur outside timing. Internal ordering benchmarks include a fresh owned budget in each operation; public query allocation includes its full snapshot. The fallback costs approximately 1.6% more allocation for its bounded uniqueness tree. Sorted scratch needs one integer array; unordered scratch needs two, each bounded by the preflighted input count. No hash-collision-sensitive uniqueness table is introduced. These local timings are informational: they confer no dedicated-machine or x64/reference-relative throughput pass.

The original generic benchmark-parent build failed on multiple existing top-level programs and was preserved. No benchmark executed in that attempt. The successful comparison uses isolated task-owned projects over exact archived original/current sources, without altering shared build files. The reverse/overlap workload was measured separately to cover the new fallback; the original eight successful measurements were not repeated.

Frozen managed qualification remains **9 passed / 1 failed**, with all ten original fixtures retained. Text-large hits the original 16 Mi-character aggregate snapshot cap in `CheckInputSize` before query execution. That failure is a separate finite benchmark-profile issue, owned by M9-performance; it receives no pass credit or release exclusion. The focused repair accepts the original entity work-budget regression and characterizes the full corpus. The runtime input quotas and release thresholds remain unchanged.

Required follow-ups: M9-performance must correct/justify the finite all-fixture benchmark profile before measurement and repeat only the affected text-large qualification; the owner of the aggregate benchmark project must resolve its top-level-source glob collision. M9-robustness must reassess earlier whole-source reuse after the runtime XPath change. Fuzz remains explicitly deferred.

Affected compatibility verification passed: 920 conformance tests, 618 differential tests, and 1,430 compiled public members across 55 fragments. All 222 first-preview signatures remain unchanged and both deliberate mismatch controls refuse.
