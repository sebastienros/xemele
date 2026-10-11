# M7 HTML/CSS corpus integration

Assigned isolated checkout `/Users/sebastienros/.codex/worktrees/0dff/xemele`,
branch `codex/m7-corpus`, claim `5e5f7572ed4ad3071d48f74cba9ec741ab5a78fc`,
verified prerequisite base `09f43321521ee2a8d31cf3b88787b62cda6899fe`.
The checkout initially had detached HEAD; it was attached to its existing assigned
branch before edits. Only the task-owned test/corpus/report paths and task JSON
change. No runtime/API/shared build/entrypoint edits or later milestone dependency.

The immutable 240-file HTML inventory integrates existing tokenizer, complete WPT,
legacy and serializer original resources, licenses, hash inventories, per-case
ledgers and prerequisite integration reports, including CSS/callbacks. Existing
corpus runners execute the original expected results; no expectations are produced
by Xemele. The exact original WPT noscript-template inner-output assertion now
passes through parser/model/selector/serializer composition. Ten named authored
parser regressions and eight independently specified malformed UTF8 witnesses
exercise the public facade, stream ownership, output bytes, selectors and import.

Every retained difference is explicitly scoped:

- Tokenizer: four XML-infoset coercion controls remain outside the HTML model;
  all 6,823 HTML lexical originals / 7,049 states run. Three immutable domjs error
  arrays require mandatory current PI-open diagnostics; original token output
  stays unchanged. Eighteen current PI witnesses remain required.
- Builder: all 166 WPT resources retain individual dispositions (27 browser
  execution dependencies, 26 original static assertions, 32 translated static,
  three metadata, three vector transports, four harness supports, 62 original
  vector resources, nine static supports). Six specific scripted tree vectors
  need actual DOM/script execution. Three template/frameset originals expand to
  six current-normative configurations; seven old encoding declarations lie
  outside the frozen first-1024-byte sniff window. Original inputs/expectations,
  rules and witnesses remain in `M7-builder/normative-discrepancies.json`.
  All 82 original sniff records, Big5 no-chardet and scripted no-execution controls,
  228 labels / 91,711 index cells, and complete static/template assertions remain
  executed by their integrated runners, with no blanket file exclusion.
- Legacy: eight named `legacy-root-envelope-v1` adaptations preserve the permanent
  single-root model while retaining original multi-root native results. Raw native
  caller-shortread outcomes remain distinct from the frozen coalesced-read API.
  The 539 native trees, encoding/name/recovery rules and all raw I/O controls are
  retained and executed. This remains the libxml2 legacy profile.
- Serializer: every original html5lib case has a disposition: ten exact original
  overlaps, 69 current WHATWG DOM witnesses, 151 unmatched-end token controls with
  no DOM representation. Original options/results and per-case reasons remain;
  token controls are not advertised runtime passes. All 640 real native graphs
  remain required, including template dual-graph/native parser distinctions.
- Selectors: all 89 inherited two-graph positive/rejection facts remain required.
  New BCL XPath overlap covers 35 authored pairs / 24 generated trees / three
  cultures, with 10,080 initial/mutated result comparisons across two graphs.
  New actual pinned xmloxide output has 23 exact overlaps, four descendant-only
  root differences and eight grammar rejections, each separately recorded. Every
  full product expectation still passes; these are reference differences.

The bounded callable fuzz lane uses seed `0x4D374854`, 96 malformed markup/Unicode
inputs repeated under three cultures and legacy/HTML5 scripting false/true:
864 string documents, borrowed UTF8 BOM spans overwritten after return, 3,456
caller short-read loads (chunks 1/2/7/31), 576 fresh fragments over 14 HTML/SVG/
MathML contexts with real form ancestry, both scripting flags and all quirks modes.
It checks complete graph/attribute/doctype/template snapshots, permanent ownership,
links/cycles, context immutability, repeatability, clone/import/fragment transfer,
UTF8 Save/string output equality, retained callback tokens/one EOF, 12 quota-family
checks, 1,728 entry/mid-read cancellations and 864 identical I/O faults. String to
byte comparison explicitly replaces malformed UTF16 through independent .NET UTF8
encoding; arbitrary malformed UTF16 fragment/token inputs remain separately used.
Byte seed `0x4D374254` adds 64 arbitrary 1–128-byte payloads under UTF8 BOM, three
profiles, 192 documents / 768 short-read loads in the final French culture. These
are equivalence/ownership invariants, not independent parser conformance or a
semantic serialize/reparse promise. Native legacy goldens and independent WPT/
UTF8 expectations supply supported semantic oracles. No long fuzz campaign,
allocation/timing benchmark or runtime optimization is part of this task.

Reproduce from repository root (actual pinned SDK10.0.400/runtime10.0.11):

```sh
DOTNET='/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet'
"$DOTNET" build tests/Xemele.Tests/Xemele.Tests.csproj -c Release -p:RuntimeFrameworkVersion=10.0.11 -p:UseSharedCompilation=false --disable-build-servers --source tests/Contracts/Fixtures/M7 -p:NuGetAudit=false
"$DOTNET" build tests/Xemele.Conformance.Tests/Xemele.Conformance.Tests.csproj -c Release -p:RuntimeFrameworkVersion=10.0.11 -p:UseSharedCompilation=false --disable-build-servers --source tests/Contracts/Fixtures/M7 -p:NuGetAudit=false
"$DOTNET" build tests/Xemele.Differential.Tests/Xemele.Differential.Tests.csproj -c Release -p:RuntimeFrameworkVersion=10.0.11 -p:UseSharedCompilation=false --disable-build-servers --source tests/Contracts/Fixtures/M7 -p:NuGetAudit=false
"$DOTNET" build docs/reports/M7-corpus/Fuzz.Driver.csproj -c Release -p:RuntimeFrameworkVersion=10.0.11 -p:UseSharedCompilation=false --disable-build-servers --source tests/Contracts/Fixtures/M7 -p:NuGetAudit=false
"$DOTNET" --fx-version 10.0.11 tests/Xemele.Tests/bin/Release/net10.0/Xemele.Tests.dll -namespace Xemele.Tests.HtmlSerialization -namespace Xemele.Tests.HtmlTreeConstruction -namespace Xemele.Tests.HtmlTokenization -namespace Xemele.Tests.LegacyHtml -namespace Xemele.Tests.HtmlModel -namespace Xemele.Tests.HtmlProcessingInstructions -namespace Xemele.Tests.HtmlParsedText -namespace Xemele.Tests.Css -namespace Xemele.Tests.HtmlCallbacks -namespace Xemele.Tests.Serialization -namespace Xemele.Tests.M3AsyncSerialization -result-xml artifacts/M7-corpus/inherited-correctness.xml
"$DOTNET" --fx-version 10.0.11 tests/Xemele.Conformance.Tests/bin/Release/net10.0/Xemele.Conformance.Tests.dll -namespace Xemele.Conformance.Tests.Html -result-xml artifacts/M7-corpus/conformance.xml
"$DOTNET" --fx-version 10.0.11 tests/Xemele.Differential.Tests/bin/Release/net10.0/Xemele.Differential.Tests.dll -namespace Xemele.Differential.Tests.HtmlCss -result-xml artifacts/M7-corpus/differential.xml
"$DOTNET" --fx-version 10.0.11 docs/reports/M7-corpus/bin/Release/net10.0/Fuzz.Driver.dll
CARGO_TARGET_DIR="$PWD/artifacts/M7-corpus/native-target" cargo +1.95.0 build --offline --locked --manifest-path docs/reports/M7-corpus/NativeSelector/Cargo.toml
artifacts/M7-corpus/native-target/debug/xemele-m7-selector-oracle < tests/corpora/css/native-input.json
python3 tools/planning/tasks.py validate
git diff --check
```

The shared fuzz bootstrap stays unchanged; the owned driver compiles and executes
the same callable source under `tests/Xemele.Fuzz/Html/`. Existing SDK source globs
discover all new conformance/differential tests without project edits. Native
probe uses the exact clean cached upstream commit, disabled default features,
Rust1.95.0 and 19 dependency package records identical to the existing shared
Cargo.lock. Locked rebuild reproduces raw output byte-for-byte. Compiler/binary/
input/license/source hashes are in `native-selector-provenance.json`.

Historical failures remain explicit. The first driver build used singular
`XmlParseException.Diagnostic`; corrected to the actual Diagnostics collection.
Two restricted test restores stalled; both were canceled and their exact process
groups terminated/reaped. A later restricted cached final build also stalled and
its exact group was terminated/reaped (exit143); pinned offline elevated builds
succeeded. These stalls are not product-check passes. The
first native two-graph harness run failed 15/39 because XML self-closing nonvoid
markup has different HTML parsing semantics and BCL XElement.Parse discarded
whitespace. Explicit nonvoid end tags and PreserveWhitespace corrected the
harness without changing native selector outputs or product expectations.

A broad inherited invocation completed exit1, 7,789 total / 7,788 passed / one
failure / zero errors/skips/not-run, 6.211s, before the stop signal boundary.
`Xemele.Tests.AttributeMetadata.AttributeMetadataTests.WarmBorrowedCompleteAndIncompleteEventsAllocateZeroWithPositiveControl`
reported 7,744 B against zero at M3-attribute-metadata line133. It inadvertently
included an embedded allocation counter without a measurement lease or controlled
tiering environment. Raw XML is retained at `artifacts/M7-corpus/inherited-all.xml`.
No failure is suppressed and no passing performance claim/rerun/runtime change is
made. The coordinator explicitly routes this pre-existing measurement check to
its owning cumulative gate under controlled isolation; it is not M7 correctness
acceptance. Final committed correctness output is recorded in `checks.json`.
