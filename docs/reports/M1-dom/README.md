# Strict DOM composition — current branch evidence

The seven frozen factories compose verified Input, Syntax, Tree and the linear
DOM builder. This source has no serialization dependency. The sink copies borrowed
names only on document-local intern misses, preserves attributes/declarations and
logical Text/CDATA/Comment/PI boundaries, and ignores document-level whitespace.
The byte adapter owns 4096 UTF-16 units and 4096 parallel Int64 offsets, plus a
six-unit declaration-recognition prefix. Stream transport owns an additional
4096-byte read buffer. Declaration framing submits at most one new original byte
per decoder call, draining detection carry before bulk body decoding. Declaration
validation occurs in the callback, or once with empty encoding on absence. Original
byte counts and UTF-16 counts remain separate; EOF is explicitly marked once.
Caller streams stay open and I/O/cancellation propagate unchanged. Structured
rejection returns no document and an owned diagnostic list.

At `15e099bd11e25a5fc2576059a39a6c707a45feef`, `verify.py` passed:

- Release solution build, 25 focused DOM cases, Input 10/Syntax 70/Tree 14/builder
  11/serialization 46 regressions and all 240 solution tests, no skips.
- Foundation 11 tests, explicitly rebuilt M0 M1/M2/ApiProbe and all M1 independent
  declaration models, and 38 protocol model assertions.
- Exact independent DOM model 199 declarations, serializer model 215, and their
  deduplicated canonical union of 222 matched against actual runtime output.
  The actual seven-factory API fragment matches the approved DOM declarations;
  mandatory discovery and compiled fragment comparison passed.
- Preserved internal Input 41-row contract from a disposable probe working
  directory; Syntax nine approved types and 80 declaration rows.
- Independent source sample and newly packed local-feed-only NuGet consumer with
  a fresh isolated package cache, with no project reference in the consumer.
- Owned benchmark driver build; planning validation and whitespace check.

The structure oracle covers all three input forms, six encoding/BOM/signature
combinations, stream read sizes 1–64, declaration precedence across every small
read size, malformed encodings/grammar/DTD, scalar and CRLF positions, original
byte EOF, exact quotas, stream ownership, I/O/cancellation identity and release of
mutated borrowed chunks. A test-only BCL oracle compares the declared supported
subset. The 12,000-depth case asserts exactly one builder attachment check per
node; 1024 attribute/declaration pairs cover wide construction. Incremental
adapter assertions observe built nodes before EOF. No full XML conformance claim
or later recovery/DTD/reader/async API is made.

The allocation sample measures the first complete byte parse before any parser
warm-up in that process; fixture generation is excluded. For 512 repeated children,
cold complete owned byte DOM allocated 469088 B, warm byte DOM 468528 B, and warm
character DOM 426656 B. These include setup, scratch and durable DOM output.
A 32-document full-GC retained-owner estimate was 229783 B/document, excluding the
shared input fixture. This is an estimate, not a precise object-layout guarantee.
The document arena intentionally retains all slots while any handle retains its
owner. Parser/sink temporary text and dictionaries belong to the operation.
Warm borrowed ordinal name hits allocated 0 B over 10000 calls after 10000 setup
calls; no allocation-free claim applies to owned output or setup.

Earlier attempts: unassigned test field warning failed the initial build; an
incorrect MTP option was passed to `dotnet run`; lone-surrogate attribute metadata
was replaced before the test, so invalid strings are now generated at runtime.
Sandboxed MTP IPC was denied and rerun with authorized escalation. The first full
verification stopped with 24/25 focused tests passing: a 100-call warm-up left a
24 B one-time allocation in the name-probe measurement. Isolating assertion
evaluation did not remove it; documented 10000-call warm-up produced 0 B. The raw
failed and passing known-revision attempts are compressed alongside this report.

## Historical blocking review at the first checkpoint

Independent review found an unresolved bounded-cancellation problem: public tree
creation and current builder member APIs repeat `TreeValidation` scans without
operation cancellation. Long text/name/attribute/namespace persistence can thus
walk more than 4096 units without a cancellation check. All executed correctness
checks above passed, but this review blocks completion and timing. No benchmark
timing has run; the task remains unimplemented.

A narrow tree-owned repair is requested: trusted syntax-validated node/member
persistence that retains arena/name quotas, checked capacity, builder freshness,
versions and mutation notifications while removing duplicate grammar scans, or
equivalent cancellable validation with checks at bounded intervals. Public tree
validation remains unchanged. The DOM worker must consume only an integrated,
verified contract and then add cancellation regressions, rerun affected checks and
request an exclusive grant for the 16 owned measurement cases. Task JSON has not
been edited while the coordinator serializes prerequisite changes.

## Verified persistence correction and second suspension

The cancellation repair was integrated and verified before resuming this branch.
At `752ec92591c0d31f1b9a90e6c834167d154aac70`, the sink forwards the actual
operation token through every trusted create/attach/member helper and checks it
before its own persistence callback work. It performs no duplicate Tree scalar,
name or delimiter scans. Platform copying/hash/arena allocation is explicitly
outside the bounded custom-loop claim. Seven 32768-character shape tests exercise
Text/CDATA/Comment/PI/name/attribute/namespace persistence and owned lifetimes over
all transports; seven deterministic wrappers cancel immediately before actual
core-validated sink persistence callbacks and verify token identity and no partial
attachment. All 39 DOM cases, 262 solution cases, prerequisites, Foundation11,
explicit models/protocol38, actual public222/internal ABI, source/fresh isolated
package sample, allocation and ownership checks passed. Full raw and concise
results remain in `persistence-corrected-checks.txt[.gz]`.

The default audit base initially included coordinator orchestration metadata in
the renewed claim. It failed before builds/tests; no allowed paths were widened.
The passing command uses `--audit-base 6dfbde16688cd1b071a6128e69b9ac362d850ac0`,
the renewed coordination claim before worker feature changes. Committed plus
working/untracked owned paths and whitespace are audited. That failed attempt is
preserved in `renewed-base-audit-failure.txt.gz`.

A subsequent trust audit found that Tree prohibits every ordinary localName
`xmlns`, while Syntax accepts it. A temporary bounded sink rejection guard and
three rejection cases were committed at
`90fda442d313bcfd16f7a72cddca655005f638e3` before coordinator steering arrived.
The coordinator clarified that Namespaces1.0 permits ordinary element localName
`xmlns` and prefixed ordinary attributes `p:xmlns`; Tree/contract/serializer must
be corrected by their owner. The temporary guard is **not approved** and must be
removed when the verified correction restores this reserved DOM claim. Replace
its rejection cases with positive structure/roundtrip cases. No such source is
integrated or represented as a passing deliverable.

At that temporary-guard revision, 43 focused DOM cases and prerequisite focused
checks passed; full solution execution reported266 total/265 passed/one failure:
existing `TreeTests.WarmTraversalAndRepeatedNameInternProbesAllocateNothing`
measured4368B instead of zero at line172. That peer test had passed in its focused
14-case lane earlier in the same attempt. No peer test/source was edited, no
automatic repeat was run, and the failure is preserved in
`peer-allocation-final-attempt-failure.txt.gz`. Verification stopped there, so the
later model/package/benchmark oracle checks were not rerun at90fda44.

Source/task edits and all checks/timing are now stopped pending the ordered
namespace correction. No measurement timing has run. Task criteria remain pending;
the task record has not been edited during coordinator prerequisite serialization.

## Final namespace-corrected source checkpoint

After merging exact renewed claim `8f4b34912f6fcb0ec7d3f2d951801c8804400ba1`,
the six prerequisites are verified ancestors. Runtime and test revision
`73673440a1ee845dbf38b4f8c506a15dc225a375` removes the temporary guard entirely.
Positive plain/default/prefixed `xmlns` element and qualified ordinary `p:xmlns`
attribute cases assert structure and independent BCL name expansion over all
input transports, keeping declarations separate. Source and package samples
include the legal names and remain independent of serialization.

`verify.py --audit-base 8f4b34912f6fcb0ec7d3f2d951801c8804400ba1` passed
44 focused DOM and288 full solution tests without skips, Foundation11, all
explicitly rebuilt declaration models/protocol38, actual API222 and actual
seven-factory fragment, Input41 and Syntax9 types/80 rows, source and fresh isolated
local-feed-only package sample, allocation probes, planning107, and explicit
committed/working/untracked owned-path and whitespace audits. The historical Tree
allocation failure is not reproduced: its unchanged test passes both focused and
full lanes at this revision. The namespace owner's unrelated planning-unit failure
remains in its historical report and belongs to the coordinator's separate repair;
no planning tool or peer report was modified by this worker.

Cold complete owned byte DOM469096B, warm byte468536B, warm character426664B;
retained-owner GC estimate229782B/document over32 owners, excluding fixture strings.
The first-use measurement precedes all parser warm-up in its process. Fixed scratch
and durable output are included in complete-operation allocations; the name-hit
primitive separately remains0B after its documented warm-up.

All four measurement shapes' complete structural oracle passed outside timing:
ASCII repeated markup, multilingual/dense references,131072-unit text and2048-depth
elements. It compares every node kind/name/URI/prefix/value, ordered child shape,
ordinary attributes and namespace declarations across character/byte/stream and
whole-source decode baseline factories. Results own their complete document; fixture
generation is outside measurement. No timing has run yet. One exclusive-host grant
for16 cases is requested; criteria stay pending until review and measurement.

## Completed measurements and handoff

Independent read-only review approved runtime/tests/workloads
`73673440a1ee845dbf38b4f8c506a15dc225a375`. One coordinator-authorized exclusive
run completed16 workloads, all16 children and wrapper exit0,48 actual measurements
(three per case),105.38seconds. No repeat was initiated. The invocation used the
owned driver, class attributes and no explicit job options. Raw console gzip,
CSV/full statistical table and child/iteration audit are in `bdn/`.

Host: Apple M4 Pro Arm64, macOS15.8.1, SDK10.0.400/.NET10.0.11, concurrent
workstation GC. High-priority scheduling was denied for each workload; ordinary
priority applies. The coordinator parked all other heavy task work. Confidence
ranges and GC counts remain in the complete result table. These are provisional
single-architecture measurements; x64 release confirmation remains required.

| Healthy fixture | Whole decode then parse | Character parse | Byte parse | Stream load |
| --- | ---: | ---: | ---: | ---: |
| ASCII repeated markup |137.61µs|137.70µs|191.99µs|189.13µs|
|2048-depth|326.74µs|332.39µs|499.23µs|464.65µs|
|131072-unit text|2647.67µs|2470.23µs|3832.84µs|3993.91µs|
|Multilingual/references|100.70µs|96.42µs|145.74µs|150.74µs|

| Healthy fixture | Whole decode allocated | Character allocated | Byte allocated | Stream allocated |
| --- | ---: | ---: | ---: | ---: |
|ASCII repeated markup|178.52KB|166.98KB|207.87KB|211.95KB|
|2048-depth|783.79KB|755.76KB|796.65KB|800.74KB|
|131072-unit text|1055.39KB|799.29KB|840.19KB|844.27KB|
|Multilingual/references|172.01KB|162.40KB|203.29KB|207.38KB|

Byte/stream paths pay fixed decode/mapping scratch and encoding validation costs.
They are slower in these fixtures; for long text, avoiding the whole decoded
input string reduces allocation about20%. The straightforward required adapter is
retained for declaration/error ordering and bounded stream transport. This run
supports no scan/matcher optimization claim. The baseline comparison is restricted
to identical successful fixture structures; malformed encoding/declaration order
is covered by correctness tests. Every result is a complete owned document; setup,
durable output, temporary buffers and arena growth are included. Fixture generation
and complete structural oracle validation precede timing.

Task is implemented on its branch after passed correctness, review and measurement.
Coordinator integration/verification remains. No runtime/test/workload change
followed the reviewed source. Historical suspension/failure evidence stays intact.
Final ownership audits include committed delta, staged, working and untracked paths;
use `audit.py --base <coordination-claim>` for independent integration verification.
