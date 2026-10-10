# Whitespace framing allocation probe

Only the original `WarmWhitespacePrimitiveHasNoAllocations` Fact body changes.
It runs one guarded real child and then one escaped-positive child of the same
compiled Fact. The synchronous non-inlined helper encloses both allocation
counters and all 1000 checked calls; 1000 identical warm calls precede it.
The cancellation token is captured before either interval. Input is the original
32-space literal after `<?xml `, with both resource limits null. No counter
subtraction, retry, tolerance, GC/JIT setting or class parallelism change exists.
Completion verifies the final scalar cursor after all 64000 whitespace units.
The positive child allocates and escapes a 37-byte array per measured call.

Child requests and reports reject missing and unknown fields. Capability, parent
PID, exact invocation, current host, assemblies and source versions, helper IL,
native runtime, core library, runtimeconfig and actual GC/environment provenance
must match. Raw stdout/stderr/XML/exit/request/progress/report files survive before
parsing or assertion. Children have a 30-second deadline and bounded tree kill.
One additive protocol Fact verifies report rejection using the real validator;
request rejection controls invoke the same allocation Fact and fail before work.

`verify.py --prepare-only` builds and fingerprints without allocation observations.
The full driver requires an exclusive coordinator grant. It supports explicit
branch, audit base and all exact inventory counts. A fresh entire BCL Release
output is pinned before inherited M1 verification builds. Tracked compiled source
identities, the complete pinned copy, discovery and direct XML counts are checked.
Known-source original/equivalent/helper diagnostics run once per fresh process;
the original diagnostic preserves per-iteration TestContext lookup outside a live
Fact and cannot explain historical test failure. Historical entity 175635c evidence
is copied byte-identically in `history/`; no failed XML was requested at that run.

No runtime, public API, entity implementation, benchmark or successful timing is
changed. Allocation failures stop dependent checks and require runtime-owner
repair if observed in the actual checked framer helper. Checks not run remain
unclaimed. Baseline 5068 = 4514 runtime + 255 conformance + 299 differential;
this change adds one runtime protocol test, so expected totals are 5069/4515.
