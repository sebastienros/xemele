# Actual counted matcher allocation evidence

Root executed the unchanged frozen payload non-isolated under the direct human policy recorded at `411907159`: allocation evidence does not require isolation, PARK, ACKs or exclusive usage across threads. No foreign-project synchronization or globally-idle-host claim. The pending PARK proposal ended before any launch; the legacy `--granted` CLI arguments supplied manifest identity and finite deadlines only. This policy supersedes earlier preparation/standby language about fresh exclusive grants.

Tested source/preparation: `186e7d9d997546c15583a244e98565fd1f1fa965`, with verified-main joined correctness at `1fd1b477e6490a4ba4993fc772e33e65e8d681a7`. Runtime/probe .NET10.0.11, Arm64, macOS15.8.1, workstation GC, tiered compilation/server GC/concurrent GC disabled by the accepted supervisor. Assembly SHA256 `89D1D78104D4FCA06516BC578817F0A49C444ADBEBC2CB8FF87FB89E634F241E`; probe SHA256 `E43D578D79C5BBD36C8D197DC3398EACB2B85E2482446C65C201B3E31BC2EF03`. Frozen467identities matched before/after; manifest `20bc977cba9ac42632200914acb713dbb241e294ddc02f99f9f7ef8cbc4d418d`, supervisor `753954e05f1940edc8515ea3a5683cecdac42bf38feba0bd0d91e8c709cbdaaa`.

Separate prebuilt `--control` then `--measure` processes consumed the same128sink value. Controls disabled every allocation/retention reading; their zero placeholders are not zero-allocation evidence. Each route had32warmups then64samples; setup/fixture generation and both warm counted banks preceded the borrowed interval. Synchronous `GC.GetAllocatedBytesForCurrentThread` numerators include all route work; outcomes are consumed. Every measured per-operation distribution was constant across its64samples as shown below.

| Route | Ownership boundary | Actual bytes /64 samples | Bytes/sample (64 occurrences) |
|---|---|---:|---:|
| Dictionary name baseline | Borrowed comparison | 0 | 0 |
| Compiled name hit/miss | Borrowed comparison | 0 | 0 |
| Unbounded declaration cursor | Warm borrowed cursor | 0 | 0 |
| Nullable group cursor | Warm borrowed cursor | 0 | 0 |
| Wildcard cursor | Warm borrowed cursor | 0 | 0 |
| Lazy cursor object | Owned first object | 6,656 | 104 |
| Cursor initialization and match | Owned state initialization | 304,640 | 4,760 |
| All-type content compilation | Owned compiled program | 1,988,096 | 31,064 |
| XML compiler/content composition | Owned parse/compile/match composition | 11,316,224 | 176,816 |
| UPA diagnostics | Owned invalid-schema output | 1,751,552 | 27,368 |
| Finite counted partition cursor | Warm borrowed cursor | 0 | 0 |
| Counted cursor first initialization | Owned state initialization | 304,640 | 4,760 |
| Counted 1,000-child match | Owned complete cursor run | 759,808 | 11,872 |

All six borrowed targets have64actual zero samples; all seven owned routes have64positive samples. Cold fixture/setup allocated **475,664 B**. The independent1024-byte array positive control allocated **1,048 B**. These measurements distinguish steady-state borrowed matching from first initialization, durable results, retained state and diagnostics; the complete1,000-child route is not allocation-free.

Exact process-wide live-heap snapshots: before retained objects **550,776 B**; after retaining32programs **472,184 B**; after retaining32initialized cursors **764,808 B**. Program delta is **-78,592 B**, cursor-after-program delta **292,624 B**. These whole-heap snapshots are illustrative and include GC/lifetime noise. The negative program delta does not establish a negative or zero program ownership cost, nor may cursor delta be treated as a precise per-object retained-size measurement. Existing fixture/retained arrays were kept alive at the documented boundaries.

Cleanup was recorded before raw inspection: children47069/47071 both exit0, reaped, ownedgroups absent, logsclosed; root lifecycle revision `f4fe62c6d`. Actual lifecycle duration1.0458552500931546seconds is a cleanup observation, not latency or throughput evidence. No retries, remeasurement for metadata, BDN, throughput, x64/AOT, asynchronous or full-corpus performance claim. Raw test/measurement artifacts remain transient and were neither copied into source nor deleted. Worker inspected its own raw results once during authorized metadata finalization.

Earlier actual ten baseline refusals and three pre-join diagnostic-limit failures remain preserved. Joined exact original ten all complete with original metadata, including VALID Z036_b1/b2. Correctness/API/normal+XML-only results remain at their actual tested revisions; metadata finalization did not rerun runtime/tests/probes/measurement.
