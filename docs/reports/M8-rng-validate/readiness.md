# Review and allocation preparation

Implementation/check revision: `ec0699e10605c1d89eecfdbe192a7150b81fe1be`.
Checkout `/Users/sebastienros/.codex/worktrees/3538/xemele`, branch
`codex/m8-rng-validate`, integrated claim `465b9e5544f83e320593218404d29a26dfad8985`,
base `cdf7a0b148e837e016f874da32b054ab4f25a93e`.

`python3 -B docs/reports/M8-rng-validate/checks.py` passes89owned cases
(38matching/concurrency,29datatype,13boundary,5budget,4resource),1191predecessor
cases, zero errors/failures/skips/not-run. Release builds use SDK10.0.400/runtime
10.0.11 on ARM. Complete compiled public comparison matches1410members
(1403predecessor + seven approved additions); all444frozen RNG public/internal
fixture members match. Normal and true XML-only consumers pass; planning
validates179tasks; diff check passes. Default256-element recursion accepts;
unlimited-quota400-depth stops safely as incomplete VALIDATION.Limit.

Bounded coordinator review found one instance-context bug: known source URI was
omitted from namespace-context construction, causing relative xml:base to behave
differently with/without an XML declaration. The accepted delta passes
`document.ParsedSourceUri` through the existing helper. Four actual regressions
now pass (complete valid, no diagnostics, unchanged revision) with/without
XML declaration and inherited/nested relative bases. Accounting/mutation/
cancellation remain covered. No other concrete blocker was found in review.

Earlier working-tree fixture failures and the superseded preparation remain
transient. The generic API discovery command fails on preexisting header-only
internal `M8-datatypes.txt`; the coordinator approves the existing complete
compiled checker, which passes. No shared source or API file was edited.

Allocation-only preparation:10exact routes,32warmups/64samples/16operations per
sample, one cold compile and positive control, retained16schemas/RSS separately.
Counter-disabled controls consume known valid/invalid results; no timing or
allocation counters ran during preparation. No optimization, speed or full-engine
zero-allocation claim. The byte-stream route explicitly includes caller-owned
MemoryStream/byte-array setup, parser buffer/DOM, per-run scratch and result costs.
Raw totals/samples and full identity manifest remain transient for root inspection.

NEW manifest SHA256 `5b7d24badda22d79471f6f12ee62684e4e9fb1a159944d36e9186f5c31c1ce9a`;
supervisor SHA256 `bdb51e0d4183cf06eb71f8df1607869021571f52de992146ee492fe3aed724fd`.
434new source/binary/runtime identities verified. Actual counter-disabled cleanup
preflight terminated/reaped a sleeping child, verified process group absent and
closed log. Old source5d69preparation is preserved at
`artifacts/M8-rng-validate/preparation-source5d69/` and INVALID for this source.
The exact prebuilt child completed under durable grant973968a6 in the coordinated
M8-rng-identity-serial-1 window. Durable release ae9cf41ea49a87752a8256acedc987ee9e66ae1a
preceded raw inspection. Root independently accepted exact denominators/totals,
sample histograms and lifecycle; [measurement summary](measurements.md) retains
compact evidence. All source/build/semantic/API/consumer/review/allocation work
is complete on this branch. Integration verification remains the coordinator's
responsibility. Raw artifacts remain for root cleanup; no raw logs, sample arrays
or result dumps are committed.
