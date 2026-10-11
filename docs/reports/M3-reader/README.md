# Synchronous reader verification

`XmlReader.Create` snapshots validated strict options without input reads. The
fixed 4096-byte transport refills only after pump NeedInput. Each false sink
result accepts one current token; there is no notification list or DOM.
Current cursor copies borrowed spans into reusable character scratch and keeps
only one tag's owned attribute/namespace rows and incomplete-part containers.
Rows necessarily own strings because the public callback view expires at return
and the integrated collection view is backed by string-valued rows. These
persistence allocations are separate from the allocation-free warmed character
copy primitive. Clearing drops all row references and owned current diagnostics.
Capacity follows the largest accepted current token/tag, bounded by operation
quotas and representation; disposal releases cursor scratch.

Read clears current state before cancellation, transport or parsing. Terminal
pump diagnostics throw an owned XmlParseException immediately. EndDocument is
one true read, followed by sticky false/None. Faults are permanent; cleanup
preserves primary exceptions. Dispose aborts and leaves caller input open.

The class is partial; asynchronous members are not present. Current pump is
constructed in synchronous resource mode. Future async input requires an owning
pump/core resource-mode hook before reusing this state without replay. Reader
private fields and helpers are accessible to that future additive partial.

Build with the pinned SDK, Release, `-m:1 -p:BuildInParallel=false
-p:UseSharedCompilation=false --disable-build-servers`, and restore with
`-p:NuGetAudit=false --ignore-failed-sources -m:1 --disable-build-servers`.
Focused xUnit invocation uses `-namespace Xemele.Tests.ReaderRuntime -namespace
Xemele.Tests.StreamingPump -noColor` on the prebuilt tests assembly.
`check-api.py <pinned-dotnet>` compares 39 sorted task rows against compiled
frozen Sync.cs.fixture using the approved unchanged M0 extractor, then checks
all composed API fragments against the runtime assembly. Contract.csproj links
the frozen fixture; it is a nonpackaged declaration assembly, never runtime.

The owned report Probe/Reader.Probe.csproj builds a prebuilt isolated executable. Its default
command measures cold cursor setup, 100,000 warmed current copies, an owned-string
positive allocation control, and full consumption of generated 1 MiB/8 MiB
character bodies. The generated stream keeps no corpus buffer. Full-GC live
sampling every 256 KiB (16 MiB for the optional GiB run) while the reader is
actively positioned, with the reader kept alive separates retained memory from
transient allocation; required large-input thresholds are 16 MiB retained delta
and no more than 8 MiB growth over the small workload. Document and text-token
quotas are explicitly unlimited only in this bounded test; ordinary reader
finite defaults are preserved. The default feature probe has a 60-second external process deadline. Optional
`gib` measures a full 1 GiB with a 15-minute deadline and progress at 128 MiB
intervals. The coordinator assigned full 1 GiB qualification to M3-corpus; it
remains required there and is not claimed by this feature probe.

`bdn` uses one explicitly configured InProcessNoEmit job with 3 measurement
iterations, 1 warmup, 1 launch, 1024 invocations and unroll factor 1; a converted
suite count guard rejects any scope other than exactly 6 cases before running. It uses
MemoryDiagnoser for multilingual short/4096-char owned-copy baseline, reusable
copy, and end-to-end parsing with setup and owned rows. Fixtures are outside
measurement. No scanning/matching algorithm changed; straightforward span copies
need no SearchValues or token matcher. Timing is informational on this shared
Arm64 host; cross-architecture release validation remains future release work.
Actual measurement outcomes and revision are recorded in the task evidence.
