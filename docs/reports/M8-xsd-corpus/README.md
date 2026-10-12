M8-xsd-corpus is in progress. No full conformance claim is made.

The original source inventory is 93 sets, 15,433 groups, 15,378 schema tests,
26,357 instance tests and 93 ordered multi-document schema checks. All 42,270
tracked upstream files are retained with hashes. Applicability currently yields
14,405 schemas and 25,079 instances, 973/1,278 exclusions, one supplemental
Unicode16 instance and five missing references (all excluded 1.1).
Twelve schema/four instance outcomes are independently marked indeterminate.

The standalone public-API runner and BCL reference adapter compile with:

```
"/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet" build tests/Xemele.Conformance.Tests/Xsd/Corpus.Runner.csproj -c Release --disable-build-servers -m:1 -p:RuntimeFrameworkVersion=10.0.11 -p:UseSharedCompilation=false -p:NuGetAudit=false --source /Users/sebastienros/.nuget/packages
python3 docs/reports/M8-xsd-corpus/run.py --engine product
python3 docs/reports/M8-xsd-corpus/run.py --engine bcl
python3 tests/corpora/xsd/import.py --check
```

Each operation has 100,000,000 work units, 1,000,000 compile components, finite
67,108,864 character/external-byte budgets, depth 1,024, 4,096 attributes,
16,777,216 token characters, 262,144 interned names, 1,024 external resources,
128 inclusion depth and 1,024 diagnostics. Cancellation deadline is 30 seconds
per product operation; independent process watchdog is 300 seconds per test set
with at most ten seconds cleanup. These are refusal safeguards, not measurements.
No budget refusal, cancellation, timeout, resource error or harness exception can
satisfy an expected-invalid oracle. Failed schema compilation leaves dependent
instances `schema-unavailable`, explicitly unexecuted.

Reference: independently implemented System.Xml from .NET runtime 10.0.11;
XmlSchemaSet compiles the same ordered wrapper and metadata resource map.
XmlReader enforces identity constraints. The harness explicitly compiles metadata
and instance-hinted inputs before validation, while both validators ignore automatic
hint fetches. Schema-test outcomes remain independent of instance augmentation.
No reference outcome rewrites upstream expectations. BCL limitations include its
Unicode database, decimal precision/range, DTD-prohibited parser and its own XSD
restriction/composition interpretations. Process watchdog bounds reference work;
BCL has no equivalent cooperative operation accounting. Full reference outcomes
and resolver journals remain under ignored artifacts/M8-xsd-corpus.

Focused boundaries verify public string/document/stream transports, caller stream
and document ownership, complete invalid instances, work refusal and cancellation.
The existing facade check.py supplies prior behavior/API/consumer preservation.
Actual tested revisions and results will be recorded after running committed source.

Harness revisions: the first source865cf046 baseline had 39,130 passed,
335 failed, 12 indeterminate observations, 2,250 exclusions and eight unresolved
Unicode instances. Corrected source25f40a58 (explicit hint staging, Unicode
adjudication, nullable authored nonresolution) produced 39,211 passes, 266 failed,
eight indeterminate observations and 2,251 exclusions, including the supplemental
Unicode16 instance. Every child exited0; no process failures. These are partial
conformance results; applicable failures prevent implementation completion.

Subsequent bounded staging correction: instance hints augment only namespaces not
already supplied by the metadata's ordered schema documents. This avoids treating
stale instance hints for an already-explicit namespace as mandatory input. In
particular, MS introspection metadata points to absent AnnotatedTSSchema.xsd while
the explicitly compiled xsts.xsd already supplies that namespace. The NIST
introspection instance requires a separate finite 1,000,000,000 work-unit budget
and 60-second cooperative deadline; the ordinary 100,000,000-unit refusal remains
recorded as an actual failed baseline observation, not a semantic invalidity.

Source35ed1974 full product run: 39,231 passed, 243 failed, 11 indeterminate
observations, 2,251 exclusions; 41,736 total rows (41,735 originals + one
supplement). All93 child processes exited0 and exact ID coverage passed.
NIST's 1,000,000,000 work profile still refused; final bounded adaptation assigns
20,000,000,000 work units/60-second cancellation only to that instance and the MS
DataTypes/Regex introspection documents. Their earlier work refusals remain failures
in the recorded baselines. All other cases retain the ordinary finite budget.

If the BCL staging parser cannot read an original instance (e.g. XML1.1 or malformed
attributes), its hint-preparation warning is retained, then each engine receives
unchanged original bytes against the explicit metadata set. The staging parser's
limitations cannot suppress execution of the product. BCL XML1.1 rejection is an
explicit reference policy refusal, separate from schema/instance semantic validity.

Bounded hint review corrections preserve each authored namespace/location pair,
encounter order, element identity and inherited xml:base. New namespaces use their
first authored hint; later hints for an already-supplied namespace are retained
without fetch. A namespace/targetNamespace mismatch is an explicit staging failure,
never an expected-invalid product pass. Descendant hints are collected. A namespace
used before its first descendant hint is reported as an ordering adaptation
limitation; a precompiled supplemental engine result cannot establish conformance
for that case. Per-instance resolver/input journals survive success and exceptions
and do not accumulate requests from earlier instances.

Forty-one runtime harness controls pass, including an otherwise-valid namespace
mismatch witness, nested xml:base descendant hint, early namespace use/late hint
limitation, and two-instance success/failure journals for both actual engines.
Six metadata controls and six supervisor controls pass. The bounded core source
`422d85c43d2b560df7af084da7ba3da494658994` passed coordinator source review.

The accepted full run at `80cd6bac0e740947cc3809fef4300068d8b4e51b`
has 93 product and 93 BCL sets, with all 186 children exiting zero, exact ID
coverage, and no process failures. Product: 39,233 passes, 241 failures, eleven
indeterminate observations and 2,251 exclusions. BCL: 37,685 passes, 1,784
failures, sixteen indeterminate observations and 2,251 exclusions. Both have
41,736 rows including the one supplemental instance. Engine disagreement is
triage evidence; it does not change the original oracle.

Verified syntax dependency `745dc3d0` was integrated through main
`13389a3f284af3975fe3928c1d96aca4a15cd1e0`, merged into this isolated branch
at `a958276b4f0ad002b78f13b5f491fe720d6dfd44`. Product-only Annotations,
Group, IdentityConstraint and Saxon XmlVersions observations at
`09b0f5e54807f99efa94d92cda891f002e8dff4f` have respectively
76/4, 342/2, 1,062/1 and 34/17 original passes/failures (nine Identity
exclusions). Exact ID coverage was independently inspected in the saved journals;
the supervisor's exit-code output was lost at a context boundary, so these saved
rows alone do not establish child exit codes. No additional full run was done.
The explicit mixed-revision overlay yields 39,248 passes, 226 failures, eleven
indeterminate observations and 2,251 exclusions; it is not a new full-run result.

Compatible XML1.1 declarations are handled only as the frozen XML1.0 Fifth
Edition parser permits. A staging XmlReader version refusal uses independently
parsed, SHA-bound original namespace facts for thirteen original Saxon schemas.
Neither engine receives rewritten bytes. That selected set now has no harness
exceptions: 44 independently adjudicated profile agreements, one component-name
failure (`xv001.xsd`) and six unassessed substrate/dependency cases. All original
expectations, outcomes and counts are retained. The report helper has twelve controls,
including the exact 51 cases / forty-file manifest and nonpass refusal/dependency
semantics.

The exact human-approved `time_maxInclusive007_119` remains an executed original
failure, separately flagged as an approved exception, never a pass. Independently
adjudicated `errF001` rejects as required by the effective Second Edition rules;
its original validity mismatch remains recorded and its dependent instance is
unexecuted. See the owned `standards-dispositions.json`,
`xmlversions-adjudication.json` and `unicode-adjudication.json` for source facts.

Five owned boundary tests pass on runtime10.0.11 at source09b. Earlier harness
build attempts failed with eight duplicate assembly attributes from nested build
outputs and six xUnit cancellation-token analyzer errors; isolated root artifacts
outputs and explicit test cancellation tokens corrected these failures. The first
manifest check incorrectly expected forty unique fixtures; the corrected original
manifest is thirty-nine unique fixtures plus one metadata file, and its hash passes.
These failures are retained as check history, not reported as passing attempts.

Remaining applicable failures and original-resource limitations prevent task
completion. The current schema diagnostic handoff is `schema-triage.md`.
Two Additional instance hints reference original schemas with malformed namespace
declarations. Their baseline staging exceptions remain in the immutable source80
observations. Focused correction `1a6d9df8c46e7cf800b280d83895385f2a244784`
passed coordinator source review and seven additional controls: a typed preparation
failure retains the authored input and each engine compiles the unchanged original
schema. Semantic schema rejection becomes `schema-unavailable`; accepted schema
probes remain an explicit preparation limitation. No instance assessment executes,
no inferred namespace is supplied and no invalid-instance pass is recorded.
Actual original addB138/addB139 focused execution confirms both engines reject
the original schemas and retain the two original failures as unexecuted instances;
both children exit zero, are reaped and close their logs. A first subset-selector
assertion failed before launching any child because it assumed a different group
ID shape; exact original instance IDs corrected that preparation check.

`instance-triage.md` groups all 136 original instance failures from the explicit
source80/source09b overlay. `schema-ambiguity-inputs.md` records twelve original
schema IDs, seven unchanged schema hashes and nine unexecuted dependent instances
for focused independent standards review. No product source was edited by this task.

Coordinator explicit profile adjudication is preserved verbatim in
`tests/corpora/xsd/explicit-profile-adjudication.json`: the original VALID
HighSurrogates/reM77/reN75 schemas violate the frozen Appendix F exclusions;
particlesZ007 directly uses NOTATION without its required enumerated restriction.
Original schema failures and four unexecuted dependent instances remain unchanged.
Separate derived profile agreement requires actual semantic schema rejection,
never a refusal. Twelve added runtime controls verify both escape polarities for
all three excluded blocks, both polarities for legal Hangul/PrivateUse neighbors,
and valid/complete-invalid instances for an enumerated-NOTATION union. These
controls pass after joining only verified attribute-fixed main48f394cc; no full
corpus rerun or anyURI adjudication was made.

Two additional coordinator source records are preserved verbatim in owned
`supplementary-category-adjudication.json` and `lexical-oracle-adjudication.json`.
The exact eighteen queried supplementary-category originals derive VALID under
pinned Unicode16. `check-instance-profiles.py` independently verifies all22
original expected/status/applicability records,43 unchanged schema/instance hashes,
metadata/UCD hashes, exact patterns and57 decoded scalar categories from original
UCD rows/ranges (missing values are Cn). No product/reference output is used by
that derivation check. The four lexical dispositions are source interpretations:
time016 VALID (explicit absent-timezone leap-second inference), legacy gMonth002/
004 INVALID, and prohibited/no-wildcard attP031 INVALID. Their original outcomes
and reference disagreements remain visible. The original time-ordering exception
is not extended; no new exception or blanket Unicode applicability is introduced.

Focused original execution at `e25e3a2da7537cbf820639b21a53e9d6bb66565f`
uses the unchanged prebuilt sourceb9d runner on verified attribute48 runtime.
Exact44 observations contain22 valid standalone schema predicates and22 original
failing instances: all eighteen supplementary cases plus time016 completeVALID,
gMonth002/004 and attP031 completeINVALID, each agreeing with its separate derived
profile. Original22 instance failures remain false. Three bounded30-second child
processes (Attribute, DataTypes, Regex) exit zero, are reaped and close logs.
No full run or additional BCL execution is represented; all earlier reference
outcomes remain preserved.
