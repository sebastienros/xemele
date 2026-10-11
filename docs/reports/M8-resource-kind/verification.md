# M8 resource kind branch verification

Implementation revision: `c90c274362e324a6f1eead44d211e8e76d0c785d`. Initial source commit:
`6d671cf6dc74db68abb73397811c64feff318542`. Claim
`5b7730916dba401857e67f6c2705328e4ea7af1d` and base
`3000efdc9d9c1768810ace6372ad52aa51be6bd2` are ancestors. Branch
`codex/m8-resource-kind`, isolated checkout
`/Users/sebastienros/.codex/worktrees/35b1/xemele`.

Runtime change is exclusively `XmlResourceKind.Schema = 5`. Values 0–4,
request/resolver declarations and ownership implementation are unchanged.
Tests exercise explicit schema authorization independently of entity/include
permission, synchronous forwarding, genuinely asynchronous overrides, default
async fallback, null/absent refusal, final URI and exact request/token forwarding,
exactly-once mixed disposal, and generic URI catalog routing with preserved
schema purpose/lexical identifiers. Existing catalog/session source is unchanged.

SDK command `/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet --version` returned 10.0.400. All final Release builds
used `--no-restore --disable-build-servers -m:1
-p:RuntimeFrameworkVersion=10.0.11 -p:NuGetAudit=false
-p:UseSharedCompilation=false`. Projects were
`tests/Xemele.Tests/Xemele.Tests.csproj` and
`docs/reports/M0-contracts/ApiCheck/ApiCheck.csproj`. Both builds passed with
zero warnings/errors. Cache-only restores used the same host/settings with
`restore --source /Users/sebastienros/.nuget/packages`; both succeeded.
Initial build at `6d671cf6dc74db68abb73397811c64feff318542` failed xUnit1051
for the omitted catalog-setup test token. The follow-up source commit fixes it;
all final checks below executed at `c90c274362e324a6f1eead44d211e8e76d0c785d`.

Exact test argv are retained in `test-commands.json`. Both runner banners and
the executed runtime assertions establish actual .NET 10.0.11. Focused run:
12 schema cases + 20 resource-kind cases + 32 catalog cases = 64 passed.
Resolver run: 42 passed. Both have zero errors/failures/skips/not-run. The
resolver command selects all resolver correctness methods and does not select
`WarmBorrowedReadsAllocateNothing`, which is a measurement. No performance or
allocation experiment ran; none applies to the enum-only runtime change.
Raw correctness logs/XML are preserved in ignored `artifacts/M8-resource-kind/`.

## Intentional cumulative test identity transition

Only `[InlineData(5)]` changes to `[InlineData(6)]` in the inherited M6 test.
Old identity: `Xemele.Tests.ResourceKinds.ResourceKindTests.UndefinedKindsFailBeforeResolver(value: 5)`.
New identity: `Xemele.Tests.ResourceKinds.ResourceKindTests.UndefinedKindsFailBeforeResolver(value: 6)`.
Value 5 is now a defined public enum member; 6 is the first undefined positive
value. Existing identities for -1, 99 and 2147483647 are unchanged and pass;
both sync/async calls reject them before the resolver. The cumulative gate must
account for this one intentional replacement, with no exclusions or dropped cases.

## API verification and check limitation

The approved compiled extractor ran with `--fx-version 10.0.11`, command
`emit src/Xemele/bin/Release/net10.0/Xemele.dll
artifacts/M8-resource-kind/actual-api.txt`. It emitted 1,254 members.
`python3 docs/reports/M5-validation-model/check_api.py` matched the exact full
API fragment union using the same approved extractor/pinned runtime.
Dictionary comparison of that emitted output against prior fragments confirmed
all 1,253 prior declarations unchanged and exactly one addition:
`F:Xemele.XmlResourceKind.Schema = 5`. `api-addition.json` retains the result.
No earlier API fragment was changed.

The broader `tools/ApiBaseline/compare.py --report
artifacts/M8-resource-kind/runtime-api.json` discovery preflight failed because
`tests/corpora/xml/xmlconf/eduni/errata-2e/E14.dtd` is missing from this checkout.
This is a corpus provisioning limitation, not an API comparison mismatch; the
existing scoped API union checker completed successfully. No fixture/cache was
removed or modified. Coordinator should provision retained corpus inputs before
running discovery/the cumulative corpus gate; this worker does not own corpora.

`python3 tools/planning/tasks.py validate` passed (171 tasks), and
`git diff --check` passed. Task is implemented on the branch, awaiting coordinator
integration and verification. No pushes, publications, shared build/contract
edits, orchestration-state edits or measurements occurred.
