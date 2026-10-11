# Completed-source revision repair

Implementation/test revision `465761d4257a895fa9bc5a9a1457a63fad5434ed`.

An internal getter-only nullable `ValidationSourceContentRevision` starts unknown.
`MarkValidationSourceComplete` copies the current `ContentRevision`. DomSink.EndDocument
checks the original cancellation token first, then stamps only strict operations
(`!budget.Options.Recover`). Recovery mode remains conservatively unknown even when
well-formed or successfully repaired; incomplete recovery also retains the existing
SourceInputComplete=false state. No EndDocument means no stamp. Programmatic and
cloned documents retain unknown source provenance. No public API or lexical sidecar
was added, and parsing never rewrites the stamp after completion.

Consumers must require SourceInputComplete, a present nonnegative source revision,
and equality with the current ContentRevision. All committed DOM mutations invalidate
that equality, including value/prefix changes and changes later restored to their
original values. The -1 revision-exhaustion sentinel never establishes trustworthy
provenance. The unchanged standalone numeric-character-reference whitespace and CDATA
boundary tests preserve original content across strict character/byte/sync/async input;
actual DTD validation consumption remains the facade task's responsibility after
integration verification.

## Actual checks

Pinned SDK10.0.400. Restore used the existing local cache, server/node reuse disabled:
`NUGET_SCRATCH=/private/tmp/xemele-provenance-nuget DOTNET_CLI_USE_MSBUILD_SERVER=0 MSBUILDDISABLENODEREUSE=1`
and pinned dotnet `restore tests/Xemele.Tests/Xemele.Tests.csproj --source /Users/sebastienros/.nuget/packages --ignore-failed-sources -p:NuGetAudit=false -m:1`.
Release test and approved API checker builds passed with zero warnings/errors, using
`-m:1 -p:UseSharedCompilation=false --disable-build-servers`; test build used --no-restore.
An initial test-only compile error attempted an IEnumerable assertion on ref-struct
XmlNodeChildren; a direct foreach corrected it before the implementation commit.

Actual executable command prefix for tests:
`'/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet' --fx-version 10.0.11 tests/Xemele.Tests/bin/Release/net10.0/Xemele.Tests.dll`.
The runner and dedicated RuntimeInformation.FrameworkDescription assertion confirm
actual .NET10.0.11, rather than inferring it from installed runtime folders.

Combined test arguments:
```
-class Xemele.Tests.M5ValidationSourceProvenance.SourceProvenanceTests
-class Xemele.Tests.ValidationMetadata.ValidationMetadataTests
-class Xemele.Tests.M5CanonicalProvenance.ProvenanceTests
-class Xemele.Tests.Dom.DomTests
-class Xemele.Tests.Dom.PersistenceTests
-class Xemele.Tests.Recovery.RecoveryTests
-class Xemele.Tests.Recovery.RecoveryLifecycleTests
-class Xemele.Tests.AsyncInput.AsyncInputTests
-method- '*WarmBorrowedNameHitsAllocateZeroAndUseOwnedInternedStrings'
-noColor
```
191 passed, zero failed/skipped at the implementation revision (13 dedicated cases).
The existing allocation method was explicitly excluded; no counter/timing experiment
was performed. Cases cover single-byte suspension boundaries, strict completion,
standalone character references/CDATA, mutation categories, unknown programmatic/clone
state, complete/repaired/partial recovery, completion and async-read cancellation,
permanent negative sentinel, generated getter/setter code and internal-only visibility.

`python3 docs/reports/M5-validation-model/check_api.py` reused the approved compiled
extractor with direct actual10.0.11 execution and matched all 921 prior-baseline members.
The branch does not include the concurrently integrated canonicalization API lane.
`python3 tools/planning/tasks.py validate`: all155 task records valid.
Broader combined milestone checks remain the coordinator's integration/gate work.

## Storage and allocation applicability

Actual Release getter IL is 7 bytes: ldarg.0, ldfld Nullable<long>, ret.
Actual Release scalar-copy method IL is 18 bytes: ldarg.0, ldarg.0,
call get_ContentRevision, newobj Nullable<long> constructor, stfld Nullable<long>, ret.
The newobj targets a value-type Nullable<long> constructor and constructs the value
inline; it is not managed heap-object construction. No boxing, newarr, lexical string,
span retention, loop, scanner or dynamic diagnostic construction is introduced.
Unsafe.SizeOf<long?> is16 bytes on the tested runtime: the logical additional value
payload per document. This is not a claim about total object size, padding, measured
heap delta, throughput or bytes per parse. The callback adds cancellation observation
and a scalar store once at completion. No additional concrete allocation hot work
requires a counter/benchmark experiment in this bounded repair. Parser persistence
allocations remain governed by their existing owners. No latency/performance result
or cross-platform result is claimed.
