# M1 runtime corpus join checkpoint

The owned Tests project compiles the same XmlSubset sources discovered by the
existing conformance/differential projects. It consumes integrated Xemele,
verified reference protocol/process infrastructure and the verified full-suite
inventory. Source fixtures remain private, outside Git. Run the import task's
provision.py offline before execution.

Pre-repair execution at claim 28a740b with working source later committed as
f21e3c5 is retained in pre-repair.txt and compressed xUnit XML reports. The first
253 rows passed252/failed1, zero skips. The complete500 rows passed495/failed5,
zero skips. The reports do not claim this task implemented or any integrated
passing revision. Source content hashes are in pre-repair-summary.json.

The product accepted supported rejection rmt-ns10-042 (colon in PI target).
Conformance and differential both expose that failure. The coordinator owns an
ordered runtime repair; no fixture reclassification or runtime edits occur here.
The BCL reference additionally accepted malformed XML VersionNum cases
not-wf-sa-102, o-p26fail1 and o-p26fail2; Xemele correctly rejected them.
Reference discrepancies remain separate from product conformance outcomes.

The harness discovers all2585 identities/242 supported rejections/2343 visible
capability gaps and hashes all3386 contained resources before execution. Original
bootstrap2 fixtures execute their frozen expectations. Eight authored accepted
models use independent BCL lexical oracles retaining CDATA/comments/PIs, prefixes,
explicit declarations and attribute order. Adjacent Text fragments coalesce as
allowed by serialization; declaration metadata and outer-root whitespace are
excluded according to the M1 DOM contract. Strings/character spans and strict
UTF8/UTF16LE/UTF16BE bytes, BOM/noBOM, one-byte-read caller streams and serialized
output are compared with the independent source model. Five accepted normalized
event models additionally traverse the verified BCL child protocol.

The sandbox denied MTP IPC before executing tests. Direct invocation of the
built xUnit test executable avoids that IPC and produced actual reports:

```sh
dotnet build docs/reports/M1-corpus/Tests/Tests.csproj -c Release --disable-build-servers -m:1 -p:UseSharedCompilation=false
dotnet build tools/ReferenceRunners/Bcl/BclReference.csproj -c Release --disable-build-servers -m:1 -p:UseSharedCompilation=false
dotnet docs/reports/M1-corpus/Tests/bin/Release/net10.0/Xemele.XmlSubset.Tests.dll -noColor -result-xml artifacts/M1-corpus/pre-repair.xml
```

Pinned SDK10.0.400 and runtime10.0.11, macOS arm64. This is a verification
harness; no runtime hot path or timing/allocation claim is added. Full verification,
API222/package-consumer checks and task evidence remain pending the renewed claim
after the PI-target prerequisite is verified. Task JSON is untouched during
coordinator suspension. No recursive workers, shared writes or publication.
