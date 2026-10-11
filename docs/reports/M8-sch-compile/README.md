# Schematron compiler evidence (in progress)

Owned source/test draft compiles on .NET SDK10.0.400/runtime10.0.11 with zero
warnings/errors. The frozen 154 compiler record/interface members match the
unchanged owned subset of Schematron.golden.txt; this task adds no public API.
A genuine source-linked XML-only compiler consumer passes detached models,
separate phase bindings, match selectors, subjects and preserved M4/M5 behavior.

The first test build failed eight test-only xUnit1051 missing-token checks;
corrected. A first focused execution passed63/65, with two fixture-helper XML
apostrophe escaping errors; corrected. The initial resource test build failed
one incorrect test property name ReferrerUri; corrected to the actual Referrer.
A second focused run passed all78 before the normative final-referrer expectation
was corrected and edge regressions were added. Third focused:79/81 passed;
failures were bare `id` selector anchoring and the shared model referrer bug.
Bare id was fixed; its focused regression then exposed a second source defect,
unused abstract diagnostic references being accepted. An authored-reference pass
now validates these independently of concrete expansion. No failed run is
represented as a pass. Final source revision/checks and measurement remain
pending, as does the independently owned model repair.

Only setup/owned graph allocation measurement is planned. There is no scan
optimizer, throughput claim, borrowed primitive zero-allocation claim, BDN run,
cross-architecture result or conformance-corpus claim. Prepared counter-disabled
controls and root-approved lifecycle are required before any measurement.

See [facade-join.md](facade-join.md) for exact profile/error-timing boundaries and
required subsequent evaluator proofs.
