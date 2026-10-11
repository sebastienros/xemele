# XSD compiler allocation evidence

Runtime source: `d5e2a1c0f15b75cbfbb627ad5ea765e69e5b12bf`; preparation `2398242f1222a328105e6ba7e3a9c6a152f44a75`.
Pinned SDK 10.0.400; actual .NET 10.0.11 Arm64, macOS 15.8.1, workstation GC.
Tiered compilation and concurrent/server GC disabled; roll-forward disabled.

One approved allocation-only run under durable `M8-xsd-compile-allocation-1`,
maximum 30 seconds. The child exited zero, was reaped, its log closed, process
group was empty and all 24 final identities passed. Lifecycle RELEASE and the
coordinator durable RELEASE ACK completed before any raw result was opened.
No retries, builds, tests, generated fixtures, timing or BDN ran during the lease.

Every route consumed its result; 32 warm operations preceded 64 measured operations.
The two sealed lookup routes allocate zero. Parsing, builders, immutable snapshots,
contexts, BigInteger occurrence values, resources and diagnostics are explicit
owned allocations; graph creation is not claimed allocation-free. No scanner or
fixed-vocabulary optimization was introduced.

| Route | Operations | Total bytes | Min / median / max bytes per operation |
| --- | ---: | ---: | ---: |
| sealed global component lookup | 64 | 0 | 0 / 0 / 0 |
| sealed datatype definition lookup | 64 | 0 | 0 / 0 / 0 |
| verified model principal parsing baseline | 64 | 1,915,392 | 29,928 / 29,928 / 29,928 |
| owned graph compilation from parsed document | 64 | 5,773,824 | 90,216 / 90,216 / 90,216 |
| principal parsing plus owned graph | 64 | 7,670,272 | 119,848 / 119,848 / 119,848 |
| external composition plus owned graph | 64 | 9,890,048 | 154,528 / 154,528 / 154,784 |
| owned invalid-schema diagnostics | 64 | 4,040,704 | 63,136 / 63,136 / 63,136 |

The external composition distribution contains 63 observations of 154,528 bytes
and one of 154,784 bytes; the other distributions are constant. The positive
1,024-byte array control observed 1,048 bytes. Cold fixture setup observed
127,960 bytes, separate from the warmed distributions. Retaining 32 compiled
graphs changed the full-GC managed heap observation from 246,424 to 2,775,496 bytes
(delta 2,529,072). That coarse process observation includes the retaining array
and runtime noise; it is not an exact per-graph lifetime/retained-size promise.

The principal fixture uses multilingual names, a QName facet/default namespace
context, expanded model group, arbitrary-size finite occurrence and unbounded
wildcard. External composition uses one include followed by a cache hit. The
parser-only and parsed-document graph routes isolate ownership boundaries; no
sum-based allocation claim is inferred because their operation setup differs.

Identity SHA-256:

- assemblySha256: `44E34F6DBEC0BEFC8C8C30BF7C4A1B7E30BFB5AEBB63F8AAB8C218830E5FA6D1`
- probeSha256: `58526EC75DB7D14E4C62571E9161A2A0684A81F0B79C015F6E25D1CEDBD484DC`
- inputSha256: `E079A237E9F61E709C3D46A133127E6034C4114BBF7597F125604572F2302C2F`
- resourceSha256: `8B1E4F909583C05B507E3ACAFEEDF9C6CE89D7A0D656DDEE7F9C6B6D5AE3F660`

The 24-file manifest in preparation.json additionally pins the host, CoreLib,
CoreCLR/GC, runtime/deps/PDB files, compiler/model/location source and supervisor.
Generated raw outputs remain transient ignored artifacts. These synchronous
per-thread allocation observations establish this fixture on Arm64 only; no
async, throughput, cross-architecture or comprehensive corpus result is claimed.
