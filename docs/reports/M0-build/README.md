# M0 build handoff

Implementation revision: `5abe06eab83d09916c3eede99262be18ef07e736`. Clean checkout: `/private/tmp/xemele-M0-build-5abe06e`.

Run `python3 docs/reports/M0-build/verify.py` from an SDK-equipped checkout. The script writes `artifacts/M0-build/verification.txt`; the committed transcript is [verification.txt](verification.txt).

Ten projects build for net10.0 only. SDK 10.0.400 is pinned with roll-forward disabled. Central direct dependency pins are Microsoft.NET.Test.Sdk 18.0.1, xunit 2.9.3, xunit.runner.visualstudio 3.1.5 and BenchmarkDotNet 0.15.8. Restore used the existing machine NuGet cache and configured sources; this was a clean source checkout, not an empty package cache.

The library has no source/runtime public types or native runtime dependencies. The package contains only `lib/net10.0/Xemele.dll` as its library payload. Other projects are not packable. SDK default globs preserve separate feature-owned source files.

The CLI registration convention is internal `sealed partial CommandRegistry` in namespace `Xemele.Tool`, with optional instance hooks `RegisterXmlCommands`, `RegisterHtmlCommands`, and `RegisterValidationCommands`. Feature partial files call `Register(string name, string description, Func<string[], int> execute)`; delegates receive arguments after the command name and return the process exit code. Help lists registered commands in ordinal order; absent hooks are erased at compilation. Unknown tool/fuzz/corpus commands return 2.

All four executable shells handle no arguments, `--help`, `-h`, and `help`. BenchmarkDotNet is wired to discover future benchmark cases from its assembly. Three test shells discover and execute successfully with zero tests; this is infrastructure validation, not runtime behavior coverage. Contract and utility projects are empty libraries. No benchmark or allocation claims apply because no runtime hot path exists.

Checks passed: restore; Release build (zero warnings/errors); test discovery and execution (zero tests); sixteen help invocations; three unknown-command exits; pack and payload inspection; dotnet-inspect exported type count (zero); task validation (93 tasks).

Environment note: the first sandboxed restore stalled and was cancelled; successful checks used SDK/cache access outside the workspace sandbox. Packing prints an informational missing-readme notice. No package is published. License/distribution metadata remains a foundation contract decision; no license was invented in bootstrap.

Coordinator follow-up: integrate both worker commits, rerun the verification script and task validation on the combined tree, then record integration evidence and set verified. No changes outside owned paths requested.
