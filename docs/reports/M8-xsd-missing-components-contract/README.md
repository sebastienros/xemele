# SOURCE review packet — all contract/lifecycle/runtime checks UNRUN

The claim checkout was clean at d12be860e118b8ce97d383a0511ff1167f8c4bab;
private branch codex/m8-xsd-missing-components-contract, worktree
/Users/sebastienros/.codex/worktrees/45c6/xemele. This worker has launched zero
restore/build/test jobs. Host-wide dotnet services/build workers exist and are not
owned or stopped by this task. No source task acceptance is marked passed.

Review missing-components.md, runtime-migration.json, baseline-delta.json, the
old/candidate fixtures, representative usages, and check.py before execution.
The full candidate projection is authored, not an emitted passing baseline.
provenance.json attributes/hash-identifies integrated source copies. The attributed
Roslyn extractor adds only an actual Environment.Version receipt and executes metadata inspection; fixture methods never execute.
The additional availability model is in a review-only namespace excluded from
this existing Xemele.Validation fixture projection. Its get-only/value/snapshot
source contract is frozen separately in the decision; it is not claimed as an
emitted additive runtime baseline. Later runtime must emit that actual surface.

After coordinator release of a clean committed SHA, the prepared offline command
is `python3 docs/reports/M8-xsd-missing-components-contract/check.py --dotnet
<absolute pinned 10.0.400 dotnet> --release-sha <reviewed HEAD>`. Do not run this
until released. Restore uses only the local fixture source, audit disabled;
10.0.11 runtime is required. Outputs/projects/CLI-home/receipts stay under ignored
artifacts/M8-xsd-missing-components-contract; no shared projects or outputs change.

The guard is a new owned finite supervisor requiring review/control evidence,
not a reused generic unbounded subprocess helper. Its separate process owns every
command and monitors caller PPID; caller exit/termination stops launches and
cleans its family. Each command has a 45s cap and shares one 180s work envelope.
12s cleanup is reserved INSIDE that envelope (no launches after 168s), with a 195s
absolute fail-closed hard timer. The caller waits at most 197s as a final guard
reaping bound, never issuing success after work/hard failure. Failure/timeout/
late completion/invariant breach stops the sequence. TERM/INT request cleanup;
SIGKILL of the caller is detected by the surviving guard. Killing the guard itself
is outside the caller-death guarantee and must not be the lifecycle release test.

Unique inherited environment marker plus observed PID/PPID/PGID closure detects
ordinary descendants, including reparented/setsid descendants; two clean family
inventories and direct-child reap are required after every command and finally.
Inventory failure is a failure, never proof of absence. Commands cannot start
MSBuild/compiler servers: no reuse/shared compilation, maxcpucount1 and disabled
build servers. No host-wide dotnet kill or full host commands persisted. This
assumes child tools preserve their inherited marker; a deliberately environment-
clearing, reparenting adversarial process is not a supported managed tool.

The receipt stores reviewed SHA, complete source hashes, plan hash, actual SDK
output, exact command arrays/expected exits, child IDs, timings, log hashes,
reap/family/log invariants and final result. Both source identity and exact
three-member compiled projection are rechecked before success. Old nonnullable
usage compiles against old; candidate nullable usage compiles; old implicit Type
conversion rejects with CS0266; dropped member/removed nullable baseline reject.
Current runtime XsdElementDeclaration and all shared fixtures/goldens stay old.

Prepare lifecycle plan files with the same released command plus
`--prepare-lifecycle-controls` (prepares only; no controls launched). The plans
contain finite 8s work / 3s reserved cleanup / 10s hard / 2s command envelopes.
Review and then launch `python3 docs/reports/M8-xsd-missing-components-contract/
lifecycle-launcher.py <control plan>` from an owned finite controller; the prepared
launcher passes its actual PID to check.py --guard; retain each receipt. The launcher
must itself have a finite bound and must remain alive for all cases except
parent-death. The normal and escaped-descendant plans should pass with two command
records and final absence. timeout/late/invariant should fail with only one
record, absent next-command.txt, and final absence. For parent-death, launch a
guard, wait for hazard.json, then terminate/kill ONLY the caller launcher; surviving
guard must report caller death, one record, absent next-command.txt and final
absence. Independently inventory the recorded descendant PID, original group and
marker after each control; receipt flags alone are insufficient. All six controls
are UNRUN and must pass their expected outcomes before contract compilation.

No actual normal/product consumer, XSD assessment, corpus classification or
allocation gate runs here. Those checks require the ordered runtime task.
