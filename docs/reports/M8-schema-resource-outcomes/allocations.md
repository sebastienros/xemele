# Actual allocation evidence

Root executed the frozen prebuilt control then measure for this task in
`M8-three-prepared-repairs-allocation-1`. Durable cleanup RELEASE
`f42e1a5e8abc41ab3a94e080e24d88c9a35e8810` preceded raw inspection. The worker
inspected its preserved `measurement-1` results once after release. No new
runtime/test/probe execution or measurement occurred during finalization.

Measured source: `f8e885a08a10df4e208df3fd64d44abaaad6f1a5`;
accepted runtime/check source: `37f8176aa7147135db106d0b817564aa8ac438b1`.
Manifest SHA256: `e49d010a30b793a277eab3bebdb00ee3148d4fc91bfc4a7794d1a729345ef79b`.
All 448 task identities matched before/after execution and at final metadata
verification. Runtime binary SHA256:
`2b6782372578e5beec901188941dc7f4cf4a0611fa4ea97a81a74dbc932e5be0`;
probe SHA256: `94d2466b39c34fea385c1bb5fb6b84e2022fec64d3f5a76e210d7edf0b55d69f`.
Full frozen source/runtime/command identities remain in `preparation.json`.

Release .NET 10.0.11 / SDK 10.0.400, Arm64, macOS 15.8.1. Tiered compilation,
server GC and concurrent GC disabled. Each route had 16 warmups; owned routes
had 64 measured operations, primitives 1,024. Both processes consumed 5,616
with 3,536 total route calls and 16 retained documents. The separate disabled
control followed the same routes; its allocation/heap fields were zero
placeholders. Actual measured rows:

| Route | Operations | Total bytes | B/op |
| --- | ---: | ---: | ---: |
| optional-unavailable | 64 | 90,624 | 1,416 |
| optional-resolved | 64 | 3,795,456 | 59,304 |
| optional-legacy-denied | 64 | 153,088 | 2,392 |
| optional-malformed | 64 | 3,854,080 | 60,220 |
| principal-external-shared | 64 | 4,523,520 | 70,680 |
| direct-legacy-denied | 1,024 | 0 | 0 |
| adapted-denied | 1,024 | 0 | 0 |
| result-factory | 1,024 | 0 | 0 |

The direct legacy callback and new adapter share the same denial semantics and
both meet the zero-allocation steady-state target. Factory construction borrows
an already-owned wrapper and neither transfers nor disposes it; its fixture
disposes the original once. Owned routes construct fresh composition snapshots,
operations, loader state, requests and scope. Resolved routes additionally own
stream/wrapper/UTF-8 input, decoding/parsing buffers and DOM persistence;
refusal/malformed routes own bounded diagnostics and exceptions. The shared
route parses principal plus external input with one operation. No DOM,
resource I/O setup or diagnostic allocation is claimed to be zero.

Cold fixture setup allocated 4,864 B, separate from warm rows. This measures
fixture/request/resolver/resource initialization, not all first-parser or JIT
initialization. The positive 1,024-byte array control allocated 1,048 B.
Process-wide retained heap rose from 130,520 to 196,424 B (delta 65,904 B)
with 16 external documents live; this is a retained estimate, not exact per-DOM
attribution or a permanent-cache claim. Fixture generation remained outside
measured rows. No async, throughput, latency, x64, AOT or global host-idle claim.
BDN/timing was not executed; this is bounded synchronous allocation evidence.

Root session 62384 terminated with exit 0. This task's control child 35324 and
measure child 35326 both exited 0, were reaped, had absent groups and closed
logs. Own supervisor lifecycle status 0, before/after identities true, no
mismatches; elapsed lifecycle 0.5044349579839036 s. The unchanged proposal was
30 s total / 20 s work / 10 s cleanup per supervisor, within the serial global
90 s window and grant+60 latest-start / grant+90 absolute expiry. Across the
three-task window all six children cleaned up and 1,358 identities matched.
Root cleared the active lease and durably RELEASED before inspecting results.
Raw files and logs remain preserved; no retry or next grant is inferred.
