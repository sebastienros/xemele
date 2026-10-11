# M8-model combined-tree correctness verification

Runtime combination: `a5c1af1e3a41f9ce22665ca8247e14d33ef6a455`, containing main
`4d6498ca8f6c7d8a4bf3e18a93fbb5ce978428fc` and worker source
`2ac7e6034a82d371e76608d0ad11fe2e2d8c8ea0`. Preparation handoff
`9dd073bb779ad9fe718e7e5258d57625c877354f` changes only the task record and reports.

The coordinator ran the commands preserved in integration-commands.py with pinned
SDK10.0.400 and actual Arm64 .NET10.0.11. Cache-only restores and Release builds
completed with zero warnings/errors. The resulting checks passed:

- 42 safe resolver cases and167 model/affected-prerequisite cases:209 total,
  zero errors, failures, skips or not-run cases; every XML result is Pass.
  The old resolver allocation test was not selected.
- Full compiled public API union:1393 members.
- Frozen model public/internal declarations:316 members.
- Real integrated consumer and XML-only source-linked consumer: both passed.
  The latter uses no HTML/CSS or future schema-engine implementation.
- Planning:175 tasks valid; whitespace check passed.

Initial root test selection combined method and class selectors, which xUnit
intersects, producing zero tests. The positive-count assertion rejected that
attempt. The corrected two commands produced the209 passing cases above. Existing
successful builds were retained; no allocation counters were enabled by these
correctness checks.

Allocation evidence remains pending. All eight prepared file hashes matched
preparation.json (SHA256 ba37e3084aedb96bf4502ea82b1bc9ea318b3f372a9cc1e43689fe7797442c4b).
No grant or model measurement has occurred. Raw correctness XML is transient and
will be removed after final integration/inspection; only this summary is durable.
