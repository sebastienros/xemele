# Independent source acceptance

Coordinator conveyed final acceptance of runtime candidate
`a58aed481ea2c6831111b73d037f92d1da21a2c1` from both existing independent reviewers:

- `/root/review_m8_contract`: normative/semantic scope; known outputs, IEEE,
  dateTime/time/date recoverable timezone, lists/selected unions, QName context
  and actual-type lexical assessment. No blocker.
- `/root/review_m8_model`: accounting/ownership scope; charged passes,
  reservations before output allocation, bounded floating scratch, charged
  temporal limbs, failure and caller exception identity. No blocker.

This is source review, not a test execution. The worker's 731 actual correctness
cases and API/consumer results are recorded separately. Coordinator retains the
original reviewer messages in chat `01a12c6c-b333-71d3-877f-0d186618e21d`.

Reviewed five canonical files, SHA256:

| File | SHA256 |
| --- | --- |
| Constraint.cs | 51c5d8ec5f85ad0124ded39a42a40fe1864ace12cae7e0f0b6b32887f8453aac |
| Numeric.cs | 6038b1b161def79f555478cdd8fef4553305460797abd9664889428c8f057fc7 |
| Temporal.cs | c6b1eefee310f02852111151040ac7d7653d04077203af8bd504cd63fa772910 |
| TextBinary.cs | 1379f07f2f1b22fcd12eade624f325ee412455d49e336cef541cdd4c49bb97b1 |
| Writer.cs | 11a90b5bc49e9d3e7ffcbe45045081119e921bce50e17555d43d24f296cebf4a |

After source acceptance, the dormant measurement harness dropped the unnecessary
borrowed-zero route; every warm route now owns a canonical constraint result.
Its TERM/KILL/reap/group/log helper functions are copied verbatim from the
integrated `docs/reports/M8-sch-rule-scope/lease.py`, with original ROOT/ARTIFACTS
bindings pointing to this task's paths. Added wrapper gates require the exact
manifest, latest start and absolute expiry, two serial control/measurement
children, durable local release, and coordinator release before raw inspection.
