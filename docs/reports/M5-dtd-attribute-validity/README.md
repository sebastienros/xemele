# DTD attribute validity repair

Runtime/regression implementation `ba2a0c793907825ca36dfd3376d5812b70370b9a`;
prepared measured revision `3f6dd74240514769665263baa2712f97ce0ad5a8` changes only
the allocation harness traversal to indexed model access.

Compiler default checks now disable ENTITY/ENTITIES declaration membership while
retaining lexical validity, normalization, enumeration/default syntax, notation
constraints, cancellation and quota accounting. Instance values still require
unparsed entity membership. Name-typed ID/IDREF(S)/ENTITY(IES)/NOTATION values use
NCName, including defaults; NMTOKEN(S) and ordinary enumeration retain valid colons.
The existing scalar decoder/name rules are reused without string materialization.
No public API change or evaluator modification is delivered.

[XML1.0 Fifth Edition §3.3.2](https://www.w3.org/TR/2008/REC-xml-20081126/#sec-attr-defaults)
requires only syntactic default validity at declaration time; non-syntactic entity
membership is checked when that default becomes an instance attribute.
[Namespaces1.0 Third Edition §7](https://www.w3.org/TR/2009/REC-xml-names-20091208/#Conformance)
requires NCName for Name-typed validity tokens, while permitting colons in NMTOKEN.

The frozen current-DOM contract explicitly permits absent ordinary defaults in manual
trees, without adding attributes. That behavior is preserved: a schema with an
undeclared ENTITY default may validate a manual tree lacking the attribute, but the
same schema rejects an actual parser-materialized undeclared ENTITY default and accepts
an explicit valid override. Missing attributes do not create IDREF bookkeeping entries.
This distinction concerns the explicit manual current-DOM profile; source documents
with materialized defaults receive the normative instance checks. Fixed defaults,
limits and cancellation retain the existing current-DOM semantics.

Two outdated prior expectations were corrected: ENTITY default 'no' is syntactically
valid, so its invalid-default fixture now uses '0bad'; IDREF 'p:名' is namespace-invalid,
so its expected result is false. Four unmodified pinned W3C regression inputs and their
source/checksums are recorded under tests/fixtures/M5-dtd-attribute-validity/README.md.
E9a/E06i now validate; ns045/ns046 now reject. No corpus expectation exemptions added.

## Actual checks

Pinned SDK10.0.400; offline NuGet source /Users/sebastienros/.nuget/packages,
NuGetAudit=false, -m:1, server/node reuse disabled. Release runtime/test/probe/APIchecker
builds pass with zero warnings/errors using -p:UseSharedCompilation=false
--disable-build-servers. Three initial test-only compile errors (ref-struct attribute
collection assertion and enum name) were corrected before the runtime commit.
An initial mixed class/namespace filter selected zero tests and is not counted.

At implementation revision, direct pinned dotnet execution with `--fx-version 10.0.11`
ran `tests/Xemele.Tests/bin/Release/net10.0/Xemele.Tests.dll` with:
```
-class Xemele.Tests.M5DtdAttributeValidity.AttributeValidityTests
-class Xemele.Tests.M5DtdModel.CompilationTests
-class 'Xemele.Tests.DtdValidation.*'
-noColor
```
186 passed, zero failures/skips, including28dedicated cases. Actual FrameworkDescription
assertion and runner identify .NET10.0.11. Relevant compiler/facade test sources contain
no allocation methods; no counters/timing ran outside the lease. Cases include unused
ENTITY/ENTITIES/IDREF(S) defaults, manual missing/defaulted/explicit attributes,
fixed defaults, all Name-typed colons, colon-valid tokens/enumeration, Unicode and
malformed surrogates, exact scalar/work caps, cancellation and original corpus inputs.
Earlier mutation/provenance/standalone cases remain in the focused facade checks.

Approved compiled extractor via `python3 docs/reports/M5-validation-model/check_api.py`
matched all948public members unchanged. `python3 tools/planning/tasks.py validate`
passes158records, including final evidence. Integrated milestone verification remains
coordinator-owned.

## Prepared lifecycle and allocation summary

Initial preparation ba2a0c7 was never measured: independent review found interface
foreach enumeration in the harness's Model.Attributes traversal. The corrected indexed
harness was rebuilt, controlled/discovered and frozen at3f6dd74 before measurement.
Runtime code was unchanged. Independent controls compare typed IDREF validation with
an explicit Unicode-scalar XML NCName oracle and exercise composed valid/colon-invalid
current DOM outcomes. Four-case discovery confirms one ShortRunNoEmit job, warmup3,
measurement3, launch1, without build/generation or CLI job flags.

Exactly one run under lease m5-dtd-attribute-validity-3f6dd74, durable grant
c3debb73aa59d554c9a5824f3cc18c949bf1d119 and release
35f998ff51181320b6db43e7e6db1f475f873470. Prepared manifest SHA256:
`45745f8f78b22056dab82b895283919fc3281dcf44a89dd725734b4e61e9df1f`.
Initial/final identity matched166source/generated/config/fixture/runner files,
43actual binaries and3852complete SDK10.0.400/runtime10.0.11/host files, plus stable
effective runtime environment. Plan caps total330s, allocation20s, BDN240s and both
identity phases35s. Allocation0, benchmark0, initial/final identity0, runner0;
finally own-process-group cleanup reported no_child=true. Durable release happened
before raw inspection/report writes. No retry or remeasurement.

Actual .NET10.0.11 Arm64 RyuJIT armv8.0-a; Apple M4 Pro14logical/physical cores;
macOS Sequoia15.8.1/Darwin24.6.0; BenchmarkDotNet0.15.8.
Fixed RollForward=Disable, TieredCompilation=0, TieredPGO=0, ReadyToRun=0.
Freeze includes PATH/LANG/LC_ALL/LC_CTYPE/MallocNanoZone/LD_LIBRARY_PATH and
DOTNET_/COMPlus_/CORECLR_/DYLD_ environment plus exact plan overrides.

| Domain | Actual byte numerator / denominator | Meaning |
|---|---:|---|
| Cold fixture setup | 55,848 / 1 | Owned model/schema/DOM/options/fixtures and initialization |
| Warm typed checks | all32batches=0B | 28,672checks/batch |
| Steady typed checks | all32batches=0B | 28,672checks/batch |
| Inclusive steady typed primitive total | 0 / 917,504 | ID/IDREF(S)/ENTITY(IES)/NOTATION, short/Unicode/colon positions |
| Owned public compilation | 732,408 / 32 | 22,887.75B/op inclusive; no overhead subtraction |
| Owned composed validation | 29,184 / 32 | 912B/op; includes operation/result/traversal ownership |
| Retained whole-heap observation | 172,248 → 409,104B | Difference236,856B with32schemas held |

The retained array was created before the first full-heap observation. GetTotalMemory(true)
includes the entire observed managed heap and runtime roots/probe lifetime effects;
the difference is not an isolated schema footprint, and no overhead is subtracted.
The warm+steady consumed success count is393,216; primitive denominator counts all
calls including rejected values. Setup/control work precedes warm measurement.

All benchmark classes have `[MemoryDiagnoser, ShortRunJob]`. These informational
Arm64 latency values cover the typed IDREF path; the allocation batches additionally
exercise all seven compiled Name-typed attributes, with reusable operation/model state.

| Case | Input | Mean | StdDev | Displayed allocation | Actual inclusive GC bytes / operations |
|---|---|---:|---:|---:|---:|
| 0 | abc | 15.705ns | 0.047ns | dash | 0 / 33,554,432 |
| 1 | 根😀 +126名 | 464.905ns | 1.737ns | dash | 0 / 2,097,152 |
| 2 | a:bc | 10.689ns | 0.050ns | dash | 0 / 67,108,864 |
| 3 | 127a +colon | 441.547ns | 2.177ns | dash | 1,968 / 2,097,152 |

Four reports succeeded, total33.07s. Case3's actual1,968inclusive GC bytes are retained
exactly despite the displayed dash (about0.000938B/op). No BDN zero-allocation claim,
unexplained wrapper subtraction or rerun is used to erase that observation. The
separate controlled917,504-call steady counter regression observed zero bytes.
This repair routes existing bounded scalar checks; it introduces no delimiter search,
vocabulary matcher or retained lexical tokens requiring SearchValues alternatives.
No throughput improvement, broader hardware or cross-platform release claim is made.
Raw allocation JSON and BDN logs/results are transient and retained until coordinator
independent inspection acknowledges summary completeness; only analysis is committed.
