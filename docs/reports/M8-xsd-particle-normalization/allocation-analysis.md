Actual particle allocation evidence and ownership

Root ran the single prebuilt control/measurement sequence against preparation commit 78c140bdec1fff43c4f19ec7111818f010525843 and manifest SHA256 38d3e41c885f9876d528c9b4a3de8908ef530819177306fbc569d4578704ab56. The 120-second bound includes 110 seconds of work and 10 seconds of cleanup. Root session90344 returned terminal0; children77071/77073 completed0 and were reaped, their owned groups absent and logs closed. Cleanup was durably committed before raw inspection at cdd93503c748b98b3d44e88e7769802e00021635. Root's independently checked compact summary was committed at 3ff6062b4833a0dc046c9821734d61715c837f8b; allocation-summary.json is a byte-identical copy from main docs/orchestration/M8-xsd-particle-allocation-summary.json. Worker performs no actual counter execution.

All465 frozen identities matched before/after root execution and during this final static check. Runtime/tests/fixtures remain identical to accepted15d975eba7c669e69d1934fa8df129a0814e6424; accepted allocation SOURCEbe7afddb6a0787e43cedd7768d7e2eadf677f5c0 and reviewed scripts remain unchanged. Runtime .NET10.0.11, Arm64, macOS15.8.1, workstationGC; tiering and concurrentGC disabled. No build or test rerun was performed for this evidence handoff.

One first call,16 unmeasured warmups and64 synchronous current-thread allocation samples per route cover24 routes and1536 warm samples. Every first call and warm sample is positive; each aggregate equals the distribution sum. The disabled control runs the same semantics with all managed-allocation/heap counter fields zero, consumption45455 and40 retained results matching measured mode. Disabled zeros do not establish allocation-free behavior. Setup allocated8830840 B; the1024-byte positive array control observed1048 B.

Representative observed per-call ranges (B):

| Operation | First call | Warm min–max |
| --- | ---: | ---: |
| Legacy elemZ027_e whole-graph join | 52096 | 52024–52024 |
| Batch elemZ027_e all programs | 53176 | 53176–53176 |
| Legacy unused addB194 carrier join | 226168 | 226168–226168 |
| Batch addB194 all programs | 232016 | 232016–232016 |
| Public addB194 schema compilation | 1046352 | 1046016–1047600 |
| Scaled16-element batch | 172952 | 172904–172904 |
| Valid64-member validation | 166536 | 166536–167096 |
| Invalid64-member validation | 169808 | 169248–169808 |

The compact JSON records all24 route aggregates/minima/maxima, including independently expected authored-choice, occurrence-bound, actual-group UPA, EDC/redefine and old-overload negative controls. Legacy and batch observations have different output scope: a requested program versus a retained map/program for every schema type. They do not establish a speed or allocation reduction. The batch correctness/default-quota improvement is supported by the earlier runtime tests, not this allocation comparison.

Cold setup owns pinned file reads, generated strings, parsed DOMs, cached declaration graphs/datatype registries and precompiled validation schema. Legacy joins own compiler/type-map/member-array/solver scratch, program arrays and cursor/operation state. Batch joins additionally own the returned map and per-type programs, sharing immutable setup graphs/particles. Public routes own component/registry/content/identity composition, schema and any per-call validation results. Diagnostic routes own exceptions/diagnostic snapshots and compiler scratch; per-call redefine stream/resource resolution and included-schema parsing are measured. Validation routes own operation state, results and diagnostics; setup DOM/schema parsing is excluded. Semantic checks are included. Sampling/reporting buffers allocate outside the counters. Returned outputs are consumed and kept alive through the boundary; compiler scratch expires after the call.

Five categories retain8 results each (40 total). Process-wide full-GC heap deltas are legacy programs -38912 B, batch maps306216 B, public schemas2810552 B, schema diagnostics14880 B and validation results -21248 B. These coarse incremental readings include collection noise and previous retained arrays/setup inputs. Negative deltas remain unchanged; they are not negative object costs, object sizes, peaks or evidence of zero allocation. No timing, throughput, native-memory, x64, AOT or global-idle claim is made.

Earlier runtime/API/test failures and the first sandbox lifecycle PermissionError remain preserved in their existing concise reports. This check satisfies branch allocation evidence; it does not claim integrated verification. Final process inspection found all owned lifecycle/preparation/preflight/measurement groups absent, zero owned background jobs. Raw control/sample/log payloads remain transient under ignored artifacts; no raw payload is committed. Root owns integration verification and any directed transient cleanup.
