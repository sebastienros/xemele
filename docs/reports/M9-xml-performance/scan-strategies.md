# XML candidate scan strategies

The performance branch caches state-specific SearchValues. The starting main XML scanners and the HTML tokenizer use scalar loops. This experiment compares the exact XML text and ASCII-name sets and the quote-aware tag delimiter set against explicit scalar predicates and runtime span searches. It does not measure complete grammar validation or establish an HTML threshold.

Source: `benchmarks/Xemele.Benchmarks/M9-xml-performance/ParserScans.cs`, revision `f3fe62e9d`. The benchmark has a once-per-call kind dispatch outside the scalar loop; tiny means include that dispatch and are not standalone instruction costs. The text/name literals were checked byte-for-byte against XmlParserCore.cs. Setup exhaustively compares all 65,536 UTF-16 code units and checks the prepared workload. Supplementary-scalar, grammar, normalization, quota and source-mapping checks remain separate production responsibilities.

On Apple M4 Pro ARM64, .NET 10.0.11 / SDK 10.0.400 / BenchmarkDotNet 0.15.8, tiered compilation, PGO and ReadyToRun disabled, 108 ShortRun cases completed (one launch, three warmups, three measurements at 250 ms). All timed scans allocated 0 B; set creation, input preparation and exhaustive checks are outside timing. These are local informational results.

| Candidate / missing stop | Scalar ns | Cached SearchValues ns | Runtime span search ns |
| --- | ---: | ---: | ---: |
| Name, 3 units | 2.19 | 2.99 | 9.05 |
| Name, 32 units | 15.66 | 2.71 | 37.93 |
| Name, 4,096 units | 1,412.31 | 162.18 | 192.23 |
| Tag, 3 units | 1.03 | 2.68 | 2.83 |
| Tag, 32 units | 10.06 | 3.04 | 2.80 |
| Tag, 4,096 units | 1,368.81 | 227.95 | 227.73 |
| Text, 3 units | 1.46 | 2.24 | 8.18 |
| Text, 32 units | 20.70 | 3.06 | 45.61 |
| Text, 4,096 units | 2,722.85 | 161.38 | 327.96 |

Runtime span search means IndexOfAny/IndexOfAnyExcept with the literal set for text/names, and the three-character IndexOfAny overload for tags. Cached sets remove work even from allocation-free searches: a text stop at index 2 in a 4,096-unit input takes 2.51 ns cached versus 48.80 ns with the span-set overload. Cache construction is not happening per token in the performance branch.

Shapes include a miss, a stop at index 2, a stop at the end, and multilingual input. The latter is an ordinary text run but stops the ASCII name candidate at index 0: its 1.00 ns scalar versus 2.21 ns cached means do not describe the subsequent Unicode name validator. On a 4,096-unit multilingual text run, scalar/cached/span-set scans take 2,587.26 / 289.46 / 327.58 ns. The results reinforce that both set and data distribution matter.

The existing attribute experiment separately covers both short and long ordinary runs, early/end stops and multilingual values. Its six-character quote/ampersand/less-than/TAB/CR/LF set differs from this larger text set. XML backslash is not an attribute escape delimiter. No conclusion here substitutes for chunk, grammar, Unicode, quota or callback tests.

## Reproduce

Build `docs/reports/M9-xml-performance/Runner/XmlPerformanceRunner.csproj` in Release with the pinned runtime. Set DOTNET_ROOT/PATH to the pinned SDK, XEMELE_PERF_ROOT to the worktree, and DOTNET_TieredCompilation=0, DOTNET_TieredPGO=0, DOTNET_ReadyToRun=0. Then run:

```text
dotnet --fx-version 10.0.11 docs/reports/M9-xml-performance/Runner/bin/Release/net10.0/XmlPerformanceRunner.dll parser-scans --iterationTime 250 --artifacts artifacts/M9-xml-performance/parser-scans-bdn
```

Raw 108-case results: `artifacts/M9-xml-performance/parser-scans-bdn/results/Xemele.XmlPerformance.ParserScans-report.csv`. Raw logs and binaries remain ignored artifacts.

## Production decision

Keep cached bulk text/name scans and the existing small-set tag overload. A temporary scalar scan for candidates of at most three units passed all 10,792 unit tests, but seven alternating before/after/BCL rounds over all ten documents gave after/before latency geomean 1.0037x. Feed-small was 1.0202x and SVG-small 1.0261x. The experiment was reverted: no useful end-to-end improvement was established. The added branch also affects the ordinary cached path, and candidate length is remaining token length, not known name length. Primitive results alone do not justify a production threshold. No material allocation change was measured. Raw paired results are `artifacts/M9-xml-performance/short-name-paired.json`; the exact rejected patch is `short-name-variant.patch` beside it, SHA-256 `3eaa103187f67985f0d2d25af88b73ba7822aa50311c328cbe26935b130d8687`. The retained runtime source remains byte-identical to the pre-experiment checkpoint. HTML runtime adoption and byte scanner thresholds remain unmeasured by this experiment.
