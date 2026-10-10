# Per-call resolver cancellation evidence

The task owns one nonpublic session hook and its tests, standalone source/package
sample, compiled hook inventory, and benchmark. It does not implement the core
consumer. `docs/decisions/M2/resolver-cancellation.md` freezes exact semantics.

Use SDK 10.0.400/runtime 10.0.11 with `DOTNET_CLI_USE_MSBUILD_SERVER=0` and the
pinned SDK on PATH. Run `python3 -B docs/reports/M2-resolver-cancellation/verify.py
--report-prefix <unique-attempt>` at committed source. The verifier provisions the
pinned corpus, builds/copies the complete BCL reference output before inherited
builds, runs cumulative M1 contract/compiler/ABI/protocol/foundation checks and
full solution MTP, independent resolver model/API comparisons, direct XML tests,
focused binding and predecessor resolver tests, fresh full-revision/hash BCL
provenance (239 matches and three documented divergences), source and fresh local
package consumers, benchmark build/discovery and standalone correctness oracle,
planning tests/validation, and committed/staged/working/untracked ownership audits.
All ordinary builds use Release, disabled build servers, one MSBuild node and
no shared compilation. Build exit success precedes test invocation. The verifier
never invokes the new allocation mode or any benchmark measurements.

After an explicit coordinator grant, the source sample `--allocation` runs
thread-local warmed borrowed binding, escaped positive allocation and a second
borrowed guard with unchanged GC/JIT settings. The standalone benchmark uses
its declared MemoryDiagnoser/ShortRunJob; no command-line job substitution. This
isolates measurements from unrelated benchmark suites. Existing allocation
regressions remain part of the cumulative full correctness suites; their exact
assertions are retained, without retries or threshold weakening.

Preparation build-attempt1 failed on ten xUnit1051 analyzer errors in uncommitted
new tests. The diagnostic log is retained; calls without a required intentionally
varying token now use TestContext.Current.CancellationToken. Preparation
build-attempt2 passed with zero warnings/errors before the source checkpoint.
Neither preparation build is a runtime correctness or allocation claim.

Every verifier attempt creates a unique report directory containing compressed
raw command logs, result XML, provenance and summary. Failed attempts remain
visible. A successful branch checkpoint is `implemented`; the coordinator alone
integrates, reruns checks and sets `verified`.
