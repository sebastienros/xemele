# M4 corpus branch checks

Final source candidate: a5159eb52f13552ef33bcf0c31ab0178b2e75dd8, based on claim 67786348b9da264c5ebfb441f9de5094e1f084dc and verified prerequisite 61d71bd1ddbeb655f810dcfbd4e05cba7961598b. All new files are owned by M4-corpus. No runtime, shared project, API, existing test, frozen contract, orchestration state or earlier milestone files changed.

Pinned executable: `/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet`, SDK10.0.400. Actual test/reference/fuzz process selection used `--fx-version 10.0.11`; xUnit XML and runtime guards confirmed .NET10.0.11, Arm64. Test invocations used invariant culture. These are correctness runs, not allocation or timing measurements.

All Release builds used `-c Release -m:1 -nr:false -p:UseSharedCompilation=false -p:NuGetAudit=false`. Each final conformance, differential and owned fuzz driver build succeeded with zero warnings/errors. Build exits were checked before running their binaries.

| Check | Actual outcome | Provenance |
| --- | --- | --- |
| Conformance DLL `-namespace Xemele.Conformance.Tests.XPath -culture invariant -result-xml /private/tmp/m4-corpus-conformance.xml` | 106 passed, 0 failed/errors/skipped/not-run:105 frozen vectors plus integrity/runtime guard; reverse-axis proximity checks included | Initial run at claim6778634 with authored files, source-equivalent to75aed25. Final relevant rebuild/run at actual75aed259b8d3e9b250799a08586de6d41c9e4186 with the three Number-kind assertions then committed byte-identically as a5159eb |
| Differential DLL `-namespace Xemele.Differential.Tests.XPath -culture invariant -result-xml /private/tmp/m4-corpus-differential.xml` | 156 passed, 0 failed/errors/skipped/not-run | Actual claim6778634 with authored files byte-identical to75aed25/a5159eb differential source |
| Owned `docs/reports/M4-corpus/bin/Release/net10.0/Fuzz.Driver.dll` | Seed1295276112:768 generated,768 reused,128 invalid grammar,32 mutation,32 exact/one-over quota pairs,64 cancellation checks | Actual claim6778634 with authored files byte-identical to75aed25/a5159eb fuzz and driver source |
| Coordinator source review | Approved75aed25 plus three explicit special-number kind assertions, exact a5159eb; no residual blockers | Source-only review, not an independent test execution |
| Runtime/build/benchmark fixture comparison against measured07487607b3df13cba3b8bcd523e47d544da589be | `git diff --exit-code 07487607b3df13cba3b8bcd523e47d544da589be -- src/Xemele Directory.Build.props Directory.Packages.props global.json benchmarks/Xemele.Benchmarks/M4-evaluate` exit0 | Actual clean source candidate a5159eb; unchanged verified performance inputs, no remeasurement |
| `python3 tools/planning/tasks.py validate` / `git diff --check` | 150 tasks valid / exit0 | Actual a5159eb before final metadata; repeated after final own task update |

The coordinator independently inspected successful106/156 XML, differential log and fuzz JSON. The successful raw outputs stay transient and are removed after this durable summary/evidence commit. The gate owns cumulative earlier-milestone verification; none is claimed here. Public API897 and public package consumers remain the verified evaluator prerequisite, not an unrun corpus check.

## Actual preparation failures

These were corrected authored harness issues, not hidden runtime failures. They were observed by the worker; the coordinator did not independently inspect their overwritten historical raw outputs.

- First conformance build failed with three errors: the pattern-only Children collection did not support LINQ directly, and XmlNode has no Name property. The oracle now traverses sibling handles directly and constructs lexical names from Prefix/LocalName. No stale test binary ran after the failed build.
- First test invocation used unsupported `--filter-namespace` and returned3. Runner help returned2 and documented `-namespace`; the corrected invocation executed all106 tests.
- First authored differential fixture attempted `xmlns:q=''`, invalid under XML Namespaces1.0. Both parsers rejected it; the run had156 total/59 failed. The fixture now retains inherited q and uses valid default `xmlns=''`; its explicit namespace expectation is p,q,xml. The frozen105-vector inventory was never edited. Corrected156-test run passed. No product repair was required.
- First fuzz build failed with two errors: switch-expression precedence and setting a struct property through a returned FirstChild value. Parentheses and a local node handle corrected them; the successful build preceded the fuzz run.

No skipped or unrun check is represented as passing. No push or publication occurred.
