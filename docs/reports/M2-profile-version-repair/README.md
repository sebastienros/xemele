# Exact E38 applicability repair

The source case `xmlconf/eduni/errata-2e/errata2e.xml#rmt-e2e-38`
remains historically `not-wf`: “XML 1.0 document refers to 1.1 entity”.
[Fourth Edition E10](https://www.w3.org/XML/xml-V10-4e-errata#E10)
explicitly reverses Second Edition E38's version restriction.
The effective [Fifth Edition production 26](https://www.w3.org/TR/2008/REC-xml-20081126/#NT-VersionNum)
admits literal ASCII `1.[0-9]+`, and
[production 77](https://www.w3.org/TR/2008/REC-xml-20081126/#NT-TextDecl)
uses optional VersionInfo for external text declarations.
The original `1.1` entity contains a compatible empty `foo` element and two
LF characters. Its historical purpose does not require effective rejection.
No XML 1.1 character or newline rules are enabled.

The classifier changes only this exact ID. `verify.py` compares against claim
`a0e243e85e0a7cf9a8ea74d8020893c42dde6b0f`, permits only its four classification
fields plus the required aggregate counts, and checks preserved original hashes.
Classifier regeneration independently hash-verifies all 3,386 resource bytes.
All other 2,584 rows and resource metadata must remain identical.

`expected.json` is independently authored from inspection of the original bytes
and grammar before executing the product. It includes complete logical events,
source URI/scalar/byte locations, nested DOM with whitespace nodes, DOCTYPE and
DTD declarations. The thin sample uses the existing decoder/core/DOM sink and
existing friend-test assembly name, with a ProjectReference to this checkout.
It checks all 46 external read sizes, one-byte primary submissions, copied event
snapshots and owned DOM/DTD after core/decoder disposal, exact URI grants, one
open/disposal, six malformed-version/illegal-character/entity-boundary controls,
and required resource denial. No framework reference parser is an oracle.

Reproduce from the repository root:

```sh
python3 -B docs/reports/M2-profile-version-repair/verify.py
python3 -B docs/reports/M2-conformance-profile/classify.py --check
dotnet build samples/M2-profile-version-repair/Sample.csproj -c Release --disable-build-servers -m:1 -p:UseSharedCompilation=false -p:NuGetAudit=false
dotnet samples/M2-profile-version-repair/bin/Release/net10.0/Xemele.Tests.dll "$PWD"
dotnet run --project tests/Xemele.Conformance.Tests -c Release --no-build -- --filter-namespace 'Xemele.Conformance.Tests.XmlFull.*'
python3 -B tools/planning/tasks.py validate
git diff --check
```

This focused correction does not establish full corpus semantic conformance.
No runtime, public API, package, configuration or hot-path implementation changes;
earlier API/ABI/performance evidence remains applicable. No new allocation or
timing measurement is required. Execution revisions/identities/results and
cleanup are recorded in the task evidence.
