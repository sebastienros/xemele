# M3-push local measurement evidence

Implementation: `5dae4fb3eeb06d882016cae111fe5c28c5c76e9e`.
Probe-only correction: `adc750dec4a48b91870f789396ba05741fd88495`.
Runtime source and passing correctness/API checks remained unchanged.

Coordinator lease: `m3-push-adc750d-measure`, maximum 240 seconds. Prebuilt
allocation stage limit 120 seconds, benchmark stage limit 110 seconds. Both
actual exits were 0, without timeout, and both process groups were absent after
reaping. The wrapper exited 0 in 46.694 seconds. No retry or rebuild ran under
the lease. Exact commands, binary hashes and exits are in measurement-exits.json;
raw output is in allocation.log, benchmark.log and benchmark-results/.

Frozen probe SHA-256:
`fce9e08c2180eea4a3c5f8fe7399cc8fa1852e3184f11cb36027d184896c7786`.
Frozen runtime SHA-256:
`0560f4e1220f96c46a98e8c3fa5918d6220f662bb6c65a36986681c90cdf34c6`.

## Allocation and active retention

The primary input is one generated, reused 4096-byte ASCII text chunk inside an
open root. Strict options remain defaults except explicit unlimited
DocumentCharacters and TokenCharacters for the stress workload. Fixtures, parser,
handler and options are prepared before warm measurements. No input document or
event list is materialized. The parser remains accepting and alive throughout
all 64 active live-heap samples, taken every 16 MiB of the full 1 GiB stress.
Completion and release are checked only afterward.

| Observation | Actual result |
| --- | ---: |
| Cold owned setup, including fixture and first parser setup | 60,224 B |
| Warm 512 appends after 128 warmups | 0 B total |
| Positive new 1024-byte array control | 1,048 B |
| Stress input | 1,073,741,824 B |
| Live heap before stress | 266,352 B |
| Maximum of 64 active samples | 266,352 B |
| Maximum live delta | 0 B |
| Live heap after stress, parser still alive | 265,424 B |
| Terminal text count, including setup/warm/measurement chunks | 1,076,363,264 chars |
| Root start/end/document completion counts | 1 / 1 / 1 |
| Owned end-to-end parsing, 32 operations | 1,820,672 B total; 56,896 B/op |

Scratch capacities before and after stress are identical:
4096-byte push transport, 4096-char decoder, 4096 offset slots, 6 prefix chars,
6 prefix offset slots, 8 attribute rows and 8 namespace rows. The noise allowance
for the retention assertion was 1 MiB; the observed maximum delta was zero.
The parser drops pump/sink/scratch references at successful completion.

The counters-disabled control was rerun after the probe correction and passed;
its zero GC fields are placeholders, not measurements. No passing runtime
correctness/API checks were repeated after the probe-only correction.

## BenchmarkDotNet

SDK 10.0.400, runtime .NET 10.0.11, macOS 15.8.1 Arm64/RyuJIT armv8.0-a;
BenchmarkDotNet 0.15.8 reports the processor as unknown. Exactly three cases
were discovered and executed under the single InProcess toolchain per class.
A hard guard rejects duplicates, build toolchains, changed iteration/warmup/
invocation counts, missing statistics, critical validation errors or failed
execution exits. All three successful reports and statistics were confirmed.

| Case | Mean | Allocated/op |
| --- | ---: | ---: |
| Warm Append, 4096-byte chunk | 285.5 us | 0 B |
| Warm Append, 65536-byte chunk | 3073.5 us | **7 B** |
| Owned mixed-XML end-to-end, 127-byte appends | 297.9 us | 55.6 KiB |

The **7 B/op result for 64 KiB appends is unexplained**. It is not relabeled as
zero or attributed to noise without evidence. The standalone zero-allocation
oracle applies to its documented 4 KiB workload. This discrepancy is retained
for coordinator review/follow-up; no automatic repeat occurred.

Timing is informational: fixed short runs produced a very wide confidence
interval for the 4 KiB case; BDN warned about short iterations and could not set
process priority in the sandbox. Neither comparative speed nor release thresholds
are claimed. Cross-architecture qualification remains unrun. This generated
continuous-body stress is bounded-retention feature evidence, not completion of
later official-corpus comparisons or fuzz qualification.
