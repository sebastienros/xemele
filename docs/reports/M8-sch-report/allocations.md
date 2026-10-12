# Owned SVRL output allocation evidence

One coordinator-executed synchronous allocation-only run of source `f6db4e76dca6c57ef97f11149348edc3a6a4272c`, after counter-disabled controls and cleanup preflight. Apple M4 Pro, Arm64, SDK10.0.400, .NET10.0.11; DOTNET_TieredCompilation=0, DOTNET_gcServer=0, DOTNET_GCConcurrent=0; runtime server GC=false. No BDN, elapsed operation timing, speedup, allocation-free, cross-architecture or release-conformance claim.

## Exact allocation totals

Cold rows are the first report API calls after detached-fixture setup, not process startup or schema evaluation:

| Route | Managed bytes | Output |
| --- | ---: | --- |
| cold-owned-string | 5024 | 615 UTF-16 units |
| cold-utf8-output | 10872 | 615 UTF-8 bytes |

Warm routes have16 uncounted warm-up calls, then one32-call aggregate counter window. Every result/output is consumed; byte totals and exact aggregate means are shown. Per-call distributions/variance were not recorded, so no repeated-run or distribution claim is made.

| Route | Calls consumed | Managed bytes total | Bytes/call (total/32) |
| --- | ---: | ---: | ---: |
| positive-owned-control | 32 | 4864 | 152 |
| short-owned-string | 32 | 160768 | 5024 |
| short-utf8-output | 32 | 44288 | 1384 |
| multilingual-long-owned-string | 32 | 1239552 | 38736 |
| multilingual-long-utf8-output | 32 | 44288 | 1384 |
| dense-escaping-owned-string | 32 | 2013952 | 62936 |
| dense-escaping-utf8-output | 32 | 44288 | 1384 |
| 128-events-owned-string | 32 | 6988032 | 218376 |
| 128-events-utf8-output | 32 | 1113600 | 34800 |
| invalid-scalar-string | 32 | 96512 | 3016 |
| invalid-scalar-stream | 32 | 56832 | 1776 |

The positive control allocates an owned128-byte array (152B with object overhead) and validates the counter window. All report routes deliberately allocate. String output owns builder chunks and the final string. Both outputs own dictionaries/lists grouping events/findings and sorted namespace nodes; stream output also owns its encoder and bounded pooled byte scratch, with cold pool initialization/growth included in the cold stream row. Invalid XML routes own thrown exceptions. These aggregate counters do not attribute individual allocator costs or establish causal residuals.

The stream routes reuse a precreated digest sink, verify exact expected UTF-8 byte counts and digest, and retain no destination data. Destination storage allocation is excluded. String routes compare owned strings with precomputed exact expected XML. Detached-fixture construction, expected XML/byte/digest creation and schema/document/evaluator work are outside all output counter windows. The observed string/stream difference describes different output ownership, not a throughput comparison or an optimization speedup.

## Approximate retained/process counters

Sixteen long multilingual report strings remain alive across a forced-GC `GC.GetTotalMemory(true)` delta: **566168B**. This is a process-local approximate delta, not exact summed string payload sizes or allocator attribution. Collection/JIT root liveness and unrelated live process objects can affect it; no causal residual or per-output retained value is inferred.

`Environment.WorkingSet` reports **52166656B** for the process after the run. RSS includes runtime/setup/pools/fixtures and live results; it is separate from managed bytes/call and the approximate retained delta. Neither is a steady-state leak or destination-memory benchmark.

## Exact identities and lifecycle

Prepared source/evidence checkpoints: `f6db4e76dca6c57ef97f11149348edc3a6a4272c` / `c44e14e0d7b567524e7d115ab2abe32f3fb97c36`. The review metadata commit changes no prepared input. The manifest pins418 identities, including full runtime/hostfxr files, source, prebuilt binaries, disabled controls and supervisor.

| Identity | SHA256 |
| --- | --- |
| Manifest (96223B) | `c365cfe33ed4a766dd6018b997c0d77b44e3795290a21704dbf5b19aec8a8d56` |
| Supervisor (14716B) | `e882cf3a4d36ea76253e7a55f441813d64d41ec7e3477ab3e8208d70133499ac` |
| Probe Xemele.Benchmarks.dll | `2c22a66f7e083414f0f036c16c387f65010be15f1205191912793bc7a96d764f` |
| Probe Xemele.dll | `44afbf052922d8af9c728e83f484fa5df2b2053d5907e0ed0e4bf56a90bba610` |
| Disabled control / cleanup-preflight control | `7f9981ab0e04ecd83d58f0cc75a31c6eac366778e3c7e931c0569be25c14f1d3` |
| dotnet host | `e86deb33d0dacad9cc1224466df6418149cb8e13cb8afd0dc6815ea6b7807975` |
| System.Private.CoreLib.dll | `9ed74c2e2efeee87f1f2d7ffe1736b7e9164ce248516ea39402a725eedc87af6` |
| Raw allocation result (preserved ignored artifact) | `b75e49afee501d2ac99e1747d93f97f4d447a6a0683f5ced80074d755b1e333b` |
| Lifecycle (preserved ignored artifact) | `612977854cba2d82471507ac6398bb3672f19bc5badcf2f22d9f45db79b06fb6` |
| Local fsynced RELEASE (preserved ignored artifact) | `bc21ad2b4691dcf69d8f2726fa5bf716148a4723293db8f429d2060b4696cdcd` |

Exact wrapper executed by root under fresh shared scope `M8-report-content-facets-serial-1`, grant `b9e9f7e9`:

```text
python3 docs/reports/M8-sch-report/lease.py --granted c365cfe33ed4a766dd6018b997c0d77b44e3795290a21704dbf5b19aec8a8d56 2026-10-12T01:50:00Z 2026-10-12T01:50:20.413078+00:00
```

One prebuilt pinned10.0.11 child, no build/discovery/retry/extra phase. Wrapper max30s includes20s work+10s cleanup and enforces latest start/absolute expiry/no overwrite. Reporting child89187 exit0/reaped, owned process group absent, log closed; all418 identities and manifest stable before/after. Lifecycle status0. Local fsynced cleanup RELEASE at2026-10-12T01:49:50.803876+00:00 precedes any worker raw inspection. Root durable host RELEASE `cf39961a` was received before this post-release analysis; all three serial phases had completed and cleaned up. Raw results/logs/generated outputs remain under ignored `artifacts/M8-sch-report/` for independent root review, and are not committed.

Source checks and earlier corrected fixture/consumer failures remain in README. Root independently reported actual joint integration `e4bf43e059fd5aa23bfb1b7dc334ac561ee3e7fc`:546 report+804 facet checks (overlapping prerequisites), public1403/frozen3/9/154 plus316, both normal and genuine XML-only consumers, planning179,0warnings/errors. This is a coordinator receipt only; worker did not rerun those combined checks or mark the task verified.
