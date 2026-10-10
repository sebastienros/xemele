# Third-party notices

Original Xemele code and these original contract fixtures are MIT licensed under
LICENSE. No third-party parser/runtime implementation is copied by M0-contracts.

Build/test dependencies retain their licenses and notices: .NET SDK/runtime and
Roslyn (Microsoft, MIT), xUnit.net (Apache-2.0), Microsoft.Testing.Platform (MIT),
and BenchmarkDotNet (MIT). Central package versions and restored NuGet metadata
identify exact resolved artifacts. Tool dependency licensing does not relicense
upstream conformance corpora or reference executables.

Reference engines, corpus data and adapters require immutable provenance,
checksum and their own license/notice in each module manifest or runner report.
libxml2 is an external MIT reference; xmloxide and each selected corpus require
review of the actual pinned artifact before redistribution. They are not runtime
dependencies of this managed toolkit. A corpus lacking confirmed provenance or
redistribution rights remains unavailable, not silently included under MIT.

Performance guides link to SearchValues documentation, Andrew Lock's explanation
and StackExchange.Redis AsciiHash as technical references. No article text,
Redis generator/code, or reference parser code is imported into the runtime or
these fixtures. Future imports must add their precise attribution and license
before integration.
