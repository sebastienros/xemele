# M8-cli handoff

Root tested `ae20a73ce8f263f67dcc8978d2cef627307b07b8` on `codex/m8-cli`.
Commands `validate-rng`, `validate-xsd`, and `validate-schematron` use the existing
public stream facades and guarded local/catalog resolver. Owned tests cover finite
budgets, resource aggregation/disposal, cancellation, exact/unknown locations,
independent concurrent runs, unchanged instances, staged SVRL and earlier CLI behavior.
No shared source, validator, public library API or corpus expectations changed.

Root's certificate-bound seven-command sequence passed:

- Normal probe:1156 assertions; XML-only probe:1157, including no HTML/CSS types.
  Each ran288 fuzz cases with seed81677; these are the same profiles in two compositions.
- Phase guards:10 bounded success/failure controls. Real CLI:38 controls, including
  prior XML/HTML behavior, actual read-only-stdout refusal and pinned EPIPE suppression.
- Full public API comparison, task validation (201 tasks), and diff-check passed.

Public exact compile-work/components/run-work boundaries: RNG2129/474/83,
XSD27466/3765/157, Schematron1315/50/904; nested compile-work2328/27701/449.
Exact and one-short checks required the actual fatal quota classifications.
Root outer session29432 exited0; all command/nested groups absent, input/binary/
certificate guards matched, deadline false, and inner raw output deleted.
Root preserves the concise authoritative result in primary's
`docs/orchestration/M8-cli-check-attempt-3.json`; local `results.json` supplies counts.

Original failures remain recorded in `preparation-failure-1.md`, `check-failure-1.md`
and `check-failure-2.md`: invalid helper entry point, temporary cleanup overriding
file exit2, and the incorrect authored closed-pipe exit3 expectation. Unix .NET
Console suppresses EPIPE; exit0 cannot establish downstream SVRL delivery. The
separate real read-only descriptor verifies observable write exceptions exit3.

Worker executable jobs:zero. Root launched all actual preparation/checks; worker
launched no builds/tests/counters and made no task status/criteria changes. This
final commit adds only this handoff; tested executable source remains equivalent
(source333 SHA256 `dddbb1e180737e65e827677b665edc3a835fd83f64cf47d9537c65d5daf52ad6`).
Primary integration/checks and M8 gate remain with root before `verified`.
No owning change requests remain. `list_artifacts` is available and returned an
empty attachment list; no managed-worktree archive identity is attached here.
Successful preparation receipts/certificate remain until root consumption/cleanup.
