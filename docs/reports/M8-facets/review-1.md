# Failed bounded source review

Coordinator review of source 5ea017796b1e32924e2f6902057707b527dba08d plus
RngAdapter delta 5cdc839f597c6f51d0e460688f441057162926c2 found five acceptance
blockers. It performed read-only source/contract review without execution.
The earlier positive tests are preserved; passing tests did not establish these
missing semantics or arbitrary facet-number accounting.

1. Restricted lists/unions must accept item/member fields exactly retained by
   the verified XSD compiler (Compiler.cs:419), while rejecting changed items or
   reordered members. Every comparison requires charged/polled work.
2. Same-level length with min/max must satisfy XSD2 section4.3.1.4: the matching
   min/max value must originate in an ancestor without length. Direct
   string length2/min1/max3 was incorrectly accepted by both implementation and
   owned test oracle. Add inherited same-value controls and explicit repeats.
3. Equal minExclusive/maxExclusive endpoints may define an empty value space;
   definition checking succeeds and values1/2/3 reject cleanly. Equal mixed
   inclusive/exclusive endpoints remain invalid.
4. Cross-facet consistency must use effective scalars, replacing overridden
   same-kind ancestor facets. FractionDigits5 -> fractionDigits3 -> totalDigits4
   must accept1.234, with fixed/derivation checks retained independently.
5. Opaque BigInteger conversion/comparison in the facet-count layer lacked
   exact observable work, growth reservations and bounded polling. Replace it
   with an owned canonical decimal-digit representation and native small counts;
   demonstrate independent conversion/comparison cost/growth oracles, exact and
   one-short work/components, and cancellation inside long paths with null caps.

The approved explicit known-alias/base/variety RNG adapter, detached
no-target-namespace handling and downstream XSD NOTATION obligation remain
accepted. No allocation/timing request is ready until these blockers close and
meaningful witness regressions/accounting checks pass at a new source revision.

Actual pre-correction execution on 2026-10-12: ReviewWitnessTests total5, failed5, errors/skips/not-run0, .NET10.0.11. Compiler retained-member join rejected; direct length origin wrongly accepted; inherited changed minLength wrongly accepted; equal exclusive endpoints rejected; obsolete fractionDigits5 conflicted with totalDigits4. Initial witness build exposed xUnit1051 cancellation argument omission, corrected before execution. Historical773 passes do not establish these cases.

Corrected source: restriction shape validation compares each retained item/member name against the resolved base graph (including order), charging each inspected UTF-16 character. Effective scalar preparation selects nearest same-kind facets, replacing opposing inclusive/exclusive bound representations at the same direction while retaining historical fixed/derivation checks and inherited bound value-space rules. Cross checks use only that effective set. Length/min/max origin scans charged ancestor sets for matching values in an ancestor without effective length. Equal two-exclusive endpoints are valid empty spaces; mixed equal endpoints remain invalid.

Facet count storage is now native ulong through19 canonical digits or one owned canonical decimal string. For N zero digits: N work,0 count growth. For native nonzero values: input length plus canonical digits work,0 count growth. For N>=20 nonzero canonical digits:2N work,1 string growth reserved before copy. A sign adds one input visit. Comparison native/native or native/big costs1; big/big differing lengths costs2; equal-length values cost2 plus inspected digits (4096 equal/last-different costs4098). Direct helper tests use independent expected numbers, exact/one-short work, component refusal before copy, callback observation of reservation ordering, cancellation inside scan/native conversion/copy/comparison with null caps, and unchanged caller exceptions. Verified provider BigInteger code is untouched.

Actual corrected verification: targeted86 passed, then full804 passed (303 owned/501 predecessors), .NET10.0.11, zero failures/errors/skips/not-run. Frozen316/public1393, normal and genuine XML-only consumers passed;178 task validation and git diff --check passed. Correction attempt initially had one compiler local-name collision; initial number tests had three zero-option setup failures; both corrected and rerun truthfully. No measurement/control/discovery result is inferred.
