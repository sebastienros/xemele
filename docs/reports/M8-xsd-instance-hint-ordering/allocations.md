# Actual hint-ordering allocation evidence

Root executed one nonisolated prebuilt control/measure pair; durable cleanup was
recorded at `57dcf90cc` before raw inspection. No isolation, PARK, exclusivity
or coordination lease was required under the updated human instruction.
No worker counters, repeated correctness tests, rebuilds or repeated measurements
ran for this metadata finalization.

Runtime/tests: `7cb7900097490846259133cb0c7e3fdc10a75f7b`.
Probe source/binary preparation: `6eeb573efd25fb7d97f4fc3bb9dbd28a8a9c8c8b`.
Emitted source revision: `417b46aec229ba40142a4d129c16d3eb3426e3bc`, the
supervisor correction; runtime/probe sources and binaries remained unchanged.
Frozen manifest: `ed1a79edf6755f7067a7b081cb9d8633940ada223c676c23ac4d2d56db7ab6d5`.
All466 identities matched before and after actual execution.
Runtime DLL SHA256: `5A85E9AD588774F922895505F6EA7597000354BF9C12E76E1F08E4D234925CD3`.
Probe DLL SHA256: `BDE674CE20CDE63E59E177DAEAF8735AE94146C2A9C9FFC3B7970EEF04BBB7CE`.

Pinned SDK10.0.400/.NET10.0.11, Arm64, macOS15.8.1, workstation GC;
DOTNET_TieredCompilation=0, DOTNET_gcServer=0, DOTNET_GCConcurrent=0.
These are synchronous per-thread allocation counters. Fixtures, schema compilation
and DOM creation are outside the warmed route counters. Each route has16 warmups
followed by64 consumed calls. Table-hit state and primitive input strings are
initialized before these counters. Results are consumed with exact semantic
oracles; both modes consumed657312. No stopwatch/BDN/throughput run.

| Ownership | Route | Bytes /64 calls | Bytes/call |
| --- | --- | ---: | ---: |
| borrowed | namespace-token-short | 0 | 0 |
| borrowed | location-token-long | 0 | 0 |
| borrowed | whitespace-long | 0 | 0 |
| borrowed | ordinal-long-hit | 0 | 0 |
| borrowed | ordinal-long-miss | 0 | 0 |
| borrowed | retained-table-hit | 0 | 0 |
| owned | no-hint-dom | 839,168 | 13112 |
| owned | timely-hint-dom | 1,041,408 | 16272 |
| owned | late-hint-dom | 1,061,376 | 16584 |
| owned | skipped-late-hint-dom | 895,488 | 13992 |
| owned | distinct32-dom | 5,557,248 | 86832 |
| owned | late-hint-string | 2,037,808 | 31840.8 |
| owned | diagnostic-refusal-dom | 1,137,152 | 17768 |

The six borrowed scan/comparison/probe primitives actually allocated0 warmed
bytes. The seven full validation routes allocate owned operation/type/context/
frame/table/identity/value/result/diagnostic objects. Those figures are whole
validation-route costs, not isolated incremental hint-table costs or a before/
after performance comparison. Growth borrows existing DOM strings and reserves
array/entry ownership before allocation; no hint location/token strings persist.

Cold initialization of one complete fixture allocated3,120,352 bytes separately.
The positive1024-byte array control allocated1,048 bytes. Counter-disabled
control ran identical routes with every reported counter/heap field0; these
placeholders do not imply that the control allocated nothing.

After fixture/warm workloads, GC.GetTotalMemory(true) changed846,744→852,216
bytes (delta5,472) while16 invalid results and the fixture stayed alive.
This process-wide snapshot is a coarse illustration, not object-size, exact
namespace-table retention or ownership accounting. It may include runtime/GC
effects. No separately measured first-call or per-call allocation distribution.
No x64/AOT, async, full-corpus, timing/throughput or global-host-idle claim.

Root process bounds were30s total/20s work/10s cleanup, with finite latest-start
and absolute-expiry deadlines. Actual supervisor duration was below1s.
Control53665 and measure53667 both completed with exit0; each direct child was
reaped, its owned group was absent, and its log closed. Cleanup release was
durable before results were read. No child retries or repeated measurements.

Historical source-review P2 is preserved in preparation-summary.json: cleanup
could overwrite a timeout exit with0 while success ignored reason. Correction
`417b46aec` requires completed reason plus successful exit/cleanup in preflight
and measurement. Actual disabled finite lifecycle control at child52932 retained
TimeoutExpired with SIGTERM exit0, workload status1, successful cleanup and no
subsequent launch. Only the supervisor identity changed; no binary refresh.

Raw outputs remain ignored under artifacts/M8-xsd-instance-hint-ordering/
measurement-1. Durable summaries above preserve the actual ownership, controls,
identity and cleanup outcomes without committing raw logs.
