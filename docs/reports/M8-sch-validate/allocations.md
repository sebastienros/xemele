# Accepted-source owned allocation evidence

The single prepared measurement ran source
`43c6ab4cc789cc1e400dfdb2cea74e3851f681d7` under fresh host-wide
`M8-sch-effective-serial-2`, serially before the separately owned effective-content
probe. Root's durable final RELEASE
`076a64c0807942316e0802c815876b39ddf77ea5` preceded this worker's raw inspection.
The coordinator's released orchestration record includes the exact lifecycle.
There was one attempt and no retry. Earlier serial-1 aborted before any grant/run.

Hardware: Apple M4 Pro, Mac16,8, Arm64. Prepared Release build: SDK10.0.400,
.NET10.0.11, tiered compilation0, workstation/serverGc=false, concurrent GC0.
Manifest SHA256
`379c2d73c7333031792405f14403b11035806be6175b418ddebc862127bf75be`
verified all215 source/runtime/binary/control/supervisor identities before and
after. Source and binaries remain unchanged after the accepted closure review.

Actual supervisor command in `/Users/sebastienros/.codex/worktrees/8502/xemele`:

```text
python3 docs/reports/M8-sch-validate/lease.py --granted 379c2d73c7333031792405f14403b11035806be6175b418ddebc862127bf75be 2026-10-12T01:03:00Z 2026-10-12T00:59:38.348819+00:00
```

Supervisor status0, child75617 exit0/reaped, owned group absent, output closed;
no identity/manifest mismatches and rawInspected=false during the lease.
Its0.367794292s lifecycle elapsed value is cleanup/accounting evidence, **not**
workload latency. Overall root grant was60s total, each supervisor bounded to30s
(20work+10cleanup); this phase had29.951663s remaining, no extension.

The synchronous probe uses GC.GetAllocatedBytesForCurrentThread after prepared
schema/DOM/closure setup. Each warm route has16 unmeasured warm-up calls followed
by one consumed32-call aggregate window. All nine warm windows consumed32/32
results; the cold route separately validated its two fired events. The table
reports aggregate totals and arithmetic means, not individual operation samples,
distributions, confidence intervals or repeatability estimates.

| Route | Aggregate allocated bytes | Calls | Mean bytes/call |
| --- | ---: | ---: | ---: |
| Cold precompiled-document validation | 35,976 | 1 | 35,976 |
| Positive owned128-byte array control | 4,864 | 32 | 152 |
| Independent M4 selector owned snapshot | 360,704 | 32 | 11,272 |
| Valid owned events/result | 1,035,520 | 32 | 32,360 |
| Failed assertion owned findings | 1,072,640 | 32 | 33,520 |
| Phase/lets/subject/diagnostics/properties | 3,282,712 | 32 | 102,584.75 |
| Long multilingual message | 2,123,264 | 32 | 66,352 |
| Strict string parse and validation | 1,462,528 | 32 | 45,704 |
| Owned syntax diagnostic | 422,144 | 32 | 13,192 |
| Owned budget terminal | 63,488 | 32 | 1,984 |

The fixed instance is `<root><item code='x'><child/></item><item code='y'/></root>`.
The valid/failed/message routes operate on two matched items. The scoped route
has schema/pattern/rule shadowing, one selected phase, attribute subjects,
dynamic messages and diagnostic/property references. The long message is4096
U+754C scalars plus one supplementary emoji (4098 UTF-16 units), emitted twice.
The syntax route rejects truncated `<root>`; the budget route stops at work1.
All fixture generation, schema compilation, prebuilt DOM creation and callbacks/
closures are outside warm counters. The cold route is the first validation of
an already compiled schema and already parsed DOM, not cold Compile/Parse.
Stream parsing allocation is not independently measured by this payload.

Retaining16 scoped detached results increased post-collection process-local
managed memory by approximately101,552 bytes. The process working set at the
single final observation was55,115,776 bytes. These are total-process estimates,
not an exact isolated retained-object census or attributable resident memory.
No schema/document/result handles are added to published result models.

Run-owned allocations include variable maps, first-match tables and attribute
ordering scratch; repeated accounted M4 navigator/value/node-set snapshots;
owned XPath location literal/path steps and final strings; expanded message and
reference text; keyed traces, findings, diagnostics, immutable result collections
and public-model defensive snapshots. The strict parse route additionally owns
its parser/DOM/input state; syntax and budget routes own terminal diagnostics.
The independent M4 control confirms its own snapshot allocation, but has different
evaluation counts and result lifetimes; subtracting it from facade totals would
not identify a precise evaluator residual. The scoped route's fractional mean
also shows why aggregate values must not be presented as individual-call costs.
Exact causal attribution of that route's full allocation total remains unknown.

This implementation introduces no scanning/matching optimizer or primitive
borrowed-zero allocation promise. Owned persistence/setup/results intentionally
allocate and are measured here. No throughput/latency/speedup, BDN result,
zero-allocation, x64 confirmation, cross-architecture release, or full corpus
conformance claim follows from this one Arm64 allocation-only window. Those
broader release obligations are not silently represented as passing checks.
Raw allocation/lifecycle/log files remain ignored under
`artifacts/M8-sch-validate/`; only this durable summary and manifest are committed.

Root independently inspected the same raw output after RELEASE and confirmed all
figures, positive owned routes, and the fractional scoped aggregate mean. Raw
SHA2567bb12f4769e30defcd98d0a943e1ab99d79d96f5ce1992c01362a1a09e5d9d62
was independently matched by this worker. Root also reported actual combined
revision8917aceb24b132e0124405279fe9abd6359bca86 passing454 SCH checks and
282 compiler checks (overlapping predecessor cases, not736 unique tests), API
seams/public1400 and all consumers with zero build warnings/errors and planning179.
These are coordinator-reported combined checks, distinct from this branch's
454-check run and planning177; coordinator owns final integration/verification.
