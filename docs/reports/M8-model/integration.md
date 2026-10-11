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

All eight prepared file hashes matched
preparation.json (SHA256 ba37e3084aedb96bf4502ea82b1bc9ea318b3f372a9cc1e43689fe7797442c4b).
The single bounded lease M8-model-allocation-1 was granted at main5a550ea after
main PARK, then RELEASED at c54c929 before raw inspection. Child83029 exited0 in
0.137156seconds, was reaped and output drained, with no remaining process group.
All eight prepared identities matched before/after. Root independently inspected
all11 distributions:64 observations each after32 warmups. Routes0–5 were exactly0B
for every observation; owned setup512B/op, namespace/report snapshots1752B/op,
direct M4 evaluation5408B/op, shared capped XPath5528B/op, and external composition
59496B/op were constant across their64 observations. Each recorded numerator
matched the sum of its distribution. The shared XPath join therefore adds120B/op
in this fixture. Positive control1048B; cold setup130856B. Retained memory before/
after159552/329896B for256contexts is an informational whole-heap observation,
not a portable per-context cost. No throughput, latency or global host-idle claim.
Raw correctness/allocation output is transient and will be removed after final
integration/inspection; only concise summaries and identities are durable.

Final integrated source/evidence revision: `04a2d5f95ffdb8b9c43cf7602bef0f8c65d6e16f`. Metadata-only integration retained the checked runtime inputs and valid execution evidence; no runtime rerun was needed.
