# M3-contracts branch verification

Tested implementation revision: `626f1fc9da456ef838c643ec96511d5c58044d41`. Worktree:
`/Users/sebastienros/.codex/worktrees/1164/xemele`, branch `codex/m3-contracts`.
Claim b7d8bdf6b8b77ca1d01c274ff35b4395e11e08f7 and integrated base
b0b5bcf3e83947117fe5b900e6c5ba3d68e8b6ab are ancestors. The coordinator
working checkout was never edited. Integration revision remains unset.

- `python3 docs/reports/M3-contracts/check.py`: passed. Independent Sync and
  Async consumer fakes pass; full sixteen callback signatures match; borrowed
  tag collections and lambda/subclass traces agree; callback exception identity,
  one-event pause/resume/EOF, populated notation nullable provenance and known
  entity provenance pass. Async memory/offset transport and genuinely suspended
  fake resource continuation pass. DOM async partial source composition builds
  with zero warnings/errors. Negative probes fail only as required with CS4007
  and CS8345. Exact output is in contract-check.txt.
- `dotnet build src/Xemele/Xemele.csproj -c Release --no-restore`: passed,
  zero warnings/errors; actual integrated product assembly built.
- `git diff --exit-code b0b5bcf3e83947117fe5b900e6c5ba3d68e8b6ab HEAD -- src Directory.Build.props Directory.Packages.props global.json tests/Xemele.Tests tests/Xemele.Conformance.Tests tests/Xemele.Differential.Tests tests/Xemele.TestUtilities tests/Contracts/Contracts.csproj`:
  exit 0 with no output, establishing unchanged runtime/config/prior test inputs.
- `git merge-base --is-ancestor 276a0bdaf6433bbda8958b068955de85c3b75a3a HEAD`:
  exit 0. The verified M2-gate is retained; its handoff records 5474 tests with
  zero failures/skips and all 2585 original strict semantic cases. These are
  preserved historical checks, **not new test runs by this contract worker**.
- `python3 tools/planning/tasks.py validate`: passed, 138 records; schema,
  evidence, acyclic dependencies, additive milestones and ordered ownership.
- `git diff --check`: passed.

A fresh earlier-runtime test restore was attempted through default sources and
local cached/empty sources with NuGetAudit disabled. It stalled at restore path
collection without producing an assets file/test run. Cancellation was requested
and the two exact remaining worker restore processes were terminated. It is
skipped preservation execution, not a passing test or a product failure. No
runtime source/config/test change requires that rerun; preserved gate inputs and
actual product build provide the applicable earlier-milestone evidence.

No runtime hot path was changed. The allocating adapter fakes establish language
and contract feasibility only; allocation/performance evidence is inapplicable.
Real source splitting, quotas, cancellation/reentrancy, async external resource
cleanup/equality and bounded 1 GiB memory remain the implementation workers'
acceptance criteria.

Required owning follow-ups are recorded in the decision/task handoff. Coordinator
has created M3-attribute-metadata, M3-async-resources and M3-streaming-pump in its
own branch; none was merged into this worker branch or treated as implemented.
The contract can be integrated independently; feature workers wait for their
actual owning prerequisites to be verified. No M4/M5 or unmerged runtime branch
is referenced by these builds.
