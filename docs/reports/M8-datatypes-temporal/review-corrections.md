# Bounded source correction batch

Original correctness evidence at `0e83c980bec319ad8ad3cfe9927967bd044db655`
remains recorded. Independent root review found two semantic blockers and one
accounting blocker. The source corrections are:

- Leap seconds: accept second60 and arbitrary fractions, retaining a phase before
  the following minute for structurally plausible direct UTC/absent-zone values.
  Inappropriate dates normalize overflow. Nonzero timezone and synthetic ±14
  normalization invoke Appendix E and overflow leap60. Zero timezone aliases
  identify UTC. The key phase precedes fraction comparison and participates in
  hash folding. Tests include all neighboring instants, fractional identity,
  offset overflow, absent-zone uncertainty and recurrent time.
- Duration: Appendix E's signed year/month arithmetic includes internal year0.
  It uses a separate floor-Gregorian ordinal from nonzero lexical BCE calendars.
  Four independently computed endpoints prove `-P1698Y` versus `-P620183D` is
  indeterminate in both directions; year/month grouping and exact caps are tested.
- Arithmetic: replace opaque arbitrary-size BigInteger operations with owned
  base10^9 limb loops, polling before each traversal step and reserving every
  growth allocation. Output and fraction copies poll through string.Create
  callbacks. A 128-limb fixture independently asserts work/component counts and
  deterministic cancellation within multiplication and output division.

Primary anchors: [ISO formats D.1](https://www.w3.org/TR/2004/REC-xmlschema-2-20041028/#isoformats),
[dateTime order](https://www.w3.org/TR/2004/REC-xmlschema-2-20041028/#dateTime-order),
and [duration addition E](https://www.w3.org/TR/2004/REC-xmlschema-2-20041028/#adding-durations-to-dateTimes).
No historical leap schedule is fetched or imposed.

Final source test revision: `8ed24fb`.
`python3 docs/reports/M8-datatypes-temporal/checks.py` passed 53 exact actual
.NET10.0.11 Arm64 cases:23 temporal+18 core+12 model; zero errors/failures/skips/
not-run. All cache-only builds had zero warnings/errors, public API1393 matched,
normal and XML-only genuine consumers executed,176-task validation and whitespace
passed. This supersedes semantic completion claims for intermediate52-case
revisions `1f70573` and `8b5be96`; both actual intermediate runs also passed.
Allocation/retention/timing has not run. Root accepted source8ed24fb and clean evidence0dbac1c after the independent final
read-only recheck. No residual blockers remained in leap normalization, signed-year
duration endpoints or bounded arithmetic. The reviewer ran no tests.
Accepted SHA256 identities:
TemporalProvider `0e1fc092106bebfb695fcc104be7c922f713558486fd1bd00bc48bd2c3026f11`;
IntegerArithmetic `5129f2a2e12e9d686f7c893e19aa1b77493eccb79f7ed51ed1b779b77d8d400e`;
TemporalTests `e2fa246cc5f9d9b7369888c460fbe1e609676b854083638d87829a41f2ad4e48`.
