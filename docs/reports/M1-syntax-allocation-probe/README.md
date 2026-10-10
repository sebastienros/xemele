# Syntax warm allocation measurement

Test/helper implementation: `f0c42a85783479efd2c76566a230a1c2763a0579`. Standalone namespace correction and
check runner: `e8b9f7ba15e0a76101adba877b2569bba09f8c0f`. Test and production source blobs are identical between
these revisions; the successful Release solution build is at the former,
focused/full processes and corrected diagnostics at the latter.
Base `51c60a3a5ab3ca14df616c03ebe96662579d670d`; coordinated claim
`98236f56b63a689f98756ee115f8f4458b756804`. Actual isolated checkout
`/Users/sebastienros/.codex/worktrees/093b/xemele`, branch
`codex/m1-syntax-allocation-probe`. The initial branch-selection race was resolved
and reverified before writes. Integrated DOM prerequisite `d95f12e36e76ce9a0a6722e13f7b5f775d015090`
is an ancestor. macOS 15.8 arm64; SDK 10.0.400; executing .NET 10.0.11.

## Boundary change

The original warmup discarded Append results at one call site; its measured loop
called Append at another call site and checked NeedInput and consumed length.
One non-inlined helper now performs both phases with identical argument, sink,
cancellation token, call site, progress checks and failure branch. Exactly 100
warm calls and 10,000 measured calls remain; the threshold remains exactly 0 B.
The root/setup precedes warmup, final completion follows measurement, and the
same core and sink identities remain throughout. Cancellation, Unicode, text,
namespace and borrowed callback behavior are unchanged. Every other test remains
byte-for-byte unchanged apart from the required MethodImpl import. Production,
parallelism configuration, API and shared build files have no delta.

This fixes a directly observable measurement-logic mismatch. It does **not** prove
that call-site mismatch caused the historical 6,000 B event or guarantee the
absence of future transient events. No allocation stack for that event exists
in this evidence. No retries, extra warmup, exclusions, thresholds, disabled
tiering or benchmark timing runs were used.

## Preserved history

`historical-baseline-full.txt.gz` copies the original DOM worker full run at
`987aa47af7c744088ea3fe92e50563230ea8d6cd`: unchanged syntax failed at 6,000 B,
287/288; DOM passed. `historical-dom-final-full.txt.gz` preserves the later
288/288 at `46f90ea2324f33617b091adbf2b8dcba2d83da18`;
`historical-dom-integration-full.txt.gz` preserves independent integrated
288/288 at `d95f12e36e76ce9a0a6722e13f7b5f775d015090`.
These copied logs are historical evidence, not newly executed checks. Historical
cause remains unresolved; variability alone supplies no causal attribution.

## Bounded fresh-process diagnosis

The standalone program copies the original CountingSink and helper logic and
accepts exactly one mode, original or helper. Each mode ran once in a separate
fresh process under default runtime settings. Its generic sink type is a local
diagnostic type, not the xUnit test type, so focused/full tests are the evidence
for the actual test instantiation. No timing measurements were collected.

| Scope, managed allocation counter | Original | Helper |
| --- | ---: | ---: |
| Counter-only | 0 B | 0 B |
| First core/options/sink construction | 6,808 B | 6,808 B |
| First root Append | 312 B | 312 B |
| First representative chunk Append | 432 B | 432 B |
| Separate probe core/options/sink plus root setup | 7,120 B | 7,120 B |
| Original-budget warmup, 100 calls | 432 B | 432 B |
| Measured 10,000 calls | **0 B** | **0 B** |

First-use counts begin after process entry and counter initialization, not at
process startup; JIT/native allocations are not measured by this counter. The
separate cold core precedes the separate probe core, so the latter setup is
already runtime-initialized. New core name interning/collection setup is retained
in the 432 B first chunk/warm scope rather than relabeled allocation-free.
Both modes completed, consumed every chunk, emitted 50,500 normalized UTF-16
units and made 121,205 callbacks. Diagnostic progress/accounting and actual
focused/full exact-zero assertions passed. No recurring steady allocation was
demonstrated that would justify a production-owner repair request.

## Executed checks and failures

`run_checks.py` records the actual command, revision, exit code and output in each
`.txt.gz`, refuses overwrites, and stops on failure. All commands ran sequentially.
No peer PI/corpus worker branch was built or consumed.

- Release solution build: 0 warnings/errors at `f0c42a85783479efd2c76566a230a1c2763a0579`.
- Corrected diagnostic build: 0 warnings/errors at `e8b9f7ba15e0a76101adba877b2569bba09f8c0f`; original/helper fresh measurements above.
- Fresh focused test: 1/1, no skips, actual exact 0 B assertion.
- Fresh full solution: 288/288, no skips, unchanged class parallelism/default runtime settings.
- API checker Release build and provisioned comparison: unchanged 222 compiled members.
- Offline pinned fixture provisioning; discovery: 3,388 fixtures / 222 API declarations.
- Planning validate: 110 tasks; planning regressions: 14/14.
- Committed/staged/working/untracked ownership and whitespace/diff checks: see audit.json and source.diff.

The first standalone build failed because its XmlLocation namespace import was
missing (`diagnostic-build.txt.gz` at `f0c42a85783479efd2c76566a230a1c2763a0579`). The import was corrected in the
second implementation commit before any diagnostic process ran. The first API
comparison stopped for missing pinned fixture bytes (`api.txt.gz`); offline
provisioning restored ignored bytes and `api-provisioned.txt.gz` passed. Both
failures are retained. The initial sandbox Git index write was denied, then the
authorized local commit succeeded with ordinary escalation. Build commands used
command-line `-p:NuGetAudit=false` from the start under the offline permission in
the brief; no ordinary-audit build or network-failure claim is made.

This is branch implementation evidence. The coordinator must integrate, review,
and rerun applicable checks before changing the task to verified.
