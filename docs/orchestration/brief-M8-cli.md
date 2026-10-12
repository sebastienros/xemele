# M8-cli dispatch preparation

This is a future assignment brief, not a claim or implementation start. Claim only after every task dependency is integrated and verified. One isolated worker owns its task JSON and the exact M8-cli paths; no edits to shared registry, project files, earlier commands, or validator engines.

## Acceptance before dispatch

Implement schema CLI commands through the existing static partial `RegisterValidationCommands` hook in an owned `Commands/Validation/Registration.cs`. Follow the existing XML CLI conventions and M6 exit/resource behavior; choose clear language-specific commands and document their actual syntax. Exercise public library entry points for all three validators. Schematron phase/report behavior must use its frozen public APIs. Preserve caller stream ownership and immutable concurrent schema use. No HTML dependency is needed for schema functionality.

Use verified XML command/resource helpers where their existing contract fits. Explicit input/schema path access is separate from resolver permission. No ambient resource grant: includes/imports/refs must pass the existing explicit local-root/catalog policy, preserving schema resource kind, final URI, cancellation, denial and disposal. Route any genuinely needed shared-file change as an exact owning request; do not copy an unrelated subsystem merely to avoid ownership.

Required checks: success and invalid schema/instance cases for each language; invocation errors, malformed input, stdin/files and output behavior; denial plus authorized nested resources and catalog routing; finite/exact/one-over shared work/resource budgets and cancellation; concurrent independent validations of the same immutable compiled schema with unchanged instances; deterministic shared resolver/resource/fuzz regressions in the owned paths. Fuzz tests must use explicit bounded seeds and report actual case counts. Preserve earlier XML/HTML CLI behavior through focused existing checks, and prove schema composition with a true XML-only build.

## Integration and evidence

Pin source and tested revisions, actual command outputs/counts, and public API baseline changes if any. Use the existing offline SDK/runtime/build conventions and compiled API extractor. Run task validation and diff-check. Source review should focus on invocation/resource/output behavior and three-library composition. If runtime parsing/hot-path changes arise, read performance-coding.md and prepare owned allocation evidence using existing infrastructure; no ad hoc measurement outside the host lease. Comprehensive cumulative/package/AOT checks belong to M8-gate. Keep only concise summaries and hashes; raw logs/results are transient.

Do not consume an active peer branch, alter corpus expectations, or turn failing product cases into exclusions. Report exact defects to the coordinator for ordered repairs. No M9 work or publication.
