# Release requirements

Publication is a separate explicitly authorized operation. Creating and checking
a package does not imply uploading it. Pushing a valid `vVERSION` tag requests
NuGet.org publication after the full correctness matrix, distribution checks, and
any `release` environment approvals. All release tags, including prereleases,
must identify a commit contained in `main`. Pushes to `main` separately publish
continuous previews of the library and portable tool to Feedz after Linux checks;
pull requests and maintenance branches never publish.

- **Compatibility:** compose API fragments and compare actual assembly signatures;
  check documentation, migration examples, exceptions, nullability, ownership, and
  compatibility with published versions. Never regenerate a baseline to hide a mismatch.
- **Performance:** follow the [targets and measurement protocol](../performance.md).
  Compare each reference separately, distinguish allocation from retained memory
  and RSS, and document exceptions to targets. Shared-host timing is informational.
- **Distribution:** deterministic packages, license/notices, Source Link, fresh
  package consumption, trimming, and NativeAOT execution. See the
  [packaging guide](../../tools/Packaging/README.md). The CLI ships a .NET 10
  tool router with Native AOT implementations for `linux-x64`, `osx-arm64`,
  and `win-x64`, plus a framework-dependent `any` fallback for other supported
  targets. Standalone native and portable archives accompany the packages.
  Verify automatic tool selection and extracted archive execution on each
  native host and on a runtime-backed target before opting into publication.
- **Robustness:** follow the [testing requirements](../testing.md), including
  applicable corpora, differential explanations, sustained fuzzing, minimized
  regressions, cancellation, and resource budgets.

Correctness checks cover Windows, Linux, and macOS; both x64 and arm64 require
actual execution before release. Compile-only cross-target builds do not establish
execution coverage. Record the actual RID, toolchain, corpus/benchmark revisions,
and limitations with results.

Known exclusions accompany supported profiles. Missing checks, unresolved
failures, and unmet thresholds stay explicit; neither declaration compilation
nor package creation establishes universal conformance or release readiness.
