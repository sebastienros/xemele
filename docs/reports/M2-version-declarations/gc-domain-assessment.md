The retained timing attempt remains failed. Offline validation now establishes
14 exact case identities and distinct positive executor PIDs, zero exits,
successful executable discovery and no executor errors. Direct Executor results
are authoritative. Optional textual exit records must agree when present.
The original report and its unrun post-freeze are unchanged.

The pinned BenchmarkDotNet 0.15.8 net8 library hash is
`d460626b2e1a9fa2f786e09e6c85cdf4d7b73deca636450a0e23892c8ce6192b`,
identical to the frozen generated child. The dotnet-inspect metadata query for
`GcStats.ReadFinal` exited zero. No methods or counters were executed.

The [pinned GcStats source](https://github.com/dotnet/BenchmarkDotNet/blob/v0.15.8/src/BenchmarkDotNet/Engines/GcStats.cs)
uses `GC.GetTotalAllocatedBytes(precise: true)` in the modern target. This covers
process allocations. Our preserved 512-operation direct delegate oracle reads
`GC.GetAllocatedBytesForCurrentThread`. These are different counter domains.

The [pinned Engine source](https://github.com/dotnet/BenchmarkDotNet/blob/v0.15.8/src/BenchmarkDotNet/Engines/Engine.cs)
collects GC statistics in a separate generated workload invocation after timed
iterations, bracketed by initial/final GC statistics. Iteration setup and cleanup
are outside that bracket. Thus the GC row does not sum the three Actual rows.

Thirteen retained cases exactly match the direct oracle. Long ParseDocument has
4,554,456,416 actual bytes versus 4,553,768,960 expected (8,192 × 555,880), a signed
difference of 687,456. The differing domains are an established limitation of
this comparison, not evidence identifying which allocations produced that gap.
No subtraction, tolerance, counter substitution, runtime/JIT change or core
change is justified by this source assessment. Strict equality remains enforced.

A3 remains unmet: the numeric mismatch and missing original post-timing freeze.
A1/A2 have no new regression evidence. The smallest next step is review of this
source/domain assessment and exact integrated discovery input. No replay of the
successful allocation phase or unchanged full timing set is needed for the
validator correction; future execution, if justified, must target missing evidence.

The [normative performance rules](../../performance-coding.md#evidence-required-for-hot-path-changes)
promise 0 B only for steady-state scan/match/slice and borrowed dispatch after
documented initialization with bounded preallocated state. They explicitly
account separately for owned results, DOM/interner/I/O setup, pool/static warmup,
and cold initialization. Task A3 asks applicable allocation/benchmark evidence;
it does not promise zero full-parser construction allocation or equality of
process-wide and current-thread totals.

`ParseDocument` constructs and disposes a new parser each benchmark operation;
`FrameOwned` does the same for its framer and retains owned declaration output.
These lifecycle benchmarks include cold instance state and owned output, unlike
the preallocated long/ordinary boundary probes. The preserved allocation pass
establishes the actual product targets: borrowed core/digit 0 B, long owned
120,064 B, ordinary owned 32 B, with exact positive controls. It does not establish
process-wide allocation freedom or a process-wide full-lifecycle budget.

Therefore the numeric A3 mismatch is an unmet harness cross-domain equality
requirement, not established evidence that a steady-state product target failed.
Its actual source remains unresolved. The smallest valid proposed correction is
to report current-thread lifecycle totals and BenchmarkDotNet process totals as
separate exact metrics with their respective scope, alongside the already passed
strict product-boundary evidence. Such a semantic gate change needs explicit
review; this candidate keeps the existing strict check and original failure.
An alternative paired experiment would require the same counter domain and
workload boundary, and still cannot subtract unobserved background allocations
or relax exact totals. It is not authorized or executed.

No unchanged full allocation or timing replay is needed to fix exit validation.
The missing original temporal post-freeze cannot be reconstructed offline after
raw binaries were cleaned. Root should decide whether the archived timing remains
provisional applicable evidence with this limitation, or authorize only the
specific missing evidence required by A3. No overall timing pass is claimed here.
