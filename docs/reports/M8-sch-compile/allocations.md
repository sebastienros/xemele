# Compiler setup allocation evidence

Accepted compiler source: `193a6fe3ddcdf209bea0c623b154cbb6abd99b9c`.
Supervisor checkpoint: `837a001b0e7fb24f59220dfe43216d5fccabe781`.
Frozen preparation/evidence: `8c41188ff24d1ad9b34778ea0430916d45eaefad`.
Manifest SHA256: `be99198a08d5f36b09bf507dae06cc07869c630832865fdc01ed881fde92c54b`.
All212 source/runtime/configuration/binary/control identities were unchanged
before and after the single prebuilt synchronous probe.

The first proposed serial window was aborted before GRANT after its start
boundary; no child, measurement or raw output occurred. Fresh durable GRANT
`5ac7a0815558aec519309e6fa2f6a663690c58f3` at2026-10-11T23:44:07.536590Z
allowed root-only execution. RELEASE
`6b35e04d057e7ec516a967fd52836a0aa46da1dd` at23:44:30.974440Z preceded every
raw inspection. Root independently inspected and accepted the same figures and
lifecycle. This report covers the successful SCH phase only, not the later
separate datatype phase in that serial window. No retry or remeasurement occurred.

Exact command from `/Users/sebastienros/.codex/worktrees/7e1d/xemele`:

```sh
python3 docs/reports/M8-sch-compile/lease.py --granted be99198a08d5f36b09bf507dae06cc07869c630832865fdc01ed881fde92c54b 2026-10-11T23:48:00Z 2026-10-11T23:54:57.536590+00:00
```

The supervisor additionally caps its own phase at30s total (25 work+5 cleanup),
checks absolute UTC bounds and refuses existing allocation/log/lifecycle outputs.
Child PID47916 exited0, was reaped, its group was absent and its log was closed.
Lifecycle status0, before/after manifest SHA and212 identities passed; elapsed
0.320030958s is a cleanup/deadline observation, not compiler latency evidence.
Raw output remained unopened until durable RELEASE.

Apple M4 Pro,14 logical cores, Arm64; SDK10.0.400/.NET10.0.11; tiered compilation
and concurrent GC disabled, workstation GC. The actual host/corelib, prepared
binaries, source/configuration and counter-disabled control hashes are in the
owned preparation manifest. The corpus is pinned by the benchmark Program.cs
identity: a simple rule/check graph; an abstract-pattern/extends graph with two
phase bindings and dynamic diagnostic/property messages; a4096-character
multilingual message; and an unresolved-variable diagnostic. Documents and
captured probe delegates are prepared outside warm counters.

Each warm route executed32 warmups, then64 consumed measured operations. The
counter-disabled control passed the same six routes and emitted null bytes,
retained delta and RSS; the positive byte-array control is strictly positive.
These are exact aggregate64-call window totals and derived means. Individual
samples, minima/maxima or allocation distributions were not collected.

| Warm route |64-call total bytes|Window mean B/op|
|---|---:|---:|
|Positive owned byte-array control|9,728|152|
|Owned simple graph|1,287,680|20,120|
|Owned abstract/phase/message graph|6,617,896|103,404.625|
|Owned long multilingual graph|1,365,504|21,336|
|Owned invalid-schema diagnostic|995,328|15,552|

The complex total is not divisible by64. Its fractional mean does not establish
a fractional or constant per-call allocation; the residual cause is unknown.
No per-call constancy or distribution is inferred from any aggregate row.

First owned parse-and-compile operation:43,216 bytes and one concrete pattern.
This counter starts after probe arguments/environment/result-list and source
string setup; it excludes process/runtime startup. Approximate retained delta
for16 complex compiled snapshots after full collections:181,592 bytes. Process
working set:54,870,016 bytes. These are process-local observations, not isolated
object sizes, peak usage or retention bounds.

Compiler source handles and borrowed lexical tokens live only during Run.
Published graphs retain immutable strings, M4 programs and owned collection
snapshots, never caller document/node handles or resolver buffers. Source
snapshots, expansion scratch, namespace/variable analysis, independent M4
compilation, final graph/context collections and diagnostic construction are
owned setup allocations, which this probe measures explicitly. The retained
snapshot estimate is separate from those transient allocations. The mutation
and concurrency regressions verify lifetime separation.

No borrowed-zero claim, optimizer speedup, BenchmarkDotNet/timing campaign,
composed validation performance, corpus conformance or cross-architecture result
is claimed. Later evaluator/facade composition and release checks retain those
obligations. The three inspected transient allocation/log/lifecycle files are
removed only after this concise evidence is committed and root inspection is
complete; no raw result archive is committed.
