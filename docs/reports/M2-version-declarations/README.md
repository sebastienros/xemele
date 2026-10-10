# Declaration version verification

`verify.py` records exact-commit runtime/conformance/differential correctness,
approved API272, source/NuGet consumers, ABI negatives, preservation identities,
planning and cleanup checks. Its passing 576a4ef observation remains valid; only
measurement infrastructure changed afterward. Historical failures are summaries.

`prepare.py` does not run allocation counters or timed benchmark executors. It
freezes committed product, main benchmark, scoped C# and Python driver inputs,
builds the primary probes, independently generates/builds the 14 declared cases,
and checks counters-disabled ordinary/long and actual generated-method oracles.
The complete 47-file child closure and generated project/`.notcs`/script are the
reference for the generated current-source variant. They are not replaced by
hashes of the distinct prebuilt primary assembly. Actual full versions and hashes
are retained in `preparation-summary.json`; prepared binaries are transient.

Only after a fresh coordinator exclusive grant, run the committed driver with
SDK10.0.400 selected in PATH/DOTNET_ROOT, without GC/JIT overrides:

```sh
python3 -B docs/reports/M2-version-declarations/measure.py --exclusive-grant '<fresh grant identifier>' --mode allocations
python3 -B docs/reports/M2-version-declarations/measure.py --exclusive-grant '<fresh grant identifier>' --mode benchmarks
```

Allocation mode runs distinct real/escaping-positive children for ordinary and
long borrowed/owned boundaries, plus distinct real/positive probes of all 14
actual generated benchmark methods. Direct delegates exclude reflection setup
from the intervals. Fixed 128 warm calls precede 512 measured calls per lifecycle
fixture; the ordinary/long boundary probe has its authored 24 warm operations.
The result includes exact allocation totals, consumed counts, six callbacks,
retention, fresh PIDs and actual source/binary/runtime identities.

Timing mode rebuilds the generated current-source variant, checks exact raw
47-file closure and generated inputs before invoking any executor, and uses each
class's `[MemoryDiagnoser, ShortRunJob]` without job overrides. It requires all
14 exact identities, 42 Actual, 42 Result, 14 GC records, both restore/build exits,
and every actual child exit. Each timed GC total must equal operation count times
the directly measured generated-method allocation oracle; no rounding or relaxed
counter tolerance is accepted. Full versions, source and primary/generated binary closures are checked before
and after. Each actual consumer reports its loaded CoreLib path/full identity/
MVID and executable identity; every file in that CoreLib-selected shared-runtime
directory (native, BCL and runtime metadata) is hashed and compared before/after.

Modes are one-shot: a durable attempt summary prevents retries. Allocation mode
has a 300-second total budget and 90-second child deadlines; timing has an
1800-second total, 240-second build and 180-second child deadline. Timeout/failure
terminates and reaps the process group, preserves the primary failure and attempts
remaining cleanup. Even a zero-exit parent must leave an empty process group;
surviving descendants are terminated and make the run fail. Actual/expected
closure/input/metadata mismatch records and partial per-case observations are
published before assertions and retained in summaries before raw cleanup. Raw logs are removed after inspection; frozen binaries survive
only between successful allocation/timing phases, then are removed. Cleanup and
summary-write failures remain distinct from the original failure. Timings are
informational for the observed arm64 host.

`python3 -B docs/reports/M2-version-declarations/test_measure.py` runs synthetic
protocol controls without counters or benchmark execution, including malformed
payloads/identities/counts, generated-source/closure/metadata changes, exit/runtime
failures, exact allocation totals, deadlines, primary-error preservation and
cleanup/summary-write denial. Test scratch is automatically removed.

File capture continues after an unreadable file to retain readable sibling hashes.
It publishes exact file-read errors and partial actual closure/input maps before
rethrowing the original first read failure. The synthetic unreadable-file control
checks both capture paths and durable evidence after cleanup.

Warmup executes all 24 long/ordinary pairs with counter reads and functional
checks unchanged. Only allocation acceptance is disabled during warmup; the
subsequent real/positive observation enforces the same exact totals. Functional
and allocation failures publish structured warm index, phase, boundary, counters,
consumption, callback and outcome evidence outside intervals, including when
normal output is disabled. Unclosed counter intervals are explicitly unavailable.

Corrected-source attempts use revision/preparation-specific summary filenames.
Every existing attempt, including the original b5 failure, stays immutable.
Reusing either a measured source revision or preparation identity is rejected.
Timing requires exactly one matching allocation attempt and its strict pass.
The original failed attempt did not emit counters; its cause remains unknown.

The direct executor's single structured result per case is authoritative:
expected identity, positive distinct PID, integer zero exit, success, executable
found and empty errors are required. Textual exits are optional corroboration;
duplicates or conflicts are rejected. Every GC comparison and its source/oracle
binding is published before strict acceptance. A post-freeze is attempted after
validation failure; any secondary freeze failure preserves the original cause.
See `offline-timing-assessment.json` and `gc-domain-assessment.md`. Offline case
acceptance does not convert the historical failed timing attempt into a pass.

`verify.py --integration-baseline docs/reports/M2-version-declarations/integrated-profile-discovery.json`
checks the integrated 258/311 suites using root's actual verified-main discovery
at `705b1171d214cfb5ca86559a7622884fbe990e97`. Set `--audit-base` to that same
revision after it is an ancestor of the integration checkout. Complete suite
C#/project sources are bound to those Git bytes. Exactly the discovered 3/12
[type, method, case display name] additions may be removed; the remaining actual
execution XML names must reproduce the original 255/299 hashes. Only quoting of
string delimiters is normalized for matching those exact added rows. Both actual
execution and compiled discovery additions are retained as evidence. No namespace
exemption or substituted discovery digest is used. The integrated suite has not
been executed here.
