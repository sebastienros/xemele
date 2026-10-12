# Actual component mapping allocation evidence

Coordinator-run alloc-2 passed on the unchanged frozen fe72a347 probe/runtime,
SDK10.0.400/.NET10.0.11, macOS15.8.1 Arm64, workstation GC, with tiered
compilation/concurrent GC disabled. Durable cleanup release e8389790a preceded
root and worker raw inspection. Children28920/28922 exited0, were reaped, owned
groups absent and logs closed. All464 identities matched before/after. The
1.201985s supervisor lifecycle is cleanup evidence only, not workload timing.

Independent control process consumed the same19 routes/36643 units with every
allocation/GC reading disabled. Each route had one separately counted first
call,16warmups and64aggregate measured operations. No worker launched a
measurement, no retry occurred, and raw logs/JSON remain ignored artifacts.

| Owned route | First call B | Warm B/op (64 operations) |
| --- | ---: | ---: |
| Mixed zero compiler | 83792 | 81264 |
| Short valid name | 76928 | 76928 |
| Long ASCII name | 76928 | 76928 |
| Long multilingual name | 76928 | 76928 |
| Short invalid name | 4928 | 4816 |
| Long invalid name | 4816 | 4816 |
| Invalid supplementary name | 4816 | 4816 |
| Declaration whitespace normalization | 77200 | 77200 |
| QName whitespace normalization | 77144 | 77144 |
| Schema-ID whitespace normalization | 77176 | 77176 |
| Empty any namespace | 82888 | 79984 |
| Whitespace-only anyAttribute namespace | 79288 | 79288 |
| Public mixed-zero composition | 1502072 | 1464634.5 |

Six borrowed ComponentNCName routes (short/long ASCII, long multilingual,
short/long invalid and supplementary invalid) each reported0B first and0B over
64warm operations. This is the borrowed scan with prebuilt operation/state and
consumption without boxing, after fixture construction and preceding compiler
routes; it is not process-start initialization or owned compilation allocation.

Cold fixture setup:7758800B; positive1024-byte array control:1048B.
Sixteen retained compiled graphs:584032B before,1717608B after, delta1133576B.
That delta is a coarse process-wide live-heap reading, not a per-result object
size. Fixture setup, first calls, warm operations, positive and retained controls
are separate readings. Owned graph construction, normalized values and diagnostics
have explicit lifetimes and positive costs; they are not asserted allocation-free.

Only synchronous managed allocations were measured. Coordination covered Xemele
actors; no globally idle host, cross-project exclusion, latency/throughput,
optimization timing, x64 or AOT claim. The copied accepted supervisor bounds work
and cleanup; the result supports the task's allocation boundary, not release-wide
performance. See measurement-summary.json for concise identities and controls.
