# M6 CLI implementation

Seven statically registered commands compose only verified prior modules. CLI code is internal; this task adds no library API. `parse` emits compact XML without a declaration; `format` emits indented XML without a declaration. XPath scalars use the prior invariant XPath lexical conversion. Node sets are emitted in document order, one serialized result per line. Attribute and namespace results use name/value XML lexical representations. UTF-8 outputs have no BOM.

Every result is privately staged in an operation-owned memory stream before stdout publication or a same-directory temporary file and atomic replacement. This is a deliberate CLI result ownership boundary, not a streaming or allocation-free promise. The library parser, query and serializer token algorithms are unchanged. Explicit primary input/output/catalog/DTD paths never grant nested access. The separate local resolver resolves every path component's symlinks and checks the final path under the canonical allowed root; nonfile resources are denied. Catalog mapping is distinct from access permission.

Final source revision: `50b2c2f93f4478ce290f61cb270bbcbb2c430f7b`. The verified source-aware DOM loader receives the explicit base URI or named primary file URI; the DTD compiler receives the named DTD URI. These bases establish identity, not access grants. Initial draft failed compilation for an incorrect XPath exception name and ambiguous target-typed serializer options; corrected. Initial process controls exposed cleanup masking a missing output directory invocation failure; corrected. Subsequent 50 process controls passed, including XPath `]]>` escaping, attribute tab/newline/carriage-return preservation and inherited namespaces. Primary-file and granted-resource read/disposal I/O controls are exercised separately by the owned sample; a first sample build flagged CA2022 on an intentionally faulting read whose count was ignored, corrected to an explicit discarded result. Cancellation and standard-stream lifetime controls pass on .NET 10.0.11. Initial NativeAOT preparation was stopped after a managed build stall, produced no native artifact, and is not passing native evidence. A separate elevated NativeAOT preparation generated a genuine arm64 Mach-O executable and seven-command help smoke, and preceded the successful final coherent source-URI-aware publish recorded below.


## Final evidence

| Check at source revision | Result |
| --- | --- |
| Forced Release CLI, owned sample and approved API extractor builds | Passed, zero warnings/errors; SDK 10.0.400 and runtime 10.0.11 |
| Managed process controls | 57 passed; explicit `--fx-version 10.0.11` |
| Owned linked sample | Seven commands, relative general/parameter entities, cancellation, I/O boundary/exception identity and standard-stream lifetime passed; actual `.NET 10.0.11` |
| Compiled library API union | 1,054 members matched; CLI adds zero library members |
| NativeAOT publish | Actual osx-arm64 Mach-O executable; Microsoft.DotNet.ILCompiler and platform compiler packages 10.0.11 |
| Native process controls | All 57 passed, including seven commands, grants, rejection and relative entities |
| Planning and whitespace | 163 tasks validated; `git diff --check` passed |
| Scoped process inspection | No worker build/native/smoke/API children remained |

The final native executable SHA-256 is `b5f229009e02870e8bc57976e04c132a5fd4fdfae40e3bbff112631f7e0d276a`. The managed CLI DLL SHA-256 is `3f62c4b9b60be2a40d97a422f0ee8f7b834d6783e34342a2a421859ab996e2b8`; the library DLL is `02504829351d46c36895357f2f7691e29779942074393edbae32e7982d0930bb`. Publish used `-c Release -r osx-arm64 -p:PublishAot=true -p:RuntimeFrameworkVersion=10.0.11 -p:NuGetAudit=false --source /Users/sebastienros/.nuget/packages --disable-build-servers -m:1`. Ignored raw logs, control results, identity manifest and native artifact remain in `artifacts/M6-cli/` for coordinator inspection.

No throughput, latency, allocation-free, or measured allocation claim is made. CLI staging is an explicitly allocated final-result boundary; existing library token algorithms are unchanged. No measurements or inherited full suite were run. Cumulative preservation checks belong to the integration/gate.
