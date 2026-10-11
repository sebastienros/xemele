# M6 adapters prepared correctness and measurement plan

The adapter owns copies of each accepted streaming event before the next read.
Attribute values/names, namespace declarations, frames and NameTable storage are
explicit persistence/setup costs. Reader event copying does not promise zero
allocation. Existing primary stream ownership and strict parser behavior remain.
Source identity separates effective ancestor xml:base from redirected entity bases.

Navigator construction reuses the integrated M4 owned XPath capture for logical
text runs. It builds private navigation rows and drops the temporary M4 capture
and live document handles. Its immutable strings may share existing owned DOM
strings. Clones allocate one independent cursor and share rows/NameTable/IDs.
Construction, aggregate string values, namespace dictionaries and row arrays are
persistence costs; retained snapshot memory is measured separately.

The prepared NoEmit harness has exactly 16 cases, each class declaring
[MemoryDiagnoser, ShortRunJob], with 3 warmups/3 iterations/1 launch and no CLI
job flags. Three fixed scenarios cover a short ASCII element, 64 repeated ASCII
names, and 64 distinct names with multilingual/non-BMP values and entity markup.
The BCL reader and XPathDocument traversal controls consume equivalent name,
value and attribute counts. Snapshot benchmarks start from a completed DOM;
parsing the DOM is setup, not snapshot timing. BCL traversal setup also excludes
parsing. Invalid-input timing includes owned diagnostics/errors. All fixture
byte generation stays outside timing.

The separate allocation phase reports inclusive numerators and denominators:
32 warmed batches ×256 groups of snapshot traversal plus current owned-attribute
position/property/namespace reads, and128 operations per scenario for owned
reader events, snapshots and actually suspending asynchronous input. Async uses
GC.GetTotalAllocatedBytes(true) across all threads. Cold fixture setup, invalid
input, positive allocation control, retained16snapshot heap and consumed checksums
are recorded. Only warmed cursor/attribute primitives promise0B; no whole-reader,
snapshot or async zero-allocation claim is made. GC evidence stays authoritative
if BDN prints a dash. This is one Arm64 host; timing is informational, not an x64
or hardware-independent release claim. No SearchValues/hash optimization is added.

Ordinary correctness, API extraction, build, benchmark discovery and nonmeasuring
BCL controls execute before PARK. M6-contracts'57Interop golden API rows must
match actual compiled metadata; the union also checks the prior948rows. The
independent sample executes only this lane and verified DOM/streaming APIs.
The dotnet-inspect skill was read and attempted with the pinned host; dnx failed
with unavailable NuGet DNS. Installed BCL reference/compiler and actual metadata
extraction provide the independent override-shape checks; no online API claim.

Frozen closure includes all committed files, all generated obj outputs, all bin
outputs, entire pinnedSDK10.0.400/runtime10.0.11/hostfxr files, host details and
effective runtime/runner environment. RuntimeFrameworkVersion10.0.11 applies to
forced restores and no-incremental builds. Runtimeconfigs are inspected before
PARK; direct parents explicitly select --fx-version10.0.11 and assert actual
FrameworkDescription. The harness is in-process NoEmit and creates no child
runtime. Exact exclusive plan cap1250seconds: initialidentity40, allocation90,
benchmark1080, finalidentity40 with cleanup reserve. Runner always checks phase
exits, kills only its own process groups, checks no children and final identity
in finally. A coordinator lease is required; --exclusive-grant is not authority.
Durable lease release must precede raw inspection. Commit summary tables/analysis
only, and inspect raw before deletion. This preparation records no measurement run.
