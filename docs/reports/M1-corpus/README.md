# M1 runtime corpus join

Implemented source: `054f240528bf4d4753b7bb5523aab2df1d7932b9`.
The complete [final summary](final-provenance/summary.json) records801 cumulative
solution tests (247 runtime,255 conformance,299 differential), zero failures/skips.
The same501 owned rows also pass in the isolated linked test project; that run is
additional verification of the same cases, not501 extra distinct standards cases.
Five provenance-negative and14 planning regression tests pass; planning validates
110 tasks. Both independent approved-model/API-fragment comparisons match222
compiled public members. A freshly packed isolated NuGet consumer passes all
three encodings and independently specified names/values/lexical kinds/PI rejection.

The [per-case join](final-provenance/case-results.json) preserves all2585 upstream
identities, original paths/full hashes, M1 target profile, expected classification,
reasons, actual product diagnostics, reference comparison category and raw response.
All242 supported rejection identities execute original checksum-verified bytes,
TryParse/Parse and one-byte-read caller streams. All reject, with owned diagnostics,
no partial document and preserved stream ownership. Case rmt-ns10-042 now rejects
with XML.Syntax through the separately verified PI-target repair. The2343 later
capability gaps remain explicitly **unrun-capability-gap**; no product or standards
pass is inferred. All812 upstream valid cases require DTD support, so accepted
coverage comes from the original bootstrap and independent authored models.

The [resource report](final-provenance/resource-checks.json) verifies all3386
original resources before execution. Combined discovery remains3388 files including
the two original bootstrap fixtures. No inventory/classification/source hash or
private source fixture is changed. Fixtures remain ignored/private, restored from
the verified offline cache; the import task retains redistribution terms.

Eight authored models use independent BCL XmlReader lexical/structural oracles
retaining CDATA/comments/PIs, prefixes, explicit declarations, ordinary attribute
order and child order. Adjacent Text fragments coalesce as allowed by M1
serialization. Declaration metadata and outer-root whitespace are excluded under
the M1 DOM contract. Strings/character spans and strict UTF8/UTF16LE/UTF16BE bytes,
BOM/noBOM, one-byte-read caller streams and serialized output agree with those
independent input models. Five additional normalized semantic event models agree
with the verified BCL child protocol before and after serialization.

Reference comparisons record239 matching rejections and **three documented
reference divergences**, separately from product outcomes. The exact-case/hash/
System.Xml10.0.0.0/.NET10.0.11 [ledger](reference-limitations.json) covers only the
three malformed VersionNum suffix cases independently reviewed against
[XML production26](https://www.w3.org/TR/REC-xml/#NT-VersionNum) and
[the pinned System.Xml implementation](https://github.com/dotnet/runtime/blob/v10.0.11/src/libraries/System.Private.Xml/src/System/Xml/Core/XmlTextReaderImpl.cs#L3505-L3521).
Each still must reject in Xemele. BCL success is retained, never counted as a
standards match or skip. Unlisted outcome/hash/identity or ledger engine/runtime
changes fail. Runner failures remain infrastructure failures.

The verifier pins the resolved SDK executable and freshly built local Release BCL
adapter, overriding caller reference-executable convenience variables. Every
collected record must match the fresh DLL hash and exact tested source build;
unchanged DLL bytes are checked after execution. Final build is
`1.0.0+054f240528bf4d4753b7bb5523aab2df1d7932b9`; executable/DLL/package hashes and
actual paths are recorded in the summary. Five focused negative checks cover
caller overrides, old hashes/builds and a later invalid record.

Resource roots accept trailing separators without losing the path boundary;
outside siblings, traversal, corruption and missing files fail. Root/under-root
parent/file symlinks fail. A separate symlink test permits a reasoned dynamic skip
only when privilege/platform support is unavailable, leaving other negatives
active. Actual macOS arm64 execution has **zero skips**. Windows/x64 execution is
unrun; no portability claim is inferred from this host.

Reproduce with pinned SDK10.0.400/runtime10.0.11 and the verified preserved cache:

```sh
python3 -B docs/reports/M1-corpus/verify.py --audit-base <pre-integration-commit> --report-prefix <new-unique-owned-directory>
```

Prepend `/Users/sebastienros/Library/Application Support/dotnetup/dotnet` to PATH on
this host and use DOTNET_CLI_USE_MSBUILD_SERVER=0. Each attempt refuses to overwrite
an existing directory. Direct built xUnit executables avoid sandbox MTP IPC; NuGet
restore needed authorized network access on this host. The complete compressed
[transcript](final-provenance/checks.txt.gz), test XML, package consumer output,
resource checks and committed/working/staged/untracked audits are retained.

Historical evidence is preserved: first253 rows252pass/1fail and complete500
rows495pass/5fail before repair; final-7a47 stopped on sandbox NU1900 before product
tests; final-network passed501 owned and247 runtime rows then stopped on an
incorrect per-assembly300 assertion; final-counts passed801 cumulative rows and
package/API checks before stronger reference-source provenance enforcement.
The final-provenance run supersedes those completion checks. No runtime edits,
allocation changes, benchmark timings, shared writes or publication occur here.
