# RELAX NG compiler handoff

Source: `d2066ee11a6c2616a5806ce67ae04385ab703316`.
Supervisor: `a1df184d9a39e68b8b17e59bba2e1ab88df12522`.
Prepared manifest/evidence: `35842767b2791948b5bfab50c5cd32a48bfd09c5`.

The internal compiler implements the 2001 XML grammar, annotation/whitespace
handling, inherited namespaces/libraries, arbitrary name classes, grammar and
parentRef scopes, combine, include overrides/externalRef, simplification and
datatype-independent restrictions. It publishes a dense immutable graph with
element references and original scalar/byte source coordinates. Detached
datatype checkpoints retain removed/unreachable authored lexical contexts for
the later verified facade's mandatory finalization; see [join notes](facade-join.md).
No public facade, evaluator, executable datatype placeholder or peer dependency
is supplied. Publication validates payload/arity/edge invariants.

Generated name-class choices/exclusions are balanced. Source traversal has a
finite 256-level bound even with null/large XML depth limits. The generated
name-class representation bound is 1,024; only its dedicated internal exception
is translated at publication into fatal SCHEMA.Limit with the pattern's source.
This preserves arbitrary caller/resolver ArgumentException provenance.

## Correctness and compatibility

Pinned host: `/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet`;
SDK 10.0.400, runtime 10.0.11. Release builds used `--disable-build-servers -m:1
-p:RuntimeFrameworkVersion=10.0.11 -p:UseSharedCompilation=false -p:NuGetAudit=false`;
restores used only `/Users/sebastienros/.nuget/packages`.

- Owned focused suite: exactly 88 passed, zero failed/skipped/not-run. Includes
  8,000-sibling choice/exclusion, 2,000-level null/large-depth input, mixed
  220-by-17 generated representation limits, immutable shapes, full grammar,
  restrictions, scopes, resource overrides, redirects, cycles, exact quotas,
  cancellation, ownership and original locations.
- Approved resolver correctness argv: exactly 42 passed; allocation regression
  `WarmBorrowedReadsAllocateNothing` was never selected.
- Narrow verified model/source-location/M4/M5 classes: exactly 131 passed.
- Frozen compiler declarations: all 119 matched `frozen.txt`, an unchanged subset
  of `RelaxNg.golden.txt`. Scoped complete public comparison: 1,393 matched using
  unchanged `docs/reports/M5-validation-model/check_api.py`.
- Real XML-only source-linked consumer passed recursive immutable graph, original
  context, definition-only registry, M4 XPath/M5 DTD preservation and absence of
  HTML/CSS source families. Builds had zero warnings/errors.
- Eleven-route counters-disabled control passed; all 198 prepared identities and
  elevated process-inspection permission verified.

Historical failures are preserved honestly in the task evidence. The first test
build had five test-only enum-conversion errors, corrected before the passing
run. The 1,024-level recursive source guard at `9d31c703` failed the new depth
regression with a stack overflow; the finite 256 source guard and dedicated
generated-graph limit translation resolve it, and the final 88 cases pass.
Independent full API inventory discovery rejects the preexisting header-only
M8-regex fragment; the coordinator selected the established complete scoped
compiled check, which passes, and directed no shared edit for that unrelated
discovery limitation. No conformance corpus or cross-platform run is claimed.

## Allocation evidence

The first proposed window was aborted before launch after its start deadline;
no measurement occurred. The root executed the one actual probe under durable
combined grant `cbf9167bd7d5213f0b7bced6ad6e272c791f4433`, then recorded RELEASE
`49b063ec7390ad03fc711287c022a98de4620530` before raw inspection.

Sole RNG command, from this isolated checkout:

```sh
python3 docs/reports/M8-rng-compile/lease.py --granted dad039ab3cd24e58cebb5bb0e7e5379c3155b60a837ef21a9f0695295f55ba93
```

Lifecycle: child PID 24472 exited 0, reaped, owned group absent, log closed;
198 identities and approved manifest SHA256 unchanged before/after; elapsed
0.281921666 seconds within 30 total (25 work + 5 cleanup). Raw results remained
unopened until coordinator RELEASE. No rebuild or retry occurred.

Arm64, runtime 10.0.11, tiered compilation disabled, workstation GC, concurrent
GC disabled. Each steady route warmed 32 times and consumed 64 measured results.
These are exact allocation totals and mean bytes/operation across the 64-call
window; individual nonzero samples were not collected.

| Route | Total bytes | Mean B/op |
| --- | ---: | ---: |
| Straightforward short-hit baseline | 0 | 0 |
| Borrowed short hit | 0 | 0 |
| Borrowed short miss | 0 | 0 |
| Borrowed 4,096-character hit | 0 | 0 |
| Borrowed multilingual hit | 0 | 0 |
| Positive byte-array control | 9,728 | 152 |
| Owned simple three-node graph | 515,584 | 8,056 |
| Owned recursive ten-node graph | 2,012,672 | 31,448 |
| Owned name-class construction/overlap scratch | 28,160 | 440 |
| Owned invalid-schema diagnostics and compilation | 830,464 | 12,976 |

First owned parse/compile operation: 23,624 bytes, three graph nodes. This begins
after probe argument/environment/result-list setup and excludes process/runtime
startup. Approximate retained delta for 16 recursive graph snapshots: 185,608
bytes after full collections. Process working set: 51,773,440 bytes. Retained
delta/RSS are process-local observations, not isolated object-size or peak-memory
guarantees. The five warm borrowed/baseline routes have exact zero total bytes;
owned setup, graph/context snapshots, overlap scratch and diagnostics allocate
as expected and retain no caller nodes or resolver buffers.

RNG used allocation counters only. No BenchmarkDotNet, latency/throughput campaign
or optimizer speedup was run or claimed. No raw benchmark output is committed.
Cross-architecture and composed validation performance belong to subsequent
integration/release checks. Raw lifecycle/control/allocation logs remain under
ignored `artifacts/M8-rng-compile/` for coordinator inspection.

## Integration follow-up

Coordinator integrates and reruns relevant checks, records integration revision
and evidence, then marks verified. The validation facade must complete the exact
datatype/facet/regex join described in [facade-join.md](facade-join.md), including
all disconnected datatype checkpoints, conjunctive repeated RNG patterns and
duplicate non-pattern parameter rejection. There are no outside-ownership change
requests from this task.
