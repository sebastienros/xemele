# M5 contracts evidence

Implementation revision: `9cf03617d159f6fd811b0bf185a6581f398d22d8`. Checker argument-only follow-up: `f93d86c3755db9c82f23087523d9ea16a10427e1`.
Claim: `0f772c8da2e2f68d1a0f1b987ec95c03c89288fe`; verified dependency
M4-gate `a0354ad6a8ea7a8f2109db3d5b9f701e39e939f9` is an ancestor.

Executed at implementation revision:

```text
python3 tests/Contracts/Fixtures/M5/check.py --dotnet '/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet'
python3 tools/planning/tasks.py validate
git diff --check
```

All passed. Pinned SDK 10.0.400 builds three nonpackaged standalone projects,
restored using local source with NuGet audit disabled. Both independent consumers
print actual FrameworkDescription `.NET 10.0.11` and are directly launched with
`--fx-version 10.0.11`; installed-runtime inventory is not treated as actual-host proof.

| Contract check | Outcome |
| --- | --- |
| DTD consumer | Compiles resolver/limits/cancellation, FromDocument and Validate usage; runs result snapshots, complete/invalid/fatal invariants, null/incomplete rejection and defaults |
| C14N consumer | Compiles both overloads and all modes; uses verified document/attribute/namespace M4 identities and checks finite output default |
| Compiled API | 28 DTD/shared and 23 C14N exact members match golden fragments |
| Negative controls | Missing member and changed signature each rejected in both independent assemblies (four controls) |
| Preservation | Only task-owned diff/untracked paths; no runtime source, earlier contract or shared build changes |
| Planning validation | 151 tasks validated in isolated checkout |

The checker follow-up adds only `--ownership-base` (defaulting to this worker's
claim) so the coordinator can check the integrated diff against its actual premerge
main revision. At `f93d86c3755db9c82f23087523d9ea16a10427e1`, `check.py --help`, planning validation and diff whitespace
check passed. The already successful compiled/runtime evidence is unchanged and
has not been relabeled as a new execution. For combined integration use
`--ownership-base <premerge-main-full-hash>`. Do not compare the integrated diff
against the earlier worker claim, which would include legitimate coordinator work.

Declarations are fixture-only. Schema/canonicalizer fail-fast method bodies were
not called, shipped, or reported as implemented behavior. No DTD or canonical-byte
conformance or performance measurements were run. No hot-path code was changed;
allocation evidence is inapplicable to this contract delivery. Full earlier-suite
preservation and combined M5 behavior belong to the gate, not this compile check.

Frozen decisions cover all acceptance semantics and cite pinned W3C specifications.
Ordered `M5-validation-metadata` must add the documented internal standalone/location
setter and DomSink.Declaration hook before M5-dtd-validate; this contract makes no
claim that the repair exists in its isolated branch. The coordinator has created
that task and dependency on main; its metadata is not copied here. No remote push
or publication occurred. No raw benchmark outputs were created.
