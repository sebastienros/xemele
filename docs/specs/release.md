# Release contract — M9

Decision owner: `M9-contracts`. The M9 freeze is [release-freeze.md](../decisions/M9/release-freeze.md), with exact [inputs](../decisions/M9/release-inputs.json) and [remaining mandatory work/issues](../reports/M9-contracts/remaining-plan.md). Earlier milestones are usable independently; M9 adds release evidence and focused compatible fixes rather than completing hidden prerequisites.

Independent lanes after the release contract:

- API review: compose task-owned API fragments and verify actual assembly signatures, documentation, migration examples, exception/nullability contracts, and compatibility with the first preview. No bulk rename without an explicit change decision.
- Performance: reproduce [targets and protocol](../performance.md), raw measurements and environments. Report each reference separately, allocations/live memory separately, and every exception to the targets.
- Distribution: deterministic packaging, license/notices, Source Link, package install/consume, trimming and NativeAOT smoke tests on the required OS/architecture matrix.
- Robustness: run [testing gates](../testing.md), full corpora, sustained fuzzing, minimized regressions, cancellation and resource budgets.

These tasks primarily own reports, samples, and packaging configuration. Performance/correctness fixes discovered during review are new narrowly owned repair tasks assigned to the relevant module. Do not let a report task edit arbitrary runtime folders while other reviews run. Add those repair dependencies to the release gate and rerun affected evidence at the integrated revision.

The final gate verifies all previous gates plus these lanes, full capability inventory, unresolved issue list, public API surface, and actual package behavior. It records exact integrated revision/toolchain/corpus/benchmark metadata. Publication remains a separate explicitly authorized operation; creating a verified package does not imply uploading it.

Report locations: `docs/reports/M9-<lane>/` for durable summaries, `artifacts/M9-<lane>/` for bulky raw runs with links/hashes. Gate evidence includes durable outcomes even when raw artifacts expire. Known exclusions are published alongside supported profiles; no blanket parity claim exceeds verified inventory entries.
