# Recursive XSD component repair

The unchanged W3C witnesses at pinned xsdtests revision
`7bc3365c652a322f3d762021b3879eb92dae7e30` distinguish legal recursion across
element declarations from forbidden group-particle and base-type containment.
The fixture manifest freezes exact original bytes, hashes, metadata expectations,
and the original copyright notice. All three schema expectations and
`particlesEb041.v` are valid. The independent BCL schema/instance checks agree.

At baseline `b2bf28d12f0586e3f02fad6f1b108416ce4c4e77`, all four product cases fail:
`addB077` and `particlesZ010` reenter the model-group guard; `particlesEb041`
reenters the type-derivation guard (schema and instance entry points). No original
is rewritten, excluded, or classified as an expected semantic rejection.

The compiler now reserves the identity of an inline element complex type and
stores a charged, operation-owned pending-body entry. The containing element,
group, or type completes first. A dynamically growing completion loop then
finishes every pending body before graph checks and immutable publication.
Element terms keep finite declaration IDs; type relationships keep schema names.
No mutable type stub escapes. Simple-type grammar still completes immediately.
Direct/indirect model-group, attribute-group and base-type cycle guards are
unchanged. Unused groups retain their existing publication checkpoints.

At `9e376cd982159b27cbb05c1fa5cf2577bd21e7d2`, the counter-disabled checker passes
1300 cases (37 owned plus the established 1263 affected set), 42 safe resolver
cases, all 1430 public members, all eight frozen M8 lanes, the internal compiler,
content, identity, evaluator and facade surfaces, normal and true source-linked
XML-only facade consumers, planning validation and diff checks. The six builds
emit zero warnings/errors. The task consumer additionally exercises recursive
string, DOM and stream entry points in both normal and source-linked XML-only
assemblies, and checks complete invalid neighbors and caller stream lifetime.

The owned inventory covers ordered/mutual group and element recursion, enclosing
base-type restriction/extension, further queued nested types, independent BCL
negative oracles, unused facet/UPA/content consistency checks, late source
mutation, owned snapshots, exact aggregate work/component boundaries including
imported schema parsing, cancellation in an actual deferred body with null caps,
and character/byte/final-resource provenance. Failed resource/work limits remain
incomplete refusals, rather than counting as semantic-invalid passes.

Historical transient failures remain visible in `correctness-summary.json`:
the first expanded build used the nonexistent `XmlLocation.ColumnNumber`
property (one compile error, no tests executed); it was corrected to
`LinePosition`. The first owned run had 32 passes/3 failures because the new
missing-component tests incorrectly expected the reference site's line. Existing
component lookup diagnostics name the owning complex type. Tests now assert that
original behavior explicitly and additionally check precise particle occurrence
sites, including both character and byte input. Runtime location behavior was
not changed to satisfy the tests.

No allocation counters, heap probes or authored timing measurements ran during
this correctness phase. Standard build/test duration text is incidental runner
output. Allocation evidence remains pending independent source acceptance and
separate authorized execution. Compilation is an owned-result boundary: pending
queue storage, component registration, immutable programs and final graph
snapshots are expected positive allocations; this repair introduces no borrowed
scan primitive or allocation-free compile claim.

## Reviewed allocation preparation

Runtime source review passed9e376cd98; independent allocation source review
passed clean822e5a756. Accepted offline prepare.py build/control and elevated
measure.py --verify-only/--preflight passed. SDK10.0.400/runtime10.0.11 on
macOS15.8.1 Arm64: zero warnings/errors;13routes,1085calls,26095consumed;
all allocation/heap fields zero in disabled mode,32retainedobjects.455frozen
identities match. Disabled preflight child52566 exited0, was reaped, its group
was absent and its log closed; before/after identities match. See
allocation-preflight.json and preparation.json for exact source/probe/runtime/
supervisor hashes. No actual counters have been executed by this worker.
Root alone launches the accepted payload without an isolation/PARK/ACK/lease
or exclusive-use gate.

Supervisor-only amendment e458a2d4b: workload success now requires
reason=completed in addition to exit0 and clean cleanup. The finite regression
child52913 timed out, handled SIGTERM by exiting0, and cleaned fully; it remained
a failed workload and the next child never launched. Normal child52915 passed.
The refreshed457identity manifest is fe4c11dbee5934ae51cfbbe3b0c4a9dd5cc90985d289372231c6409605fbcad2,
supervisor b49f0beefb943c0e531f88235d12d269f671584da0563834a3d41159b1e100e5.
Actual verify-only/ps permission passed. The prior455identity control/preflight
is preserved; runtime/probe/control were neither rebuilt nor rerun. No counters
were launched. See lifecycle-check.json and allocation-preflight.json refresh.

## Final actual allocation evidence

Root nonisolated allocation run passed after supervisor amendment acceptance.
Durable cleanupcc2f59ce6 preceded raw inspection; control53183/measure53185
completedexit0 and cleaned fully,457identities matched. All832owned samples
positive, aggregates equal distributions; semantic consumption26095 identical.
Cold3452776B/positive1048B;32retainedobjects in4categories. Negative whole-heap
diagnostic/result deltas are GC/lifetime noise, never negative/zero ownership.
See allocations.md and measurement-summary.json for summaries and limitations.
No source/probe/binary change, build, runtime test or measurement repeated during
this final metadata update. All branch criteria pass; state implemented awaits
coordinator integration verification.
