# Shared parser budget repair validation

Source revision: `d850d4fdd3a291db9acae25b3938417fb6040522`.

The two constructor changes accept one sequential operation budget and preserve the default independent budget. The byte adapter uses its authoritative options for decoder, core and DOM sink, carrying final source URI through framing and diagnostics. No parser scan or callback algorithm changes and no public API changes.

Pinned host `/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet`, SDK 10.0.400, Release builds with RuntimeFrameworkVersion=10.0.11 and no incremental reuse. Direct xUnit invocations used --fx-version 10.0.11; XML and runner report actual .NET 10.0.11.

Six focused tests passed: shared exact Unicode scalar and expansion counters, depth release, authoritative byte options, declared Latin1 framing/final source, independent defaults, external entity resolver/expansion/cleanup. Earlier SyntaxTests, ContentTests and ResolverTests passed all 107 cases. Approved scoped compiled API extractor matched 951 members. Planning validated 162 tasks; git diff --check passed. Bounded owned process groups complete and scoped process scan found no repair build/test children.

Initial test build failed due nonexistent TextContent and xUnit missing cancellation token; corrected in d1781e6. Subsequent forced builds pass with zero warnings/errors. Ignored raw logs/XML are retained in artifacts/M6-parser-budget until integration inspection.

Independent source-only contract review approved the narrow diff and tests; it performed no executable checks. No allocation or timing measurements ran; constructor injection adds no steady-state scanning algorithm, and cumulative allocation regressions remain the milestone gate.
