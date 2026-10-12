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
