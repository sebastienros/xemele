# Canonicalization checks

Product source86d201877172af32c0afee7a511bb82b281bf8dc; prepared/evidence-tested
revision8528b1b48756ac6cdb873b69f335b4326d3d8db1. Only verified dependencies:
M5-contracts e10170f4c913d7de607773e59eda66ffa9afc4ed and canonical-provenance
integration af63f96ee2ebccf00d6d3e8c7b9100721787c5d7, contained in claimed base.
No sibling runtime assembly is used. Runtime implementation uses Xemele DOM and
XPath identities; no System.Xml or pretty printer implements canonicalization.

Release tests build0warnings/errors;63 focused canonical tests passed actual
.NET10.0.11. Tests pin complete/full-exact selections in all modes; UTF8/scalar
sorting/escaping; namespace context/prefix lists/default undeclaration; inherited
XML attributes/base/id; logical adjacent text; comments/outer-root LF; options,
invalid/stale/mixed/detached/incomplete/unresolved/negative-revision sets;
quota/cancellation/token/stream lifetime/I/O and mutation callback boundaries.
Both overloads preflight invalid input before output. Exact selection never
manufactures missing namespace/attribute items; inconsistent programmatic source
bindings reject before output. Source-only reviewer and root approved86d2018.

The published1.1 section3.8 vector was corrected explicitly using official
[W3C erratum E11-01](https://www.w3.org/2008/05/xml-c14n11-errata): e3 basebar/foo,
not the publication's erroneous something/bar/foo. Section2.4's contiguous
omitted-ancestor base chain stops at any included ancestor, including one
without physical base; lang/space inheritance continues. Empty/query-only
reference tests pin pre-resolution trailing '..' basis normalization.

Preservation command:
```
pinned dotnet --fx-version10.0.11 tests/Xemele.Tests/bin/Release/net10.0/Xemele.Tests.dll -noColor -noLogo -namespace Xemele.Tests.M5C14n -namespace Xemele.Tests.M5CanonicalProvenance -namespace Xemele.Tests.Tree -namespace Xemele.Tests.Recovery -namespace 'Xemele.Tests.M4*' -namespace Xemele.Tests.AsyncInput -namespace Xemele.Tests.M5ValidationModel -namespace Xemele.Tests.ValidationMetadata -method- Xemele.Tests.Tree.TreeTests.WarmTraversalAndRepeatedNameInternProbesAllocateNothing
```
582 tests passed,0failed/errors/skipped, including63canonical tests and prior
DOM/recovery/provenance/model/XPath. The existing Tree allocation regression
method is explicitly excluded; functional checks perform no benchmark counters.

Approved verified M0 API extractor was reused as tooling, via:
```
python3 docs/reports/M5-c14n/check_api.py --checker /private/tmp/xemele-m5-integration/docs/reports/M0-contracts/ApiCheck/bin/Release/net10.0/ApiCheck.dll
```
Actual explicit10.0.11 runtime,944 compiled public members exactly match the
checkout full docs/api union (921preserved+23canonical). New owned API fragment
docs/api/M5-c14n.txt matches frozen C14NConsumer.golden.txt. No shared API file edits.

Release sample build0warnings/errors. Direct --fx-version10.0.11 sample execution
exactly matches samples/M5-c14n/expected.txt via diff -u. Sample demonstrates
selected payload in allthree algorithms and inherited xml:lang differences.

One controlled allocation+12caseBDN sequence passed; actual inclusive numerators
and distinct ownership domains are in performance.md. Prior provenance's
historical unleased Tree child measurements are not reused as new evidence.
Task planning validation passed153records before final evidence update; final
validation follows the durable evidence edit. Integrated verification belongs
to the coordinator; this branch task is implemented only.
