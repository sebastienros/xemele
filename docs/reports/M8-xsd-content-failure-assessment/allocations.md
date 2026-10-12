# Allocation evidence — content-failure continuation

Root launched the accepted frozen `bbe7442014d7cba8ec9da3ad4ba514f3547b1e52128a5b22e4f8ef07ad3c54db`
manifest under `M8-three-prepared-repairs-allocation-1`. Durable root RELEASE
`f42e1a5e8abc41ab3a94e080e24d88c9a35e8810` preceded raw inspection. This worker inspected its
preserved raw control/allocation/lifecycle/release files once after that release.
Prepared source: `c7d92f0624b189658fd42d56bf43a537c1f104a5`; runtime repair:
`eb2dd42c84c560e9d4337befb629c624627e4eb9`.

.NET 10.0.11, Arm64, macOS 15.8.1; workstation GC, tiering and concurrent GC disabled.
Each synchronous route has 16 warmups followed by 64 consumed calls. One fixture owns
the compiled schema and seven prepared instance documents; fixture construction and
input generation are outside warm counters. DOM routes reuse those inputs. The string
route includes parsing and owned DOM construction. These are allocation ownership
measurements, not latency, throughput, baseline improvement, x64, AOT, async or corpus
conformance evidence.

| Route | Aggregate bytes | Calls | Bytes/call |
| --- | ---: | ---: | ---: |
| valid-local-dom | 867,840 | 64 | 13560 |
| rejected-one-dom | 800,256 | 64 | 12504 |
| rejected-1025-dom | 154,416,640 | 64 | 2412760 |
| rejected-2048-dom | 307,883,008 | 64 | 4810672 |
| later-datatype-dom | 933,376 | 64 | 14584 |
| rejected-1025-string | 185,295,072 | 64 | 2895235.5 |
| independent-diagnostic-refusal | 139,279,480 | 64 | 2176241.875 |

All seven routes assert the independent validity/completeness and diagnostic counts.
The one/1025/2048 unmatched-sibling routes finish complete-invalid with one initial
content diagnostic under unchanged Diagnostics1024; later datatype errors remain;
the independent-diagnostic route genuinely refuses at the same cap. Every owned route
is positive: run/context/cursor/traversal state, owned results and bounded diagnostics
remain allocations. String parsing additionally owns a DOM. The added per-frame bool
introduces no new allocation site; these measurements do not claim validation is
allocation-free or attribute a per-object size change to the bool.

Cold fixture/static setup: 4,808,136 bytes for one construction.
Positive control: 1048 bytes for one 1024-byte array.
Whole-heap estimate: 1,914,392 to 1,919,864 bytes,
delta 5472 while 16 one-error results and the fixture remain alive.
This process-wide estimate is illustrative, not per-result retained size or a leak bound.
The separate disabled control has all allocation/heap fields zero, the same seven
routes/64 calls, 576 total calls including warmups/retained results, and 82,992 consumed
units. This probe has no borrowed primitive route and makes no zero-byte claim.

Root session62384 ended exit0; own children35330/35332 exited0, were reaped, had absent
owned groups and closed logs. Own448 identities matched before/after; the serial root
sequence matched1358 and cleaned all six children. Each unchanged supervisor has
30s maximum/20s work/10s cleanup; global scope90s, every child latest startgrant+60,
absolute expirygrant+90; no retries/builds/extensions. Raw outputs remain under ignored
`artifacts/M8-xsd-content-failure-assessment/measurement-1/`. Coordination was limited
to Xemele; no global host-idle claim is made.

Source DLL SHA256: `2D20A79635BB972766B84903B5930BD351916391A1D68D94E96EA3689A7EAE51`.
Probe DLL SHA256: `224B77B9566333AC2F5924DC76BE3FAFD2D65D9DD65D4A27863122F55A7917AC`.

The three actual Z034_a2/a3/b counted-matcher diagnostic-limit failures remain pending
integration/follow-up evidence. These independent witnesses are not those executions.

