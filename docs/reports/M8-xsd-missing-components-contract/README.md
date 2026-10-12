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
