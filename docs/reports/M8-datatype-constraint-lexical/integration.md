# Coordinator combined-tree verification

Root coordinator actually tested isolated combined candidate
`e66a40943021b76c57923d3440446c4bfdcb43d0`, containing accepted helper runtime
`a58aed481ea2c6831111b73d037f92d1da21a2c1`, current integrated M8 runtime and the
separate lexical repair. Its actual command was
`python3 docs/reports/M8-datatype-constraint-lexical/checks.py`.

The root run passed 731 tests (86 own, 645 affected), zero errors, failures, skips
or not-run cases; Release builds had zero warnings/errors. Public compiled API
1,410 and frozen Datatypes/Facets 316 each match. Both normal and true XML-only
consumers pass. Planning 182 tasks and whitespace checks pass. Allocation
methods were excluded; counters were disabled. Bench/report/metadata-only
follow-ups do not require repeating source-unchanged correctness tests.

Root persisted the original two independent reviews and actual integration
results at `65458699`,
`docs/orchestration/review-M8-datatype-constraint-lexical.json`:
check-log SHA256 `1c7aece721c1a4996a4f0cf3118f70d57aa4c6b330fda38e086ed2ece12c6463`,
test-XML SHA256 `23647a9a1e495b1c5db9e9d882273d09f971111e18fd40299a9d3ae27ef70c9f`.
The worker read that record from the known commit and recorded this concise
summary. Raw test XML/logs are not committed here.

Allocation evidence and coordinator final task verification still remain.
