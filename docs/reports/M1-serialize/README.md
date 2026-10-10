# M1 serialization branch evidence

Latest source/measurement-driver revision: `c54d39be7c5a638b6cb591e1211fe435e760f529`.
Only the allocation driver changed after the full correctness/timing revision;
its focused Release build and allocation/oracle checks pass in
`allocation-followup.txt`.

Correctness source revision: `707d52bc2130492daa690c7255d91a9104edea59`.
Runtime implementation is unchanged since reviewed boundary repair
`972b438ef0fefbfc52f32e15c84bc6deb36c006d`; subsequent commits correct test
cancellation tokens and construct unpaired surrogate fixtures at runtime.

Pinned SDK 10.0.400, .NET 10.0.11, macOS Arm64. `verify.py` ran in Release with
build servers/shared compilation disabled and MSBuild `-m:1`. Environment:
`DOTNET_ROOT='/Users/sebastienros/Library/Application Support/dotnetup/dotnet'`,
that directory first on PATH, `DOTNET_CLI_USE_MSBUILD_SERVER=0`.

## Implemented behavior

Exact XML1.0 string/Stream serializers and document wrappers, immutable save
options, strict/temporary repair namespaces, deterministic declarations and
attributes, mixed-content indentation, scalar/name/delimiter validation and
adjacent text escaping. No parser, later writer, async or DTD dependency.
String results own one final StringBuilder output; streaming uses one rented
8KiB byte buffer and a strict operation-local encoder. Stream transport retains
encoder state across arbitrary lexical chunks, including supplementary scalars,
and drains local bytes without flushing or closing the caller stream.

Traversal uses one value frame per open element and one undo entry per namespace
binding. An ordinal Dictionary scopes prefixes; a reverse URI index with ordinal
SortedSets chooses the smallest existing attribute prefix without walking
ancestors. Operation scratch collections are reused across elements. These
collections own operation state and die at return; generated names own temporary
strings. There are no per-character allocations or full-result streaming buffers.
Character/scalar scanning and work loops check cancellation at most every 2048
scalar/work steps; writes check it before touching caller streams.

## Executed checks

`checks.txt` contains durable command/count summaries; `verification-raw.txt.gz`
is full output. The verifier completed with exit 0:

- Solution Release build: zero warnings/errors.
- 46 focused serializer tests; 134 full runtime tests; 11 FoundationChecks tests.
- All six approved independent-construction vectors execute against actual trees
  and assert exact strings and hex bytes, with no parser-generated oracle.
- XML transport/lexical/name/reserved-binding/mixed-content/order/repair tests,
  UTF8 and UTF16LE/BE BOM/declaration controls, surrogate chunk regressions,
  I/O/cancellation identity, caller ownership, and counted 10,000-deep traversal.
- Approved compiler extractor: exact 215-member Serializer golden union,
  retaining all 192 Common/Tree members plus 23 serializer members; actual
  extraction agrees byte-for-byte with the task fragment. Discovery comparison
  is in `runtime-api.json`.
- Both M0 Foundation model assemblies are freshly built and compared; all seven
  M1 declaration lanes compile and compare (Protocol executes 38 model assertions).
  These are model regressions, separately labeled from runtime tests.
- Independent source sample and freshly packed NuGet-only consumer pass. Consumer
  restore uses a unique temporary package cache and local-only package feed.
- Planning validates all 101 tasks and `git diff --check` passes.

## Allocation and lifetime

The allocation driver records actual output length consumers outside measured
fixture generation. Cold setup plus first stream operation was 14,728 B.
After explicit pool/static warmup, Stream.Null serialization is 1,392 B/op for
both 16 and 1,000,000 character inputs (50 repetitions); this demonstrates
text-length-independent scratch, not allocation-free operation setup. Owned
string output is 1,528 B/op and approximately 4,011,735 B/op respectively
(10 repetitions); StringBuilder growth and final owned result account for the
size-dependent allocation. Temporary 1M-character document weak reference is
not alive after forced collections; post-collection heap delta was 11,496 B
and includes runtime/pool noise rather than document retention.

## Honest failure history and review

- The first sandboxed restore failed NU1900 because NuGet audit service access
  was blocked; normal network-authorized rerun succeeds. Raw output retained in
  `restore-failure.txt.gz`; no audit checks were disabled.
- The initial full build reported xUnit1051 missing MTP cancellation tokens;
  tests now pass TestContext tokens. `analyzer-failure.txt.gz` preserves output.
- Initial unpaired-surrogate string InlineData was encoded through UTF8 assembly
  metadata and reached tests as replacement characters, causing two failed
  assertions. Tests construct raw UTF16 code units at runtime and pass;
  `metadata-surrogate-failure.txt.gz` preserves that failure.
- Two coordinator read-only reviews identified one valid UTF8 encoder carry
  boundary: pending high surrogate plus the following 2048-unit chunk needs
  6145 bytes rather than 6144. Fixed with safe carry capacity and partial-progress
  handling; exact CDATA reproducer and broad comment/CDATA/PI UTF8/16 regression
  coverage pass. Coordinator focused review of `972b438` approved the correction.

## Exclusive ShortRun timing

At source revision `707d52bc2130492daa690c7255d91a9104edea59`, coordinator
granted an exclusive window with all peers/root heavy work parked. The command
`dotnet run --project docs/reports/M1-serialize/SerializerBenchmarks.csproj -c Release --no-build -- --filter '*' --artifacts artifacts/M1-serialize/bdn`
executed all 20 ShortRun workloads with 20 child exit codes 0, no workload
failures, and 12+8 valid CSV results. Total 152.04 seconds. Raw console and
Markdown/CSV results live in `bdn/`. BenchmarkDotNet 0.15.8, Apple M4 Pro,
.NET10.0.11 Arm64 RyuJIT; warmup=3, iteration=3, launch=1.

String/Stream.Null means respectively: short 213.8/270.1ns; 64KiB ASCII
111.93/105.95us; multilingual escapes 83.68/129.14us; 64 namespaced attributes
12.24/15.87us. Streaming plain-text allocation is 1.36KiB/op; namespace workload
30.8KiB/op accounts for temporary scope dictionaries/sets and generated prefixes.
String output includes final ownership/builder growth (1.48KiB short, 271.16KiB
long, 279.55KiB multilingual, 43.38KiB namespace workload). Fixture generation
is outside timing, Stream.Null excludes device I/O, and these values are
local informational measurements with broad ShortRun confidence intervals.

The follow-up at `c54d39b` explicitly checks all four delimiter corpora against
the scalar oracle and measures cached SearchValues cold static initialization
(2,072B). All three scanners together allocate 0B across 1,000 warm repetitions
on every corpus. Its first serializer setup/stream operation after those scanner
initializations was 12,712B, versus the original fresh-process 14,728B; these
setup values depend on preceding runtime initialization, so both are retained.

Delimiter-only alternatives recognize the same seven stops, with zero allocated
bytes per warm primitive scan. Cached SearchValues is 244-249ns on 8192-unit
long-hit/long-miss/multilingual cases versus scalar loop 3.9-4.06us and runtime
IndexOfAny 313-325ns. Short-hit scalar loop was 0.759ns versus cached1.181ns
and runtime2.281ns, sensitive to measurement overhead; no BDN warning was
emitted. This proves delimiter recognition performance, not full XML escaping.
The serializer must inspect every scalar for malformed UTF16/XML characters and
adjacent-text bracket state. Adding a delimiter pre-scan duplicates work without
a measured equivalent full-validation benefit; retain the simpler fused loop.
No end-to-end SearchValues speedup or cross-host threshold claim is made.

No x64, Windows,
Linux or hosted CI execution is claimed. Cross-architecture validation remains a
release follow-up, not a local correctness claim.
