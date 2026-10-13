# M8 gate completion

All mandatory gate checks passed. See [durable results](summary.json) and [handoff](handoff.md). The product, tests, fixtures, recipes, package and runtime remain equivalent to the successful preparation at `61fb064d`; only gate output/reporting assertions changed. Full cumulative suites passed 12,279 tests with zero failures or skips. The 155 approved XSD nonpasses remain separate with zero pass credit.

The source packet and execution details below preserve the historical prepared recipe and its earlier unrun/source-only statements; current observed outcomes are in `summary.json`. The human-directed single-chat continuation reused valid preparation and successful commands rather than replaying whole phases after reporting-only fixes.

# M8 gate source packet

Source repair only. Root attempts1/2/3 remain failed at their original project
path/feed-layout/CPM errors. Attempt4 remains failed at command46 trim publication:
ILLink10.0.11 requested10.0.11-servicing.26373.116 while exact installed10.0.11
and DOTNET_ROLL_FORWARD=Disable prevented its default host launch. Root recorded
46 normal commands, one failed andfour unrun; CPM-corrected consumer restore and
managed build passed only their preparation scope. No successful preparation/
certificate/gate credit is claimed. The new owned linker host wrapper is UNRUN.
Root alone executed those attempts; worker has run no executable gate workload.
The task record remains with root. This isolated branch owns only this directory.
All commands below require root's reviewed release and root process custody.

`source-packet.json` freezes complete relevant source/config membership and bytes,
this recipe, inherited M7 class membership, helper provenance, source equivalence
and retained original outcomes. Every explicitly listed authority/policy record
is also hash-bound in initial/final inputs and before each check/counter command;
new gate orchestration outcome records stay outside that stable authority list. `recipes.py` specifies exact argv and child work
bounds; it executes nothing on import. `checks.py` uses the accepted corpus
supervisor unchanged. `nested_ownership.py` equals the accepted corpus helper
except `OUT = .../artifacts/M8-gate`; its original helper bodies retain durable
ancestor intent, group identity, every-ancestor registration ACK before exec,
parent-loss checks and whole-family terminal cleanup. ACK refers only to
process-registration custody, never host/project/actor permission.

Root launches each phase under an outer registered finite family, propagating
`XEMELE_GATE_PACKET_SHA256` equal to the reviewed source-packet hash,
`XEMELE_CORPUS_OWNED_REGISTRIES` and immutable `XEMELE_GATE_WORK_DEADLINE` /
`XEMELE_GATE_HARD_DEADLINE`. Every ancestor registry must be inside this checkout's
`artifacts/M8-gate`. Root must actually observe outer exit0, completed reason,
reaped leader, closed log, all direct/nested groups absent, no pending intents,
matched guards, inspected and deleted raw log, and no deadline overrun BEFORE
issuing the next certificate. Worker cannot issue a certificate.

| Phase | Commands | Inner work / hard seconds | Root outer work / hard seconds |
| --- | ---: | ---: | ---: |
| preparation | 51 | 14000 / 14180 | 14780 / 14800 |
| correctness checks | 18 | 6200 / 6380 | 6980 / 7000 |
| root-only cumulative counters | 4 | 1800 / 1980 | 2580 / 2600 |

Accepted root parent source SHA256:
`28c344b25ef8653abc8cff38de60c63f5b36879179c7092d58d94a94cdc36a13`.
The documented outer bounds match that parent, whose post-phase allowance is
600 seconds. Accepted observer SHA256
`23be0a30713915c09f7fbadae61461a66bba3274e853cff8917f20178e44eb99`
has a fixed600-second hard bound. Root initial input binding remains inside the
shared phase clock. Attempt1 observed183.77 seconds for that binding; this is
lifecycle budget evidence only, with no performance/timing/isolation claim.

`recipeSha256` hashes the contained packet `recipe` object using UTF-8 bytes of
Python `json.dumps(recipe, sort_keys=True, separators=(',', ':'), ensure_ascii=True)`
with no trailing newline; it includes argv, command counts and every bound.

The root outer total does not reset the inner clock. Inner work is also clipped
to inherited hard-deadline minus the full phase reserve; a shorter ancestor
window never consumes the terminal reserve or resets a clock. Preparation starts with16 counter-free synthetic controls:11 receipt/cleanup/
publication controls plusfive tiny filesystem catalog controls using the actual
helper. Allfive new controls are UNRUN until root releases fresh preparation.
After package freeze, three actual archive controls prove valid acceptance,
mutation refusal and missing-record refusal without mutating canonical inputs.

Per-child work bounds appear
in the recipe (30–4000 seconds). Each child has at most10 seconds cleanup, split
by the accepted helper into at most4 direct and6 nested cleanup seconds. Child
hard deadlines are clipped to the immutable phase hard deadline minus its terminal reserve less20 seconds;
the phase reserves180 seconds for remaining binding, terminal sweeps, raw
inspection/deletion, fsync/publication and failure reconciliation. Source/input
binding work is inside the phase total; the clock is established before hashing.
No automatic retries, extra discovery, builds, commands or sessions are permitted.
Every lifecycle, semantic or binding failure stops before the next command.

```text
python3 -B docs/reports/M8-gate/checks.py prepare
# Root externally observes clean terminal exit0, then creates root preparation certificate.
python3 -B docs/reports/M8-gate/checks.py check ABSOLUTE_ROOT_PREPARATION_CERTIFICATE
# Root externally observes clean terminal exit0, then creates root check certificate.
python3 -B docs/reports/M8-gate/checks.py counter ABSOLUTE_ROOT_CHECK_CERTIFICATE
```

Certificate fields: issuer=`M8-root-coordinator`, `preparedSha256`=hash of actual
`artifacts/M8-gate/state/prepared.json` for checks, or `state/check.json` for
counters; `outerReceiptPath` and `outerReceiptSha256` identify the externally
observed completed outer phase receipt. That receipt must carry exit0,
reason=completed, directChildReaped/ownedGroupAbsent/logClosed=true,
nestedOwnership.allOwnedGroupsAbsent=true with empty pendingIntents/remainingGroups,
deadlineExceeded=false, inputsMatched/logsInspected/rawDeleted=true. Both before
and after the phase must match the same certificate/outer receipt. Old passing
inner JSON after interruption or late publication grants nothing. Source is
committed before any grammar Git/binary provenance guard can run.

Preparation restores offline with an explicit local cache source, no ignored
restore errors or network feeds, single-node builds and disabled servers/shared
compilation. It first runs existing M1 `provision.py` WITHOUT flags, then
`import_suite.py --check`. The complete3386 XML original files/2722978 bytes,
2585 IDs, bundle, original archive and E14.dtd hashes are required; all DTDs and
entities are included. `XEMELE_CORPUS_RESOURCES` selects this worktree's corpus.
Canonical XML archive/cache, original fixtures and reference caches stay intact.
Native preparation guards use actual xcrun clang/ld/SDK selection before native
compilation and again after; selected compiler/linker/toolchain/usr, SDK file
membership/content and symlink targets are hashed in initial/final runtime guards.
The complete global NuGet closure is conservative and can consume substantial
binding time; the finite phase bounds include both endpoint hashes.

All projects/reference drivers/API extractors and package consumers are built
before executable checks. NativeAOT/trim toolchain readiness alone is not a pass.

The external package is unpublished local `0.8.0-integration`, net10.0 only,
with DLL equality to actual source/API product DLL. The actual nupkg archive
is rehashed against its frozen preparation record before/after every check. New gate phase guards include19 controls: the16
preparation receipt/cleanup/publication/late-terminal-output guards plus valid prepared-archive acceptance, actual
post-freeze archive-mutation refusal and missing-record refusal.
 The external consumer template explicitly sets ManagePackageVersionsCentrally=false
so its unchanged PackageReference owns exact0.8.0-integration despite repository
Directory.Packages.props inheritance. Shared repository configuration is unchanged.
The consumer's GatePinnedLinkerHost target runs BeforeTargets=_RunILLink and
selects its owned linker-host/dotnet via _DotNetHostDirectory/_DotNetHostFileName.
It requires the normal SDK DOTNET_HOST_PATH so installed targets cannot reset
that selection. The generated wrapper validates exact private ILLink DLL path,
canonical DLL/deps/runtimeconfig/task hashes and pinned Python/dotnet identities,
then execs pinned dotnet --fx-version10.0.11 with original linker arguments.
It retains the registered process group, inherited work deadline and Disable
roll-forward; original tool/cache/runtimeconfig bytes and warnings remain intact.
Template/rendered wrapper, executable mode, consumer target and expected tool
identities are bound during layout, freeze and every check/package identity guard.
No command, deadline, restore source or output expectation changes.

[ILLink source](https://raw.githubusercontent.com/dotnet/runtime/v10.0.11/src/tools/illink/src/ILLink.Tasks/LinkTask.cs)
constructs the original DLL invocation;
[MSBuild ToolTask source](https://raw.githubusercontent.com/dotnet/msbuild/main/src/Utilities/ToolTask.cs)
selects explicit ToolPath/ToolExe before the default generated tool path. Installed
10.0.11 targets supply those properties and reset them only without DOTNET_HOST_PATH.
Local NativeAOT targets set RunILLink=false and directly Exec native tools/ilc;
no runtimeconfig exists in that tool folder. This is source review only. Trim
repair and NativeAOT build/run remain UNRUN pending root release.
A fresh absent private consumer cache, explicit local feed and no ProjectReference
are enforced.
The local-feed layout600-second command first preflights the complete canonical
catalog before creating any feed links. Exact ID/version directory coordinates,
case-folded canonical target, canonical source path, length and SHA256 are bound.
Every generic alias must have one same-directory canonical peer and identical
length/SHA; missing peers, differing bytes, unexpected archive paths, identity/
filename mismatch, case-folded target conflicts and a cached generated integration
identity all fail. Identity is directory-derived as accepted by root contract review. No nuspec
validation is claimed beyond the16 audited collisions, and no NuGet version
normalization is inferred. Whole-cache endpoint byte guards and actual offline
restores remain authoritative. Canonical entries are selected
in deterministic target order; full bindings stay active in memory, with only
concise counts/digests retained. Canonical cache bytes remain untouched.

Read-only audit found7415 archives:7387 canonical targets plus28 byte-identical
generic copies, with zero canonical case-folded collisions. The six generic-name
collision groups contain16 distinct versions; all have identical canonical peers.
They are top-level separate regular copies, not embedded archive entries. Their
sixteen checked nuspec IDs/versions match their directory coordinates.

Preparation child work now sums12180;51 cleanup windows add510, leaving1310
shared work seconds in14000 plus180terminal reserve. Root initial binding remains
inside the shared clock; bounds are reviewed before a fresh launch, never reset.
 Managed,
trimmed and native osx-arm64 binaries assert independent earlier XML/HTML/CSS
outputs, RNG/XSD valid+invalid results, Schematron findings and independently
expected SVRL element/attribute/text/location semantics plus UTF8 stream ownership.
The native header must be arm64 Mach-O; each execution requires runtime10.0.11/
Arm64 stderr and frozen stdout. No binary is run during preparation.

Normal and true XML-only availability consumers execute independently. Complete
normal+trueXML-only public profile/budget/location/resource/cancellation/fuzz/CLI
coverage is inherited from authoritative primary `M8-cli-primary-checks.json`
checkpoint247203d7 (branch attempt3/failures retained separately) only when the333-file
SHA and frozen inputs match. It retains1156/1157 assertions,288 fuzz cases each,
guards10, CLI38 and full API/task201 checks. Three historical CLI failures stay
failures; Unix Console EPIPE suppression provides no downstream SVRL delivery
credit. The actual read-only-stdout exception control remains independently
verified. No old CLI launch is invoked blindly.

API compares compiled public1430 rows and exact55 fragment membership, every one
of the eight frozen schema lanes, Xsd615/compiler257, the exact approved nullable3
migration and canonical additive93 SHA33a4823adf25f638d0e52990918f859c2b64a35359d25aa59c318f4543a1812d.
The authoritative compiled extractor and verified availability projection are
used; header-only discovery cannot substitute. Earlier XML grammar compares
original counts, source context/resources, normative models/output witnesses and
reference resolutions against M7. Its two persistent children register in every
ancestor using the accepted Popen wrapper; original Child bodies remain unchanged.
XPath/HTML fuzz require entire frozen JSON outputs, not just counts. The earlier
XMLToolkit, linked XML/HTML/CSS CLI scenario and serialization sample execute.

The cumulative phase has no namespace/method filter or WarmBorrowed exclusion.
All unit/conformance/differential cases execute serially, invariant culture,
explicit runtime10.0.11, tiering/PGO/R2R disabled. Unique runtime IDs and display
names are required. M7 source type membership determines the inherited subset;
exact7789/408/520 display identity digests must match after the sole documented
UndefinedKind5→6 mapping. Newly added Schema=5 positives live in the distinct
`Xemele.Tests.SchemaResources.SchemaResourceKindTests` type; all12 rows/six
methods must pass and receive their own identity digest. No new-namespace-only
filter is used. Every failure/error/skip/not-run is retained and blocks success.
The historical uncontrolled WarmBorrowed expected0/actual7744B failure remains
historical; the full current suite includes the original assertion.

Actual intended-wrapper identity controls run positive / missing-packet negative / changed-packet negative /
unknown-environment negative / final-positive with actual exits0/1/1/1/0 required, no tests/counters and
complete prepared source/binaries/runtime/env/resource/certificate guards. Each
gets700 seconds inner work plus40 seconds inner terminal reserve. Each enclosing
family waits through the inner hard deadline plus5 seconds launch allowance,
then has its own10-second cleanup window: at most755 seconds per pair. Allfive
pairs share one immutable3800-second controller work deadline plus40 seconds
controller terminal reserve (3840 total); the recipe command has4000 seconds
work plus10 cleanup. Five maximum755-second pairs sum3775, leaving25 seconds
in controller work. Check command work sums5320 plus18 ten-second cleanup
windows=5500, leaving700 seconds work slack plus180 terminal reserve.
These fixed reviewed bounds replace the insufficient old caps before any new
launch; no launched deadline is reset or extended. Thus the parent never kills an identity phase at its work
deadline while final input/binary inspection still owns its full reserve. This is a proposed control recipe, not an
observed result. The counter check certificate is consumed once; repeat execution
requires new root review and fresh prepared custody. The inherited NoEmit children
have only real+positive modes. The compatible verified resolved-availability
disabled6-route/254-consumed evidence is source-bound and reused; no disabled
NoEmit child is invented.

A separate new counter-disabled4096-byte escaped-object control runs in correctness checks;
all counter outputs stay zero while runtime/product/environment identities match.
The root-only counter phase first requires the real positive counter to observe
at least4096 bytes, then runs all three suites including their inherited six
real+positive NoEmit child pairs. Their12 request/report pairs, ownership,
runtime/product/module hashes, actual overrides, child XML and real zero/positive
allocations are independently inspected after durable family termination. These
C# descendants inherit the registered group; they do not create new sessions.
No host isolation, global-idle, lease, PARK, foreign-project or timing claim exists.
Earlier compatible benchmark measurements retain their own scopes; no new
throughput, cold, retained-size or crossplatform claim is inferred.

XSD full186-child campaign/five separate profiles are reused with explicit
branch source attribution atdb2e6e2ca, post-CLI product/options/corpus byte
and membership equivalence, and fresh primary controls932561a4 / boundary8626e609.
Original product39329 pass/144false/11null stays exact. The155 human-approved
nonblocking dispositions retain144false/11null and ZERO original/gate pass credit.
The unwaived annotZ002 originalVALID passes under unchanged ordinary caps.
Five application profiles retain originalPassed=null and zero original/gate
credit; they do establish their separate advertised profiles. RNG requires1035
product checks per engine/Jing1035 agreements/libxml1020 agreements+15 exact
reference limitations; Schematron preserves95 agreements+1BCL Unicode limitation.
Reference limitations never exclude product expectations.

Phase outcomes become durable BEFORE raw inspection/deletion. Failed semantics
cannot become success merely because wrapper cleanup succeeds. Raw refusal
retains output for finite root inspection; nothing is archived. Final success
requires raw deletion, final fsync/publication, terminal printing and a post-print
hard-deadline check inside the same one-attempt failed-state reconciliation block.
Interrupted or overtime publication/terminal output gets one durable failed reconciliation; actual
outer completion still governs certification. Active binary/package inputs remain
only until final root inspection/checkpoint custody. Root handles cleanup of all
prepared outputs, retention of the necessary unpublished package/manifest under
existing policy, integration verification/task state and final write transfer.
