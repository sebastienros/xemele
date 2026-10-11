# M8-model allocation evidence

Source: `2ac7e6034a82d371e76608d0ad11fe2e2d8c8ea0`.
Preparation: `9dd073bb779ad9fe718e7e5258d57625c877354f`.
The eight committed prepared identities in preparation.json matched before and
after execution; manifest SHA-256
`ba37e3084aedb96bf4502ea82b1bc9ea318b3f372a9cc1e43689fe7797442c4b`.
Actual runtime .NET 10.0.11, pinned SDK 10.0.400, macOS 15.8.1 Arm64.
Workstation GC; DOTNET_gcServer=0, DOTNET_GCConcurrent=0,
DOTNET_TieredCompilation=0. Input is the prebuilt multilingual XML
`<r xmlns:p='urn:p'>根😀</r>` with SHA-256
`45c4387546874d35d2fdaf00d8cd7220eabe6f28993cac74676305dfdba2c237`.

## Lease and lifecycle

Root granted M8-model-allocation-1 on durable main `5a550ea` after PARK and
correctness completion. One supervised prepared child ran the exact command
below with a 30-second timeout; no builds, tests or retry occurred under lease.
PID 83029 exited 0 in 0.137156 seconds. The parent waited/reaped it, drained 77
stdout bytes and zero stderr bytes, and found no surviving process group.
Lifecycle RELEASE was reported before opening raw results. Root subsequently
acknowledged durable RELEASE and cleared active lease/benchmark_owner; only then
were results inspected. This is independent of the excluded incidental M2 run.

```sh
env DOTNET_TieredCompilation=0 DOTNET_gcServer=0 DOTNET_GCConcurrent=0 XEMELE_MODEL_REVISION=2ac7e6034a82d371e76608d0ad11fe2e2d8c8ea0 '/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet' --fx-version 10.0.11 benchmarks/Xemele.Benchmarks/M8-model/bin/Release/net10.0/Xemele.Benchmarks.dll --measure artifacts/M8-model/allocation.json
```

## Actual counts and ownership

Each case performed 32 counters-disabled warm operations followed by 64 consumed
measured operations. GC.GetAllocatedBytesForCurrentThread enclosed the entire
synchronous measured loop and each sample. The total equals the sum of all 64
samples for every case. **Every sample within each case was identical**: the
bytes/op column is also that case's exact minimum, median and maximum; there
were no measured outliers. No subtraction of setup/control costs was used.

| Case | Measured operations | Total allocated bytes | Bytes/op (all 64 samples) | Owner/lifetime |
| --- | ---: | ---: | ---: | --- |
| Existing M5 charge baseline | 64 | 0 | 0 | Preinitialized run ledger |
| M8 compile charge/components | 64 | 0 | 0 | Preinitialized compile ledger |
| M8 validation adapter charge | 64 | 0 | 0 | Preinitialized M5 adapter/run |
| Owned namespace lookup | 64 | 0 | 0 | Existing immutable namespace snapshot |
| Baseline borrowed stream read | 64 | 0 | 0 | Preallocated stream and borrowed 256-byte destination |
| Tracked schema borrowed read | 64 | 0 | 0 | Preinitialized loader/aggregate ledger and borrowed destination |
| Compile operation setup | 64 | 32,768 | 512 | Owned options/limits snapshot and operation |
| Namespace/report snapshots | 64 | 112,128 | 1,752 | Owned namespace maps, trace collections and detached result |
| Existing direct M4 accounting | 64 | 346,112 | 5,408 | Existing evaluation context/snapshot/result |
| Capped shared XPath join | 64 | 353,792 | 5,528 | Existing M4 ownership plus capped context/limit copies |
| External schema composition | 64 | 3,807,744 | 59,496 | One operation/loader/resource and strictly parsed owned document |

The new XPath join adds exactly 7,680 bytes across 64 operations, or **120 B/op**,
over the unchanged actual-accounting route. These are explicit owned copies of
per-expression limits/context, necessary to cap the shared budget and supply
its cancellation token. Neither route is claimed allocation-free.

The positive control's owned byte[1024] allocated **1,048 bytes**, confirming
that the meter observed allocations. Cold fixture/setup observation was
**130,856 bytes**, including initial documents, contexts, parser setup and
initialization; it is not a pure model-constructor measurement. After retaining
256 independent namespace contexts, GC.GetTotalMemory(true) changed from
159,552 to 329,896 bytes (delta **170,344 bytes**). That is an informational
whole-process retention observation including owners/root array/GC effects,
not an exact per-context retained-size promise. The contexts were kept alive
through the observation and then released with the child process.

## Limits of this evidence

This is allocation-only synchronous evidence on one Arm64 host and one small
multilingual composition fixture. It does not establish throughput, async
allocations, large-corpus conformance or x64/other-OS results. Borrowed primitives
reuse preallocated state; setup, persisted graphs/documents, reports, diagnostic
snapshots and M4 evaluation retain their separate ownership budgets. Invalid
paths are covered by correctness/quota/exception tests; this probe does not claim
allocation measurements for every invalid-input diagnostic path. No new
scanning/token-matching implementation or performance campaign was introduced.
Cross-architecture/release composition confirmation belongs to later gates.

Generated allocation.json/control.json/lifecycle.json remain ignored and
transient for coordinator inspection, and must be removed only after that
inspection. Committed evidence is this summary, source/identity manifest and
thin reproducible driver; no generated raw measurement log is committed.
