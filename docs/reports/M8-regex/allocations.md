# M8-regex branch measurement evidence

Runtime source: `7f693470168f481bb63812516bde66d5be9f0822`.
Prepared NoEmit harness: `0ecdc0cdbe4220b5bc81e1829e24e4042b9ea96f`.
Remaining-only supervisor: `ea9974285b08d0e9154a8d4a7d71f4b4cf016dda`.

Apple M4 Pro, Arm64, macOS 15.8.1; SDK 10.0.400, actual runtime 10.0.11,
Release, workstation GC, BenchmarkDotNet 0.15.8. All 42 prepared
source/binary/dependency/runtime/input identities passed before and after the
completed sequence. Preparation and concise summaries retain their hashes.

Two independently launched thin probes each exercised eight semantic routes,
32 warmups and 64 observations per route. All 1,024 warmed matching observations
allocated 0 bytes on the current thread, including the warmed pooled route.
The 128 owned-run observations each allocated 456 bytes. Both processes measured
41,808 bytes for cold fixtures/program/static setup, 10,256 bytes for the first
pooled match and 1,048 bytes for a positive 1,024-byte-array allocation control.
Program construction and owned run ledgers are separate setup boundaries; input
spans never survive a run. Only the actual thin-probe routes establish this warmed
zero-allocation result; pool misses/growth and arbitrary concurrency do not inherit
that promise.

Whole-heap retained measurements were 153,008 and 161,224 inclusive bytes for
256 compiled programs plus retaining-array/runtime bookkeeping. Both are
informational observations, not exact object sizes or portable memory contracts.

The prebuilt in-process NoEmit BDN suite had exactly eight borrowed-match cases,
one launch, three warmup/measurement iterations and MemoryDiagnoser. All eight
reports succeeded with nonnull statistics. No generation, restore or build ran
under the measurement lease.

| Case | Mean (ns) | Standard deviation (ns) | BDN bytes/op |
| --- | ---: | ---: | ---: |
| ShortHit | 117.96 | 0.95 | 0 |
| LongHit | 29,554.15 | 186.44 | 0 |
| EarlyMiss | 4,628.34 | 3.81 | 0 |
| LateMiss | 29,561.66 | 456.43 | 0 |
| Unicode | 154.50 | 0.56 | 0 |
| Nullable | 3,380.43 | 15.79 | 0 |
| Pooled | 175,496.69 | 3,062.29 | 1 |
| Malformed | 54.40 | 0.93 | 0 |

**The pooled BDN case reports 1 B/op over 4,096 memory-diagnostic operations.**
Both thin-probe pooled routes report 0 bytes after their own warmup. Attribution
is unestablished; no cause or all-BDN-zero result is inferred. No repeat was
requested or run to erase this discrepancy.

Lease 1 ran the first valid probe before the sandbox denied its supervisor's
process inventory. Root verified no residual processes and recorded durable
RELEASE ACK `a6181ba` before inspection. The successful first probe was retained.
Lease 2 used the approved remaining-only supervisor with execution escalation for
process inventory: second probe, then eight-case NoEmit BDN. Both children exited
zero, were reaped, groups drained, logs closed and all 42 final identities passed.
Durable RELEASE ACK preceded inspection. No other workload ran in this worker
during either lease. The partial receipt preserves the first lifecycle failure;
`measurements.json` records concise distributions/counts/identities and both
retained deltas. Raw outputs remain ignored and transient for root inspection;
they are not committed or archived.

These are provisional engine microbenchmarks on one Arm64 host. Straight span
loops are the baseline; no SearchValues/hash/SIMD optimization or speedup claim is
made. Full datatype/facet/facade composition is outside this worker's verified
dependency set. Gate-owned composition, cumulative corpus and x64 release
confirmation remain follow-ups; no normative regex feature is excluded.
