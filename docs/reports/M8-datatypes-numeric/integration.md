# Numeric integration verification

Runtime-tested combined revision `49e98cf47976e6cd016a643a0ddb6e130bc0b9aa`
merges accepted source `9dee538394f076df86ea12ea282126fbe39da8f2` and prepared
payloads into the M8 coordination tree. The coordinator ran
`python3 docs/reports/M8-datatypes-numeric/checks.py` in the isolated integration
checkout on actual .NET 10.0.11/SDK10.0.400 Arm64. It exited zero:

- 106 tests:76 numeric,18 core,12 model; zero failures/errors/skips/not-run.
- 1393 compiled public API members matched.
- Genuine normal and XML-only provider/core consumers passed.
- All176 task records validated; Git diff check passed; builds had zero warnings/errors.

Handoff merge `13e9de4cae7be97b54c8fc22e1f56ec8ee826919` incorporates worker
handoff `9b261c2735743339ea5ec966ae680e8848998295`. Only two concise allocation
reports and task metadata changed after the runtime-tested combination. A zero-exit
Git diff comparison confirmed identical src/tests/benchmarks/build configuration,
consumer sources/projects, API extractor and check scripts. The real tested
revision remains integration_revision; no metadata-only rerun is claimed.

Independent source review accepted the bounded floating conversion after the
original unbounded runtime scan was corrected; the final opaque input is at most
1116characters. Decimal/integer exactness and formal digit measures remain intact.

Root independently inspected the released measurement lifecycle,13routes×64samples,
all10 BDN cases, source/payload identities and raw GC aggregate counts. Grant
530f968 and immediate release059ce1b bracket the one71.7018669169629second sequence;
children11035/11037 exited zero, reaped, groups absent/logs closed,438identities
stable. Eight borrowed probe routes measured all zeros. Owned allocations,
small unattributed BDN residuals, denied-priority timing limitation and inclusive
retained-heap estimate are preserved in allocations.md/measurement-summary.json.
No repeat measurement, universal-zero or cross-architecture claim is made.

Raw generated logs/results are transient and were inspected before terminal
checkout cleanup. Source, summaries and identities remain durable. The next
composition/facets tasks must preserve the numeric joins, including totalDigits
of .001 equal to3. No public library facade is introduced by this internal family.
