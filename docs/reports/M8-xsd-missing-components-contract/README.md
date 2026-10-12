# Renewed SOURCE packet — no execution release

The previous runner at source0ed18306d / metadata c3898a6c0 was rejected in SOURCE
review: cleanup could fail before signaling known children; raw fork/setsid closure
was unproved; deadline/publication and preparation/control identity were incomplete.
Those revisions are not accepted execution inputs. All contract compilation,
lifecycle controls, runtime, corpus and allocation checks remain **UNRUN**. Task
stays in_progress, criteria pending. Zero owned build/test jobs have launched.

This correction uses owned copies of the coordinator-specified accepted corpus
supervision.py (origin SHA256 407b228d8b4741445eaf928a44aedd4de85aacaac894e6ad23b8ed8071395903)
and nested_ownership.py (origin SHA256 fe1db7d70cc4b10a04aaa5ac8107cd69a95f7a16e21a0888892ef70c5f05a128).
Exact read-only copy origins and local hashes are in supervision-provenance.json.
There are no runtime imports from a peer checkout. The supervision body is copied
unchanged. Nested ownership changes only owned constants and adjacent prelaunch/
ACK/exec binding/deadline checks, with adaptation recorded for renewed review.
The copies' earlier acceptance does not establish this adaptation's behavior.

Known groups receive TERM/KILL before ps; direct child is reaped. Even inventory
failure or an expired cleanup deadline best-effort KILLs registered groups and
reports failure without a clean claim. Every new session publishes its PID=PGID in
ALL ancestor registries and obtains a complete durable ACK before payload exec.
Intent without registration cannot become a clean result. Closing registries
prevents late launches. Controllers can signal descendants directly even if the
intermediate caller's finally cannot run. The descendant and parent-death controls
now use an explicitly registered/ACKed new session, with readiness after exec.
There is **no** guarantee for arbitrary unregistered fork/setsid escape.

A separate guard monitors caller death, unwinds copied cleanup, and stops subsequent
commands. An outer registered controller independently drains every registered
session; fallback never kills only the guard. Immediately before launch and ACK,
and immediately before child exec, verify deadline, parent/closed-owner and bound
inputs. Slow all-absent inventory cannot pass its deadline. Failure/timeout/late
completion/output invariant stops the sequence. No host-wide dotnet signaling.

The inner compile guard has 180s work, reserves 12s INSIDE work (launch cutoff168s),
195s absolute hard timer, and a40s command cap. Its caller has200s work, reserves
cleanup before its work end, and214s hard timer. Python lifecycle controls use10s
work/4s reserved cleanup/14s hard/2s command caps; their caller is24s work/38s hard.
The independent full verifier has100s work/10s reserved cleanup/115s hard. No passing
receipt or exit may survive a late work/absence/publication check. Receipts are
exclusive fsynced atomic publications with file/directory durability. Passing
candidate receipts require a separate commit marker; late candidates/markers are
quarantined, never overwritten or accepted. No realtime OS scheduling claim.

The source-reviewed preparation phase launches no .NET workload. It creates fresh
staged project/config files under ignored task artifacts and binds their hashes,
commands, source/configuration, actual dotnet/SDK10.0.400/Roslyn/host/runtime10.0.11
files. Offline NuGet.Config clears user sources. No shared project/output changes.
The required command recipe is independently regenerated and compared, so editing
commands and updating the plan hash alone is insufficient. --guard requires the
expected plan hash and verifies all inputs before any payload launch. Per-command
bound snapshots add controls receipt/candidate, actual prior compiled assemblies,
all emitted projections and dependency configs; child bootstrap rechecks snapshot
before exec. Source/project/binary replacement cannot reuse a prior passing receipt.

controls.py is the supplied finite outer verifier, not a manual launcher recipe.
It asserts normal/registered-descendant pass and timeout/late/invariant/parent-death
failure; independently inventories recorded groups; asserts exact launch count and
no next-command marker. Parent death is forced only after descendant registration,
all-ancestor ACK and readiness. The outer owner may kill the guard before it can
publish a receipt; the verifier preserves that absence and uses its own durable
registered cleanup evidence, never inventing a guard result. Additional required
controls cover ps failure and expired cleanup with a LIVE child; delayed prelaunch,
absence and final publication; changed command/project/source/dependency binary;
missing/mismatched controls receipt; and wrong expected control outcome. Rejections
must have the expected reason. The compiler requires every expected control and a
matching frozen successful controls receipt before any restore/build launch.

After a new explicit SOURCE release only, review these prepared phase commands:

1. check.py --prepare --dotnet <pinned absolute launcher> --release-sha <clean HEAD>
   produces an exclusive plan and its hash; no compilation or controls run.
2. controls.py --plan <plan.json> --expected-plan-sha <frozen plan hash>
   runs the finite verifier and emits controls-receipt identity only if all required
   outcomes and independent cleanup checks match.
3. check.py --plan <same plan.json> --expected-plan-sha <same frozen hash>
   --controls-receipt <controls-receipt.json> --expected-controls-sha <frozen receipt hash>
   compiles old/candidate/reject usages, runs only the attributed Roslyn metadata
   extractor, and compares the exact three-member delta. Fixture methods NEVER run.

All three phase executions remain unreleased and UNRUN. Review source-manifest,
baseline-delta, copied fixtures/usages and runtime-migration alongside the harness.
OwnerKind.TypeDefinition and an xsi:type-only complex-program consumer edge now
represent the semantic review case. Actual selected-type availability must be
checked before deferred content-program consumption, independently of declaration
lookup; pair its later runtime check with an unrelated assessable root. This adds
no public/frozen-interface or extra nullable baseline change. Facet ownership
request remains explicit. Runtime/shared goldens/corpus/counters stay unchanged.

## Terminal retention correction — SOURCE only

`terminal.py` is the terminal boundary shared by the compile caller and finite
control seams. It consumes concise source/plan/runtime, command outcome/lifecycle,
log/input/output/projection identity hashes and case outcomes while their inputs
still exist; independently confirms every registered group absent; and physically
deletes the unique owned scratch tree. Traversal never follows symlinks into
canonical sources, fixtures, caches or toolchain directories. Summaries live only
under ignored task `summaries/`, identify themselves as terminal summaries, and
explicitly cannot serve as a fresh verification or controls gate. Deleted artifacts
are never claimed to be rechecked later. No raw logs, emitted API text, negative
baseline copies, staged projects/config, bin/obj, CLI scratch or control archives
survive a terminal compile success/failure or failed controls phase.

Preparation keeps staged inputs active for controls. A passing verifier deletes
all per-case and campaign raw scratch after consumption/absence; it retains only
a concise **active-controls-gate** receipt plus the original staged inputs needed
for the explicitly pending compile. The gate binds the unchanged original plan
and source as before. Compile claims its stage exclusively, keeps every existing
prelaunch source/project/toolchain/control/assembly mutation assertion, consumes
the actual guarded outcome, and deletes the whole stage on success, failure or
interruption. Preparation failure deletes its partial stage. Existing owner claims
prevent a second concurrent attempt from deleting another live phase's inputs.

The control launcher hands active readiness/outcome inputs to its live verifier;
it cannot honestly declare its own registered process group absent while running.
The verifier owns terminal deletion in `real_case` finally after reaping that caller,
including SIGKILL-parent-death and failed/interrupted cases. Passing handoff markers
are temporary active records, not archives. Outer controller registrations remain
until all registered sessions are drained, then campaign scratch is deleted too.

Four extra mandatory controls use finite supervised **Python** payloads, not .NET:
successful compile terminal cleanup, failed compile cleanup, failed control cleanup,
and interruption during recursive cleanup. They create real staged project/bin/obj,
nonexecutable binary/projection/negative-baseline sentinels and raw logs; consume
bound identities and lifecycle outcomes; assert physical scratch deletion; preserve
canonical/cache sentinels; and reject stale deleted-input reuse. The interruption
seam resumes deletion with the same deadline, records failure, then propagates the
interruption after cleanup. TERM/INT are deferred only during terminal deletion;
there is no new workload, extended deadline or discarded mutation assertion.
Failure to prove absence or finish deletion yields a failed cleanup summary, never
a passing gate. All these controls, and every preparation/compilation phase, remain
**UNRUN** pending renewed SOURCE review and explicit execution release.

## Four bounded ownership/publication SOURCE fixes

The compile caller now verifies the committed matching controls gate and unchanged
bound inputs BEFORE acquiring its exclusive cleanup claim. A missing gate grants
no cleanup ownership; it cannot delete a controls stage paused before its next
child. The new pause control calls the actual controls-claim helper, attempts the
production compile-ownership entry, asserts rejection leaves marker/project/stage
untouched and no compile claim, then proves controls can launch/complete its finite
Python continuation. All existing mutation checks remain in place.

A SIGKILLed compile caller cannot run terminal finally. This is now an explicit
coordinator-owned recovery requirement: retain the plan hash and the hash of the
exclusive compile-started.json claim while active. That claim identifies the owner
PID, exact stage and compile-owned registry. Every production guard/child session
registers in that ancestor registry before payload execution. The surviving
coordinator uses `check.py --recover-stage <stage> --expected-plan-sha <frozen plan>
--expected-claim-sha <frozen claim>` within the prepared12s recovery bound. Recovery
refuses a present owner, checks exact plan/claim/stage/registry identity, drains
registered groups, independently proves whole-family absence, consumes any available
failure summary, and deletes the whole stage. It launches no workload, revalidates
no deleted output and grants no compilation/pass credit. This is explicit recovery,
not a promise of automatic cleanup after an uncatchable caller kill.

The new caller-death control invokes the SAME production compile-ownership and
terminal path with an owned registered Python payload seam, never .NET. It kills
that actual cleanup-owning controller only after child registration/ACK/readiness,
proves the killed caller left physical scratch, then invokes the matched surviving
recovery and asserts complete deletion. retention-caller.py is only this finite
control producer; its synthetic gate/command fixture is not compilation evidence.

Retention controls now require actual record.exit, record.reason, direct reap,
logClosed and registered-group absence. Requested exit7 must be observed as actual7
with reason failed and a closed log. Another control creates the same sentinels but
exits9 unexpectedly; it must reject and still delete scratch. Outcome is never
inferred merely from the exit code the test requested.

Terminal publication rechecks deferred TERM/INT AFTER fsynced final publication.
If interrupted, it removes both unreconciled phase-success summary and consumed
candidate, publishes an exclusive interrupted summary with consumed.state=failed,
then propagates interruption. The new final-publication control sends actual TERM
during durable final publication and asserts deletion completed, interruption
propagated and every retained summary for that scratch root reports phase failure.
All four new controls, every prior control and all preparation/.NET phases remain
**UNRUN**, with execution release NONE. Semantic candidate and shared baselines are
unchanged; renewed SOURCE review is still required.
