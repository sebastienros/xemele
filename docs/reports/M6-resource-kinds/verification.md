# M6 resource kinds branch verification

Implementation: `5ce5e3c64e4a6f85a09c6f44508e75119b00d54c`. Claim `fb19185edb63e2e81483ee3fd0f06a03917505dc`; integrated base `c1561c06b783cb7a5f5790ef26cdae8a8c10ca49`, both ancestors. Only enum fields changed in runtime; resolver/session implementation unchanged.

SDK 10.0.400; generated test runtimeconfig requests 10.0.11. Direct runner banner and the executed ActualRuntimeIsPinned assertion establish actual `.NET 10.0.11`.

Commands executed from the isolated checkout:

```sh
'/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet' restore tests/Xemele.Tests/Xemele.Tests.csproj -p:RuntimeFrameworkVersion=10.0.11 -p:NuGetAudit=false --source /Users/sebastienros/.nuget/packages --disable-build-servers -m:1
'/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet' build tests/Xemele.Tests/Xemele.Tests.csproj -c Release --no-restore --no-incremental --disable-build-servers -m:1 -p:RuntimeFrameworkVersion=10.0.11 -p:NuGetAudit=false
'/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet' restore docs/reports/M0-contracts/ApiCheck/ApiCheck.csproj -p:RuntimeFrameworkVersion=10.0.11 -p:NuGetAudit=false --source /Users/sebastienros/.nuget/packages --disable-build-servers -m:1
'/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet' build docs/reports/M0-contracts/ApiCheck/ApiCheck.csproj -c Release --no-restore --no-incremental --disable-build-servers -m:1 -p:RuntimeFrameworkVersion=10.0.11
'/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet' --fx-version 10.0.11 docs/reports/M0-contracts/ApiCheck/bin/Release/net10.0/ApiCheck.dll emit src/Xemele/bin/Release/net10.0/Xemele.dll artifacts/M6-resource-kinds/actual-api.txt
'/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet' --fx-version 10.0.11 tests/Xemele.Tests/bin/Release/net10.0/Xemele.Tests.dll -class Xemele.Tests.ResourceKinds.ResourceKindTests -list tests
'/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet' --fx-version 10.0.11 tests/Xemele.Tests/bin/Release/net10.0/Xemele.Tests.dll -class Xemele.Tests.ResourceKinds.ResourceKindTests -result-xml artifacts/M6-resource-kinds/focused.xml
'/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet' --fx-version 10.0.11 tests/Xemele.Tests/bin/Release/net10.0/Xemele.Tests.dll -method Xemele.Tests.Resolution.ResolverTests.PublicShapeAndOwnership -method Xemele.Tests.Resolution.ResolverTests.RequestValidationHappensBeforeResolver -method Xemele.Tests.Resolution.ResolverTests.RelativeRequestUsesFinalDeclarationUriAndPreservesLexicalIds -method Xemele.Tests.Resolution.ResolverTests.DefaultAsyncFallbackChecksCancellationWithoutTaskRun -method Xemele.Tests.Resolution.ResolverTests.GenuineAsyncOverrideAndAsyncReadsAndDisposal -result-xml artifacts/M6-resource-kinds/earlier-focused.xml
python3 docs/reports/M5-validation-model/check_api.py
python3 tools/planning/tasks.py validate
```

Both builds: zero warnings/errors. Tests: 20 new focused cases + 5 selected earlier resolver cases passed, zero failures/skips/not-run. The direct runner's discovered `-class`/`-method` filters selected only these cases; no inherited allocation test or full-suite workload ran.

Approved compiled extractor emitted 951 members. A dictionary comparison against all prior API fragments found all 948 earlier declarations identical and precisely Catalog=2, XIncludeXml=3, XIncludeText=4 added. Task baseline contains only those three actual compiled rows. Composed union comparison matched all 951. Planning validated 161 task records.

Initial default-source restore and a concurrent cache-only retry stalled; cancellation through sessions did not finish promptly. Read-only PID/cwd inspection established both exact processes belonged to this checkout; only PIDs 56690 and 56906 were terminated. Retry used a 90-second bounded Python subprocess with a new session, `-m:1`, build servers disabled, and own-process-group cleanup finally; restore exited 0 within a second. NuGet audit disabled for this offline restore only; no tracked build configuration changed. Cache-only restores printed CSSM_ModuleLoad informational output, then succeeded. Final process inspection showed no worker restore/test children, only the inspection command itself.

Builds and baseline emission preceded the implementation commit with identical source bytes; focused executions and union comparison ran at the recorded implementation revision. Allocation/timing evidence is inapplicable to the enum-only runtime change. No benchmarks, cache deletion, pushes, or publications occurred. Branch evidence is not integrated verification.
