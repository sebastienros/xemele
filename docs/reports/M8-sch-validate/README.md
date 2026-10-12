# Schematron evaluation evidence

Source `2137c73a24b0b2a544819c00bde0efd14abdd631` implements the frozen public
Compile/Validate document/string/stream facade and internal evaluator seam.
The published compiler model is immutable; every run owns its variable scopes,
match table, traversal scratch, events, diagnostics and detached findings.
M4 evaluations use the verified SchemaXPath spent-work adapter in finally with
minimum remaining/per-expression limits. Strict instance parsing consumes the
same aggregate validation ledger, prohibits DTD/resolver use, preserves stream
position/open state and distinguishes owned syntax diagnostics from caller
exceptions. No SVRL serializer, successor feature, shared source or project is edited.

Coordinator review found one blocking location defect: empty physical Text/CDATA
runs incorrectly consumed a logical XPath text position. The independently added
programmatic-DOM regression failed against the original runtime with text()[2].
Focused source/test fix `43c6ab4cc789cc1e400dfdb2cea74e3851f681d7` counts only
nonempty maximal runs while retaining per-node charges/cancellation polling.
The regression proves both a text() context and an element's text()[1] subject
round-trip through M4 to the single expected coalesced AB identity.

At that fixed revision, `python3 docs/reports/M8-sch-validate/checks.py` passed:

- 35 owned cases: phases/#ALL/#DEFAULT, lazy missing phase variables, typed lets
  and outer-self-shadow initializers, singleton focus, first authored rule wins,
  schema-pattern/document order and distinct keyed events, diagnostic/property
  scope and presentation text, document/attribute/namespace/text subjects,
  detached source locations (including mixed-quote namespace literals), includes/
  abstract parameters/extends, strict input overloads, caller exceptions, scalar
  quotas, exact aggregate/diagnostic caps, concurrency, mutation and cancellation.
- 419 prerequisite cases: M4 evaluation163, compiler93, accounting15, M5 model18,
  M8 model20, source locations10, resource-base11, M5 DTD validation89.
  Zero failures/errors/skips/not-run across all454 cases.
- Frozen evaluator/facade9 and compiler154 metadata matched. All existing1393
  public members are preserved and the exact7 public facade declarations bring
  the additive baseline to1400. Both normal and genuine source-linked XML-only
  consumers exercised all six facade overloads, phase isolation, focus/subjects,
  detached results and existing M4/M5. XML-only has no HTML/CSS runtime types.
- Release SDK10.0.400/runtime10.0.11 builds: zero warnings/errors; planning177
  and whitespace validated. The initial default-source restore was cancelled;
  all successful restores used the existing local NuGet cache.

Early test drafts used invalid presentation/diagnostic grammar, a duplicate
schema-wide ID, and an incorrect assumption that recovered EOF implies incomplete
provenance. Those fixtures were corrected; their failed runs are not passing
criteria evidence. Incomplete-provenance coverage uses unrecoverable CDATA.
The check script's initial nonmatching M5 namespace was corrected to the actual
DtdValidation namespace; the89 cases were separately proved before the final454
combined check. No result claims unselected tests.

The prebuilt allocation-only probe passed10 consumed control routes with all
allocation/retained/RSS counters disabled. Frozen manifest preparation verifies
215 identities and process-inspection permission. Supervisor maximum30s is
20work+10cleanup, with absolute UTC start/expiry, no-overwrite guards, owned
process-group cleanup, before/after hashes and no raw inspection during lease.
`preparation.json` contains the proposed command and payload identities.
No measurement has run; no borrowed-zero, speedup, BDN, throughput, cross-
architecture or full conformance-corpus claim is made. Runs deliberately allocate
owned XPath snapshots, scope/match/traversal state, locations/messages/events/
findings/diagnostics/results; retained results are immutable snapshots.
Fresh host-wide coordinator PARK/GRANT and RELEASE are required before any
measurement or subsequent raw inspection. Earlier wildcard/parser proposals
are unrelated and cannot authorize this task's probe.

Task state remains in_progress pending accepted allocation evidence and coordinator
integration. This is a branch deliverable, not integrated verification.

Coordinator relayed closure review acceptance of43c6ab4 with no remaining finding
while this worker stayed parked during M8-wildcard-parser-serial-2. Read-only
SHA256 checks confirmed the exact accepted evaluator
01532f6938c72704c0476f858b7420397d98545723dbb3c9b3dd886f9696ef2d and
location tests c40aa56f8cee484955d1d96f82ef108282a8a74f8c5e0da76636ac3f7cc94315.
Both context/subject M4 round-trips and per-node charges were accepted. This is
review evidence, not a new test run. Runtime/probe identities remain unchanged;
no build/test/discovery/measurement was performed while parked.
