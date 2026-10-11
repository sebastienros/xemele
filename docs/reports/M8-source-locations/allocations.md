# Allocation evidence

Baseline `33659d7bd7a576d8e2b8e11fb38fd90ed927642d`; candidate `0315f407f6e15091932d90c498759d3e80612baf`; preparation `1e1f4a1143367df620fe87e72ff8635c215c4b34`. Manifest SHA-256 `860c83a40c68aa36f416689b85e9b158048c99f86353beb756441d259e3e9682` retains twenty source, probe, runtime and binary identities. Identical input SHA-256 `CAB6E2FBB504C02921182DFF0293ECB0B848496C5C08BAF65CA010303E8733DF` in both results; probe SHA-256 `494B10F601F749C4FB180DE103AB6D752C74CC862FB61D95EF226A1DAF66A550`. SDK 10.0.400, actual Arm64 .NET 10.0.11, macOS 15.8.1; tiered compilation, server GC and concurrent GC disabled.

Lease `M8-source-locations-allocation-1`: main grant e13fc88 after four explicit PARK ACKs; immediate durable RELEASE 916ccb3 before raw inspection. Both child PIDs 94146/94148 exited zero, were reaped, logs closed and groups empty; all twenty identities matched before/after. Total lifecycle 0.563249084 seconds. The supervisor's process-inspection permission was preflighted before the lease. No build, test or retry ran during it.

Each consumed route ran sixteen warmups followed by sixty-four individual allocation observations. Every distribution was constant; every numerator equaled its distribution sum. New node, attribute and namespace lookups each allocated exactly zero bytes for all sixty-four observations. The owned parse costs include all existing DOM/parser setup plus the new retained location dictionary:

| Route | Baseline B/op | Candidate B/op | Added B/op |
|---|---:|---:|---:|
| borrowed root navigation | 0 | 0 | 0 |
| owned character parse 1 | 12720 | 13472 | 752 |
| owned byte parse 1 | 54624 | 55376 | 752 |
| owned character parse 32 | 29912 | 38256 | 8344 |
| owned byte parse 32 | 71816 | 80160 | 8344 |
| owned character parse 512 | 318968 | 508376 | 189408 |
| owned byte parse 512 | 360872 | 550280 | 189408 |

Both positive controls allocated 1048 bytes for a retained 1024-byte array. Cold setup (including delegate reflection/binding and one schema parse) was 14400 baseline / 17104 candidate bytes; this is not a pure static-initialization comparison. Retaining sixteen 32-element documents changed whole-heap readings from 252312 to 521360 baseline (269048 delta), and 257928 to 680120 candidate (422192 delta). These inclusive heap observations are informational, not portable per-document retention claims. The additional owned dictionary and its capacity growth explain an explicit memory tradeoff; warm lookup remains borrowed and allocation-free. No latency, throughput, global host-idle or cross-architecture claim is made. Raw outputs remain transient and are removed after integration inspection.
