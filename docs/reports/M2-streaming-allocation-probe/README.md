# Streaming allocation boundary

Only the streaming allocation Fact/helper/imports and owned diagnostic/report
files change. Production source, default JIT/GC/test parallelism, exact 0 B,
100 warm iterations and 10,000 measured iterations remain unchanged. Each
iteration checks NeedInput, full UTF-16 consumption and null Diagnostic, using
the same core, sink, chunk and cancellation token. Measurement must consume
50,000 text units and 120,000 callbacks, then complete the document.

## Preserved observation and bounded comparison

`history/` contains byte-for-byte raw copies from the PI parent's
`source-9286cd6-attempt1` report (source
`9286cd6cac00df0ccd5f4627d4c29e5046c16af3`, evidence
`3e9bd43179ee3599b7c4881455857dfde95e4ae8`). That cumulative MTP run failed
Streaming with exactly 224 B; its separate direct runs passed 247/255/299 tests
(801 total). These are historical observations, not verification of this branch.
No unmerged PI implementation is imported.

`experiment-f71cee2-attempt1` retains one focused MTP, one cumulative MTP and one
focused direct XML run of instrumented test source
`f71cee2` (full hash obtainable from Git). Counters nested inside the checked
NoInlining helper and outside its return were compared before the original
Assert expression, with output after the assertion. All 801 cumulative tests
passed; focused direct XML records primitive=0, surrounding=0, 50,500 total text
units and 121,202 callbacks before finalization. Instrumentation changes the
caller scope; this does **not** establish the historical 224 B cause.
The diagnostic-build/original/equivalent logs in this first directory are a
preliminary, uncommitted diagnostic prototype that selected outer versus inner
snapshots around the same loop. They are retained as exploratory output only,
not known-source criterion evidence.

`experiment-95006d1-attempt1` compares two fresh-process modes at committed source
`95006d1`: original NoInlining checked helper with caller counters, versus
NoInlining checked helper that returns internally captured counters. Both retain
100 warm/10,000 measured iterations and the identical checked Append workload.
Both measured 0 B. Independently reported scopes: counter-only 0 B; cold options/
core/sink 6,816 B; root first use 312 B; first chunk 432 B; second core/root setup
7,128 B; warm100 432 B; completion 0 B. Measured work is 50,000 UTF-16 text units
and 120,000 callbacks; final totals 50,500 units and 121,205 callbacks with exactly
one EndDocument. No timing or retained-memory claim is made.

## Chosen boundary and limitations

The final test asserts the exact result returned by the shared synchronous
checked helper. Both counters now execute inside that same NoInlining helper;
caller dispatch, test context access, assertions, validation output, cold setup
and completion are outside the primitive scope. Both warm and measured phases
use the same helper and Append call site. It additionally verifies callback/
unit totals, consumption, Diagnostic and completion. The independent executable
links this actual helper source and retains a separate original-counter control.
There is no filter, extra warm-up, threshold change, production repair, retry
until success, or runtime configuration adjustment.

The controlled experiments do not demonstrate framework/Assert/JIT causality
for the historical 224 B. They establish a meaningful allocation-free checked
primitive and make its scope explicit. Test context remains outside the
synchronous counter scope; no broader framework or async allocation claim is
made. If genuine steady primitive allocation appears, request the runtime owner;
do not relax this test. Original-versus-equivalent equality is useful evidence
but is not a claim that the old failure must reproduce in every environment.

## Independent source/integration checks

Run `verify.py` at a committed source revision with unique `--report-prefix`,
explicit `--expected-branch`, `--audit-base`, and all expected count arguments.
It builds Release with build servers disabled; preserves fresh BCL output before
solution build; runs both diagnostic processes, focused and cumulative MTP,
separate full direct runtime/conformance/differential suites, compiled API,
discovery, reference provenance, planning regression/validation and ownership
checks. It retains every outcome, including an unrelated PI failure if present,
then returns failure when any combined check fails. Counted direct XML reports
include the actual streaming allocation/unit/callback output. Final summary
records full source revision, SDK/runtime information and source hashes.

A passing child run does not discharge the original PI parent's mandatory
integrated combination criterion. That parent still requires full allPass
verification after composition before encoding/resolver/framing consumers can
be verified. Child status is `implemented` until independent integration checks.
