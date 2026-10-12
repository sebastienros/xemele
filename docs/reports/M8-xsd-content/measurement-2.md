# Content measurement evidence

Measured source `127d15b2fedd1675572bad17b4822c80ee94d241`; prepared tip
`75d796baad6ae6123d2ef973bca6d927c8499dd8`, review receipt tip
`dd42401afae5b0875c926374e8a6f91e1c64a35a`. Root serial grant `b9e9f7e9`
ended with durable cleanup RELEASE `cf39961a` before raw inspection. Both
content children (89193, 89195) exited 0, were reaped, had absent groups and
closed logs; all 213 before/after identities matched. The content supervisor
finished in 42.06422708299942 seconds under its 600-second bound. No retry.

SDK 10.0.400, .NET 10.0.11 Arm64 RyuJIT, Apple M4 Pro, macOS 15.8.1,
concurrent workstation GC (`serverGc=false`), synchronous paths,
`DOTNET_ROLL_FORWARD=Disable`. Input/setup and result consumption are in the
owned fixture; neither BDN compilation nor generated code was used. NoEmit's
generic “Building” banner was a 0.01-second toolchain preparation step.

## Allocation observations

Each route has 32 warm calls followed by 64 current-thread counter samples.
Every route's 64 samples equal the listed value: its frequency histogram is
`value:64`, its min/max equal that value, and the aggregate equals their sum.

| Route | Bytes/sample (min=max) | Samples | Aggregate bytes |
| --- | ---: | ---: | ---: |
| Dictionary name baseline | 0 | 64 | 0 |
| Borrowed compiled name hit/miss | 0 | 64 | 0 |
| Warm unbounded declaration cursor | 0 | 64 | 0 |
| Warm nullable group cursor | 0 | 64 | 0 |
| Warm wildcard cursor | 0 | 64 | 0 |
| Owned lazy cursor object | 104 | 64 | 6,656 |
| Owned initialization, banks, operation and first match | 4,760 | 64 | 304,640 |
| Owned content compilation, checking all types | 31,080 | 64 | 1,989,120 |
| Actual schema/XML/compiler/content composition | 177,416 | 64 | 11,354,624 |
| Owned UPA diagnostic compilation/exception | 27,360 | 64 | 1,751,040 |

The name routes consume five identical membership/declaration-ID candidates:
hits `item`/`根`, an unknown name, a longer shared prefix and wrong namespace.
The declaration/wildcard cursor routes consume one child and completion; the
nullable group consumes `a`,`b` and completion. Each has its own semantics.
The composition route actually parses the schema and XML instance and matches
three children; it does not execute datatype/facet or public-facade validation.

Cold fixture creation plus its owned preparatory warming allocates 340,336 B;
this boundary excludes process startup and any earlier runtime/type setup.
The positive 1,024-byte-array control reports 1,048 B. Whole-heap retained
observations with explicit keep-alives are 337,216 B initially, 369,368 B with
32 programs (+32,152 B), and 661,992 B after adding 32 initialized cursors
(+292,624 B). These are aggregate live-heap observations, including other live
managed/runtime effects, rather than per-object size or a lifetime bound.

## Five-case BDN observations

MemoryDiagnoser, ShortRun, one launch, three warmup and three measured
iterations, InProcessNoEmit. Error is the half-width of the 99.9% interval.
All raw GC collection counts (Gen0/1/2) are zero. The table preserves the raw
managed-byte total and its operation denominator instead of rounded `0 B`.

| Method | Mean ns | Error ns | StdDev ns | Raw GC bytes / operations | Residual B/op |
| --- | ---: | ---: | ---: | ---: | ---: |
| DictionaryBaseline | 27.98 | 0.124 | 0.007 | 0 / 16,777,216 | 0 |
| BorrowedNameHitMiss | 52.78 | 8.971 | 0.492 | 1,968 / 16,777,216 | 0.000117302 |
| DeclarationCursor | 232.93 | 48.941 | 2.683 | 1,632 / 4,194,304 | 0.000389099 |
| NullableGroupCursor | 1,758.57 | 177.344 | 9.721 | 1,968 / 524,288 | 0.003753662 |
| WildcardCursor | 230.16 | 10.426 | 0.571 | 1,680 / 4,194,304 | 0.000400543 |

Four BDN raw totals are nonzero. Their precise attribution is not established;
the rounded BDN display is not proof of exact zero. The direct probe supports
zero current-thread allocations for its sampled synchronous warmed routes.
The BDN timings are a single provisional Arm64 run, with no repeatable speed,
optimization, cross-architecture or asynchronous claim. The dictionary matches
the same name answers, while the content primitive also charges/polls its
operation and returns policy; cursor ratios to that baseline do not compare
equivalent grammar workloads. No SearchValues or specialized hash optimization
was introduced. Cold state, bank growth, arbitrary-precision counter growth and
owned results are separate boundaries; the seeded overflow/cancellation tests
are correctness evidence, not measured allocation coverage of overflow.
The eventual real facade composition and x64 release checks remain later joins.

## Exact identities

The counter-disabled control consumed ten routes without counters/timing;
five BDN semantic controls and exact five-case discovery passed before freeze.
Actual 285 correctness cases, 1,400 public/nine frozen members and the genuine
XML-only consumer pass at the measured source. Independent bounded source
review accepted the indexing/accounting and failure-persistence correction.

| Identity | SHA256 |
| --- | --- |
| 213-identity preparation manifest | `8bcc6182e3e89db9f8a3e627cc1f58e3c635b6d89f3a072a352eb7f0d1fb52a9` |
| Guarded supervisor | `b77dee00b003416eb10999c2dbe8788619a2dfff6b9c69adeb54d5d4115ca552` |
| Runtime assembly | `d8d837c0b81a58ca60d142b518f6ccca6d114f66d683b77b434153dce175f557` |
| Allocation probe assembly | `92dbd6c688d7599f9d30ed8cdcc3c2d3d0901f55dea361fcd233f97798e05c8c` |
| BDN assembly | `d7bb5e9a05d7179a82e97ce175f5cbd4b8009c8158b287df6970e96fd5c444bc` |
| Disabled control output | `35a262a39d175d56759d8dfc4c2819d4e6d586d8312ffb730b7b2818e38cf49d` |
| Schema corpus | `5b771acdbbbdd4fe120d6c32169f3360570dee05ab3f8bdfef97b0c9ee32b9e5` |
| Invalid UPA corpus | `7237cb0a54c05fbf67cffed27cc2660dc171dc95e6ec1506759469c2a43d9e0a` |
| Actual XML instance | `54c18857ba97adc444ca94970d53398dbdf905711544b2a9c796cc4e6afd921d` |
| Measured allocation output | `c29e88557f6b3a2624a324c059d69d9b1d2fca77adcd2fe4347ad155f160ea9b` |
| Lifecycle output | `fcd9af8b0078c90bed435f082dbd56e577d548a1d64c0425a8860d0058250d8d` |
| Raw BDN command output | `0b3ad57ee4699bad355b349aa9a7342bb626bbfbd790282137646b1f52bf52c4` |

Raw outputs remain under ignored `artifacts/M8-xsd-content/measurement-2/`
until coordinator inspection/verified cleanup; they are not committed.
The original failed attempt and its absent numeric evidence remain documented
in [measurement-1.md](measurement-1.md). No raw arrays or generated logs are
copied into this report.
