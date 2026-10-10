# PI allocation probe repair

The only existing test changed is
`PiTargetTests.RepeatedBorrowedPiTargetsAllocateZeroAfterWarmup`. Both phases
now use one non-inlined helper checking NeedInput, complete chunk consumption,
and no diagnostic. It retains the same sink/cancellation snapshot, 100 warmups,
10,000 measured iterations, exact 0 B assertion, and default runtime/test settings.
Setup, root, warm callback assertions, completion, and final callback assertions
remain outside the allocation interval. Each chunk delivers three PI tokens,
ten callbacks, and four text units; measurement must deliver 100,000 callbacks
and 40,000 units, then consume the four-unit closing tag and complete.

`Diagnostics` is a separately compiled, synchronous replication using a simple
counting sink. It distinguishes options/core/sink setup, first root append,
first PI append, the other 99 warmups, measured 10,000 appends, and completion.
Splitting the warm scope allows first-use observation without increasing warmup.
It uses CancellationToken.None, rather than the test framework token, and its
counting sink lacks the test sink's unused pause/hook fields. Its original mode
replicates the old two-call-site/ignored-warm-results logic; equivalent mode
uses the checked helper. These bounded process-local observations cannot
identify causes of earlier process-local allocation failures. The actual test
is independently executed in focused and cumulative MTP and direct runtime.
No timing or retained-memory claim is made.

Historical reports copied byte-for-byte from preserved framing commit
`25d879f7db7a02adae2cdc8a2a267ab55c4f13aa` are under `history/`:

- checkpoint source `46104cecdce70170aea050612d13a77f1467251a`: cumulative
  MTP 2040 tests / one PI failure (1944 B); subsequent direct runtime 1490
  tests / all passed. `runtime-after-mtp.xml.gz` preserves that separate pass.
- bounded-checkpoint source `cea326c7ed59ac53a832af2f8cc4e528a118d89a`:
  MTP 2040 / all passed; subsequent direct 1490 / one PI failure (712 B).

These historical counts include unmerged framer tests. None are imported into
this branch's implementation or represented as current checks. No historical
failure is attributed to JIT, framework, parallelism, cold initialization, or a
runtime allocation without direct evidence.

Run `verify.py` with a new `--report-prefix`, `--expected-branch`, `--audit-base`,
and explicit `--runtime-count`, `--conformance-count`, `--differential-count`,
`--api-count`, `--fixture-count`, `--planning-count`, and `--task-count` values
for the integrated tree. Prefixes cannot be reused. The harness preserves
failures and independently continues checks after a check-level failure.
Prerequisite failures stop dependent execution. Runtime counters and XML
counts are actual observations, not inferred from task status. Reference runner
output is freshly built and copied in full before consumers; 242 records and
unchanged runner SHA are checked. Source, working, staged, untracked and
whitespace audits cover all owned paths. No thresholds, production code, shared
projects, or test parallelism settings change.
