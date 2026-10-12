# Harness correction — SOURCE ONLY; current correction execution UNRUN

Runtime/compiler, original fixtures, BenchmarkDotNet and the 24-route probe
remain exactly at 8cb31895a01fccf945dc2b2ff5a58ef39fd14fab. Tests retain the original
acceptance assertions with the two explicit cancellation-token corrections below. Compiler SHA256 remains
843e59a35be16bd838f31c9b5859223e3043f179a865baf72bbe7d70d566bed3. Historical
source-identities and source handoff are immutable. The current owned harness is
separately pinned in harness-source.json; only the coordinator can create an actual
harness-review.json with SOURCE PASS and the manifest's exact hash.

supervision.py is the exact accepted 407b228d8b4741445eaf928a44aedd4de85aacaac894e6ad23b8ed8071395903
copy. nested_ownership.py retains the fe1db7d70cc4b10a04aaa5ac8107cd69a95f7a16e21a0888892ef70c5f05a128
template's functions, adapting only OUT and REGISTRIES. No peer checkout dependency.
Registered sessions publish intent, atomic PID/PGID, and complete ancestor ACK
before exec. Normal and abnormal child completion reap/drain groups and close logs.
A lifecycle failure prevents the next command. TERM/INT unwind root cleanup;
intermediate-parent SIGKILL leaves descendants owned by enclosing registries.

All passing candidates use finite publication: crossing the hard deadline
quarantines the candidate and removes its acceptance path. Final acceptance also
requires the enclosing controller-release.json, binding source/review, payload,
whole-family cleanup and terminal-cleanup receipts. Check has 840 seconds total;
prepare 440; controls 100; measurement 150 enclosing/120 inner (100 work/20 cleanup).
Each normal correctness/build child has 180 seconds including 10 cleanup seconds.
No resume, build overlap exemption, or previously failed attempt reuse.

## Artifact-policy correction

The content-addressed binary archive is removed. No runtime, host or generated
binary bytes are copied into evidence. Actual active files are hashed before and
after execution, with directory membership and fresh argv/lifecycle receipts.
Every command, including builds, verifies all previously executed binary inputs;
a later rebuild changing their bytes or membership fails. Test/extractor/probe
builds and normal/XML-only consumers use distinct active output roots. Restore
and build receive matching SDK UseArtifactsOutput/ArtifactsPath and global
OutputPath flags; intermediate directories separate referenced projects. The
API union uses the same canonical full-row validation as the established M5
checker, with an actual extractor/runtime in the owned active roots. All 53 API
files remain bound by membership and hashes. SDK output relocation is source-only,
unverified; see [SDK artifacts output](https://learn.microsoft.com/en-us/dotnet/core/sdk/artifacts-output).

After verified whole-root group absence and closed logs, terminal_cleanup removes
owned active bin/obj outputs, raw XML/API/control/counter results, derived union,
control scratch and raw logs. This runs on success, ordinary failure and signal
interruption. Canonical sources, original fixtures, package caches and installed
runtime/host files are preserved. Only concise outcomes, lifecycle receipts and
hash/membership identities survive. Deleted files cannot be rechecked later:
terminal receipt consumers explicitly verify recorded deletion and surviving
inputs; they never interpret summaries as active-byte readiness. Any newly restored
file at a deleted identity is rejected. Failed or late enclosing controllers still
cannot authorize a child candidate. Cleanup is refused while any owned group may
remain. SIGKILL of the outermost controller cannot run a Python finally block;
the coordinator must recover/drain that root before terminal cleanup. Absence or
expiry alone never supplies a passing controller receipt.

Prepared probe outputs are active only while awaiting their one measurement or
explicit discard. Preparation deletes its raw disabled-control result after
publishing a concise semantic summary; binaries remain actual inputs, fully pinned
with dependency/runtime/host membership. Measurement/discard atomically claim the
preparation before work; claimed preparations cannot be reused. Root compares
active identities and membership before and after each disabled/counter child.
After the entire enclosing family is clean, root alone reads allocation output,
validates 24 routes/32 operations, positive control, consumption and 28 retained
results/seven categories, publishes concise cold/first/warm aggregates and retained
heap deltas, then removes raw output and prepared compiled files. Allocation
summary and release identities are bound by final controller acceptance. No raw
allocation distributions or binary payload archives remain.

## Release sequence

1. Independent coordinator SOURCE PASS for the exact manifest.
2. Root `launch.py --released REVIEW_SHA controls`: all eleven proposed controls
   and enclosing cleanup must pass. The controls cover real normal/timeout/forced
   parent-death families, partial ACK, missing intent, late absence/publication,
   late enclosing acceptance, source/command/API/binary/result mutations, failed
   correctness/diff and no-next. Finite source test seams now cover terminal
   deletion for success/failure/interruption, restored-byte rejection, and refusal
   while ownership remains uncertain. All controls remain UNRUN.
3. Root `launch.py --released REVIEW_SHA check`: fresh owned/affected correctness,
   safe resolver42, datatypes77, full/frozen/additive API checks, normal/true XML-only
   consumers, disabled probe, planning and final diff. A concise terminal receipt
   replaces the transient XML/API outputs after their actual validation.
4. Root `launch.py --released REVIEW_SHA prepare POSITIVE_RECEIPT SHA`: fresh offline
   single-node probe build and disabled semantic control; active prepared.json.
5. Root solely invokes `launch.py --released REVIEW_SHA measure PREPARED_JSON SHA`,
   or `launch.py --released REVIEW_SHA discard PREPARED_JSON SHA` when abandoning
   prepared outputs. No isolation/PARK/ACK/lease campaign is introduced.

Ten Python files parsed statically; planning and diff/source-copy checks only.
No harness imports, executable controls, builds, restores, tests, consumers,
extractors, preparation jobs, counters or measurements have run. Runtime and all
acceptance criteria remain pending integrated verification. Freeze this source
packet for review; no readiness or control pass is implied by syntax/hash checks.

## Outer preparation failure correction — SOURCE ONLY

Preparation retention now requires a successful outer outcome as well as a clean
child family. launch.py passes any outer postcondition error to terminal_cleanup;
that path removes preparation/active and prepared.json, even when familyOutcome
is true. Final controller publication for preparation uses hard-deadline minus
10 seconds, preserving a rollback reserve inside the existing total bound.
Every publication exception revokes the preparation/controller acceptance paths
and purges the active files after verified family absence. A distinct concise
preparation-abort.json records the outer error, removed hashes and the prior
terminal summary hash; aborted controllers cannot authorize readiness even if a
candidate is restored. Payload-hash/finalization failures use the same abort path.

Two additional owned-scratch scenarios inside the existing eleven proposed
controls cover a clean child with a failed outer postcondition, and a clean child
whose actual final publication crosses the earlier acceptance deadline. Both
assert no active binaries, preparation candidate or accepted controller survives;
the late scenario also checks that the hard-deadline rollback reserve remains.
These controls, and every other executable check, remain UNRUN. Only static AST,
identity/source-diff and planning validation are permitted for this correction.


## Failure diagnostics correction — SOURCE ONLY

The coordinator reports the preceding 66d1e4a/b656d780 source passed eleven actual
finite controls (main fc1321e26). Its fresh check then restored tests successfully,
but the Release --no-restore build exited1. Main231609bf2 records verified cleanup;
this worker inspected the existing argv/exit lifecycle receipt only. No tests or
counters launched. Raw logs were already deleted, so the actual compiler cause
remains unknown. Historical control PASS is preserved; this new packet and its
new proving seam are UNRUN and require renewed review before any root retry.

Runner now records exact argv, reviewed source-manifest/source-set hashes and
observed executable/entry/selected SDK/restore-derived input identities BEFORE
launch. The resulting receipt hash is fixed in the child lifecycle record. These
are declared/observed identities, not an exhaustive trace of MSBuild file reads.
After confirmed whole-family absence and closed logs, terminal cleanup consumes
at most64KiB (head/tail) per failed command log, at most12 failed-command summaries,
and at most24 diagnostic excerpts across the attempt, each at most768characters.
Only structured compiler errors/warnings, test-failure headings and exception
messages survive; JSON payloads/arbitrary console output do not become archives.
Truncation and unrecognized/unavailable diagnostics are explicit. Exact failed
argv, exit/reason, lifecycle/input receipt hashes and bounded outer-exception
detail are retained; an input receipt lacking its pre-launch hash binding is
labelled untrusted. Diagnostic summaries are hash-bound by cleanup receipts.
Raw logs and generated outputs are still deleted. A diagnostic-capture error
records a bounded failure, blocks passing acceptance and continues terminal deletion. Measurement/preparation failure
folders participate after the same whole-root cleanup proof. No failure becomes
a passing check or readiness claim. A new owned-scratch seam proves retention,
head/tail limits, raw deletion, failed-capture cleanup and refusal to read before ownership is absent;
no compiler, test, probe or counter is launched by that seam's fixture construction.

Static SDK relocation review used the installed SDK10.0.400 Sdk.props,
UseArtifactsOutputPath.props, DefaultArtifactsPath.props, DefaultOutputPaths.targets
and DefaultItems props/targets. Early global artifacts properties enable the
project-specific intermediate root; explicit ArtifactsPath avoids the project-folder
fallback error. The targets honor an existing global OutputPath and explicitly
exclude old bin/obj folders from source globs. The current flat bin folder is
shared by one build graph; the referenced test/library assembly names differ,
and normal/XML-only graphs retain distinct active roots. The flags remain unchanged:
this inspection does not establish actual evaluated properties or the build's
failure cause. See [SDK artifacts layout](https://learn.microsoft.com/en-us/dotnet/core/sdk/artifacts-output).

Only owned reports changed; task/runtime/test/fixture/probe files remain untouched.
Current packet: eleven Python sources parse statically; all source identities and
API membership hashes are refreshed; new diagnostics/control behavior is unrun.
Coordinator-owned harness-review.json is preserved and must be renewed by root
for the new manifest. Hold immutable for review; no execution is authorized here.


## Test cancellation-token correction — SOURCE ONLY

The coordinator reports actual diagnostics-correction controls PASS11, followed by
a separate fresh check failure: xUnit1051 at81:24 and124:24 in
AnnotationTests.CancellationDuringSourceLexicalWorkUsesOperationToken and
BoundaryTests.SourceMutationDuringAcquisitionStillRefusesCompilation. These
captured diagnostics supersede the earlier attempt's unknown cause; historical
attempts remain separate. The root confirmed all groups absent/reaped/logs closed
and deleted66rawfiles. No tests or counters launched in that failed attempt.

Only the two flagged tests change. Static source-location inspection places both
diagnostics on XmlDocument.Parse, which prepares each compiler input; the internal
XmlSchemaCompiler.Compile overload has no CancellationToken parameter. Both parse
calls now pass cancellationToken: TestContext.Current.CancellationToken. The
annotation test retains the deliberate CancelAtSource operation/cancellation.Token
and exact SourceWork64 assertion. The mutation test retains its Compile options,
SchemaCompileOperation construction, expected InvalidOperationException and source
mutation. All compiler calls/operation tokens remain unchanged. No analyzer suppression, quota,
oracle, runtime/compiler/probe or harness behavior change. The harness manifest
refreshes only these two test identities and this handoff. Runtime/control commit
8cb31895 remains the original runtime/probe identity; the two test corrections are
explicit exceptions to the earlier all-tests-frozen source packet. API membership
remains this worker's53fragments; main's54-fragment integration binding is root-owned.

Only static diff/hash inspection and planning validation for this correction;
worker builds/tests/controls/counters remain UNRUN. Freeze for bounded source
review; the coordinator owns any fresh actual retry and eventual integration.
