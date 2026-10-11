# Focused correctness and contract review

Runtime/test source revision: ff628c8836f07f372c9c37ea3008e6bc62ef234c.
Preparation revision: c999c2cf9d836990c0342445ed68bc844e14bd79.
Process-inspection/manifest revision: 02d5f22728bd8256c02b18f66b35df2819aae2e9.
SDK10.0.400, actual runtime10.0.11 Arm64, Release; restores use only the local
NuGet cache with audit disabled. Owned `checks.py` contains reproducible commands
and asserts positive owned/affected prior test counts.

Thirty correctness tests passed: eighteen owned core cases and twelve prior
M8-model cases, zero errors/failures/skips/not-run. Public API union checker
matched all1,393 members. Genuine empty-list/union/retype/normalization consumers
passed against both normal and explicitly XML-only verified prerequisite builds;
no numeric/temporal/text-binary parser is required or advertised. Builds completed
with zero warnings/errors. Allocation resolver tests were not selected.

Owned tests cover all45 metadata names/bases/whitespace/families, exact scalar work
and retained component boundaries, XML-only whitespace, supplementary scalars,
malformed surrogates, short/overlapping destinations, owned input detachment,
pooled long outputs/refusal, immutable item snapshots, selected-member and declared
name preservation, primitive/derived identity/hash joins, canceled/stopped operations,
iterative nested union traversal, culture independence, Boolean TextBinary routing,
and direct Unwrap zero-cost polling with exact N/N-1 edges.

The initial build attempts exposed missing test imports/name qualification and
xUnit cancellation analyzer requirements; those were corrected before source freeze.
An initial three-case Unicode theory failure came from custom-attribute encoding
replacing unpaired surrogate literals. Those fixtures now construct invalid code
units at runtime and all three cases pass. A preparation-script count assertion
initially assumed20owned/10model; emitted XML established18owned/12model and the
script now asserts that actual split, while the total30 remained passing. No
failed check is used as acceptance evidence.

Coordinator read-only batch1 requested Boolean routing to TextBinary and zero
polls at Unwrap entry/return. Both corrections and their proving checks are in the
source commit. Coordinator's later source/preparation recheck explicitly accepted
ff628c8/c999c2c with no residual blocking review findings. The accepted source
freeze is `contracts.md`; original composer/facet tasks own the recorded joins.

Counters-disabled controls passed for the ten-route allocation probe and four
baseline/core fixtures. Discovery asserted eight ShortRun/MemoryDiagnoser cases,
one launch, three warmups/three iterations and in-process NoEmit toolchain; no
code generation/build is required inside a lease. Default sandbox denies ps;
approved elevated `preflight.py` proved process-inspection permission, reaped the
control child and asserted its owned process group absent. The measurement driver
requires the same elevated execution permission and verifies it before counters.
At this preparation checkpoint, measurement evidence awaited a separate durable
coordinator grant. The subsequently completed approved sequence is recorded in
allocations.md and measurement-summary.json.
