# M6 XML toolkit integration

Tested source: `01574c80d23fe05a4af425dd5b8b16cb88642781`, based only on verified M6 dependencies. Independent source-only review approved the corrected harness at that revision. No runtime, shared project, public API, or earlier task files changed.

The public-only `samples/XmlToolkit` example loads a caller-owned primary stream, maps URI requests through a catalog, asynchronously includes XML and text through a closed offline allowlist, and honors the returned final URI for the nested text request. It enforces two aggregate resources, a depth of two and 4096 external bytes, checks exactly-once transferred-stream closure, and preserves the source document. It then composes DTD validation, native XPath, serialization/canonicalization, a BCL reader and a BCL navigator. Fixtures under `tests/corpora/catalogs` and `tests/corpora/xinclude` contain independent literal expected output:

```xml
<r xmlns:xi="http://www.w3.org/2001/XInclude"><item>offline &amp; safe</item></r>
```

The source URI and `XIncludeOptions.BaseUri` are explicit caller inputs; catalog mapping grants no network access. XInclude produces a synthetic document whose standalone/input provenance is incomplete. The example first asserts that direct DTD validation returns `DTD.IncompleteInput`, then serializes and strictly reparses complete input before validating. This materialization is an intentional allocated boundary; it makes no zero-allocation claim. No runtime hot-path algorithm changed.

## Passing checks

All commands ran from the isolated worktree with the pinned host `/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet`. Repository builds used SDK 10.0.400, Release, `--no-restore --no-incremental -p:RuntimeFrameworkVersion=10.0.11 --disable-build-servers -m:1`; restores used `-p:NuGetAudit=false --source /Users/sebastienros/.nuget/packages`. Managed executables used `--fx-version 10.0.11`. Builds passed without warnings/errors.

- `build samples/XmlToolkit/XmlToolkit.csproj` and direct sample execution with `tests/corpora`: frozen expected output; actual `.NET 10.0.11`, Arm64.
- `build tests/Xemele.Conformance.Tests/Xemele.Conformance.Tests.csproj` and direct runner `-method 'Xemele.Conformance.Tests.Integration.ToolkitTests.*' -result-xml artifacts/M6-integration/tests.xml`: 3 selected, passed, none skipped. Controls cover composed output, canceled entry, and rejection/closure at the nested aggregate resource limit.
- `python3 samples/XmlToolkit/cli.py`: seven real CLI processes pass parse, format, XPath, DTD validation, canonicalization, catalog mapping, and XInclude with an explicit local-root grant. Scalar/serialized outputs have fixed independent expectations; Python's independent XML parser checks included structure and content.
- `python3 tests/PackageSmoke/Xml/run.py`: external managed, trimmed, and genuine native package consumers pass the same composition. Every source/package/trim/native stdout is byte-identical. Consumer source lives outside the repository, uses only public APIs and a package reference, and has no project references or friend assembly identity.
- `python3 docs/reports/M5-validation-model/check_api.py`: matched all 1054 compiled API members, preserving earlier rows.
- `python3 tools/planning/tasks.py validate`: 163 tasks valid. `git diff --check` passed; scoped process inspection found no remaining owned integration workloads.

## Package and deployment identity

The unpublished local feed contains `Xemele.0.6.0-integration.nupkg` plus links to approved cached platform/compiler `.nupkg` files. The external consumer package cache was initially absent. Restore used that local feed exclusively with an explicit fresh `--packages` path. The copied repository `global.json` and an actual external `dotnet --version` check prove SDK 10.0.400 before consumer restore/build/publish. No remote restore, publication or external corpus download occurred.

External consumer directory retained for coordinator inspection: `/private/tmp/xemele-m6-package-7toho6_e`. Native and trimmed restores target `osx-arm64`; publishes use `--no-restore --disable-build-servers -m:1 -t:Rebuild,Publish`, `--self-contained true`, and respectively `PublishAot=true` or `PublishTrimmed=true`. Each publisher has a 180-second bound; ordinary phases have a 120-second bound and execution 60 seconds. All subprocesses start owned process groups with cleanup in `finally`, including interruptions/errors and successful completion. The AOT executable ran successfully, reports `.NET 10.0.11`/Arm64, is identified by `file` as `Mach-O 64-bit executable arm64`, and has no companion `Consumer.dll`.

| Artifact | SHA-256 |
| --- | --- |
| Source, packaged and restored Xemele DLL (byte-identical) | `8f2010a41a5ebdfdf181ca6f685c4e17ed0246d04fbfe14f26ff54a27f26ce57` |
| Local nupkg | `ef3c9eee7fffe0e2ad4a44c08b95f0fd685ce7ca2d3084eb6e8290ab4386caa0` |
| Native consumer | `1607dc7ea115d1e05b41fbab0c9dd816127917ed77e2d2687fd3e82ea8bea99d` |
| Trimmed consumer apphost | `e92ba5b7fe9663c52fe65c8bbf018f8f7adc8ac47af5b969943d1615b15273ac` |

Exact commands, stdout/stderr, build/publish logs, selected-test XML, package identity and process inspection remain under ignored `artifacts/M6-integration/`. These outputs and both external consumer directories remain until independent coordinator inspection. Scope is actual osx-arm64 deployment; no other platform result is claimed.

## Honest preparation failures and deferred checks

Before the stable revision, the first include demonstration omitted explicit XInclude base options and failed; adding the frozen explicit base corrected it. Direct DTD validation then correctly rejected synthetic incomplete provenance; the final example preserves this as an executable control and introduces the complete-input boundary described above.

At source `1049a14fd0a627da70069efc20b39c9e05e9b355`, the first external package preparation passed managed execution on actual .NET 10.0.11 but selected installed SDK 11.0.100-rc.1.26425.128 outside the repository and failed trimmed publish with MSB1001 because `publish` does not accept `--no-incremental`. It produced no passing trim/native evidence. The corrected revision pins/validates the SDK and uses explicit Rebuild/Publish targets. Its complete package run passed. Earlier raw attempt logs are preserved in `artifacts/M6-integration/attempt-1049`, and the failed consumer directory is `/private/tmp/xemele-m6-package-d5unktp2`.

No full inherited suite, performance measurement, supported-platform matrix, or final cumulative package gate ran under this task. Those checks belong to M6-gate. Earlier milestone runtime/API source remains unchanged; the full cumulative gate must establish integrated preservation. No shared-file follow-up is required.
