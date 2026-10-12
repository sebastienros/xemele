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
