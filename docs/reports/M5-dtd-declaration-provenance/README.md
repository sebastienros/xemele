# Duplicate DTD declaration provenance

Implementation `134ba62c1bd0eca93819ec5fd342fd35f9141ec5`, assigned base `4e13c32ed3da5e90d28af028c4a1e70e27e9b97c`, claim `d06947c27385d8c2a443ed56f96cc75cd78f8525`. Only verified prerequisites were used; no dependency on an active compiler/facade branch.

The internal get-only `DtdModel.DeclarationValidityDiagnostic` retains the first ignored duplicate element or notation diagnostic. Its optional final constructor parameter preserves existing source call sites. Codes are `DTD.DuplicateElement` and `DTD.DuplicateNotation`, severity Error, with fixed messages and the ignored declaration's original `declarationAt`. Parsing remains successfully nonvalidating, keeps first bindings, and does not append duplicates or report the retained diagnostic through the parser's syntax-error result.

This preserves the distinct XML validity constraints for [unique element declarations](https://www.w3.org/TR/2008/REC-xml-20081126/#vc-UniqueElementTypeDeclaration) and [unique notation names](https://www.w3.org/TR/2008/REC-xml-20081126/#UniqueNotationName). Repeated attribute and entity declarations continue their permitted first-binding behavior. The future facade must check the retained diagnostic before compiling the effective DTD model; this task does not implement that facade.

Seeding stages the prior diagnostic with collections and publishes it after the final cancellation check. A prior diagnostic retains identity and precedence. Synthetic prior duplicate element/notation entries can produce a staged first diagnostic, preserving first bindings. Failed staging never publishes it. External duplicates naturally encounter seeded declarations.

## Checks at implementation revision

Pinned executable `/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet`, SDK `10.0.400`, executable argv `--fx-version 10.0.11`. Runner reports actual 64-bit ARM .NET 10.0.11; the new IL test also asserts actual `RuntimeInformation.FrameworkDescription`.

| Check | Outcome |
| --- | --- |
| Offline cache restore | Pass |
| Release test-project build | Pass, zero warnings/errors |
| New focused tests | 15 passed |
| Earlier focused DTD preservation | 1,979 passed |
| Combined check after implementation commit | 1,994 passed; no failures/skips/not-run |
| Full compiled public API union | Matched all 921 members unchanged |
| `python3 tools/planning/tasks.py validate` | 154 tasks pass |
| `git diff --check` | Pass |

The new tests cover internal/external subsets at every split, original URI/line/column/byte location through external parameter entities, sync/async cross-subset DOM composition with genuine async one-byte reads, 100 duplicates retaining identical first diagnostic, prior diagnostic identity/precedence, synthetic seed duplicates, cancellation/failure before publication with successful retry, allowed repeated attributes/general/parameter entities, default null evidence, immutable property shape and actual Release getter IL.

Restore/build commands:

```sh
DOTNET_CLI_USE_MSBUILD_SERVER=0 MSBUILDDISABLENODEREUSE=1 '/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet' restore tests/Xemele.Tests/Xemele.Tests.csproj --source /Users/sebastienros/.nuget/packages --ignore-failed-sources -p:NuGetAudit=false -m:1 --nologo
DOTNET_CLI_USE_MSBUILD_SERVER=0 MSBUILDDISABLENODEREUSE=1 '/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet' build tests/Xemele.Tests/Xemele.Tests.csproj -c Release --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false --disable-build-servers -m:1 --nologo
```

Combined test command:

```sh
'/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet' --fx-version 10.0.11 tests/Xemele.Tests/bin/Release/net10.0/Xemele.Tests.dll -class Xemele.Tests.DtdDeclarationProvenance.DeclarationProvenanceTests -class Xemele.Tests.DtdParserTests -class Xemele.Tests.DtdParserBudgetTests -class Xemele.Tests.DtdParserSourceTests -class Xemele.Tests.DtdCompositionTests -class Xemele.Tests.DtdGrammarJoinedTests -class Xemele.Tests.DtdGrammarPinnedTests -class Xemele.Tests.DtdNonvalidating.BoundaryTests -class Xemele.Tests.DtdNonvalidating.OriginalTests -result-xml artifacts/M5-dtd-declaration-provenance/combined.xml -noColor
```

Earlier separate runs used the new class alone (15), and the remaining classes alone (1,979). These selected source files contain no allocation-counter or benchmark methods. Pinned grammar fixtures used the existing hash-verified private corpus cache. No performance measurements ran.

The first build exposed an existing test namespace/type-name collision for DtdModel; the new test uses a fully qualified Model alias, and the final build passed. The initial preservation command combined class and namespace filters, selecting zero tests; it is not passing preservation evidence. Corrected class-only selection and postcommit combined selection produced the counts above.

API union generation loads `docs/api/M[0-5]*.txt` entries, verifies duplicate IDs have identical canonical lines, sorts unique entries by ID and prefixes `xemele-api-v1`. The generated transient union contains 921 entries. Comparison:

```sh
'/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet' --fx-version 10.0.11 /private/tmp/xemele-m5-integration/docs/reports/M0-contracts/ApiCheck/bin/Release/net10.0/ApiCheck.dll compare src/Xemele/bin/Release/net10.0/Xemele.dll artifacts/M5-dtd-declaration-provenance/api-union.txt
```

## Allocation applicability and ownership

The normal path adds nullable diagnostic reference storage and copying to the parser/model. It does not create a diagnostic for valid declarations. The model getter's actual Release IL is `ldarg.0; ldfld XmlDiagnostic; ret`, verified including the resolved field type. The model constructor stores the provided reference; parser completion supplies the already-owned diagnostic reference. No extra collection, formatting, string copy, borrowed buffer retention or lexical sidecar is introduced.

Each parser/model gains one reference field, 8 bytes of logical payload on the tested 64-bit host; object alignment/layout can change the exact heap-size delta, which was not measured. Existing owned model construction and its snapshot arrays still allocate as before; no unchanged-total-bytes claim is made. Duplicate parsing allocates one immutable diagnostic on the first ignored duplicate, with static code/message strings and a value-copy location retaining its existing URI reference. Later duplicates skip the diagnostic factory via `??=`; the identity test confirms 100 ignored duplicates retain that same object. Existing duplicate-declaration scratch/content parsing allocations are unchanged and are not claimed allocation-free.

For synthetic prior duplicates, staging may allocate a diagnostic before a later seed failure; that diagnostic remains transient and unpublished. A retry may allocate its own first diagnostic. Each successful parser/model retains at most one diagnostic. Prior model diagnostics are reused without allocating a replacement during seeding. Diagnostic retention follows the owned model/document lifetime; no raw source buffer is retained. These are source/generated-code allocation applicability checks, not a throughput, inclusive GC byte total, or retained heap measurement. No exclusive measurement lease or benchmark claim is required or implied here. Only summaries/analysis are committed; transient test XML and generated union remain under ignored artifacts.

The coordinator source-only review at the implementation revision approved bounded retention, lazy first-invalid allocation, first-binding preservation, transactional publication and focused tests without correction. The approved existing wrapper `python3 docs/reports/M5-validation-model/check_api.py` also passed with `Matched 921 compiled members`; its exact union verifies all docs/api fragments, canonical fields/lines, distinct member IDs, and sorted compiled IDs. Its approved M0 extractor binaries were copied to the identical ignored path from the integration checkout; no tracked shared files changed.
