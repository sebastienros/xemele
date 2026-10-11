# Provenance checks

Implementation 34b626bf37f58d149fbcf83762aba1e719e168cf adds internal provenance
only. Harness fixes at 390055f and 5c1fad8 change no product source or binary.

Pinned executable `/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet`,
SDK 10.0.400. Release offline restore used `--source /Users/sebastienros/.nuget/packages
--ignore-failed-sources -p:NuGetAudit=false --nologo -m:1`; builds used
`--no-restore -p:NuGetAudit=false -m:1 -p:UseSharedCompilation=false
--disable-build-servers` with MSBuild reuse disabled. Tests and probe invoked
`--fx-version 10.0.11`, with actual FrameworkDescription confirmed by the probe.

- Release tests project and independent provenance probe build: zero errors/warnings.
- Focused new provenance tests: 10 passed, zero failed/skipped/not run.
- Preservation command: `dotnet --fx-version 10.0.11 tests/Xemele.Tests/bin/Release/net10.0/Xemele.Tests.dll -noColor -noLogo -namespace Xemele.Tests.M5CanonicalProvenance -namespace Xemele.Tests.Tree -namespace Xemele.Tests.Recovery -namespace 'Xemele.Tests.M4*' -namespace Xemele.Tests.AsyncInput`: 484 passed, zero failed/skipped/not run, 1.134 s.
- Owned `--control` and `--discover` passed before the first lease; actual .NET10.0.11 Arm64, 3 one-job declared ShortRun NoEmit cases.
- Corrected harness-only build with `-p:BuildProjectReferences=false`: passed.
- `--il`, `--primitive-control`, `--discover` with corrected explicit settings: passed; generated hot-method IL and counter-disabled batch counts inspected.
- Two exclusive measurement lifecycle outcomes and actual inclusive allocations are detailed in [performance.md](performance.md).
- `python3 tools/planning/tasks.py validate`: 153 records passed before measurements; final evidence rerun also passed all 153 records.

The initial default-source restore stalled and its own process group was stopped
before the offline restore. The first test compilation found invalid temporary
struct setter usage, attribute indexing and missing xUnit cancellation tokens in
new tests; these were corrected before successful builds. No failed check is
reported as passing. An attempted API comparison referenced an absent historical
checker binary; it failed before executing a checker. The coordinator then provided the unchanged approved M0 checker. The owned
`check_api.py --checker /private/tmp/xemele-m5-integration/docs/reports/M0-contracts/ApiCheck/bin/Release/net10.0/ApiCheck.dll`
passed, matching all 897 compiled public members against this checkout’s full API
union with explicit .NET10.0.11. The earlier unavailable-checker attempt remains a
failed preparation attempt; it is not this successful check.

Internal consumers must require SourceInputComplete, and require document
ContentRevision >= 0 before capture and unchanged during their operation. Exact
XPath input must reject CapturedContentRevision < 0 or a captured/current mismatch.
The permanent -1 exhaustion sentinel is intentionally unusable. Public XPath
snapshot getters, equality and hashing retain their previous behavior even for
stale snapshots. Structural MutationVersion still has its previous semantics.

Completeness is false only for incomplete recovery outcomes, not every recovered
or diagnostic-bearing tree. Programmatic/strict complete trees default true.
Whole-document clone preserves incomplete source provenance. Tests cover all five
sync/async character/byte/stream transports and postparse mutation/clone behavior.

## Integrated qualification and verification

The earlier484-test command included TreeTests.WarmTraversalAndRepeatedNameInternProbesAllocateNothing outside an exclusive lease. Its actual execution outcome remains disclosed, but that one method is not accepted allocation evidence;483ordinary functional tests remain applicable. Dedicated prepared provenance leases, including the honest first failure and corrected actual successful probe, supply the accepted allocation evidence.

Combined revision `af63f96ee2ebccf00d6d3e8c7b9100721787c5d7`:519focused tests pass including sharedmodel/standalone metadata, excluding that allocation method; actualhost10.0.11 asserted by metadata test. Release0warnings/errors, full921public members matched,153taskvalidation passed. Source-only independent review approved. Exact command:

```sh
'/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet' --fx-version 10.0.11 tests/Xemele.Tests/bin/Release/net10.0/Xemele.Tests.dll -noColor -noLogo -namespace Xemele.Tests.M5CanonicalProvenance -namespace Xemele.Tests.Tree -namespace Xemele.Tests.Recovery -namespace 'Xemele.Tests.M4*' -namespace Xemele.Tests.AsyncInput -namespace Xemele.Tests.M5ValidationModel -namespace Xemele.Tests.ValidationMetadata -method- Xemele.Tests.Tree.TreeTests.WarmTraversalAndRepeatedNameInternProbesAllocateNothing
python3 docs/reports/M5-validation-model/check_api.py
python3 tools/planning/tasks.py validate
```

Coordinator and worker inspected the unleased child payloads before checkout cleanup: actualruntime10.0.11; real warm/measured0B over1000iterations each,positivewarm/measured64000B,consumed21000,identitytrue; both childexits0/exited/drained and stderr empty. These disclosed observations are not accepted allocation evidence and were not remeasured. Productassembly SHA256A83375C14B68D6F2C5C4AFDED4B2E1660A4241A8F4B44CD39BD31916DFF7846E; embeddedversion243860c from preparation build, not falsely relabeled as committed revision.
