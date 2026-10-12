# Accepted allocation evidence

Coordinator executed the frozen allocation-only sequence at preparation source `7b29ab22e478fe6949d75147391fdc5382e00167`, accepted runtime source `75d037bcddf3f1515a3ecf48ac524f4a2c6ae74c`. Grant `de7c0ac6c5402019e844683e4c53eb02c86a219b`; cleanup RELEASE `84386ee5dd989e3f19743c401a3a8d984887962a` was committed before raw inspection. Both control/measurement children (37540/37542) exited 0, were reaped, had absent owned groups and closed logs. All 448 identities matched before/after. No retry, build, correctness rerun or timing/zero-allocation claim. Coordinator accepted required source/evidence. Worker inspected the released allocation summary and confirmed raw SHA256 `ae8720835d04212334a2cc3bfffe2400e5f75d784fadba77e75105c1b079a1b9`.

.NET 10.0.11, Arm64, macOS 15.8.1, workstation GC; tiered compilation and concurrent GC disabled. Each owned-output route ran 16 warmups, then one aggregate thread-allocation reading around 64 evaluator calls. Independent validity/completeness checks consumed the results. Typed-default includes canonical int `01` element default and fixed `01` versus actual `1`.

| Route | Allocated bytes / calls | Mean bytes/call |
| --- | ---: | ---: |
| valid-owned | 1,685,504 / 64 | 26,336 |
| invalid-diagnostic | 1,174,016 / 64 | 18,344 |
| typed-default | 1,807,872 / 64 | 28,248 |
| typed-duplicate | 1,676,072 / 64 | 26,188.625 |
| document-ids | 1,778,688 / 64 | 27,792 |
| long-owned (64 elements) | 30,325,624 / 64 | 473,837.875 |
| wildcard-scopes | 1,063,424 / 64 | 16,616 |
| nil-owned | 589,312 / 64 | 9,208 |
| work-refusal | 82,432 / 64 | 1,288 |

Cold fixture setup allocated 1,810,728 bytes / one setup (schema compilation, registry, prepared content/identity programs and parsed inputs). Positive 1024-byte array control allocated 1,048 bytes. Forced-GC whole-heap readings around 16 retained invalid results were 442,808 and 450,864 bytes: approximate delta 8,056 bytes / 16 retained results. This is not an exact per-result object size or leak measurement. All route outputs include owned operation state, typed values, identity state, results and diagnostics where applicable; allocations are expected. Cold/retained observations can include setup/JIT/scaffolding/GC effects. No allocation-free, throughput or public-conformance claim.

The passing correctness source remains unchanged. Worker actually ran 937 selected correctness cases and separate 42 resolver cases at `75d037bc`; coordinator reported combined 937 + 42 = 979 passed at `7c862528d88fb725e322eb53c278efe268b9fac8`, with zero failures/errors/skips/not-run/build warnings, public 1410/frozen evaluator 2 matched, normal/true XML-only consumers passed, planning 182/diff passed. These coordinator executions are not worker executions. Earlier 851 selected results with two canonical failures and root before/after derivation checks remain preserved in task evidence.

Final handoff: implemented branch work; coordinator alone owns final integration and verified metadata. No raw result files are committed.
