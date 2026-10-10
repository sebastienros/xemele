# Corpus infrastructure verification

Core code lives in `tools/CorpusManager/Core/`; task-owned runner and test apps
reference that actual project, rather than a copied implementation. All types
are internal infrastructure; no runtime APIs or provisional `IConformanceCase`
contract is introduced. The focused test app consumes centrally pinned
`xunit.v3.mtp-v2` 4.0.1 and Microsoft.Testing.Platform.

After the coordinator releases the timing slot, run with the pinned SDK:

```sh
DOTNET_ROOT='/Users/sebastienros/Library/Application Support/dotnetup/dotnet' \
PATH='/Users/sebastienros/Library/Application Support/dotnetup/dotnet':$PATH \
DOTNET_CLI_USE_MSBUILD_SERVER=0 python3 -B docs/reports/M0-corpora/verify.py
```

The transcript is written to ignored `artifacts/M0-corpora/verification.txt`.
Focused test XML and demo caches stay under that task's ignored artifact folder.
The script verifies builds, positive and negative outcomes, actual output hashes,
package inventory, integrated discovery, 13 existing Python tests, three existing
MTP infrastructure tests and their controlled failures, bootstrap help commands,
managed-only runtime package payload, and task metadata validation. It reports
its actual host, revision, and commands; it makes no hosted-platform claim.

Manual demo (offline, no parser execution):

```sh
dotnet run --project docs/reports/M0-corpora/Runner -c Release -- import "$PWD" xml-well-formedness-bootstrap-v1
dotnet run --project docs/reports/M0-corpora/Runner -c Release -- provision "$PWD" xml-well-formedness-bootstrap-v1
```

Import validates checked-in local source bytes, inventory, and fixture hashes,
then populates cache. Provision reads only the cache. The JSON report carries
provenance and one outcome per fixture; all six outcome counts sum to total.
Exit 0 requires a nonempty inventory with every fixture provisioned. Exclusions,
unsupported profiles, missing/corrupt cache, and infrastructure failures exit 1.
Malformed manifest/import failures produce an explicit infrastructure error and
exit 1, without inventing a per-case success when no inventory can be loaded.

For the contract owner: proposed internal outcomes are `Provisioned`,
`MissingCache`, `CorruptCache`, `UnsupportedProfile`, `Excluded`, and
`InfrastructureFailure`. Cases carry path, declared classification, detail/reason,
and verified cache path on success; corpus reports preserve source URL/revision/
SHA-256, license/notice, adapter/profile. `ParserCasesExecuted` and
`StandardsPassed` are always zero. These are provisioning results for consideration
by the future reporting contract, not frozen conformance outcome shapes.

Outside-ownership follow-up: `tools/CorpusManager/Program.cs` remains the verified
help-only bootstrap. Its owner should wire `import`/`provision` to this core once
the command/reporting contract is agreed. The task-owned runner provides the
complete usable demonstration in the meantime. Existing solution test projects
are unchanged; the coordinator must explicitly run the focused report test app.

This is filesystem/JSON/checksum infrastructure; no parser/scanner hot path is
introduced. There is no parser allocation or benchmark claim.
