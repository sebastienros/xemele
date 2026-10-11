# Compiler branch checks

Implementation/source candidate includes the verified construction repair at
73636914659607df5f7bc20ecfa8767b54a97a96. This is branch evidence only, not integrated
compiler verification. The coordinator owns integration and any M4 gate checks.

| Check | Actual outcome |
|---|---|
| Pinned Release test-project build (`-m:1 -nr:false -p:UseSharedCompilation=false`) | 0 warnings/errors |
| Focused compiler namespace | 78 passing cases, no skips; includes 105 frozen compile outcomes, 333 framework scalar expressions, 415 decimal rounding inputs, 19 exact/one-over quota production cases, depth/width 10,000 and concurrent compiler use |
| Affected combined compiler/model/construction namespaces | 131 passed = 78 compiler + 41 prior model + 12 verified construction repair; no errors/skips |
| `python3 docs/reports/M4-model/check_api.py --dotnet <pinned host>` | 893 actual public members = 720 prior + 173 model; all preserved; 2 corruption controls reject |
| Compiled standalone internal seam sample | Deterministic expanded path and XPATH-ARITY output listed in design.md |
| `python3 docs/reports/M4-compile/check_consumer.py --dotnet <pinned host>` | Fresh net10.0 local-package consumer built and executed; internal packed compiler reflection + public owned-string model succeeded |
| Owned Release probe build after one fixture correction | 0 warnings/errors |
| Prebuilt probe `--control` with explicit `--fx-version 10.0.11` | All semantic controls passed, counters disabled; actual `.NET 10.0.11`, Arm64; checksum 2372765696 |
| Prebuilt probe `--discover` with explicit host runtime | 25 cases, one ShortRun NoEmit job, 3 warmups/3 iterations/1 launch; no generated build |
| Planning/ownership/whitespace | Final validation is recorded in task evidence after source candidate review and measurement |

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
correction now passes build/control/discovery. None are passing evidence for the
failed attempts, and no measurement failure has occurred because no measurements
have been authorized or run.
