# Nonvalidating DTD PE nesting

The repair distinguishes PE insertion in DeclSep (`Between`) from insertion
inside a declaration, entity literal, or conditional header. Each source retains
its insertion context and a conditional depth floor. A DeclSep replacement must
finish in `Between` at its entry depth with no declaration left open; it cannot
close a conditional section opened outside that replacement. Nested sources
inherit that floor. Declaration/group/conditional source identity alone no longer
causes a validity-only fatal error. Whole-subset expanded grammar, internal-subset
PE placement, source EOF scalars, complete comments/PIs, quote containment,
recursion, cancellation, quotas and balanced source entries remain enforced.
Declaration locations still come from the `<` opener, including resource bases
when a redirected child supplies a declaration's closing tokens.

The six original corpus cases have independently authored complete DTD model,
particle, default, DOM and source-location oracles in
`tests/fixtures/M2-dtd-nonvalidating/original-oracles.json`. The fixture contains
metadata and expectations, not duplicate corpus payloads. Offsets count original
bytes including CRLF; locations count normalized scalars. Tests assert original
resource hashes and immutable profile expectations, every primary split, every
callback pause, one-byte external reads, source suspension and resource cleanup.
Paired synthetic controls cover every parent/child split, malformed expanded
groups, incomplete DeclSep replacements, conditional escape, literal containment,
internal placement, cycles, cancellation, quotas and budget exceptions. The only
historical transitions are the authorized `(a` fragment and two
`ChildCannotCloseParentMarkup` rows. The five `ChildCannotLeaveOpenMarkup` rows
and internal incomplete declaration still reject.

`measure.py` is a thin adapter over integrated entity-base preparation/execution
support. Its only source substitutions route task artifacts and the benchmark
class label. It hashes that reused Python source; the owned measurement project
links the unchanged driver, project binding and binary provenance sources. The
existing project filename and assembly name preserve the verified binding
contract. Offline adapter controls verify counter-disabled routing, exact class
routing and refusal of missing/duplicate/nonzero borrowed proofs. No alternative
process-control, preparation, binary-identity or benchmark framework is added.

The two declared `[MemoryDiagnoser, ShortRunJob]` cases retain the driver method
names `InternalNestedModel` (internal PE fragments in an external DTD) and
`RedirectedComposition` (three exact resources, default, opener base and complete
document). Cold setup/oracles and warm owned model/document output are allocated
owners. The allocation child also measures 1024 warm borrowed whitespace Appends
inside a suspended external PE: the existing 0 B target is unchanged, initialization
and terminal model construction are outside that interval. These counters execute
only for the explicitly granted `--allocation` invocation. Timing is informational
on this Arm64 host; no cross-platform speedup is claimed.

Use pinned SDK 10.0.400/runtime 10.0.11 with
`DOTNET_CLI_USE_MSBUILD_SERVER=0` and no GC/JIT overrides. Preparation executes
identity/correctness and Generator/Builder only, never Executor or counters:

```
python3 -B docs/reports/M2-dtd-nonvalidating/measure.py --mode prepare --report preparation-<revision> --benchmark-report benchmark-<revision>
```

After source review and a fresh bounded exclusive coordinator grant, use the same
prepared child for one allocation and one benchmark sequence, then immediately
release the host. Preserve actual successful observations, including failures of
later reporting; do not repeat unchanged successful measurements. The inherited
guards enforce actual source/binary/runtime inputs, complete child proofs, expected
case/observation/GC/exit counts, final freeze, bounded process groups and cleanup.
Raw logs, test XML, feeds, package caches and generated children are transient.
Concise reports record actual checks, identities, counts and remaining gaps.
