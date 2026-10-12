# Allocation infrastructure preparation

Initial infrastructure was source-only. Final readiness below records actual build/control/freeze; measurement remains unrun.
Coordinator accepted the bounded facade correction review at
`cfba0a33e24a970075b4035f3b57defcdc7972af`; this is source review, not reviewer
execution or acceptance of the still-failing shared joins. Final binary preparation
requires root's exact integrated/verified repair checkpoint, all public witnesses
passing, and recorded `joined-correctness` evidence. `prepare.py` refuses to proceed
without that evidence or when runtime/public-source inputs differ from its revision.

Twenty public owned-output routes cover DOM/string/stream validation, invalid and
malformed instances, typed defaults/fixed values, identity duplicates, IDs/IDREFs,
64-row instances, wildcard scopes, nil and work refusal, DOM/string/stream schema
compilation, an included owned resolver resource, resource denial, invalid canonical
schema constraints, and NOTATION-list compilation/validation. Each route has an
independent validity/completeness/refusal expectation, consumes owned results or
compile exceptions, and uses sixteen warmups then one aggregate allocation reading
around exactly 64 operations. Streams and byte fixtures are prepared independently:
stream-route numerators include the per-operation caller stream object plus the
public wrapper's input buffer, parser state and owned outputs; fixture byte encoding
is outside the warm readings. Resolver include routes include owned returned stream/
resource setup and exactly-once disposal through the public compiler.

Two narrowly scoped borrowed primitive routes use preallocated inputs/operation
state: `a b c` delimiter scans and long QName-prefix hit/miss delimiter scans.
Each uses sixteen warmups and one aggregate reading around exactly 1,024 consumed
operations. Only these scans carry a proposed zero-allocation expectation.
Full NOTATION lookup includes iteration through context/notation dictionary views;
its allocation cost is included in the owned end-to-end NOTATION routes and is not
called allocation-free. Cancellation/work polling and inspected-unit correctness
are covered separately by the accepted public correction tests. Straightforward
scalar scans are retained for bounded polling; no speedup or custom matcher claim.
Optional `[MemoryDiagnoser]`/`[ShortRunJob]` benchmark declarations are included, but
neither BDN nor timing runs are part of this allocation-only command sequence.

Cold setup counts the fixture's initial public schema compilations, datatype/content/
identity preparations, parsed DOMs, namespace fixtures and byte/long-string inputs;
it can include runtime initialization/JIT scaffolding and is not a cold-start timing
or per-schema claim. A positive control retains a 1,024-byte array. Retained heap
compares forced-GC readings around eight compiled schemas and sixteen invalid
validation results; arrays, owned diagnostics and schemas stay alive through the
second reading. This is an illustrative live-state signal, not exact retained bytes
per object, an isolated leak test or complete cache accounting.

`--control` consumes identical routes with every allocation counter and heap reading
disabled. After verified joins/build release, preparation will run that actual
control, hash the executable/runtime/source/probe/check closure, and freeze exact
absolute argv in `preparation.json`. The reused verified lifecycle supervisor has
one 120-second global bound:110work+10cleanup, shortened by the host's absolute
expiry; fresh manifest SHA/latest-start/expiry are mandatory. It runs prebuilt
`--control` then `--measure` children, checks frozen identities before/after, kills/
reaps owned process groups, closes logs and writes durable release before any raw
results are inspected. No retry, late-start extension or raw output replacement.
The counters-disabled supervisor preflight uses the same cleanup path.

Warm numerators/denominators and cold/retained observations will be reported exactly
after an explicit fresh host grant, with architecture/runtime/environment and
ownership limits. No rounding to infer zero; no throughput, x64, full corpus or
cross-architecture release claim. Raw outputs remain transient ignored artifacts;
only concise summaries/analysis are committed.

## Final prepared readiness

Accepted runtime revision: `a75d851db90277e41a631e1e79eb1b4326c7b6af`.
Prepared source HEAD: `1836e724f9bd4c1c5bf3cc1fb5538ed2f6040304`.
Both coordinator-reported independent final seam reviews passed; reviewers did
not execute checks. Actual joined driver passed1110affected+42resolver, including
all93public witnesses. Runtime/tests/API/sample/check inputs remain frozen.

Pinned offline Release probe restore/build passed with zero warnings/errors.
The actual disabled control consumed all20owned and2borrowed routes with no
counter/heap readings enabled. Control fields are zero placeholders, not measured
allocation evidence. The elevated supervisor preflight passed: child58226 exit0,
reaped/groupabsent/logclosed,453identities matched before/after. Initial sandbox
verify-only could not invoke ps and launched no probe; elevated verify-only and
actual control preflight passed. Compact receipt: allocation-preflight.json.
Zero owned active jobs remain.

ManifestSHA: `a780f386c8adb85fdf2723c69fcbd617561b6a80677aa56f8c3e6e7793a1af85`.
SupervisorSHA: `c3d70cf60809809cd58faaaf5e17c8583ad8f6708ae8b5a33cf003e11e694774`.
The exact frozen commands, identities and120/110/10bounds are in preparation.json.
Fresh durable grant must bind this manifest, latest-start and absolute-expiry.
No counters, heap measurement, BDN or benchmark timing have run. Metadata/report
readiness updates do not change the frozen source/binary/control/supervisor inputs.
