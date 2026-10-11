# M3 callback verification

Runtime/test/API revision: `2380b0dc300f2732fbd4458b66b76d3b2640f786`.
Final source/ownership-probe/BDN revision: `7951a08bd16284871f0e6a885bf6bf920df9b893`.

The final commit adds only the owner probe; runtime, correctness tests, API and BDN source are unchanged from the tested runtime revision. Earlier passing checks were retained.

## callback-build

Result: passed; revision `2380b0dc300f2732fbd4458b66b76d3b2640f786`.

`dotnet build tests/Xemele.Tests/Xemele.Tests.csproj -c Release --no-restore -p:BuildInParallel=false -p:UseSharedCompilation=false -m:1 --disable-build-servers`

Release build succeeded, no warnings/errors on pinned SDK10.0.400.

## callback-correctness

Result: passed; revision `2380b0dc300f2732fbd4458b66b76d3b2640f786`.

`dotnet tests/Xemele.Tests/bin/Release/net10.0/Xemele.Tests.dll -namespace Xemele.Tests.Callbacks -parallelMode none -reporter verbose`

6/6 tests passed; every notification delegate/subclass equivalent; optional no-ops; identical callback exception objects including XmlParseException; metadata, defaults, incomplete parts, nullable notation provenance, checked bounds and enumerator states.

## callback-allocations

Result: passed; revision `2380b0dc300f2732fbd4458b66b76d3b2640f786`.

`dotnet tests/Xemele.Tests/bin/Release/net10.0/Xemele.Tests.dll -parallelMode none -result-xml /private/tmp/m3-callbacks-tests.xml`

Full test report captures 0 B total for each subclass/delegate/optional path over8192 groups x16 notifications after4096 warmups; start attributes/namespaces iterated; allocation-positive control88 B. Owner initialization/persistence excluded.

## earlier-regression

Result: passed; revision `2380b0dc300f2732fbd4458b66b76d3b2640f786`.

`dotnet tests/Xemele.Tests/bin/Release/net10.0/Xemele.Tests.dll -parallelMode none -result-xml /private/tmp/m3-callbacks-tests.xml`

4916/4916 tests passed,0 failed/errors/skips/not-run.

## compiled-api

Result: passed; revision `2380b0dc300f2732fbd4458b66b76d3b2640f786`.

`dotnet docs/reports/M0-contracts/ApiCheck/bin/Release/net10.0/ApiCheck.dll compare src/Xemele/bin/Release/net10.0/Xemele.dll /private/tmp/m3-callbacks-composed-api.txt`

Matched625 compiled public members against composed canonical task fragments; callbacks owns248 actual metadata entries. Composition sets id=member in-memory per approved checker scheme.

## global-api-discovery

Result: failed; revision `2380b0dc300f2732fbd4458b66b76d3b2640f786`.

`python3 tools/ApiBaseline/compare.py --report artifacts/M3-callbacks/runtime-api.json`

Global discovery failed before comparison because existing corpus fixture tests/corpora/xml/xmlconf/eduni/errata-2e/E14.dtd is missing; direct compiled API check passed independently. Not treated as global success.

## runtime-sample

Result: passed; revision `2380b0dc300f2732fbd4458b66b76d3b2640f786`.

`dotnet samples/M3-callbacks/bin/Release/net10.0/Sample.dll`

Actual output: start; borrowed text Ω; end; subclass text. Null optional comment callback is a no-op; only verified runtime dependencies used.

## borrowed-lifetimes

Result: passed; revision `7951a08bd16284871f0e6a885bf6bf920df9b893`.

`dotnet build docs/reports/M3-callbacks/Lifetime/Lifetime.csproj -p:LifetimeCase=STORAGE -c Release --no-restore -p:BuildInParallel=false -p:UseSharedCompilation=false -m:1 --disable-build-servers`

Expected compiler rejection at7951a08: heap field CS8345, exit1 is the passing negative-test oracle.

## borrowed-await

Result: passed; revision `2380b0dc300f2732fbd4458b66b76d3b2640f786`.

`dotnet build docs/reports/M3-callbacks/Lifetime/Lifetime.csproj -p:LifetimeCase=AWAIT -c Release --no-restore -p:BuildInParallel=false -p:UseSharedCompilation=false -m:1 --disable-build-servers`

Expected compiler rejection at2380b0d: collection across await CS4007; exit1 is the passing negative-test oracle.

## setup-ownership

Result: passed; revision `7951a08bd16284871f0e6a885bf6bf920df9b893`.

`dotnet docs/reports/M3-callbacks/OwnershipProbe/bin/Release/net10.0/Xemele.Benchmarks.dll`

Cold first complete owner graph9056 B; warm fresh setup1672 B/graph x1000; informational retained GC heap delta3365016 B for1000 graphs including reference container and runtime bookkeeping. Owners/delegates/parts allocate at initialization, not per notification.

## serialized-bdn

Result: passed; revision `7951a08bd16284871f0e6a885bf6bf920df9b893`.

`dotnet benchmarks/Xemele.Benchmarks/bin/Release/net10.0/Xemele.Benchmarks.dll --filter '*Callbacks.CallbackBenchmarks*' --inProcess --job short --iterationCount 5 --warmupCount 5 --invocationCount 65536 --unrollFactor 1 --artifacts docs/reports/M3-callbacks/bdn --join`

Lease m3-callbacks-7951a08: prebuilt in-process run completed3/3 cases exit0 in0.82s, MemoryDiagnoser0 B for subclass/delegates/optional. Mean194.01/132.22/28.01ns per16-notification group, provisional only: delegate variance/tiering, min iteration warnings2–13ms, high-priority setup denied. No throughput advantage claim. AppleM4Pro Arm64/macOS15.8.1/.NET10.0.11 SDK10.0.400 concurrent workstation GC. Raw outputs retained.

## planning

Result: passed; revision `7951a08bd16284871f0e6a885bf6bf920df9b893`.

`python3 tools/planning/tasks.py validate`

Validated141 tasks, schema/evidence/DAG/additive milestones/ordered ownership; final metadata validator also run before evidence commit.

## Allocation boundary

The borrowed notifications and item/enumerator wrappers own no heap state. The later pump retains bounded row owners and enforces callback-return invalidation. Strings/parts/delegate targets and optional consumer copies are explicit setup/persistence costs. The synthetic owner probe retains two attributes, one namespace, two immutable part records, one sink, one callback adapter and sixteen delegates per graph. The GC retained-heap delta is an informational process estimate, not an exact object-size or parser-memory claim.

BDN is a short provisional microbenchmark on one Arm64 host; timing varies with tiering and iteration duration. Keep the straightforward direct dispatch; no optimization or cross-architecture throughput claim is made. Real parser composition is intentionally deferred to its owning task.
