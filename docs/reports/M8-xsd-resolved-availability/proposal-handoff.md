Source-only proposal after root accepted the actual baseline at main c3c6a3e28.
Proposed builds, restores, executable imports, controls, tests, API emission,
consumers, preparations and allocation counters remain UNRUN. Root owns actual
phase launches and the main/task-state writes. Task status/criteria are unchanged.

The only runtime change is AvailabilityBuilder in Availability.cs. One private
invocation-local Boolean becomes true after a successfully registered unavailable
reference. Before any such registration, Unavailable performs Charge(0) and
returns false: every closure edge refers to a registered entry, and links create
no entry IDs. This is not a cached query result. Any later unavailable registration
permanently disables the shortcut for that builder. Partial-graph traversal is
unchanged. Reference/Link registration charges, Closure, Join, Seal and immutable
snapshot publication are byte-for-byte unchanged apart from the new flag update
inside Reference. No public/internal visible API changes are proposed.

Root baseline:15 proving controls,24 check commands/702 methods/2218 cases,
exact93/frozen615+257/public1430/55 fragments and both consumers passed. Fresh
preparation3 commands and disabled6 routes/254 consumed passed. The unchanged
annotZ002 reproduced SCHEMA.Limit at100M work/1M components/64MiB document
characters. Root alone measured baseline warm resolved query allocations:
size8=1224B,32=5112B,64=10256B per query;32-call work counts21184/850496/6190144
and retained-component charges320/1088/2112. Warm public compile means were
418307.5/685675.5/1019743B; cold fixture49472B, positive4120B and coarse retained
owner heap delta6496600B. All registered families were reaped, logs closed and
absent; raw341/442/5/52 removed and consumed preparation outputs deleted.
These are accepted baseline measurements, not proposed results. Concise root
report/hash/receipt bindings are retained in baseline-evidence.json.

Phase is now frozen as proposed. The default fixed selection is704 methods/2222
cases and includes all sixteen owned cases, adding only the baseline-excluded
three zero-traversal-budget cases and annotZ002 VALID expectation. Original
tests/fixtures, both phase-specific selections, allocation probe, finite lifecycle
implementation and full verified93 canonical projection remain unchanged.
All original policies/bytes/outcomes and zero bad-case original/gate credit are
preserved. No quota increases or fabricated definitions are introduced.

Fresh review must bind the new source/manifests before root executable phases.
Root controls/check/prepare/measure retain the same finite ownership, binary/input
binding and cleanup protocol. Compare proposed rows to accepted baseline rows;
query expectations are zero allocation/work/component deltas with cancellation
and prior refusal polling. Public first/warm compile allocations remain owned
results and must be reported as observed. Cold/retained values are coarse owner
observations; no timing, host isolation, static initialization or distinct-object
census claim. No raw artifacts, archives, leases or worker counter launch.

Required next steps: three-reviewer source acceptance; fresh root controls,
proposed correctness/API/consumers, immediate preparation, actual allocation
comparison and integrated verification. No branch-complete/verified claim yet.
