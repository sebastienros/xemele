# Controlled compiler allocation evidence

Source: dc3c6e2a2bd8a5268b708015ed825471fe2e2307. Release SDK 10.0.400,
runtime 10.0.11, Apple M4 Pro / Arm64, tiered compilation disabled, workstation
GC; the configured environment is recorded in preparation.json. The pinned
prebuilt probe and runtime/hostfxr,
source/build/tests/consumer/checker/control inputs were frozen by 497 identities.

The coordinator launched exactly two serial prebuilt children (counter-disabled
control, then measurement) under grant 419db89c96a16c6eca5d9bf98be8eaaf2814d1f1.
PIDs 28133/28135 exited 0, were reaped, had no remaining group members, and closed
their logs. Every identity and the manifest matched before/after. The supervisor
completed in 0.810758 seconds within its 120-second total bound; this is cleanup
evidence, not a performance result. Durable host release
df4ed5106d3058de348b85d4acc92ed41119ad4f preceded raw inspection.

Each warm route ran 16 warm calls, followed by **one counter interval containing
64 consumed calls**. Totals divided by 64 are aggregate means. There are no
individual samples, minima, variance or distribution claims. DOMs, fixture
strings, reflection-bound delegates and closures were prepared outside those
intervals; per-compilation graph/context/operation/adapter/diagnostic allocations
are included. The private projected-identity route reuses its prepared compiler
owner/operation and measures its owned output string. Work/component/cancellation
correctness is separately covered by formula-derived tests.

| Route | Allocation total (B) | Denominator | Mean (B/call) |
| --- | ---: | ---: | ---: |
| Cold owned parse + compile | 23,952 | 1 | 23,952 |
| Positive byte-array control | 9,728 | 64 | 152 |
| Owned ASCII graph | 528,896 | 64 | 8,264 |
| Owned historical-name graph | 529,408 | 64 | 8,272 |
| Owned projected library identity | 5,632 | 64 | 88 |
| Owned projected library graph | 540,160 | 64 | 8,440 |
| Owned long ASCII graph | 528,896 | 64 | 8,264 |
| Owned long Unicode graph | 9,973,536 | 64 | 155,836.5 |
| Owned malformed-percent rejection | 202,752 | 64 | 3,168 |
| Owned historical-start rejection | 215,552 | 64 | 3,368 |

The cold operation consumed a three-pattern graph; every warm route consumed
64 successful oracle outcomes. Control mode used identical semantics with all
allocation fields disabled. The long Unicode input requires projected strings
during syntax and parse reads; these are owned outputs, not borrowed primitives.
The adapter reserves one owner per compilation. Each changed library reserves
one string owner plus one component per 256 output UTF-16 units before allocating
a single final string, with every output character charged. ASCII retains its
existing string owner; no primitive zero-allocation measurement is claimed here.

Sixteen retained graphs produced a process-local approximate managed-memory
difference of 161,016 B; process working set was 56,590,336 B. These are approximate
single-process observations. This is allocation-only evidence on Arm64: no timing,
speed improvement, individual sample distribution, primitive-zero, or x64 claim.
Raw JSON and logs remain ignored transient artifacts; only the aggregate summary
and analysis are durable.
