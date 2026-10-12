# Allocation design — source only, measurements UNRUN

Import.Probe.csproj builds the standalone sync probe with prepared owned inputs.
Fixture construction measures cold input preparation separately from the first
Run per route; the first Run includes lazy runtime/compile initialization. Each
warm route is consumed eight times before 32 observed operations. Every operation
returns an owned compiled graph, public schema, compile exception or cancellation
exception; positive allocated-byte assertions make no zero-allocation claim.

24 routes cover two original import rejections, their principal-import controls,
namespace-only and unavailable-location permission, nested unauthorized principals,
locally imported included authors, the reverse principal-to-child denial,
authorized missing components, mutual imports, work/resource refusal and acquired
resource cancellation; plus exact original a001 annotation rejection, each invalid
digit-leading form on each wrapper, numeric-relative and normalized absolute URI
controls, opaque payload, work refusal and cancellation during lexical work.
Resource counts, exactly-once disposal, declaration presence and diagnostic code,
message, principal identity and cancellation tokens are consumed semantic controls.

`--control output.json` takes the identical routes without calling allocation or
heap counters. `--measure output.json` is coordinator/root-only after reviewed
source release. Counter execution uses GetAllocatedBytesForCurrentThread only for
these synchronous operations; it does not claim to measure async work. A 1024-byte
owned array positive control must be visible to the counter. Retained categories
keep four returned results alive while taking full-GC live-heap deltas, retaining
prior categories and inputs: 28 results total in seven categories. These deltas
are coarse process observations, not object sizes. Raw reports stay in ignored
artifacts; only analysis/summary belongs in committed reports.

The immutable baseline Compiler.cs SHA256 is
88bb317cc79e0646726acc1ae38aee6b9e19ddfe7a9fea833fe006e8dec65357
at 8aa0a2786. Legal locally-imported/namespace-only routes can compare identical
semantics across revisions. Invalid original routes intentionally disagree before
the repair and must be recorded separately, never compared as equivalent work.
Compare warm legal routes and initialization/results/retention separately. The
removed authorization HashSet has no new replacement allocation; full compiler
results allocate. Normalized annotation strings have call-local owned lifetime.
The BenchmarkDotNet class has MemoryDiagnoser/ShortRunJob for later timing, but
this source handoff does not launch it. Root pins Release .NET 10.0.11, records
runtime/architecture/GC/assembly and probe hashes plus XEMELE_IMPORT_REVISION, then
launches the bounded probe once per fresh process. No isolation/PARK/ACK/lease
protocol is required for allocation-only counters.
