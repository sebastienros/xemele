# Allocation infrastructure preparation

Prepared source only; no probe build/control/freeze or measurement has run.
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
