# Model measurement analysis

Prepared runtime source review approved dbbf5cb68cab910eeb990d87a8684aa956633d1c. The subsequent runtime change at 73797d8ca8e0fa2082d6e74b6a2ab7ba7a38df58 removes one trailing space from Ast.cs:96; no semantic changes. The prepared manifest SHA-256 was 951c6723ca84b1d2814892540c42cdcabf5de8a444241bca8fc35539dd46e57d (106 source/generated/config files, 40 driver binary closure files and 195 runtime/host/SDK files). Initial and finally identity checks passed.

The first authorized allocation attempt failed the exact-zero gate: **128 B total across 65,536 primitive groups**, or 0.001953125 B/group. The positive 1024-byte array allocated 1048 B. 256 explicit owned node-set constructions allocated 931,840 B (3640 B/op); 256 double.Epsilon number-string constructions allocated 370,688 B (1448 B/op). These nonzero owned/positive observations do not make the primitive gate pass. The process threw before persisting JSON, so cold setup data was unavailable. Timing was correctly skipped. The lease was released immediately with no remaining subprocess, before reading the transient failure log. No raw logs/results are committed.

The origin of 128 B is unattributed. The original warmup and measurement used different loop/call shapes (256 vs 65,536 direct primitive calls). A coordinator-approved harness correction uses the identical non-inlined 512-group batch in both 128-batch warm and measured phases, with attribution arrays preallocated outside all counters. The exact-zero gate covers the entire measured loop, including counter sampling and distribution writes: no thresholds or excluded allocations were introduced. JSON is persisted before any exact-zero failure. Batch-shape attribution remains a hypothesis until counters support it. Counters-disabled preparation does not establish allocation results.

The model primitive group uses shared-budget charging, scalar boolean/string/number conversions, borrowed numeric scanning and stable logical identity equality/hash/metadata reads. Long overflow/whitespace and malformed UTF-16 are included. Setup, item metadata and explicit result materialization own allocations. Fixture cold setup includes DOM/model/budget state, not arbitrary caller setup or every possible AST/context constructor. Warm and owned costs are reported separately. BDN compares borrowed parsing with identical parsing following owned string materialization; identity and tree-handle baselines have related but different abstraction costs. No cross-domain equality is asserted. All BDN classes use [MemoryDiagnoser, ShortRunJob], with a toolchain-only InProcessNoEmit mutator preserving one ShortRun job. Preparation asserts ten cases, three warmups, three iterations and one launch.

All timings are provisional model microbenchmarks on the shared Arm64 host; no composed query implementation exists. Query composition validation and M9 x64/Arm64 hardware confirmation remain follow-ups.

## Authorized corrected sequence

Harness revision e919eac07f221d64a4a78fa549609f2d12f6d293 changes only probe attribution and this analysis. Runtime source, generated runtime inputs and Xemele.dll are byte-identical to the first prepared run. Driver-only rebuild used `BuildProjectReferences=false`. Prepared manifest SHA-256: 7ba441c288da7a10937c8cc94447e64629bac91f7c4977ff545cd53bdf046a88; root independently checked all 106/40/195 source/binary/runtime entries. Driver SHA-256: 35e381bd5d9a8ccba89cf9287aefb72d4125537990d81d9797529897a5c271d6; loaded product: 7368e83bf521328167f7785580b1cab1d077b00bcf6f2907c45504b975527697. Runtime: .NET 10.0.11, SDK 10.0.400, macOS 15.8.1, Arm64 RyuJIT armv8.0-a, concurrent workstation GC. CPU model was not identified by BDN. Default runtime settings were retained; no explicit job CLI option was supplied.

Counters-disabled control passed with 3,309,568 consumed in **each** of the two identical 128 x 512 phases. Discovery confirmed ten cases in the one declared ShortRun job. The second exclusive lease executed exactly one allocation probe and, only after it succeeded, one ten-case BDN suite. Total sequence elapsed 97.17 seconds. Initial and finally identity checks passed; no generation, restore, build, test, source change or retry occurred. Both processes exited and the lease was released before report inspection.

| Standalone current-thread scope | Total bytes | Interpretation |
| --- | ---: | --- |
| Fixture cold setup | 10,320 | Explicit DOM/model/budget setup |
| 65,536 warm groups | 1,448 | Entirely warm batch 0; other 127 warm batches zero |
| 65,536 measured groups | **0** | Entire loop including sampling and attribution stores; all 128 measured batches zero |
| Positive escaped 1024-byte array | 1,048 | Counter sensitivity confirmed |
| 256 owned node-set calls with boxed result consumption | 944,128 | 3,688 B/op; includes standalone consumption/harness boundary |
| 256 owned epsilon number strings | 370,688 | 1,448 B/op |

Warm and measured consumption were each 3,309,568. This supports the warm/steady phase distinction. It does **not** identify the original 128-byte allocation's causal origin. The earlier owned-set observation (3640 B/op) and corrected observation (3688 B/op) remain separately recorded; neither is an equality target or inferred runtime regression.

## BDN summary (in process; ten successful cases)

| Operation | Mean ns/op | BDN displayed allocated/op |
| --- | ---: | ---: |
| Materialized parse, XML-S-spaced -12.5 | 27.974 | 40 B |
| Borrowed parse, XML-S-spaced -12.5 | 23.400 | 0 B |
| Materialized parse, invalid +1根😀 | 8.200 | 32 B |
| Borrowed parse, invalid +1根😀 | 3.760 | Rounded 0 B |
| Materialized parse, 42 | 23.252 | 32 B |
| Borrowed parse, 42 | 18.255 | Rounded 0 B |
| Tree handle equality | 0.3533 | Rounded 0 B |
| Logical item equality | 0.9908 | Rounded 0 B |
| Owned node-set | 561.587 | 3.49 KiB (approximately 3576 B) |
| Owned epsilon number string | 111.657 | 1.41 KiB (approximately 1448 B) |

BDN's whole-process diagnostic domain is distinct from the exact current-thread standalone gate. Its displayed zero does not imply every inclusive aggregate was zero: borrowed invalid and `42` cases recorded 2640 B across 134,217,728 and 33,554,432 operations respectively; both identity cases recorded 3312 B across 536,870,912 and 268,435,456 operations. XML-S borrowed parsing recorded an exact zero aggregate. These small diagnostic totals are retained as summary observations, not subtracted from the standalone gate or equated with it. The direct owned node-set return in BDN differs from standalone `GC.KeepAlive` boxed value consumption. No equality target across those domains is asserted.

BDN could not set High/Normal process priority in the sandbox; all ten reports still succeeded. ShortRun confidence intervals and overhead subtraction, especially subnanosecond identity means, limit timing interpretation. No speedup or release hardware claim is made. Raw JSON/log/CSV/HTML/BDN reports remain transient for coordinator inspection; only this summary and analysis are committed.

### Inclusive BDN diagnostic totals (summary, distinct domain)

| Case | Inclusive allocated bytes | Diagnostic operation divisor | Derived B/op |
| --- | ---: | ---: | ---: |
| Materialized XML-S -12.5 | 671,088,640 | 16,777,216 | 40 |
| Borrowed XML-S -12.5 | 0 | 33,554,432 | 0 |
| Materialized invalid +1根😀 | 2,147,483,648 | 67,108,864 | 32 |
| Borrowed invalid +1根😀 | 2,640 | 134,217,728 | 1.96695327759e-05 |
| Materialized 42 | 1,073,743,456 | 33,554,432 | 32.0000486374 |
| Borrowed 42 | 2,640 | 33,554,432 | 7.86781311035e-05 |
| Tree handle equality | 3,312 | 536,870,912 | 6.16908073425e-06 |
| Logical item equality | 3,312 | 268,435,456 | 1.23381614685e-05 |
| Owned node-set | 3,749,829,624 | 1,048,576 | 3576.11620331 |
| Owned epsilon string | 12,146,707,696 | 8,388,608 | 1448.00039482 |

These are the exact diagnostic totals/divisors underlying the rounded display, not an exact-zero primitive acceptance gate. BDN-owned framework/diagnostic effects are inclusive and not causally attributed or subtracted.
