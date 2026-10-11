# Verified M4 XPath checkpoint

Combined integration checkpoint `a0354ad6a8ea7a8f2109db3d5b9f701e39e939f9` contains all11 M4 tasks including the explicitly owned API-fragment repair. Complete solution, original XML grammar driver, API extractor, standalone public demo and XPath fuzz driver built in Release on SDK10.0.400 with zero warnings/errors. Actual executable checks selected .NET10.0.11 on macOS15.8.1 Arm64. All6368 cumulative tests passed without failures/errors/skips/not-run. Full test execution occurred at `ca92b718bc883696be792e0e9dc13bf51eeaa462`; subsequent repair changes only declaration-free API files and coordination metadata. `git diff --exit-code` proved runtime/test/config/driver inputs unchanged. Successful tests/grammar/fuzz were preserved; the previously failed official API check alone was repeated after its owning repair.

| Suite | Passed | Failed/errors | Skipped/not-run | Test identity SHA256 |
| --- | ---: | ---: | ---: | --- |
| Xemele.Tests | 5525 | 0 | 0 | `a748a74bee2e38784ddbff74339e1704f91bf8d4ce404fde5317b823a03e8650` |
| Xemele.Conformance.Tests | 374 | 0 | 0 | `486b9932d17382e6d0c7a1c0da543151458aa481f7cbac59cb3fd8e75bc9eaf6` |
| Xemele.Differential.Tests | 469 | 0 | 0 | `96e743a46cdd84fd07ba755228341ba81b0d1eccb042b59c5936c9dcff79cd32` |

XPath coverage includes353 focused model/compiler/navigation/functions/context/evaluator tests, all105 byte-identical frozen vectors plus integrity guard, and156 pinned differential tests. Deterministic seed1295276112 passes768 generated/768 reused BCL comparisons,128 invalid grammar,32 mutation/snapshot/reuse checks,32 exact/one-over quota pairs and64 cancellation-token identity controls. Attribute/namespace membership and product order are checked separately from implementation-dependent BCL order. The documented supplementary-character System.Xml UTF-16 deviation remains explicitly classified against the frozen Unicode scalar contract; no vector is excluded or expectation changed.

The unchanged original XML source-context grammar lane passed all2585 cases:1394 accepted/1191 rejected,3386 hash-verified resources,1394 bulk-decode/all-callback-pause models,409 original-suite output models and15 unchanged authored effective-XML10 overrides. All771 known reference disagreements were resolved by frozen normative rules; no new exclusions. The earlier no-primary-base/streaming preservation tests remain in the cumulative suites.

Official discover/approved M0 extractor matched897 compiled public members:720 preserved legacy plus177 frozen XPath rows. The failed first official discovery rejected three internal lanes' header-only API artifacts; M4-api-fragments removes exactly those files under explicit ownership, preserving every declaration and the official nonempty rule. Its intermediate graph validation failure is recorded in the owning repair report. Source-only gate harness/demo review approved ca92b71; execution results above are coordinator-observed actual evidence.

Local `Xemele.1.0.0-m4-preview.nupkg` SHA256 `ebb7bfb598093f5eec1750deee44baad7639fd11c93b75ebb5416e27ad35c369` contains only net10.0 runtime assets; assembly SHA256 `fb2beaf607596e37a722c3bdb16300f19e195b60aa7f8d4d38ea526e97c0e846` matches the API-tested DLL. A fresh empty-cache independent public consumer restores only from the local feed, has no repository/friend references, builds with zero warnings/errors, and exactly matches source demo output. Real assertions cover XPath namespaces/scalars/sum/reverse proximity/owned result metadata/reuse plus DOM, callback/subclass SAX, chunked push, sync/async reader/writer, suspended async DOM, DTD defaults and recovery. Package remains ignored/local and unpublished. CSSM informational output and missing package readme notice did not cause warnings/errors or failure.

## Preserved allocation and streaming evidence

| Ownership boundary | Verified evidence retained |
| --- | --- |
| Model primitives |128 uniform warmed batches0B; cold/owned arrays/formatting separate; M4-model |
| Construction/context copying |Cancellation/accounting controls; context64 warm/steady batches0B, cold6944B, owned static1968/dynamic728Bop; M4-model-construction/context |
| Compiler primitives |32 warmed/steady batches0B; owned compile3552/context3176Bop;25 actual BDN cases; M4-compile |
| Navigation primitives |128 warmed/steady batches0B; snapshots/axis/results explicitly owned;10 actual corrected-runtime BDN cases; M4-navigation |
| Functions |128 warmed/steady batches0B; owned node-set/string boundaries disclosed;13 actual BDN cases; M4-functions |
| Whole query composition |Two compile/six evaluate workloads, positive owned allocations and complete distributions, cold223968B, retained aggregate1519664B;8 actual BDN cases; M4-evaluate/corpus |
| Earlier streaming |M3-gate five1GiB modes with preEOF progress and whole managed heap≤383776B; genuine suspended async costs and owned setup separate |

All new benchmark classes declare `[MemoryDiagnoser, ShortRunJob]`, no CLI job flags. Complete pinned source/binary/SDK-runtime-host identities, exclusive leases, direct lifecycle outcomes, and post-release actual inspection remain in owner summaries. Positive inclusive BDN totals despite rounded dashes and original incorrect-runtime/harness failures remain disclosed; none is subtracted or relabeled zero. Whole query capture/frames/results deliberately allocate. No new optimization or benchmark run was introduced by this gate or corpus; source-equivalence preserves applicable measurements. Timings remain informational single-host Arm64 evidence; M9 retains broader x64/Arm64/platform/trimming/AOT/release gates. Earlier measured streaming source/config is unchanged by XPath-only additions, so successful1GiB observations were not repeated to change labels.

Planning151 and owned whitespace checks passed. All prerequisites are integrated/verified, no later milestone is needed, and only summaries/analysis are committed. Coordinator inspected raw XML, actual grammar/fuzz/API/package output and build logs; transient raw artifacts are removed after durable evidence. Sole-writer successor handoff follows only after this gate is marked verified.
