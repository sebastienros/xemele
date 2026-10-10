# Incremental external text declaration framing

Task state: implemented, awaiting coordinator integration verification. Fresh renewed
correctness verification passed at `dbcd535e80f2da0b808a5d9e40af78fd8e75be19` after
adopting renewed claim `7034b2769307fbab70ea524ea2a16f5defa8e7d6` and its verified
measurement prerequisites. MTP, direct execution and discovery agree on 2,044
tests (1490 runtime =247 earlier +1243 framing, 255 conformance, 299 differential),
with no failures/skips. API222, registry3388, all242 fresh pinned reference record
hashes/builds and unchanged DLL, planning14, validation114 and renewed-claim
ownership audits passed. `renewed-correctness/` contains the complete results.

Before renewal, MTP discovered 1222 split cases while direct execution ran1226;
the missing four were the lone-low-surrogate fixture at splits0/1 with/without
mappings. An ASCII fixture identifier restores those four cases without a runtime
change. Raw discovery/execution evidence is preserved in `renewal-preparation/`;
`renewed-correctness/count-reconciliation.json` verifies all four discovered
ASCIIcase32 rows and actual passing XML records. The underlying native transport
collision mechanism is not separately established.

Runtime source was independently reviewed at
`46104cecdce70170aea050612d13a77f1467251a`. The latest complete source checkpoint is
`cea326c7ed59ac53a832af2f8cc4e528a118d89a`; its only runtime change after that review
is the friend assembly for the task's uniquely named benchmark harness. Later
dbcd535 changes only owned test identity and the verifier, preserving runtime.

The internal `Xemele.Parsing.Dtd` types implement the approved framing contract:
`IDtdBudget`, `XmlTextDeclarationFramer`, `XmlTextDeclaration`,
`TextDeclarationResult`, and `TextDeclarationStep`. The independent explicit-source
project compiles actual implementation with common limits/diagnostics and scalar
locations only, then compares its member inventory and shared-budget constructor
against separately compiled approved fixture declarations. The sample demonstrates
split CRLF declaration framing and once-only absent-prefix handoff. These are
working framing components, not a DTD grammar, resolver, or body decoder.

The recognition prefix owns at most seven UTF-16 units and original mappings. One
unfinished surrogate is separate bounded carry. Declaration grammar uses scalar
state and fixed literal comparisons; only its encoding label uses growable owned
scratch. Whitespace is never buffered. The live shared ledger is charged once at
recognition and before every subsequent declaration scalar; absent prefixes remain
uncharged until a downstream consumer accepts them. Owned results and diagnostics
survive disposal. Source views never survive `Append`.

`bounded-checkpoint/` records the actual committed-source attempt. All 1,243
framing tests passed, including every two-chunk split, mapped supplementary
recognition, malformed declarations, exact/one-over quotas, incremental live
refusal, CRLF, EOF, cancellation, budget exceptions, reentrancy, disposal, owned
snapshots, long-label growth, and real M1 decoder handoff before body decoding.
The inherited model/foundation checks and cumulative MTP run passed (MTP reported
2,040 tests, zero failures/skips), and the compiled public API matched all 222
members. A subsequent direct run reported 1,490 runtime tests with one unchanged
M1 PI allocation assertion failure: expected 0 B, observed 712 B. Its XML and the
complete command log are preserved. Final reference provenance, separate
conformance/differential XML, and final verifier audits were unrun after that
failure. The coordinator-owned measurement repair was required before renewal;
it is now verified and integrated. This task did not modify the earlier test.

`checkpoint/` preserves the earlier 1,944 B PI assertion failure in cumulative
MTP execution and a separate direct runtime run with all 1,490 tests passing.
`preparation/` preserves the initial sandbox NuGet failure, cancellation analyzer
errors, four corrected test expectations, and corrected sample consumption count.
No failed attempt is represented as a successful complete verification.

Allocation probes on pinned SDK 10.0.400/runtime 10.0.11, macOS Arm64 observed
768 managed bytes for cold/complete owned declaration operations and zero bytes
for 1,000 warmed whitespace calls. The forced-collection retained-result estimate
was 160,048 B for 1,000 results; the declaration struct is 48 B. This is a GC heap
estimate, not an exact object-graph size or a zero-allocation persistence claim.
The earlier less-isolated estimate (160,744 B) remains in the first checkpoint.

Both benchmark classes use `MemoryDiagnoser` and `ShortRunJob`. The dedicated
assembly is `Xemele.DtdFramingBenchmarks`, with only this task's linked benchmark
source and driver. Scan state resets outside each measured iteration and its
8,192 invocations accept at most 33,554,432 raw units per iteration. Untimed
preparation passed four scan combinations times three iterations times 8,192
calls with matching cursors/consumption, plus six owned-result workloads.

The coordinator then granted one exclusive-host measurement at dbcd535. The
generated restore/build and all14 benchmark children exited0; all14 cases produced
three positive actual observations (42 total), completing in41.914seconds within
the300second build/1200second group deadlines. All eight scan cases used8192
operations per observation and measured0B managed allocation. Owned operations
measured648B absent/multilingual,768B short/chunked,720B malformed,25376B long-label.
Short declaration framing averaged242.59ns and a4096character encoding label
21061.39ns. Fixed-state framer scans were2.75–3.71times slower than the narrower
scalar baseline; no optimized matching or speed benefit is claimed. The direct
warm probe and the benchmark both demonstrate allocation-free whitespace work
separately from setup, owned strings and diagnostics.

`exclusive-measurement/` preserves complete raw output/wrapper/BDN reports in
`full-raw.zip`, its hash, actual per-case progress/child exits and CSV/Markdown
results. Hardware: AppleM4Pro14cores, macOS15.8.1Arm64, SDK10.0.400/runtime10.0.11,
ConcurrentWorkstationGC. Process priority elevation was denied. Six scan cases
received minimum-iteration-time warnings; several intervals are wide. These are
provisional framing measurements, without a general throughput claim; full DTD
composition and x64/Windows/AOT checks remain later integrated work. The wrapper
confirmed no process-group survivors; an initial sandbox-blocked separate `ps`
inspection was repeated successfully with authorized escalation. No timing retry
was performed.

For subsequent integration, run a fresh unique report using the pinned SDK
environment and `verify.py --report-prefix <unique>`.
The verifier accepts `--audit-base`, `--expected-branch`,
`--expected-runtime-tests`, `--expected-conformance-tests`,
`--expected-differential-tests`, and `--expected-public-api`. Worker counts are
1490/255/299 and API222; integration must pass its actual combined counts. It
provisions the private corpus cache and copies a fresh complete BCL runner output
before inherited solution builds, then checks all 242 record hashes/builds and
the unchanged copied DLL when the preceding tests pass. No private corpus is
published. Final planning14, validation112, and owned-path audits must be
recorded at the eventual integrated checkpoint; renewed correctness already
passed planning14/validation114 and its owned-path audits.
