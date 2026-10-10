# Borrowed-name allocation measurement

Implementation revision: `46f90ea2324f33617b091adbf2b8dcba2d83da18`.
Base: `78ae04d908d9b4401f971ebb746630c58ca519f1`; coordinated claim:
`987aa47af7c744088ea3fe92e50563230ea8d6cd`. macOS 15.8 arm64,
SDK 10.0.400, executing runtime .NET 10.0.11. No production code changed.

The old test warmed 10,000 discarded-return delegate invocations at a different
call site from its measured 10,000 identity-checked invocations. The test now
calls one non-inlined helper for both phases. It exercises the exact same span,
delegate call site, reference comparison, and failure branch. This fixes a
measurement-scope mismatch without changing the runtime operation or budget.
Each phase remains exactly 10,000 hits. The independently constructed input
string is asserted distinct from the document-owned root name before warmup;
every warm and measured result must have the original owned identity. The
measured helper must allocate exactly zero bytes. DOM/parser construction,
reflection, delegate creation, independent input construction and assertions
remain outside the steady-state boundary. No retries, extra warmup iterations,
parallelism changes, exclusions or lowered thresholds were introduced.

## Observations and limits

Historical PI worker evidence is copied immutably with `historical-pi-` prefixes.
At its unmerged d3eb revision, two combined runs recorded 299/300 and the
unchanged DOM probe's 216 B failure; a separate full run passed 300/300. That
branch was neither built nor consumed here. The historical 216 B failure was
not reproduced in this bounded investigation. We do not claim to know its
allocation stack or prove that this boundary correction fixes that exact event.

At the unchanged claimed source, one fresh focused original test passed 1/1.
One fresh full baseline failed 287/288 because the unchanged syntax allocation
test (`StreamingTests.cs:59`) measured 6,000 B. The DOM probe passed. This is
retained in `baseline-full.txt.gz`, reported to the coordinator, and requires
separate owner assessment; this task makes no syntax repair claim.

The committed standalone diagnostic reports allocation counters only, never
timing. It executes the original warm/measured logic and the equivalent helper
logic, checks identity, and records setup separately. In each of two default
fresh-process diagnostic observations, the counter-only delta was 0 B,
DOM/parser setup 10,720 B, reflection/delegate/input setup 864 B, first reflected
hit 0 B, original discard-return warm loop 24 B, original measured loop 0 B,
helper first invocation 0 B and warmed helper invocation 0 B. With
`DOTNET_TieredCompilation=0`, the original warm loop was also 0 B. This is evidence
of a first-use/context cost in that loop and an association with the tiering
configuration, not a demonstrated allocation stack or explanation of 216 B.
The first reflected hit follows parser setup, which already exercises name
ownership; it is not a cold runtime lookup. Process/setup costs are explicitly
counted rather than represented as allocation-free. There is no evidence here
of a true steady-state runtime allocation requiring a DOM owner repair.

## Checks at the implementation revision

Commands below each ran once. Logs (including failures) are retained as `.txt.gz`.

- `dotnet build Xemele.slnx -c Release --disable-build-servers -m:1 -p:UseSharedCompilation=false -p:NuGetAudit=false`: passed, no warnings/errors.
- `dotnet test --project tests/Xemele.Tests -c Release --no-build --no-restore --filter-method '*WarmBorrowedNameHitsAllocateZeroAndUseOwnedInternedStrings*'`: passed 1/1; exact 0 B assertion and identities.
- `dotnet test --solution Xemele.slnx -c Release --no-build --no-restore`: passed 288/288, no skips; default tiering and existing class parallelism.
- `dotnet build docs/reports/M1-dom-allocation-probe/Diagnostic -c Release --disable-build-servers -m:1 -p:UseSharedCompilation=false -p:NuGetAudit=false`; then `dotnet docs/reports/M1-dom-allocation-probe/Diagnostic/bin/Release/net10.0/Xemele.Tests.dll`, once default and once with `DOTNET_TieredCompilation=0`: passed; results above.
- `dotnet build docs/reports/M0-contracts/ApiCheck/ApiCheck.csproj -c Release --disable-build-servers -m:1 -p:UseSharedCompilation=false -p:NuGetAudit=false`; `python3 -B tools/ApiBaseline/compare.py --report docs/reports/M1-dom-allocation-probe/runtime-api.json`: passed, unchanged 222 compiled members.
- `python3 -B tools/ApiBaseline/discover.py`: passed after `python3 docs/reports/M1-corpus-import/provision.py` restored ignored pinned fixture bytes offline. Incidental generated Python cache removed; no peer metadata changed.
- `python3 tools/planning/tasks.py validate`: passed 109 tasks.
- `python3 -B -m unittest discover -s tools/planning -p 'test_*.py' -v`: passed 14 regressions.
- Committed, staged, working and untracked ownership/diff audits: passed; see `source-audit.json` and `source.diff`. Production diff empty; only existing DOM name-probe/helper changes plus one import.

Before the successful build, the ordinary restore/build failed NU1900 because
NuGet vulnerability data required unavailable network access. The subsequent
build used the command-line offline audit override; no package/build files were
edited. Early test attempts failed on sandbox named pipes and missing binaries
following that failed build; zero tests ran, and both logs are retained. Actual
successful test runs used authorized local named-pipe access. No benchmark
timing runs were performed. Final evidence records branch implementation, not
integrated verification; independent coordinator review is still required.
