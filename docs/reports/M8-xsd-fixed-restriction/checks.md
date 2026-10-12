# Correctness and source review

Runtime/test source: `0c54888ba0a4c2c010996eaabfda63c7f6ce8a50`.
Final check harness/driver: `a68d7330fef65d4c72353cd189f5673d375944ff`.
The coordinator reported bounded model source-review PASS for the former;
the reviewer did not build or execute tests. Runtime/test bytes are unchanged.

Actual `python3 docs/reports/M8-xsd-fixed-restriction/check.py` at the latter
passed 1,314 owned/affected cases (35 owned) plus the explicit safe 42 resolver
cases, zero errors/failures/skips/not-run. The selection covers content/compiler,
evaluation/identity, datatypes/core/numeric/text/temporal, facets, constraint
lexical, model, source locations and the ordered wildcard/effective-content
repairs. WarmBorrowed allocation tests are excluded. Release builds have zero
warnings/errors with SDK 10.0.400, runtime 10.0.11 on Arm64.

The unchanged approved M0 public extractor matches 1,410 members. Frozen content
9, model 316, compiler 257, identity 24 and evaluation 2 rows match; all 316
datatype consumer rows also match. New normal and true XML-only consumers reject
fixed decimal `2` against `1`, accept `01.0`, and exercise the resulting cursor.
The earlier independently compiled structural XML-only consumer also passes.
Planning validates 184 tasks; `git diff --check` passes.

Initial failures are preserved here: an unsupported `--filter-namespace` runner
argument exited 3 before tests; corrected to `-namespace`. One initial test
expected ArgumentException from the declaration-only registry, while facets
correctly returned schema rejection; the assertion now expects
SchemaCompileException and the 35-case rerun passes. The first broader harness
completed its underlying checks but its summary printer indexed an empty diff
output and exited 1; the printer was corrected and the full harness reran cleanly.
No runtime change was needed for those failures.

The counters-disabled allocation control and supervisor preflight pass six owned
routes and 442 frozen identities. The preflight child exited 0, was reaped, its
process group is absent, and its log is closed. The sandbox initially denied
`ps`; the escalated process-membership verification and preflight succeeded.
Measurements are pending the coordinator's exact grant. Compilation and typed
parse results are owned allocation boundaries; no zero-allocation primitive or
throughput improvement is claimed by this repair. No scanning/token optimization
or new SearchValues matcher is introduced.
