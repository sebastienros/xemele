# Review and allocation preparation

Implementation/check revision: `5d69a1361eb55f771a5da428fc67ee7272c47d6a`.
Checkout `/Users/sebastienros/.codex/worktrees/3538/xemele`, branch
`codex/m8-rng-validate`, integrated claim `465b9e5544f83e320593218404d29a26dfad8985`,
base `cdf7a0b148e837e016f874da32b054ab4f25a93e`.

`python3 -B docs/reports/M8-rng-validate/checks.py` passed:
85 owned cases (38 matching/concurrency,29datatype,9boundary,5budget,4resource),
1191 predecessor cases, zero errors/failures/skips/not-run. Release builds use
SDK10.0.400/runtime10.0.11 on ARM. Complete compiled public comparison matches
1410 members (1403 predecessor + seven approved additions); all444 frozen RNG
public/internal fixture members match. Normal and true XML-only consumers pass;
planning validates179tasks; diff check passes. The owned tests include default
256-element recursion and an incomplete, safely stopped unlimited-quota400-depth
run. No acceptance relies on unmerged peers or later milestones.

Initial working-tree test run preserved two failures from incorrect regex fixture
expectations: the verified schema regex engine matches complete strings. Corrected
fixtures use `a.*` and `.*b` to independently demonstrate conjunction, and `a+`
for the exact-budget witness. Subsequent full-revision checks above pass. Initial
sample referenced internal ContentRevision; it now demonstrates unchanged public
serialization, while owned tests directly assert the internal revision contract.
The generic API discovery command fails on preexisting header-only internal
`M8-datatypes.txt`; the coordinator explicitly identifies this known infrastructure
limitation and approves the complete compiled checker, which passes. No shared
source or API file was edited.

Allocation-only preparation:10 exact routes,32warmups/64samples/16operations per
sample, one cold compile and positive control, retained16schemas/RSS separately.
Counter-disabled controls consume known valid/invalid results; no timing or
allocation counters ran during preparation. No optimization, speed or full-engine
zero-allocation claim. The byte-stream route explicitly includes its caller-owned
MemoryStream/byte-array setup, parser buffer/DOM, per-run scratch and result costs.
Exact raw totals/samples and identity manifest remain transient for root inspection.

Manifest SHA256 `6c38cf4809edd1286d2ec42889b1aef4099fe48340d2e6c9d669cd3678efae1f`;
supervisor SHA256 `bdb51e0d4183cf06eb71f8df1607869021571f52de992146ee492fe3aed724fd`.
434 source/binary/runtime identities verified. Actual counter-disabled cleanup
preflight terminated/reaped a sleeping child, verified its process group absent
and closed its log. Proposed sole prebuilt child has45s total/35work/10cleanup;
requires a fresh all-host coordinator grant, then explicit RELEASE before raw
result inspection. No raw logs, sample arrays or result dumps are committed.
