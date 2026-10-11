# Compiler branch checks

Implementation/source candidate includes the verified construction repair at
73636914659607df5f7bc20ecfa8767b54a97a96. This is branch evidence only, not integrated
compiler verification. The coordinator owns integration and any M4 gate checks.

| Check | Actual outcome |
|---|---|
| Pinned Release test-project build (`-m:1 -nr:false -p:UseSharedCompilation=false`) | 0 warnings/errors |
| Focused compiler namespace before resolution correction | 78 passing cases, no skips; includes 105 frozen compile outcomes, 333 framework scalar expressions, 415 decimal rounding inputs, 19 exact/one-over quota production cases, depth/width 10,000 and concurrent compiler use |
| Affected combined compiler/model/construction namespaces before resolution correction | 131 passed = 78 compiler + 41 prior model + 12 verified construction repair; no errors/skips |
| `python3 docs/reports/M4-model/check_api.py --dotnet <pinned host>` | 893 actual public members = 720 prior + 173 model; all preserved; 2 corruption controls reject |
| Compiled standalone internal seam sample | Deterministic expanded path and XPATH-ARITY output listed in design.md |
| `python3 docs/reports/M4-compile/check_consumer.py --dotnet <pinned host>` | Fresh net10.0 local-package consumer built and executed; internal packed compiler reflection + public owned-string model succeeded |
| Owned Release probe build after one fixture correction | 0 warnings/errors |
| Prebuilt probe `--control` with explicit `--fx-version 10.0.11` | All semantic controls passed, counters disabled; actual `.NET 10.0.11`, Arm64; checksum 2372765696 |
| Prebuilt probe `--discover` with explicit host runtime | 25 cases, one ShortRun NoEmit job, 3 warmups/3 iterations/1 launch; no generated build |
| Planning/ownership/whitespace | 149 tasks validate; all introduced paths vs verified7363691 owned; full introduced diff whitespace passes |

The 131-case run completed during construction-overload adoption at merge HEAD
7f24f56f36f9b3c3bf96d4ed1a4d5d396a2cfea2 with owned source/test edits then uncommitted.
Those exact runtime/test source bytes were retained through the parked interval;
the subsequent change only corrected the owned benchmark fixture's erroneous
int return expression. Following coordinator instructions, unchanged correctness
checks were not rerun merely to change the Git label. Final evidence will distinguish
the source-equivalent candidate from the actual execution checkout revision.

Earlier successful API/sample/package consumer evidence preceded the verified
model repair. That repair's separate 893-member API check verified zero public
changes. The compiler itself adds no public declarations. API output and sample
claims therefore remain applicable; later final source review will decide whether
any affected delta needs a focused recheck.

Honest preparation failures: initial --no-restore build lacked assets and was
resolved by restore; first test build omitted `using Xunit` and then required
explicit test cancellation tokens; unsupported runner `--filter-namespace`
was replaced by the runner's actual `-namespace` syntax. A count() diagnostic
expectation incorrectly included trailing whitespace; a filter/path quota
expectation initially omitted the outer path variant. Both expectations were
corrected against the frozen span/count contract. The expanded benchmark probe
had one CS0266 caused by an accidental broad expression replacement; its one-line
correction now passes build/control/discovery. The first resolution long-scan cancellation test could complete one scan before
the scheduled cancellation; it now performs a bounded sequence of resolution-only
scans so completion speed does not determine whether cancellation is observed.
None are passing evidence for the failed attempts, and no measurement failure has occurred because no measurements
have been authorized or run.

## Resolution correction cycle 1

The source reviewer identified a whole-span colon IndexOf after polling and
generated expanded-name dictionary hashing/equality in function lookup. Both
are replaced by the owned XPathResolution helpers: colon scanning and ordinal
URI/local/prefix comparisons happen in at most 256-unit chunks, with supplied
operation-token checks during the actual work. No dynamic QName resolution or
public API was added. Functions resolve by immutable registration enumeration.

The affected Release compiler namespace passes 82 cases on explicit .NET
10.0.11 (78 prior + 4 resolution cases). This run completed at ea8431e0a0ac605958b0ba5c98600a33208f1a24
with the corrected resolver/compiler/test source then uncommitted. The later
probe-only changes add colon/comparison primitives to the warm gate and use a
representative core call in full compilation. Runtime/test source bytes remain
identical. The prior 53 model/construction cases remain applicable because this
correction does not edit their implementations.

New controls use independent string oracles across 255/256/257-unit colon
boundaries, long unprefixed names, long namespace prefixes and URI/local names,
absence/mismatch registrations, and an enumeration-only dictionary that throws
if any hashing/indexer lookup is used. Registration enumeration cancellation is
deterministic. Direct resolution-only long scan/comparison/registration loops
check cancellation with the supplied token; no lexer runs in those controls.

## Approved source and granted evidence

Independent source-only review approved the grammar, quotas, explicit stacks,
number conversion and construction-overload adoption, then requested the single
resolution correction above. Focused re-review approved corrected source
1943b1b87b7e8245139e20e6bd9f5b65ccd7ff98 with no further requests.

The final prepared build/control/discovery at b30a81daeef5c3df7706200729ef1a36383c7353
used explicit --fx-version10.0.11 with DOTNET_ROOT pinned and
DOTNET_ROLL_FORWARD=Disable. Cleanup73eb77a only removed accidentally staged
Python bytecode; executable source/config/generated/binary bytes were unchanged.
The clean prepared73eb77a lease passed exact allocation gate, all25 timing cases,
initial/final complete freeze and no-child checks. Release was recorded before
raw inspection; root independently confirmed identities and results. Details,
exact domains, timing/priority warnings and rounding differences are in
performance.md. No unaffected correctness suite was rerun after this approval.
