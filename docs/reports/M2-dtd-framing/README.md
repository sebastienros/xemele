# Incremental external text declaration framing

Task state: in progress. Runtime source was independently reviewed at
`46104cecdce70170aea050612d13a77f1467251a`. The latest complete source checkpoint is
`cea326c7ed59ac53a832af2f8cc4e528a118d89a`; its only runtime change after that review
is the friend assembly for the task's uniquely named benchmark harness.

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
failure. A coordinator-owned measurement repair is required before another full
attempt; this task does not modify the earlier test.

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
calls with matching cursors/consumption, plus six owned-result workloads. No
timing has run; an exclusive-host coordinator grant remains required. These are
provisional framing workloads; full DTD composition and x64/Windows/AOT checks
belong to later integrated work.

After the measurement prerequisite is repaired and integrated, run a fresh unique
report using the pinned SDK environment and `verify.py --report-prefix <unique>`.
The verifier accepts `--audit-base`, `--expected-branch`,
`--expected-runtime-tests`, `--expected-conformance-tests`,
`--expected-differential-tests`, and `--expected-public-api`. Worker counts are
1490/255/299 and API222; integration must pass its actual combined counts. It
provisions the private corpus cache and copies a fresh complete BCL runner output
before inherited solution builds, then checks all 242 record hashes/builds and
the unchanged copied DLL when the preceding tests pass. No private corpus is
published. Final planning14, validation112, and owned-path audits must be
recorded at the eventual successful checkpoint.
