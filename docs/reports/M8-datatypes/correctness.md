# Correctness evidence

Initial source f042b43d1d086c9e7280406824cfa40fa4f4f3be passed the complete
correctness command with 349 tests (72 composition plus 277 accepted child/model
cases), public API1393, frozen internal/public datatype contract316, normal and
genuine XML-only consumers, planning176 and whitespace checks. Independent
source review found two registry blockers despite those passing checks: valid
no-target-namespace string/token definitions and an unmetered final map freeze.

Corrected source ee8514ad7b641fa46e6c889b7f5661050e5fe8d1 passed
`python3 docs/reports/M8-datatypes/checks.py`: 353 tests (76 composition,
148 text/binary, 76 numeric, 23 temporal, 18 core, 12 model), no failures/errors/
skips/not-run, exact public API1393 and frozen datatype contract316 matches,
normal and source-built XML-only complete-library consumers, planning176 and
whitespace checks. Every restore used the local package cache. All Release builds
had zero warnings/errors with SDK10.0.400/runtime10.0.11, single MSBuild node and
disabled compiler/build servers. Allocation/timing tests are excluded.

Composition's independent corpus executes all45 built-in metadata names, including
all three nonempty lists and textual anySimpleType. It covers XML-only whitespace,
Unicode/scalar failures, atomic item failures, snapshots, declared/canonical/RNG
names, cross-family primitive identity, QName aliases, NaN/signed zero, decimal
digits, absent-zone/duration partial orders, list/union/retype joins, exact quotas,
private refusal translation and caller/cancellation exception propagation. The
registry correction adds exact no-target-namespace custom/string/token definitions
without changing either RNG library, private-map snapshot retention, independently
calculated47+Nwork/366+Ncomponents over128 definitions, one-short independent caps
and deterministic cancellation during the64th caller entry.

These are authored focused tests and accepted child regressions, not an imported
external conformance suite or cumulative release gate. Source review is recorded
separately from execution. Measurement requires independent accepted source,
prepared payload identities and the root's serialized lease.
