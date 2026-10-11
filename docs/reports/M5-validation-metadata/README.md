# M5 validation declaration metadata

Implementation: `dfa303076cb8d8d2120b91a5a2ce05f38830dc1d`.
Verified prerequisite: M5-contracts integration `e10170f4c913d7de607773e59eda66ffa9afc4ed`, ancestor of assigned base `110f84094e3df287133bf30e709ae96824428f76`; claim `395b26f7aa04b3cbe7f52f16d733a3dff24bcd02`.

`XmlDocument` retains internal nullable standalone and declaration location through two value fields. `DomSink.Declaration` copies them before the character-input decoder-null return. No declaration leaves null standalone and default/unknown location. A declaration without standalone leaves null standalone but retains its declaration location. Manually constructed documents retain unknown provenance. No public API, lexical strings, sidecars, borrowed buffers or later milestone dependency was added.

## Checks

Pinned executable: `/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet` (SDK `10.0.400`). Release checks run with explicit `--fx-version 10.0.11`; runner reports `.NET 10.0.11`, and `ActualTestHostUsesPinnedRuntime` asserts the actual `RuntimeInformation.FrameworkDescription` equals `.NET 10.0.11`.

| Check | Outcome |
| --- | --- |
| Local-cache restore, command below | Pass |
| Release test-project build, command below | Pass; zero warnings/errors |
| New metadata test class | 18 passed; no failures/skips/not-run |
| Focused earlier DOM, decoder-terminal, async-input, encoding, standalone, entity-production, declaration-version tests | 138 passed; no failures/skips/not-run |
| `python3 tools/planning/tasks.py validate` | Pass; 152 tasks validated |
| `git diff --check` | Pass |

Restore/build:

```sh
DOTNET_CLI_USE_MSBUILD_SERVER=0 MSBUILDDISABLENODEREUSE=1 '/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet' restore tests/Xemele.Tests/Xemele.Tests.csproj --source /Users/sebastienros/.nuget/packages --ignore-failed-sources -p:NuGetAudit=false --nologo -m:1
DOTNET_CLI_USE_MSBUILD_SERVER=0 MSBUILDDISABLENODEREUSE=1 '/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet' build tests/Xemele.Tests/Xemele.Tests.csproj -c Release --no-restore -p:NuGetAudit=false --nologo -m:1
```

Focused tests:

```sh
'/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet' --fx-version 10.0.11 tests/Xemele.Tests/bin/Release/net10.0/Xemele.Tests.dll -class Xemele.Tests.ValidationMetadata.ValidationMetadataTests -result-xml artifacts/M5-validation-metadata/focused.xml -noColor
'/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet' --fx-version 10.0.11 tests/Xemele.Tests/bin/Release/net10.0/Xemele.Tests.dll -class Xemele.Tests.Dom.DomTests -class Xemele.Tests.DomDecoderTerminal.DecoderTerminalTests -class Xemele.Tests.AsyncInput.AsyncInputTests -class Xemele.Tests.EncodingCompatibility.EncodingTests -class Xemele.Tests.ConformanceGrammar.StandaloneTransportTests -class Xemele.Tests.ConformanceGrammar.StandaloneOriginTests -class Xemele.Tests.Entities.ProductionTests -class Xemele.Tests.Versions.VersionTests -method- Xemele.Tests.Dom.DomTests.WarmBorrowedNameHitsAllocateZeroAndUseOwnedInternedStrings -method- Xemele.Tests.EncodingCompatibility.EncodingTests.WarmLegacyConversionAndDeclarationMatchingAllocateZero -result-xml artifacts/M5-validation-metadata/preservation.xml -noColor
```

New tests cover standalone yes/no/absent and absent declaration, string/span/byte DOM factories, sync/async streams with one-byte reads and genuine async yields, caller stream lifetime, UTF-8/UTF-16 BOM offsets, Windows-1252 body decoding, invalid standalone syntax and encoding mismatch rejection, internal hook preservation of line/column/offset and identical URI reference, unknown manual-document provenance, and no public metadata exposure. Current public DOM factories have no source-URI input or async character input; URI identity is tested directly at the internal declaration callback, and async coverage uses supported byte-stream input.

Initial default build/restore and a first cache-only restore stalled before generating obj files; both were terminated in their confirmed own process groups. The successful server-disabled local-cache restore above followed, without changing package/project configuration. The runner's `--help` invocation exits 2 as usage output; it was command discovery, not a failing test execution. A first git add/commit was sandbox-blocked; the authorized isolated branch commit succeeded with escalation.

## Allocation applicability

This repair adds no scanning, matching, lexical ownership or decoder selection logic. Its new callback work is scalar/value assignment. `SetterIlContainsOnlyTwoValueFieldStoresAndReturn` inspects the actual Release assembly method body and resolves both stored fields. The complete instruction sequence is:

```text
ldarg.0; ldarg.1; stfld bool?
ldarg.0; ldarg.2; stfld XmlLocation
ret
```

There are no calls, `newobj`, `newarr`, `box`, temporary strings, or buffers in that method. The existing readonly declaration view's properties supply the already-parsed bool? and location; copying the location retains its existing URI reference without allocating/copying a URI. This is generated-code applicability evidence for no added allocation operations in the setter, not a throughput or end-to-end allocation measurement.

On the tested 64-bit host, `Unsafe.SizeOf<bool?>()` is 2 bytes and `Unsafe.SizeOf<XmlLocation>()` is 32 bytes, both asserted by the test. These fields add 34 bytes of logical document field payload. Actual object size can grow by a different amount because of alignment and field layout; exact retained heap size was not measured. The URI reference can retain the existing URI for the document lifetime. Document construction was already an owned allocation boundary; it now includes these fields. No claim of unchanged per-document allocated byte totals is made.

The two earlier allocation measurement methods were explicitly excluded from the preservation command because no exclusive measurement lease was held. Their unchanged source-applicable prior evidence is preserved. No benchmark or allocation workload was run, no result was inferred from allocation dashes, and no raw benchmark outputs are committed. Broad milestone integration/API/preservation checks remain the coordinator's gate responsibility.

## Integrated verification

Combined revision `097d9f06163b88e2fdadd9560a863fc982c736ad`: Release build0warnings/errors,153taskvalidation and132tests passed on actual10.0.11 with no errors/failures/skips/not-run. Coordinator reviewed exact owned scalar setter/hook. Focused command (no allocation lease held):

```sh
'/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet' --fx-version 10.0.11 tests/Xemele.Tests/bin/Release/net10.0/Xemele.Tests.dll -class Xemele.Tests.ValidationMetadata.ValidationMetadataTests -class Xemele.Tests.DomDecoderTerminal.DecoderTerminalTests -class Xemele.Tests.AsyncInput.AsyncInputTests -class Xemele.Tests.EncodingCompatibility.EncodingTests -class Xemele.Tests.ConformanceGrammar.StandaloneTransportTests -class Xemele.Tests.ConformanceGrammar.StandaloneOriginTests -class Xemele.Tests.Entities.ProductionTests -class Xemele.Tests.Versions.VersionTests -method- Xemele.Tests.EncodingCompatibility.EncodingTests.WarmLegacyConversionAndDeclarationMatchingAllocateZero -noColor
```
