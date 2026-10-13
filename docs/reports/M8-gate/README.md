# M8 gate source packet

Source preparation only. No gate build, restore, provisioning, discovery, test,
probe, allocation counter, package consumer, trim or NativeAOT execution has run.
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
| preparation | 51 | 12500 / 12680 | 12700 / 12720 |
| correctness checks | 18 | 3500 / 3680 | 3700 / 3720 |
| root-only cumulative counters | 4 | 1800 / 1980 | 2000 / 2020 |

The root outer total does not reset the inner clock. Inner work is also clipped
to inherited hard-deadline minus the full phase reserve; a shorter ancestor
window never consumes the terminal reserve or resets a clock. Preparation starts with10 counter-free synthetic receipt/inspection/publication
guards and registered tiny-child cleanup controls before provisioning/builds.
After package freeze, three actual archive controls prove valid acceptance,
mutation refusal and missing-record refusal without mutating canonical inputs.

Per-child work bounds appear
in the recipe (30–600 seconds). Each child has at most10 seconds cleanup, split
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
is rehashed against its frozen preparation record before/after every check. New gate phase guards include13 controls: the10
preparation receipt/cleanup guards plus valid prepared-archive acceptance, actual
post-freeze archive-mutation refusal and missing-record refusal.
 A fresh absent private consumer
cache, explicit local feed and no ProjectReference are enforced. Managed,
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
gets180 seconds inner work plus40 seconds inner terminal reserve. Each enclosing
family waits through the inner hard deadline plus5 seconds launch allowance,
then has its own10-second cleanup window: at most235 seconds per pair. Allfive
pairs share one immutable1250-second controller work deadline plus40 seconds
controller terminal reserve (1290 total); the recipe command has1400 seconds
work plus10 cleanup. Thus the parent never kills an identity phase at its work
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
requires raw deletion plus final fsync/publication before immutable hard deadline.
Interrupted or overtime publication gets one durable failed reconciliation; actual
outer completion still governs certification. Active binary/package inputs remain
only until final root inspection/checkpoint custody. Root handles cleanup of all
prepared outputs, retention of the necessary unpublished package/manifest under
existing policy, integration verification/task state and final write transfer.
