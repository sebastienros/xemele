# Branch verification

Implementation/tested checkpoint: `2ae7416e1d7eb96a453fbb5afdbefda3ffe02114`.
Pinned host `/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet`;
Release builds force RuntimeFrameworkVersion=10.0.11 and UseSharedCompilation=false,
executables use explicit --fx-version 10.0.11. Cached NuGet packages only.

Owned model tests: `dotnet --fx-version 10.0.11 tests/Xemele.Tests/bin/Release/net10.0/Xemele.Tests.dll -namespace Xemele.Tests.HtmlModel -noLogo -noColor -result-xml artifacts/M7-html-model/model-tests.xml`
passed 9 tests, errors/failures/skips/not-run 0. All named-reference entries and
expected codepoints, foreign tables and misses, ambiguous attribute references,
source locations/doctype absence versus empty values, immutable owned tokens,
fragment/context/option snapshots, template ownership cycles and actual ancestry,
DOM clone/import/navigation/mutation/atomic preflight/quotas/live fail-fast views
are covered. Source-only coordinator review approved source refinements, then
bounded identity ownership refinement and exact frozen closure before grant.

Owned sample executable passed HTML graph/token/context/namespace/template probes
and real earlier XmlDocument probe. API extractor compare matched 146 members;
removing a real baseline member rejected with exact exit 1, then unmodified
baseline matched. Deterministic fixture generator reproduced both tables exactly.
Official tools/ApiBaseline/discover.py passed (1200 API inventory members,3388
corpus fixtures, bootstrap exemption false; scoped compiled comparison is146)
after coordinator provisioned all
3386 ignored original XML resources from unchanged original offline cache; no
fetch, cache mutation or shared tracked source edit. Planning validate passed all
164 records; git diff --check passed. Only assigned owned paths/task JSON changed.

Earlier exploratory failures are not passing evidence: first no-restore build
had no assets; sandbox restore stalled and exact owned process was terminated
before successful elevated cached restore; generated code needed #nullable enable;
unsupported --filter-class was corrected to runner -namespace; inventory initially
lacked ignored XML resources. Preparation-only tuple plan-path failure was repaired
in 05c8cb6 before successful controls. The first control negative intentionally
rejects a changed environment; positive/final-positive pass. No full cumulative
suite ran on this worker; coordinator integrated verification owns that check.
