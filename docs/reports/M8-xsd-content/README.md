# Internal XSD content deliverable

The branch supplies `XsdContentCompiler : IXsdContentCompiler`,
`XsdContentMatcher : IXsdContentMatcher`, isolated `IXsdContentCursor` instances
and immutable opaque programs. Every compilation checks all declared types,
including unused anonymous redefine carriers. Checks cover UPA, element
consistency, occurrence/compositor rules, restriction/extension correspondence,
extension-first history, substitution blocks and effective attribute/wildcard
restrictions. Matching consumes child names and returns declaration IDs plus
strict/lax/skip policy.

Programs retain sealed particles and owned arrays exposed only as read-only
spans. Cursor banks are run-owned and grow lazily; repetitions use counters
without schema-bound state expansion. Traversal, state copies and integer work
are charged and cancellation-polled. Nullable-body dominance preserves exact
nonnullable state; UInt64 exhaustion uses reserved, charged arbitrary precision.
The warm wildcard loop reads the immutable namespace list by index.

Final implementation source is `127d15b2fedd1675572bad17b4822c80ee94d241`.
It consumes both verified declaration-compiler repairs from main `6de242de`.
The actual focused check passes 285 cases (102 owned), zero errors/failures/
skips/not-run on .NET 10.0.11 Arm64. It includes the preserved lost-intermediate
wildcard negative, corrected effective-empty positives and original nonempty
extension rejection. Public comparison matches 1,400 members, frozen content
comparison matches nine, and the true XML-only consumer passes. Release builds
have zero warnings/errors; planning validates 179 tasks. Independent bounded
source and correction reviews accepted the exact implementation.

[Measurement evidence](measurement-2.md) records the granted, cleaned-up run:
all 64 direct samples on each of five warmed routes are zero bytes. Owned lazy
cursor, initialized cursor/banks/operation, compilation, real XML composition
and diagnostics have explicit costs; cold and retained observations are
separate. Five MemoryDiagnoser ShortRun NoEmit cases complete. Four raw BDN GC
totals are nonzero despite rounded zero-byte displays; the report preserves
exact totals/denominators. Timing is provisional for one Arm64 run, with no
repeatable speed or cross-architecture claim.

Historical source-review corrections and pre-repair failures remain in
[review-corrections.md](review-corrections.md). The first allocation attempt
failed before saving numeric data; [its report](measurement-1.md) preserves
that fact, the exact enumerator cause and the bounded correction. The successful
attempt used fresh output paths and persisted observations before assertions.
Raw/generated outputs remain ignored and preserved for coordinator inspection.
The task is implemented on this branch; coordinator integration verification is
still required.

## Required later joins

The graph preserves IDs, types, fixed/default lexical values, namespace context
and actual source locations. Datatype/facet/facade finalization must check typed
element fixed-value equality, declaration/binding lexical-value consistency,
ID/NOTATION/ENTITY and simple-type/facet validity, including unused declarations.
Content checks enforce structural fixed presence and the attribute-use string
restriction. The later evaluator validates text/attributes/xsi values and
identity hooks; no executable datatype fallback or public schema facade is
supplied here. Real facade composition and x64 allocation confirmation remain
later-owner release checks.
