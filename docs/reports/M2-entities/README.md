# M2-entities branch handoff

DTD Parse mode now composes verified DTD/resource hooks with internal and external entities, defaulted and typed attributes, ID tracking, standalone rules, cumulative quotas, cancellation and exactly-once cleanup. Prohibit remains the default. Runtime implementation lives in Parsing/Core and Parsing/Entities/Runtime; the foundation contracts and shared build configuration are unchanged. No public API additions, so no new API baseline fragment is required.

## Checks and identities

- Correctness source `978317bb347e3d7a70715e098b2c34530ef40656`: full MTP 5148 passed, zero failures/skips; direct/discovery 4594 runtime +255 conformance +299 differential tests; 79 owned entity tests and nine foundation tests. Earlier identities preserved with only the two authorized transitional Parse-mode theory changes. Actual public API 272 members/zero additions; independently compiled approved/actual internal constructor/context/hooks contracts equal. Fresh local package consumer and real DOM/SAX/context/resolver ownership sample pass. Full fresh BCL copy provenance includes 242 records, 239 matching rejections and three documented divergences. See correctness-978317b/summary.json and preservation.json.
- All original 363 compiled inputs remain byte-identical to that correctness source. Additional owned provenance/build-only diagnostics bring the final source closure to 367; parser/sample/timed benchmark contents are unchanged.
- Allocation source `961a74831935635d8d20df499e8b9060fe9e0262`, exclusive grant m2-entities-961-exclusive-20261010: 10000 iterations, warmed ordinary/entity parser and borrowed lookup each zero allocated bytes; escaped positive control 320000 bytes. Cold core/context costs are separate. Caller buffer released while exact owned DOM/DTD retained after one outside-interval collection. This successful allocation run was never repeated. See exclusive-allocations-961/summary.json.
- Timing/driver source `17e85d806303956303ee8a7591e3b777e78e1256`, exclusive grant m2-entities-17e85-variant-exclusive-20261010: one bounded invocation, six cases, 18 Actual +18 Result +six GC records, all child/build exits zero. Every generated complete 44-file raw closure and project/code identity matched the independently built expected variant at the exact future output/artifact paths. Child executable and production-containing assembly full versions, runtime 10.0.11 and Arm64 checked. All 367 source and eight parent binary identities plus preparation manifest stayed frozen. See exclusive-benchmarks-variant-final/summary.json and final-preparation-17e85.json.
- Eleven lightweight driver/cleanup/variant tests pass, including mocked outer missing-executable/library durable evidence and cleanup. Five final Release builds and complete sample/standard/generated benchmark oracles pass; main and standalone discover all four methods/six cases. Planning validates 124 tasks; final ownership/diff checks recorded in task evidence.

## Actual timing observations

| Method | Length | Mean | Allocated |
|---|---:|---:|---:|
| InternalDocument |16|7.449 us|22.2 KB|
| ExternalDocument |16|9.073 us|67.82 KB|
| InternalDocument |4096|233.988 us|138.8 KB|
| ExternalDocument |4096|141.706 us|101.86 KB|
| GeneralLookup | |3.790 ns|0 B|
| DefaultLookup | |3.742 ns|0 B|

These are informational macOS Arm64 ShortRun observations, not cross-architecture or speedup evidence. Full DOM composition intentionally retains its allocations; the exact-zero probe covers warmed SAX/core and borrowed lookup.

## Failures and limits

Historical failures remain recorded: the inherited framing allocation guard failed at175635c before its integrated narrow repair; initial benchmark961 executed cases but its generated-child SHA gate failed, so those timing observations remain unverified. No later build reconstructs that missing child identity. Preparation/mock/API-discovery failures have concise records and are not passing checks. Existing historical artifacts were not rewritten; new raw logs, XML, BenchmarkDotNet outputs and generated child payloads were removed after inspection. Only concise summaries, actual metrics, hashes and provenance remain.

Eight pinned W3C simple document cases and the BCL whole-root oracle are limited coverage, not full XML conformance or a mixed-node ordering oracle. Only verified integrated prerequisites were consumed. The coordinator must integrate this branch and rerun relevant checks before marking it verified; no dependent milestone is unlocked by branch implementation alone.

## Coordinator integration command

After merging into main, run the following with pinned SDK10.0.400/runtime10.0.11 and an unused report prefix:

```sh
python3 -B docs/reports/M2-entities/verify.py --expected-branch main --audit-base a29d2dac7bdd4c36c51797bd21752ebcec6b81e9 --report-prefix integration-m2-entities-final
```

This runs fresh integrated correctness, contracts/API, package/sample/oracles, reference provenance and planning/ownership checks without timing or allocation mode. Preserve measured known-source results; do not repeat the successful allocation or timing runs. The committed premeasurement manifest describes measured17e85, so it is historical evidence after later metadata commits, not a grant to run against a different HEAD.
