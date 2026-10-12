# Bounded instance hint ordering

Decision frozen at `dbde48ae2` before runtime edits. Original fixture revision
`354550f94`: all seven desired INVALID assertions actually failed with complete
valid product results (SDK10.0.400/runtime10.0.11, build0 warnings/errors).
Original bytes, upstream revision, SHA256 pins, statuses and adapter limitations
are preserved in owned provenance.json. First authored foo1/bar3/no-namespace5
schemas are compiled explicitly through an allowlisted resolver; later hint
schemas2/4/6 are never acquired. Each instance keeps its original source URI,
caller stream ownership and DOM revision. No oracle expectations were changed.

Runtime/tests/check source `7cb790009`: one Enter hook plus new SchemaHints.cs.
Start tags are atomic. Ordinal probes borrow DOM strings, scan hint spans with
XML whitespace, and charge comparisons and table growth through the shared
ledger. No location strings/components are fabricated. Names under skip take
part in global ordering only: the skip corner is an explicit project
interpretation, not independently W3C adjudicated. Values.cs remains unchanged.
The simple scalar loop is the charged/cancellable correctness baseline; no
SearchValues/hash/SIMD optimization or throughput benefit is claimed.

Actual corrected owned58 pass (7 unchanged originals plus51 independent
neighbors). First draft build had CS0411 on non-generic Children.Single and
xUnit1051 on a missing test cancellation token; fixed before fresh execution.
First repaired owned execution passed55/failed3 work-boundary witnesses because
the test recorded a budget with a custom hook then used a real identity session
at the boundary. The test now uses the same hook for each run; runtime unchanged.
Corrected58:0 failures/errors/skipped/not-run.

`python3 docs/reports/M8-xsd-instance-hint-ordering/check.py` actually passed1557
distinct affected cases (including58 owned) plus42 resolver cases,0 failures/
errors/skipped/not-run. Public1430, all8 frozen declaration lanes and emitted
model139/facade7/compiler257/identity24/content9/evaluator2 baselines matched.
Five Release builds had0 warnings/errors; normal and true source-linked XML-only
facade consumers passed; planning196 and diff checks passed. No WarmBorrowed,
allocation counters, heap probes, BDN or performance measurements ran.

Counter-disabled preparation commands:

```
python3 docs/reports/M8-xsd-instance-hint-ordering/prepare.py
python3 docs/reports/M8-xsd-instance-hint-ordering/measure.py --verify-only
python3 docs/reports/M8-xsd-instance-hint-ordering/preflight.py
```

Root reviews and launches the frozen prebuilt control/measure pair under a fresh
bounded authorization, with30s total/20s work/10s cleanup. Isolation/PARK and
exclusive host usage are not required under the newer direct human instruction.
Each child has a finite lifetime; source/runtime/workload identities and owned
process cleanup remain required. Worker does not launch counters or inspect raw
measurement output before the root's cleanup release. No host-idle claim.

Six borrowed primitive routes target0 steady-state allocations (short/long token,
whitespace, equal/near-miss ordinal comparison, warmed retained-table hit).
Seven full validation routes own setup/context/table/results/diagnostics and
should allocate positively. Cold fixture setup, 1024-byte positive control and
16 retained invalid results are reported separately. The illustrative whole-heap
delta includes fixture/runtime effects; it does not establish exact retained
namespace-table memory. Synchronous Arm64 evidence cannot establish async,
x64/AOT, throughput or full corpus claims. BDN classes are supplied with
MemoryDiagnoser/ShortRunJob; no timing authorization or run is claimed.
