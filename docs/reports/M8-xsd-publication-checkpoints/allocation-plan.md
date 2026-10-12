# Owned allocation preparation

Eight synchronous routes are built into a dedicated precompiled probe, separate
from BenchmarkDotNet. The accepted runtime/test/sample/check source remains
`233aa3cf738ffffe4ae7b52e37e8ddb96eb9c8a7`. Probe source and all executable inputs
are frozen by SHA256 in `preparation.json` after counter-disabled control.

Routes: legacy compilation of a previously parsed document; shared-context
principal string parse/compile; unused model group; unused attribute group with
fixed and ID uses; zero-self-reference redefine; self-reference redefine;
aggregate scalar rejection; multilingual principal stream plus included stream
at exact aggregate scalar/expansion caps. Each route independently checks its
expected checkpoint count or quota diagnostic and consumes its owned output.

The driver executes 16 warmups and 64 aggregate calls per route. Cold setup plus
one shared parse/compile is measured separately, including corpus creation and
the legacy backing document. A separate 1024-byte positive allocation control
checks counters. Sixteen unused-model checkpoint graphs remain rooted across
full collections for an approximate retained heap delta. The persistent fixture
holds its corpus and one legacy document outside steady-state calls.

Compilation owns dictionaries/lists, immutable definition/type snapshots,
namespace contexts, parser trees/buffers, resource streams and diagnostics.
Those allocations are required persistence/setup boundaries; the fixture's
independent result checks are included. The legacy and shared routes have
different parsing/setup boundaries and are not a comparative optimization
benchmark. No scanning/matching optimization, zero-allocation primitive, latency,
throughput, async-per-thread measurement or cross-architecture release claim is
made.

`prepare.py` builds with offline SDK10.0.400/runtime10.0.11 Release flags, runs
`--control` with allocation/heap counters disabled, and freezes runtime/host,
source, test/probe/sample and binary/dependency/configuration identities.
`measure.py --preflight` runs the same disabled control through the real
TERM/KILL/reap/process-group/log-close lifecycle. `--verify-only` verifies all
identities and process inspection without starting a child or touching counters.

Only the coordinator's exact durable manifest hash, latest-start deadline and
absolute expiry permit `measure.py --granted ...`. That mode launches only the
prebuilt disabled control and allocation-only child, serially, under a maximum
30-second bound with 10 seconds reserved for cleanup. No builds or tests run
under the grant. Before/after identities are checked, groups drained, children
reaped and logs closed; durable RELEASE is written before raw result inspection.
The coordinator must acknowledge host release before allocation results are
opened. Existing outputs are never replaced or retried.
