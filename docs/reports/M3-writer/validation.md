# Synchronous forward writer

Implementation revision: `370b592443d224056cd5b58d7da0a131a0601787`.
Checkout: `/Users/sebastienros/.codex/worktrees/1dcc/xemele`.
Branch: `codex/m3-writer`; coordinated claim and integrated base verified as ancestors.

The sealed partial `Xemele.XmlWriter` implements the frozen synchronous surface
without async stubs or a DOM dependency. It retains a reusable single pending
tag, iterative element/scope undo state, and one pooled 8192-byte output buffer.
It validates arguments before mutation, faults permanently after cancellation
or a failed emitting operation, and rejects overlap/reentrancy through a guard.
Namespace declarations may follow attributes until the tag is sealed. Resolution
preserves explicit declarations and ordinary attribute insertion order; repair
uses deterministic ordinal prefix reuse and the operation-wide checked ns counter.
Forward indentation follows the first-child commitment rule. Existing M1 DOM
serializer source and behavior are unchanged.

Internal continuation boundary: `WriterOutput` is partial and owns Stream,
Encoder, Bytes and Buffered. Encode consumes a bounded span, BufferedMemory exposes
only the live buffer, and ConsumeBuffer releases it logically after successful
transport. A later async partial must retain owners/cursors across awaits, use
this storage only while owned, hold the writer operation guard across awaits,
and never call the synchronous transport to implement async I/O. The public
writer is partial for that additive lane. No public async method ships here.

## Checks

Pinned SDK 10.0.400 / runtime .NET 10.0.11, macOS 15.8, arm64. Builds use
`DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER=1`, `--disable-build-servers`, `-m:1`,
`-p:BuildInParallel=false`; restore uses `--ignore-failed-sources -p:NuGetAudit=false`.
The previously stalled broad restore is avoided by the controlled single-node
restore; runtime, test utilities, tests, benchmarks and sample restored/built.
All builds completed with zero warnings/errors. One initial test build failed
xUnit1051 due to missing test cancellation tokens; the committed revision fixes
those calls. One exploratory runner invocation used an unsupported option;
the recorded successful command below uses this assembly's xUnit runner syntax.

At the implementation revision:

- `dotnet build tests/Xemele.Tests/Xemele.Tests.csproj -c Release --no-restore --disable-build-servers -m:1 -p:BuildInParallel=false --verbosity minimal`: passed.
- `dotnet tests/Xemele.Tests/bin/Release/net10.0/Xemele.Tests.dll -namespace Xemele.Tests.Serialization -namespace Xemele.Tests.M3Writer -noColor -result-xml artifacts/M3-writer/tests.xml`: 69 passed, zero failed/skipped. Writer owns 19 cases; earlier serialization owns 50 cases.
- Sample binary: exact independent Unicode/namespace/escaping output and leave-open assertion passed.
- Compiled approved API extractor comparison against all task API fragments composed into `artifacts/M3-writer/api-union.txt`: matched 391 members, including 14 writer rows. The union validates duplicate member identities before comparison and is ignored/transient.
- `python3 tools/planning/tasks.py validate`: validated 141 task records.

Repository inventory-discovering `tools/ApiBaseline/compare.py` could not execute
its compiled stage: pre-existing offline corpus fixture
`tests/corpora/xml/xmlconf/eduni/errata-2e/E14.dtd` is missing in this checkout.
This limitation is reported to the coordinator; no manifest/source was changed.
The direct compiled union check above still proves the actual API agrees with
all checked-in API fragments and preserves earlier members.

Coverage includes exact independent golden output; strict/repair prefix collisions,
ordinal URI-prefix reuse, ns-name reservation, inherited scopes/default
undeclaration; adjacent split `]]>` escaping across calls/Flush; mixed/empty text,
CDATA/comment/PI validity; Unicode and 2048-char encoder boundaries; all UTF-8,
UTF-16LE/BE BOM and declaration combinations; method sequence/duplicate errors;
explicit Flush versus EndDocument; abort/idempotent disposal and caller ownership;
permanent cancellation/I/O fault; caught reentrant write/flush/dispose rejection;
10,000 element iterative depth; caller-encoding snapshot isolation.

## Allocation and retention

Sample `--probe` at the implementation revision uses a first document followed
by 100 warm-up documents, then 1,000 measured documents with Stream.Null. Options,
input strings and fixtures are prepared outside the counter. This counter is
allocation evidence, not a timing benchmark:

- First document including cold writer/pool/static initialization: 11,976 B.
- Warm setup + document output + disposal: 1,672 B/document.
- Warm writer with preallocated tag/frame state: 1,000 repetitions of borrowed
  multilingual escaped text, split closing brackets, repeated child names and
  attributes allocate exactly 0 B in the focused regression test. This does not
  promise zero allocation for writer setup, depth/tag capacity growth, unique
  generated repair prefixes, pool misses, errors, or caller-owned results.
- A no-inline helper supplies separate 1 MiB attribute and text strings, seals
  the pending tag and Flushes to Stream.Null. Full GC while retaining the live
  writer collects both input strings, proved by weak references. Closed scopes
  and sealed tag rows clear their references; completed output is never retained.
- Scratch is one rented 8192-byte buffer (pool bucket may be retained by the
  shared pool after return); scope/tag/frame capacities track maximum active
  depth, bindings and current-tag members, not total document output length.
  Dispose clears references and returns scratch without caller flush/close.

BDN representative output and provisional scan results below ran afterward
against the same frozen source, under the coordinator's exclusive lease. This
single Arm64 host does not establish x64 results or release-grade throughput.

## Serialized measurement result

Lease `m3-writer-370b592`, maximum 180 seconds, granted after both Xemele peers
parked and the unrelated host benchmark ended. One frozen command executed;
exit 0, all 24 cases completed in 39.29 seconds. No retries, source/config edits,
restore, or SDK build ran during the lease. BDN's InProcessEmitToolchain emitted
its normal in-memory harness (reported as 0.02 seconds building); no separate
MSBuild/toolchain process was invoked. Exec session 23162 completed, and the
coordinator was immediately notified of no remaining worker children before
reviewing or copying reports, allowing peers to resume.

```sh
PATH='/Users/sebastienros/Library/Application Support/dotnetup/dotnet':"$PATH" dotnet benchmarks/Xemele.Benchmarks/bin/Release/net10.0/Xemele.Benchmarks.dll --filter '*WriterBenchmarks*' '*WriterScanBenchmarks*' --warmupCount 1 --iterationCount 3 --iterationTime 100 --artifacts artifacts/M3-writer/bdn
```

Environment: BDN 0.15.8; macOS 15.8.1/Darwin 24.6.0; SDK 10.0.400;
.NET 10.0.11 Arm64 RyuJIT armv8.0-a; concurrent workstation GC; 128-bit vectors.
BDN reports unknown processor (host kernel architecture ARM64_T6041); hardware
model/frequency were not independently captured. Priority elevation/restoration
was denied by the sandbox. Short iteration durations and large confidence
intervals make timing informational. No speedup/regression threshold is claimed.

Representative output builds a root with 16 identical attributed text children.
Setup prepares input and an equivalent M1 DOM, then verifies equal emitted byte
counts for forward and DOM methods. BDN consumes the returned byte count. This
is a byte-count semantic guard, **not** a content hash or independent byte-for-byte
checksum; the separately passing exact golden-output tests provide lexical
correctness evidence. Corpus text is `hello&<]]>`, 65,536 ASCII x characters, or
4,096 repetitions of `界😀&<\r]]>` (actual CR). Both implementations write to an
owned counting stream without retaining output. Setup and fixture generation
are outside timed methods.

| Corpus | Forward mean | DOM mean | Forward allocation | DOM allocation |
|---|---:|---:|---:|---:|
| short | 3.737 us | 4.468 us | 1.63 KiB | 1.55 KiB |
| long ASCII | 2001.356 us | 1665.620 us | 1.76 KiB | 1.74 KiB |
| multilingual | 3382.161 us | 3121.415 us | 2.00 KiB | 1.94 KiB |

These inclusive BDN memory values include per-document setup plus in-process
measurement overhead, particularly at low invocation counts; they do not replace
the separate exact allocation counters above. They show bounded setup/output
allocation across roughly kilobyte-to-megabyte output workloads, rather than
allocation proportional to emitted output length. Raw CSV/Markdown/full log are
preserved under `docs/reports/M3-writer/bdn/`, including all confidence intervals,
GC counts, overhead calibration and warnings. Long-ASCII forward 99.9% error is
3068.891 us (larger than its mean), so its apparent ratio is inconclusive.

Provisional scan comparisons use identical first-delimiter recognition for
`&`, `<`, CR, `]`, `>`, and validate all three methods' return values in setup:

| Corpus | Scalar mean ns | Cached SearchValues mean ns | Runtime IndexOfAny mean ns |
|---|---:|---:|---:|
| long hit | 5849.323 | 315.743 | 690.961 |
| long miss | 5906.207 | 317.399 | 684.675 |
| multilingual | 5970.648 | 318.640 | 692.853 |
| short hit | 0.984 | 1.643 | 1.205 |
| short miss | 3.832 | 3.045 | 2.055 |

All scans report no managed allocation at displayed precision after static/setup
initialization. Dense first-position scan values are excluded from interpretation:
scalar overhead subtraction produced 0 ns and BDN warns ZeroMeasurement;
sub-nanosecond dense/short values are near the harness floor. These are not zero
execution-time claims. SearchValues clearly favors long isolated delimiter scans
on this host; full escaping additionally validates scalars, tracks bracket state,
selects replacements and writes output, so scan-only ratios do not establish a
repeatable benefit for replacing the full algorithm. Retain the straightforward
correctness baseline in this frozen deliverable; evaluating a full bulk-escape
implementation with identical semantics is a future optimization, not a missing
public writer behavior. No SIMD/hash or unmeasured scanning optimization ships.
