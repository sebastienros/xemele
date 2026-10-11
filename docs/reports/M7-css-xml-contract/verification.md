# CSS XML contract verification

Implementation revision: `365b6745316726ea085034f21214806afaeb9312`. Claim: `67c9894f783898715e735359500ad89292b07c39`.
SDK 10.0.400; forced Microsoft.NETCore.App 10.0.11 for every build/execution.
Restore uses the fixture directory as its source with NuGetAudit=false; existing
cached dependencies satisfy restores. No shared project/checker/source changes.

Commands executed at the implementation revision:

```sh
NoWarn=CS0436 python3 tests/Contracts/Fixtures/M7/check.py --ownership-base 365b6745316726ea085034f21214806afaeb9312 --dotnet '/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet'
python3 docs/reports/M7-css-xml-contract/check.py --ownership-base 67c9894f783898715e735359500ad89292b07c39 --dotnet '/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet'
python3 tools/planning/tasks.py validate
```

Declaration checker: PASS for Engine 173, Tokenizer 116 and Selector 133 exact
compiled members; all three missing-member controls reject; all actual XML
tree/stream probes pass on runtime 10.0.11. Selector adds exactly Matches(XmlNode)
and Select(XmlNode), preserving every previous declaration. The other two
baselines are byte-for-byte unchanged from the claim.

ActualModels: normal warnings-as-errors build PASS, zero warnings/errors;
actual HTML/XML model assembly identities and HTML tree probe PASS; both
signature consumers compile using actual verified model types; actual XML
name/attribute casing, text/default/detached handles and streaming probes PASS.
Only actual model/tree code executes, never CSS declaration bodies.

Planning: PASS, 165 tasks. Strict task-owned full claim-to-current tracked and
untracked paths plus git diff --check: PASS. The declaration checker hardcodes
the old contract task's ownership, so its final ownership-base is the tested
source commit; that part sees no worker delta. The separate task-owned checker
performs the actual full claim-to-current ownership audit including this task
record and report subtree. It also checks the exact additive baseline delta.

An initial unsuppressed Selector fixture build failed with 40 CS0436 duplicate
model errors after the actual HTML model became available. The frozen lanes
intentionally compile Model.cs.fixture along with the runtime model reference.
The coordinator approved explicit NoWarn=CS0436 for only those nonpackaged
canonical declaration lanes, retaining their exact fixture metadata. This does
not establish actual HTML consumption, which the separate ActualModels project
proves without suppression and without Model.cs.fixture. The initial failure
is resolved for this contract workflow; no shared build-file edit is required.

Semantic review: XML graph uses ordinal case-sensitive names/attribute names and
expanded names even in XHTML; HTML retains frozen namespace casing. Namespace
option snapshots, default type namespace, scope/root/query bounds, current DOM
attributes, detached trees, mutations, preorder unique result snapshots, default
XmlNode and null HtmlNode rules are explicitly frozen. The implementation task
M7-css remains responsible for runtime matching tests and performance evidence.
Allocation/benchmark checks are not applicable to a nonpackaged declaration
repair; no runtime hot-path work or allocation claim is made here.
