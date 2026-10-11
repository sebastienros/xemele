# Resource source identity and effective base repair

The concrete internal `SchemaResourceLoader.Load(reference, effectiveBaseUri, operation)` resolves lexical href against the explicit base while retaining `reference.Referrer` as the actual referring resource identity. Explicit null means no resolution base: relative references are denied even when options have a base; absolute references work. Relative base arguments fail before resolver access. The unchanged frozen two-argument interface preserves its old reference/options fallback. Diagnostic locations are never routing inputs.

RNG include/externalRef and XSD include/import/redefine now pass scope.SourceUri as referrer and inherited context.BaseUri separately. Cache/cycle identity, source snapshots, quotas, cancellation, resolver exceptions and exactly-once disposal remain in the same implementation. The request still has Schema kind, original lexical SystemId and null PublicId.

Schematron must consume this overload only after the task is verified: pass its actual final source URI in reference.Referrer, inherited xml:base separately, and preserve its independent cycle/fragment logic. No frozen model or public signature changes.

## Actual integrated verification

Source commit `adb2b4ef82b5a56221306875255930c90f71320e` was merged with current coordination main and tested at `0b862ae46cf928f6d43fd9ec72e7c21a50d1ba87` using `python3 docs/reports/M8-schema-resource-base/integration-checks.py`. Release SDK 10.0.400 / Arm64 .NET 10.0.11 builds had zero warnings/errors. All **243** tests passed: 11 owned, 88 RNG, 72 XSD, 30 model/source-location and 42 safe resolver. Each XML result was parsed and its exact count asserted, with zero errors/failures/skips/not-run. The allocation resolver case was not selected. Frozen RNG119 and XSD257 members matched, public union1393 matched, and genuine XML-only RNG/XSD consumers passed. Planning177 and whitespace checks passed. Original milestone evidence remains historical evidence.

Independent reviewer `/root/review_m8_model` accepted exact source `adb2b4ef82b5a56221306875255930c90f71320e` with no blockers; this was source inspection, not execution. Loader SHA256 `9536db0b3705f61cf091d3c63a7cbd80a6701ce3b9f3f4d54fe9e454a8f0402e`; tests `ccbb8105449e839e78890df609c5077d1e4d83cd1471cc25e0364886fedba57f`.

The first branch check executed 11 owned +88 RNG +68 selected XSD successfully, then honestly failed its expected72 assertion because four XSD LocationTests were omitted. The corrected selection includes that class; an unexecuted wrong consumer filename was corrected before the integrated run. No runtime failures were hidden and the incomplete script was never called a pass.

## Allocation scope

This is a routing correctness change: two already-owned Uri references are passed separately through an internal overload. It adds no token scan, scratch storage, result allocation, cache or retained object. The existing Uri resolution, request/resource construction, parsing and owned graph boundaries are unchanged. Earlier model/RNG/XSD allocation reports remain evidence for their historical revisions; no new allocation or timing run was performed and no speed or exact new-revision byte claim is made. URI-dependent I/O and graph construction continue to be owned setup costs.
