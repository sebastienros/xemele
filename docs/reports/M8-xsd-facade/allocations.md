# Actual facade allocation evidence

Runtime source: `a75d851db90277e41a631e1e79eb1b4326c7b6af`; prepared source:
`1836e724f9bd4c1c5bf3cc1fb5538ed2f6040304`; readiness evidence:
`c750b4c30687ba39597d2568c8f6178af1c5175d`.

Root sole launcher used grant `ead683a0afa21a346c0ec1fe0aeb697d47d19280`.
Durable host release `5b7b54e868f089990a9c2f6aff3a216626ab02fe` occurred before
this worker inspected raw results. Supervisor and both children59459/59461 exited0;
children reaped, owned groups absent, logs closed,453identities matched before/after.
No retry, extension, builds, tests, BDN or benchmark timing ran in this window.

Host:Apple M4 Pro,Arm64,macOS15.8.1,.NET10.0.11,SDK10.0.400 Release.
Workstation GC;TieredCompilation=0,gcServer=0,GCConcurrent=0. Synchronous per-thread
allocation counters only. Root independently ran1152combined correctness checks
at9b2816007f9e5aa39cdb85752f8c416524dbfa40; source identity checks above preserve
the accepted runtime/tests/API/check closure. No x64 or cross-platform run is claimed.

Each owned route had16warmups followed by one reading around64consumed operations.
Totals include owned public schema/result/diagnostic states. String/stream routes
include public parsing; stream routes also include the caller stream object and
operation buffer. Include routes include resolver-returned owned resource/stream
setup/disposal. Corpus is the task-owned fixture:typed integer defaults/fixed and
unique rows, IDs/IDREFs,64-row instances, open wildcard/nil/work-refusal cases,
resource denial/invalid canonical schema, and NOTATION-list composition.

| Public route | Measured bytes | Operations | Exact aggregate bytes/call |
| --- | ---: | ---: | ---: |
| validate-dom-valid | 1676288 | 64 | 26192 |
| validate-dom-invalid | 1164800 | 64 | 18200 |
| typed-default-fixed | 1799464 | 64 | 28116.625 |
| identity-duplicate | 1666048 | 64 | 26032 |
| document-ids | 1769472 | 64 | 27648 |
| long-instance | 30317216 | 64 | 473706.5 |
| wildcard-scopes | 1054208 | 64 | 16472 |
| nil-instance | 580096 | 64 | 9064 |
| work-refusal | 73216 | 64 | 1144 |
| validate-string | 2489344 | 64 | 38896 |
| validate-stream | 5439784 | 64 | 84996.625 |
| malformed-instance | 784384 | 64 | 12256 |
| compile-dom | 107028080 | 64 | 1672313.75 |
| compile-string | 109426832 | 64 | 1709794.25 |
| compile-stream | 112375408 | 64 | 1755865.75 |
| compile-include | 113404784 | 64 | 1771949.75 |
| compile-resource-denied | 1240064 | 64 | 19376 |
| compile-invalid-constraint | 90633576 | 64 | 1416149.625 |
| compile-notation-list | 93838696 | 64 | 1466229.625 |
| validate-notation-list | 659456 | 64 | 10304 |

The two borrowed delimiter routes use preallocated inputs/operation state,16warmups,
and one reading around1,024operations each. `borrowed-list-scan` recorded0/1024bytes;
`borrowed-qname-delimiter` recorded0/1024bytes. Those are exact zero numerators,
not rounded values. The latter scans a2,048-character QName-prefix hit and miss.
These narrow results do not cover full NOTATION dictionary lookup, datatype
normalization, owned compile/validation paths, async operation or whole parsers.
Full NOTATION costs appear in the owned compile/validate rows above.

Cold setup:3434824bytes for one fixture initialization, including initial
public schema compilations, prepared datatype/content/identity state, parsed DOMs,
namespace/byte/long-string fixture inputs and possible runtime/JIT scaffolding.
This is neither cold-start timing nor a per-schema cost. Positive control:
1048bytes around one retained1,024-byte array.

Whole-heap readings:889656bytes before and2841720bytes
after retaining8compiled schemas and16invalid results; delta1952064bytes.
Arrays, schema programs, diagnostics and outputs remain alive across the second
forced-GC reading. This is illustrative live state with GC/cache/scaffolding noise,
not exact retained bytes/object, a leak bound, or savings evidence. Each warm batch
is one aggregate sample; fractional averages preserve the exact denominator and
do not describe individual-call distributions. No throughput, speedup, BDN, full
W3C conformance, async or cross-architecture release claim is made.

Consumed counters:owned2216,primitive4160. Both binary hashes matched the453-file
frozen manifest. The counters-disabled companion control consumed identical routes.
Raw JSON/logs remain ignored transient artifacts and are not committed or archived.
Only this concise table/analysis and task evidence are retained.
