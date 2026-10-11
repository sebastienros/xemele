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
now validates these independently of concrete expansion. Targeted regressions
then proved preparatory token-limit misclassification (2/3 passed), inclusion
cycle severity (0/1 passed), and outer-self-shadow initialization (0/1 passed).
Corrections preserve fatal SCHEMA.Limit and SCHEMA.ResourceCycle, charge every
foreign/inclusion/fragment child edge and whitespace scan, accept unnamespaced
annotations, and evaluate a shadow initializer using the outer same-name value.
The independent-review batch now passes92/93; only the normative final-referrer
case remains failing on the independently owned shared-model defect. No failed
run is represented as a pass. Final source/checks and measurement remain pending.

Only setup/owned graph allocation measurement is planned. There is no scan
optimizer, throughput claim, borrowed primitive zero-allocation claim, BDN run,
cross-architecture result or conformance-corpus claim. Prepared counter-disabled
controls and root-approved lifecycle are required before any measurement.

See [facade-join.md](facade-join.md) for exact profile/error-timing boundaries and
required subsequent evaluator proofs.
