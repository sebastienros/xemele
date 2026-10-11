# M3 async resource continuations

Implementation/test revision: `27e7deda70b34dbcd08f4bf1daab118501cbc0f2`.
Final probe revision: `216e28daa249690bed3da2896948298f9dbdf5e5`.
The probe-only changes after the implementation revision correct its explicit
long-token quota and add a one-limit correctness control; production code and
tests are unchanged. The branch imports verified async token binding checkpoint
`d299cbdcc00bade01158db8afd474c48c0cc2efd`.

## Internal owner protocol

Construct `XmlParserCore(options, sourceUri, asyncResources: true)`. The default
remains synchronous. `Append` returns `ScanStep.NeedResource` before invoking a
resolver, reading/framing an external source, or disposing an external source.
Its `Consumed` counts primary input exactly. Retain the input owner and resume
its unconsumed tail; never resupply the consumed prefix. Await
`AdvanceResourceAsync(operationToken)` to service the pending operation, then
resume `Append` with the same sink and bound token. The service constructs no
callback views and invokes no sinks. Buffered source characters run through
ordinary synchronous grammar and callback pause/no-replay handling.

External DTD processing retains its prior model, DTD parser, each parent's source
scratch/cursor, and pending PE request. Internal-subset PE suspension preserves
the consumed triggering scalar and delimiter tracking. General entities reserve
their depth/frame before awaiting open, and transitional frames cannot drain as
internal replacements. Resource EOF disposes asynchronously before any subsequent
notification. The operation gate covers all awaits and token rebinding.

On structured rejection, `Append` first requests async cleanup, then returns the
original terminal diagnostic after that service. On callback/cancellation failure,
synchronous `Append` immediately propagates the identical primary exception and
faults the core; **the async pump owner must await `DisposeAsync` before propagating
that exception**. Cleanup on a faulted/rejected core preserves that primary
failure. Use `DisposeAsync` for async-mode cores; synchronous `Dispose` rejects
rather than performing synchronous user cleanup. Disposal is idempotent.

## Actual checks

- Release test build, single-node/no-server controls: 0 warnings/errors.
- Async resources: 29 passed, 0 failed/skipped at `27e7ded`.
- Affected synchronous entities/resolution/encoding/metadata/recovery/external
  declaration/conformance groups: 292 passed, 0 failed/skipped at `43bbf5f`.
  Runtime source did not change after that revision.
- DTD parser/source/budget/framing groups: 3,141 passed, 0 failed/skipped at
  `27e7ded`.
- Task validator: 142 records validated. `git diff --check`: passed.
- Probe-only Release build: 0 warnings/errors at `216e28d`.
- Narrow offline long-token control: default 1,048,576-character token quota
  rejects 1,048,577 generated characters with `XML.ResourceLimit`; changing only
  TokenCharacters to null completes with the exact content count.

Tests cover genuine delayed open/read/disposal with synchronous methods throwing,
nested subset→PE→PE and general→general sources at every primary split with every
notification paused, original source offsets/redirects, UTF-8/UTF-16 byte splits,
Unicode and CRLF, decode-valid prefixes, malformed declarations/markup, denial,
cycles, aggregate byte/resource/depth quotas, canceled/rebound tokens, gates,
exactly-once cleanup, and primary callback/I/O/cancellation exception identity.

## Allocation and performance scope

Exclusive lease `m3-async-resources-216e28d`: prebuilt allocation child ≤30 seconds,
then conditional prebuilt in-process BDN child ≤150 seconds. Both exited 0; six
BDN cases completed in 36.5 seconds. No overlapping worker workloads or build
children. macOS 15.8.1, Apple M4 Pro Arm64, SDK 10.0.400/runtime 10.0.11,
BenchmarkDotNet 0.15.8. BDN uses three warmup and three measured iterations.
These are provisional single-host composition results, not cross-architecture
release evidence or a claim of a repeatable speed improvement.

`allocations.json` records actual process-wide `GC.GetTotalAllocatedBytes(true)`
counters, compiled assembly hashes, runtime and process identity. Sixteen warmup
parses initialize runtime/continuation state before measured end-to-end scopes.
Cold initialization is excluded, not claimed free. The owned context, source
buffers, framing/model/name state and awaited continuation costs are included in
end-to-end totals, separate from buffered grammar/borrowed dispatch:

| Scope | Work | Actual allocated bytes |
| --- | --- | ---: |
| Buffered scalar Append/borrowed sink | 512 buffered segments after 128 warmups; all thread allocations counted | 0 |
| Positive escaped allocation | byte[1024] | 1,048 |
| Immediate async, including owned context/resource setup | 32 × 64 KiB sources | 2,287,360 (71,480/op) |
| Actually suspended async, including owned setup/continuations | 32 × 64 KiB sources | 2,807,248 (87,726.5/op) |
| Actually suspended async, including owned setup/continuations | 32 × 1 MiB sources | 8,657,232 (270,538.5/op) |

Increasing suspended input allocates additional per-I/O continuations; it does
not increase source buffer capacity. Each active source owns fixed byte[4096],
char[4096] and long[4096] arrays (45,056 payload bytes, plus headers/decoder and
bounded lexical state). Depth and lexical quotas bound owner stacks/state. The
probe stream generates directly into caller scratch and never allocates a body.
Owned source/context setup remains visible in the roughly 69.8 KiB per-parse
immediate BDN memory result; it is distinct from the zero-allocation buffered
primitive. Retained source scratch is fixed by construction; no whole-resource
string/cache is introduced.

| Payload | Sync mean / allocation | Immediate async mean / allocation | Suspended async mean / allocation |
| --- | --- | --- | --- |
| 32 B | 7.931 µs / 69.81 KiB | 9.549 µs / 69.81 KiB | 18.169 µs / 74.52 KiB |
| 64 KiB | 2.679 ms / 69.82 KiB | 2.723 ms / 69.81 KiB | 2.917 ms / 85.66 KiB |

Raw BDN iterations/logs are in `benchmark.stdout.txt`; exported statistics are in
`benchmarks.csv`/`benchmarks.md`. Suspended async allocation is corroborated by the
independent process-wide probe, not inferred from per-thread counters.

The original `27e7ded` allocation child exited -6 when the generated 4 MiB
primitive exceeded the default token quota; its stderr and conditional exit log
are preserved under `failed-27e7ded/`. Its BDN command did not run. The narrow
one-limit control proves the corrected measurement fixture while preserving
production defaults. The initial control expected a quota-specific diagnostic
message, whereas the existing core uses a generic resource-limit message; the
final control checks its actual stable code and differential outcome.

## Follow-up

The coordinator must integrate/verify this task. The future streaming pump must
implement the explicit async fault-cleanup obligation above. Cross-architecture
and broader performance confirmation remain release/performance work.
