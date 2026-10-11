# Internal DTD compilation

Task-owned runtime files are `src/Xemele/Validation/Dtd/Compilation/`.
The internal compiler consumes only the immutable verified M2 effective declaration
model and the verified shared `ValidationOperation`; it ships no public facade or
future schema stub. `samples/M5-dtd-model/DtdModelSample.csproj` is nonpackaged and
uses a task-owned friend assembly declaration. No public API baseline fragment is
needed for this internal deliverable.

## Frozen internal interface

- `DtdCompiler.Compile(DtdModel, ValidationOperation)` returns an immutable
  `CompiledDtdModel`. Declaration rejection throws `XmlParseException` with one
  fatal `DTD.*` diagnostic and original declaration location. Work/capacity failure
  throws `XmlParseException` with the operation's `VALIDATION.Limit` diagnostic;
  cancellation throws the original operation token. Argument exceptions propagate.
- `CompiledDtdModel.Source`, `Elements`, `Attributes` preserve M2 declaration
  snapshots and provenance. Collections are owned read-only wrappers.
  `FindElement`, `FindAttribute`, `HasNotation`, `HasUnparsedEntity` take borrowed
  ordinal character spans and an operation; they return null/false on exhausted
  work. A caller must stop if `operation.IsComplete` is false.
- `CompiledDtdElement.Declaration` and `.Content` expose each effective element.
  `DtdContentModel.Kind`, `InitialState` (0), `TryAdvance(ref int, span, operation)`,
  and `IsAccepting(int)` implement one-unambiguous position automata. Mismatch
  returns false and sets state -1 (sticky). Quota refusal returns false without
  changing the cursor. `ANY` accepts child names; `EMPTY` accepts no children;
  mixed content permits only listed child names and accepts the empty sequence.
  Text/whitespace/undeclared-child checks belong to the tree evaluator, which uses
  Kind to distinguish EMPTY, ANY, mixed and element-only content.
- `CompiledDtdAttribute.Declaration`, `NormalizedDefault`, and
  `IsValueValid(span, model, operation, location=default)` implement XML scalar,
  Name/Nmtoken, normalized token-list, enumeration, NOTATION, and unparsed ENTITY
  constraints. This method checks type, not REQUIRED/FIXED presence/equality or
  document-wide ID uniqueness/IDREF resolution. Those belong to the evaluator.
  Values are already normalized; repeated/leading/trailing spaces or tabs in
  non-CDATA token lists are rejected. Compilation uses the verified M2 default
  normalizer, retaining external declaration provenance and source locations.

## Bounded work and storage

Compilation uses an iterative postorder traversal and Glushkov first/last/follow
positions. Initial and every follow position set must have distinct lexical names;
repeated names in different deterministic positions (e.g. `(a,a)`) are allowed.
Occurrence and nullable groups contribute follow edges before publication. Every
visited particle, scalar/name comparison, union/edge, collection population and
normalizer entity probe is charged. Hash tables are compilation-only, with input
names/scalars charged before insertion. Warm lookup uses direct ordinal probes;
there is no fixed vocabulary search or SearchValues scanning to optimize here.

Operation caps remain aggregate, including default expansion across attributes.
The shared default budget meters each replacement scalar and each dictionary probe,
with aggregate ExpandedCharacters and EntityDepth. TokenCharacters counts Unicode
scalars. Compiler depth is the minimum of caller Depth and a finite implementation
4096; retained storage units have an aggregate hard maximum 1,048,576 even when
input/work caps are explicitly null. Units include compiled objects, stack entries,
position/edge/set entries, token references and normalized default UTF-16 units;
they are an implementation capacity, not an exact retained-byte estimate.
Successful comparisons charge each inspected UTF-16 code unit (a conservative
scalar-work charge for supplementary names), plus the comparison/transition step.
`IsAccepting` is a direct immutable array access; the evaluator charges its visited
node/member separately. No no-error diagnostic string formatting occurs.

## M2 boundary and follow-up

M2 intentionally publishes first bindings. Its parser discards duplicate element
and notation declarations, so their source-level uniqueness cannot be recovered
from the effective model. The compiler rejects duplicates in an explicitly supplied
internal model, but does not claim to detect information absent from M2. The
coordinator integrated and verified an ordered M2 metadata repair separately on
main `14b7510` for source conflicts and cross-subset combination; the later facade will check that
retained evidence. Repeated attribute definitions remain M2 first-binding semantics.
No declaration source is reparsed or silently merged here.

## Validation and performance preparation

Focused correctness covers EMPTY/ANY/mixed/sequence/choice/all occurrences,
ambiguity, attribute/notation/default constraints, external default provenance,
borrowed ordinal matching, culture independence, malformed UTF-16, original
locations, aggregate/exact/one-over quotas, cancellation and 64 concurrent reuses.
An independent regular-expression language oracle exhaustively enumerates all
four-name sequences up to length five across at least ten deterministic models.

The Release sample `--control` verifies actual `.NET 10.0.11` on Arm64 and prints
`Demonstration=accepted 名,last; normalized default=one; unparsed entity=e; immutable schema reuse`.
Build/control/discovery run outside an exclusive lease. `[MemoryDiagnoser, ShortRunJob]`
benchmarks cover eight methods at NameLength 8/128/4096: transition/lookup hit and
last-character miss, enumeration hit/miss, owned compilation and composed model use.
`--measure` separately reports ten inclusive warm allocation numerators/100,000
operations, owned compilation/100, and inclusive whole-heap retention/100 schemas;
it makes no overhead subtraction. Warm allocation nonzero is an actual failure.
The task-owned NoEmit probe checks exactly 24 cases and the single ShortRun job
(3 warmup/3 measurement iterations, one launch); it never builds or generates under
the lease. The prepared plan bounds allocation at 60 s, benchmarks at 900 s, and
whole lifecycle at 1050 s with checked direct phase exits, scoped group cleanup,
initial/final identity and no-child proof. Freeze includes full runtime source,
probe/sample source/config/generated files, actual binary closure, entire pinned
SDK/runtime/host closure and stable effective environment.
Measurements below ran only under the coordinator prepared exclusive lease. Composition with the future tree
validator, broader hardware, AOT/trimming and release checks remain later owners.


## Executed evidence

Final implementation: `3ccd3c2ad9adefce71ebf943d48ceaa5c846541d`.
Measured prepared source: `d43355552ebc1a685ce45afcf8434a5da0a7482c`.
Initial preparation was `b7216c505ded6dca7f89d7187393123ee9d3cc7e`;
source review found IReadOnlyList-backed ReadOnlyCollection foreach enumeration
could allocate. Indexed loops replaced it before any measurement. No measurement
was performed on that initial enumeration shape and no speedup is asserted.

At final implementation, Release test build and explicit `--fx-version 10.0.11`
launch passed **71 focused cases**, plus **26 existing M2 model/shared M5 validation
cases**. Final sample build/control passed, printing actual FrameworkDescription
`.NET 10.0.11` / Arm64 and the demonstrated language/default/entity output above.
Planning validation passed all 153 task records; `git diff --check` passed.
The independently specified regex oracle asserts at least 13,650 exhaustive
comparisons and at least ten deterministic models; its successful test is included
in the focused count. No full milestone suite is claimed by this branch.

One precompletion InlineData surrogate case was serialized into replacement
characters by xUnit; the invalid UTF-16 regression now constructs its surrogate
inside a fact. One initial entity-work test used an unjustified fixed threshold;
its corrected exact successful-operation boundary now checks one-under failure.
These were actual precompletion failed checks, then corrected and rerun successfully.

Source review also found empty-model entry cancellation absent. After the measured
sequence completed and the lease was durably released, the compiler gained exactly
one entry `ThrowIfCancellationRequested` and a focused empty-model test. Warm
compiled lookup/transition/value source is unchanged from the measured revision.
All figures below are honestly attributed to d433555; owned compile timing is
**before that entry guard**, informational only, and is not a measured mean for the
patched compiler. No successful measurement was rerun for this guard/report change.

### Exclusive lifecycle

Grant `m5-dtd-model-d433555` was durable at coordinator main `1b85a35`; release was
committed at `77e2b67` immediately after lifecycle completion, before raw inspection.
`python3 docs/reports/M5-dtd-model/run_measurements.py --exclusive-grant` directly
executed allocation then benchmarks, with both phase exits 0, runner exit 0,
initial identity 0, final identity 0 and `no_child=true` after scoped group cleanup.
No builds/restores/generation/edits/tests/retries occurred under the lease.
Initial/final manifest SHA256:
`10c230866713d5a71fb9fee5bd462d0ae639a10c71f1a77c10e496a84db93a75`;
181 source/generated/config/runner records, 50 actual binaries, 3,852 full pinned
SDK/runtime/host records, stable environment. Actual host `.NET 10.0.11`, SDK
10.0.400, Arm64 Apple M4 Pro (14 physical/logical cores), macOS 15.8.1; tiered
compilation/PGO/ReadyToRun disabled, roll-forward disabled. NoEmit ShortRun
3 warmups/3 iterations/one launch completed 24 cases in 155.04 s.

### Separate allocation domains

| Domain | Inclusive numerator | Denominator | Analysis |
| --- | ---: | ---: | --- |
| Fixture initialization (parser/model/compiler/operation setup) | 22,712 B | 1 fixture | Cold setup, separate from warm primitives |
| Positive control, new byte[1024] | 1,048 B | 1 | Allocation counter detects owned storage |
| Each of ten warm borrowed scenarios | 0 B | 100,000 calls each | Transition hit/miss, element hit/miss, attribute lookup, enum hit/miss, list value, unparsed entity and notation |
| Owned compilation, nonempty sample | 531,200 B | 100 schemas | 5,312 B/schema at measured revision |
| Retained whole heap before/after | 375,832 → 534,584 B | 100 schemas | Inclusive delta 158,752 B, approximately 1,587.52 B/schema; includes retained array and whole-heap noise, no subtraction |

The dedicated warm counter gates passed exactly 0 B. BenchmarkDotNet's allocation
dash is rounded display, **not zero inclusive GC evidence**. Its positive 2,352 B
or 2,640 B totals remain disclosed in full below. Attribution to wrappers/runtime
is not established, and no unexplained overhead has been subtracted. Borrowed
inclusive B/op is 0 to 0.0201416015625 in these runs. Cold positive GC totals also
include all measured allocations. Latency is informational on this single Arm64
machine; comparisons of long names charge each inspected character and therefore
scale with name length. No hashing/SearchValues/SIMD optimization is claimed.

### Benchmark summary, including actual inclusive GC totals

| Method | Name length | Mean | Gen0 / Gen1 / Gen2 | GC bytes numerator | Operations denominator | Inclusive B/op |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| TransitionHit | 8 | 16.534 ns | 0 / 0 / 0 | 0 | 33,554,432 | 0 |
| TransitionLastCharacterMiss | 8 | 16.961 ns | 0 / 0 / 0 | 2,640 | 33,554,432 | 7.86781311035e-05 |
| BorrowedLookupHit | 8 | 23.714 ns | 0 / 0 / 0 | 2,640 | 33,554,432 | 7.86781311035e-05 |
| BorrowedLookupMiss | 8 | 25.891 ns | 0 / 0 / 0 | 2,640 | 33,554,432 | 7.86781311035e-05 |
| AttributeTokenHit | 8 | 32.491 ns | 0 / 0 / 0 | 2,640 | 16,777,216 | 0.000157356262207 |
| AttributeTokenMiss | 8 | 32.157 ns | 0 / 0 / 0 | 0 | 16,777,216 | 0 |
| ColdCompilation | 8 | 822.243 ns | 537 / 4 / 0 | 4,496,709,712 | 1,048,576 | 4288.39656067 |
| ComposedModelUse | 8 | 63.539 ns | 0 / 0 / 0 | 2,352 | 8,388,608 | 0.000280380249023 |
| TransitionHit | 128 | 168.565 ns | 0 / 0 / 0 | 2,352 | 4,194,304 | 0.000560760498047 |
| TransitionLastCharacterMiss | 128 | 170.585 ns | 0 / 0 / 0 | 2,640 | 4,194,304 | 0.000629425048828 |
| BorrowedLookupHit | 128 | 175.533 ns | 0 / 0 / 0 | 2,640 | 4,194,304 | 0.000629425048828 |
| BorrowedLookupMiss | 128 | 177.983 ns | 0 / 0 / 0 | 0 | 4,194,304 | 0 |
| AttributeTokenHit | 128 | 33.390 ns | 0 / 0 / 0 | 2,640 | 16,777,216 | 0.000157356262207 |
| AttributeTokenMiss | 128 | 33.159 ns | 0 / 0 / 0 | 0 | 16,777,216 | 0 |
| ColdCompilation | 128 | 1.742 us | 268 / 2 / 0 | 2,248,356,176 | 524,288 | 4288.39907837 |
| ComposedModelUse | 128 | 370.797 ns | 0 / 0 / 0 | 2,640 | 2,097,152 | 0.00125885009766 |
| TransitionHit | 4096 | 5.215 us | 0 / 0 / 0 | 2,640 | 131,072 | 0.0201416015625 |
| TransitionLastCharacterMiss | 4096 | 5.100 us | 0 / 0 / 0 | 2,640 | 131,072 | 0.0201416015625 |
| BorrowedLookupHit | 4096 | 5.058 us | 0 / 0 / 0 | 2,640 | 131,072 | 0.0201416015625 |
| BorrowedLookupMiss | 4096 | 5.046 us | 0 / 0 / 0 | 2,640 | 131,072 | 0.0201416015625 |
| AttributeTokenHit | 4096 | 33.336 ns | 0 / 0 / 0 | 2,640 | 16,777,216 | 0.000157356262207 |
| AttributeTokenMiss | 4096 | 33.561 ns | 0 / 0 / 0 | 2,352 | 16,777,216 | 0.000140190124512 |
| ColdCompilation | 4096 | 31.582 us | 8 / 0 / 0 | 70,263,472 | 16,384 | 4288.54199219 |
| ComposedModelUse | 4096 | 10.262 us | 0 / 0 / 0 | 0 | 65,536 | 0 |

The coordinator independently inspected the full allocation log, all 24 GC records,
actual runtime/environment and BDN summary, acknowledged the values, and authorized
raw cleanup after this durable summary commit. Raw logs/CSV/HTML/BDN results are
transient artifacts and are not committed. Historical tracked raw artifacts from
earlier milestones are untouched. Future full-tree validator composition and M9
broader hardware/AOT/trimming/platform checks remain their owning tasks.
