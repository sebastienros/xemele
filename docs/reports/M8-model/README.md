# M8-model source handoff and measurement preparation

Implementation/preparation source: `2ac7e6034a82d371e76608d0ad11fe2e2d8c8ea0`.
Pinned SDK 10.0.400; execution host .NET 10.0.11, macOS 15.8.1 Arm64.

## Delivered boundaries

The runtime implements the exact frozen Model declarations, immutable collection
snapshots (including init-only record replacement), guarded compile document and
namespace/base snapshots, compile quotas/diagnostics, an M5 validation-operation
adapter, strict structured principal/resource parsing, per-composition schema
resource ownership/quotas/provenance, and capped actual XPath accounting.
Abstract datatype/facet/regex contracts contain no fake engine implementations.

Report constructors reject required null strings/records, undefined kinds,
negative/duplicate keys, missing or inconsistent event/pattern/context joins,
inconsistent authored IDs and complete-result validity. Subject locations remain
independent of the fired event's context. Optional authored IDs remain null.

A SchemaCompileContext owns one operation, parser aggregate ledger and loader.
Compile graph owners must discard that context after publishing owned immutable
output. SchemaDocumentScope borrows a document only for guarded traversal.
Document ContentRevision is checked around snapshots and before final acceptance;
incomplete sources refuse. Namespace contexts retain owned strings/URI values.
The loader's SourceUri(document) supplies final resource identity even when the
XML source has no declaration. Pass scope.Context(element).BaseUri as the inherited
base for nested references; no current-directory fallback exists.

Loader EnterComposition scopes cover traversal of an expanded external document
through its nested loads, then release. Principal input starts at depth zero.
The compiler owns language-specific cache/graph cycle policy, including legal
XSD mutually importing components; no global cache is introduced. Transfer occurs
only on resolver success; all returned resources are disposed once, with primary
parser/I/O/caller/cancellation errors preserved. Strict DTD prohibition prevents
resolver authorization from enabling entity access.

DocumentCharacters and ExpandedCharacters aggregate across principal and external
input. The parser's actual accepted scalar count debits shared work; consumed
expansion counters seed each new source under the original positive quota so an
exhausted expansion allowance still accepts input with no expansions. External
bytes/resources and inclusion nesting aggregate at the loader. Parser depth,
per-element attributes, per-token characters and document-local intern tables
retain their verified M2 scope/units. Component counts belong to the owning graph
compiler and must be charged before persistence. External-byte terminal locations
use the current final URI and unknown byte offset rather than an aggregate offset.

## Checks at the implementation revision

All builds use `-c Release --no-restore --disable-build-servers
-p:UseSharedCompilation=false -m:1`; restores use only
`--source /Users/sebastienros/.nuget/packages -p:NuGetAudit=false`.
The pinned executable is
`/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet`.
Every executable below ran with `--fx-version 10.0.11`.

- Integrated test project Release build: zero warnings/errors.
- `Xemele.Tests.dll -namespace Xemele.Tests.M8Model -parallelMode none -noLogo -noColor`:
  20 passed, no failures/skips. Covers snapshots/options/diagnostics, run isolation,
  report joins, namespace/source/base/revision guards, strict parsing, stream
  lifetime, cancellation and exception provenance, aggregate scalar/expansion/
  external-byte/resource/depth quotas and exact XPath debit/caps.
- Affected prerequisite selection: 189 passed, no failures/skips. Selected classes
  Resolution.ResolverTests, M8XPathAccounting.AccountingTests,
  M5ValidationModel.ValidationModelTests, M5ValidationSourceProvenance.SourceProvenanceTests,
  ValidationMetadata.ValidationMetadataTests, M4Evaluate.EvaluationTests under
  Xemele.Tests. Explicit exclusion:
  `-method- Xemele.Tests.Resolution.ResolverTests.WarmBorrowedReadsAllocateNothing`.
- Owned ApiCheck compares the runtime's exact frozen type families and all their
  visible members to Model.golden.txt: 316 matched. The same check against the
  additive public docs/api/M8-model.txt fragment: 139 matched.
- Model.Consumer built with `-p:ModelXmlOnly=true`, using only the frozen XML-only
  verified prerequisite source set plus SchemaCommon. Executed real models,
  shared XPath/M5 accounting, namespace snapshots, detached reports and preserved
  M5 DTD behavior. Passed. No HTML/CSS/later engine dependency is required.
- Owned allocation probe build and `--control artifacts/M8-model/control.json`:
  11 consumed cases, each 32 warm + 64 iterations, counters disabled, passed.
  Environment: DOTNET_TieredCompilation=0, DOTNET_gcServer=0,
  DOTNET_GCConcurrent=0, XEMELE_MODEL_REVISION set to the implementation commit.
- Planning validator: 171 tasks validated. Whitespace check passed.

No broad corpus/conformance run or cross-platform measurement is claimed.

## Allocation lease request — completed

Prepared identities are in preparation.json. Source/preparation code is committed
at the implementation revision; the metadata-only handoff commit does not rebuild
or replace these binaries. Working directory is this isolated checkout.

Sole proposed command (maximum duration **30 seconds**, one attempt, no retries):

```sh
env DOTNET_TieredCompilation=0 DOTNET_gcServer=0 DOTNET_GCConcurrent=0 XEMELE_MODEL_REVISION=2ac7e6034a82d371e76608d0ad11fe2e2d8c8ea0 '/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet' --fx-version 10.0.11 benchmarks/Xemele.Benchmarks/M8-model/bin/Release/net10.0/Xemele.Benchmarks.dll --measure artifacts/M8-model/allocation.json
```

The driver reuses the M8 accounting/M5 model probe pattern: no timing campaign,
no build/generation under lease, guarded counters-disabled control, consumed
oracle, warmed allocation distributions and positive allocation control.
It separates six zero-allocation primitive routes (existing M5 charge baseline,
compile charge/component counters, validation adapter, owned namespace lookup,
baseline borrowed read, tracked schema borrowed read) from five owned routes
(compile setup, namespace/report snapshots, unchanged M4 evaluation baseline,
new capped XPath join, external schema composition). Cold setup and retained
namespace contexts are separate informational ownership observations.
These are synchronous per-thread allocation checks, not async or throughput claims.

Root PARK/GRANT, supervised child completion/reaping, lifecycle RELEASE and durable
RELEASE ACK all completed before raw interpretation. The sole attempt passed;
actual distributions, ownership numerators and limitations are in allocations.md.
A3 now passes at the tested source revision. Runtime source/check evidence is
unchanged; no correctness rerun or new measurement was performed after release.

## Incidental prerequisite measurement (excluded from M8 evidence)

Before the source commit, a correctness class selection inadvertently included
`Xemele.Tests.Resolution.ResolverTests.WarmBorrowedReadsAllocateNothing`. Exact
parent argv was the pinned host with `--fx-version 10.0.11` and
`tests/Xemele.Tests/bin/Release/net10.0/Xemele.Tests.dll`, followed by:

```text
-class Xemele.Tests.Resolution.ResolverTests
-class Xemele.Tests.M8XPathAccounting.AccountingTests
-class Xemele.Tests.M5ValidationModel.ValidationModelTests
-class Xemele.Tests.M5ValidationSourceProvenance.SourceProvenanceTests
-class Xemele.Tests.ValidationMetadata.ValidationMetadataTests
-class Xemele.Tests.M4Evaluate.EvaluationTests
-parallelMode none -noLogo -noColor
```

Parent PID 73852 completed with exit 0 (190 passing cases). Existing harness
RunPair waited for its real/positive children, PIDs 73855/73856. Both exit metadata
records say started/exited/drained=true and exitCode=0; the Process objects were
disposed. A subsequent process inventory found no Xemele.Tests.dll or probe
children remaining. Ignored transient exit records are under
artifacts/M2-resolver-allocation-probe/child-runs/73852-da14f85acbe4433fbf207448d49bfea2/.
No allocation values from that unleased run are used as valid M8 evidence.
The explicit exclusion above applies to all subsequent correctness selections.
The final revision checks were rerun to record a known committed source revision.

Outbound status messaging was rejected by automatic approval review because it
found no trusted human authorization for the coordinator destination. The worker
uses this own-chat handoff for root collection and does not bypass the rejection.
