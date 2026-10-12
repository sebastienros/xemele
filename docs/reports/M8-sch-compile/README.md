# Schematron compiler evidence (implemented on branch)

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

## Accepted repaired source and preparation

Compiler source `193a6fe3ddcdf209bea0c623b154cbb6abd99b9c` adopts verified
M8-schema-resource-base from coordination main095448a3. All93 owned tests now
pass, including the previously failing final-referrer/xml:base regression.
`python3 docs/reports/M8-sch-compile/integration-checks.py` at this revision
passed277 asserted tests (93 owned,11 resource-base,42 safe resolver,131
prerequisite), frozen154/public1393, genuine XML-only consumer, planning177 and
diff checks. All Release builds have zero warnings/errors. The coordinator
accepted the bounded review73ef790 and inspected the exact one-line adoption.

Supervisor checkpoint `837a001b0e7fb24f59220dfe43216d5fccabe781` carries the
already-verified absolute UTC start/expiry and no-overwrite guards. The prebuilt
source193a6fe probe passes all six counters-disabled routes. Elevated prepare
and verify-only confirm212 identities and actual process-inspection permission;
manifest SHA256 `be99198a08d5f36b09bf507dae06cc07869c630832865fdc01ed881fde92c54b`.
Scope: one synchronous prebuilt allocation-only child, maximum30s (25 work+5
cleanup), cold owned parse/compile, positive owned control, simple/abstract-phase-
message/long-multilingual graphs, invalid diagnostics,16 retained snapshots/RSS.
Warm routes consume64-call aggregate windows; these are not individual-operation
distributions. No actual allocation measurement has occurred at this checkpoint.
Root supplies exact grant UTC bounds and durably releases before raw inspection.

## Completed allocation evidence and handoff

The sole actual SCH probe succeeded under durable GRANT5ac7a081 and
RELEASE6b35e04d before inspection. Root independently accepted the same raw
figures and lifecycle. See [allocations.md](allocations.md) for exact totals,
means, cold/retained/RSS boundaries, identities and the unknown complex residual.
This task is implemented on its branch; integration_revision remains unset.
No runtime, test, build or consumer source changed after accepted193a6fe.
Coordinator reported277 combined checks ataa66f68e1f0c2169e08d7b9f0c1347e22cea761e;
final metadata still requires coordinator integration and verification.
