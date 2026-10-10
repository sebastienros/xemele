# Foundation contract handoff

This task approves measured M0 foundation contracts, not a runtime implementation.
Approved decisions are in docs/decisions/M0/: foundation.md (representation/input/
common limits/lifetime/allocations), conformance.md (unchanged IConformanceCase and
concrete infrastructure outcomes), api-compatibility.md (compiled normalization
scheme and limitations). Common/foundation/performance specs link those decisions.
MIT is the explicit user choice; LICENSE and THIRD-PARTY-NOTICES.md record it.

Declaration/usage models live under tests/Contracts/Fixtures/foundation as
`.cs.fixture`, never SDK-default `.cs`. Projects live under this report's Fixtures
folder, keeping generated obj sources outside bootstrap Contracts globs. M1 has
no future DTD/resolver/recovery references; M2 preserves all M1 normalized members.
Checks is a nonpackaged xunit.v3.mtp-v2 4.0.1/MTP executable. It checks handles,
span usage, defaults/unlimited/invalid limits, diagnostic snapshots, result/error
invariants, resolver exactly-once disposal, conformance failure/exclusion semantics,
and pause/EOF/cancellation/callback/reentrancy/input-reference rules, including
cancellation in the last data callback suppressing EOF and faulting the pump. The pump and
DOM are deliberately minimal contract models, not an XML parser or product tests.

## Reproduction

Use SDK 10.0.400 (global.json) with DOTNET_ROOT and PATH prefix
`/Users/sebastienros/Library/Application Support/dotnetup/dotnet` on this host.
The verifier preserves caller-configured DOTNET_ROOT/PATH (including setup-dotnet
on CI), checks dotnet --version against global.json, and records the actual host,
architecture and full Git revision. Run `python3 docs/reports/M0-contracts/verify.py`.
It builds with
`--disable-build-servers -m:1 -p:UseSharedCompilation=false`, runs native MTP using
`dotnet test --project ... --no-build --no-restore`, compares stage goldens,
checks M1 subset preservation, rejects missing/added/changed baseline inventories,
builds an actually changed signature probe (generic constraints/interfaces/
sealing/nullable/accessor/default/virtual/constant/addition), compares and restores
it, then preserves solution/build/MTP/discovery/tree/input correctness checks.
Full transcript is artifacts/M0-contracts/checks.txt; the committed checks.txt is
the durable tested-revision copy. The verifier never regenerates approved goldens
or runs timed benchmarks. All final check revisions/counts live in task JSON.

A first in-sandbox MTP invocation stalled before any output and only that process
was terminated. The prebuilt native MTP invocation succeeded with sandbox
escalation. An initial full solution build caught nested fixture obj contamination;
projects were moved into this owned report tree, without shared-project edits.
These failed attempts are not recorded as passing checks. Later passing runs use
the corrected layout; hosted CI and other operating systems have not run here.

## Exact shared follow-up request

Create an ordered CI/repair task owning tools/ApiBaseline/, tests/Xemele.TestUtilities/
Build/ and .github/workflows/build.yml; add ownership/dependency for this normalizer
if it is moved. Compose validated API fragment member/declaration pairs in memory,
preserve fragment provenance, detect duplicates, sort by member identity, and
compare the union with the actual Release Xemele assembly. The current prototype
accepts one precomposed canonical fragment, not a directory of task fragments.

Build: `dotnet build docs/reports/M0-contracts/ApiCheck/ApiCheck.csproj -c Release
--disable-build-servers -m:1 -p:UseSharedCompilation=false`.
Compare: `dotnet docs/reports/M0-contracts/ApiCheck/bin/Release/net10.0/ApiCheck.dll
compare src/Xemele/bin/Release/net10.0/Xemele.dll <temporary-composed-fragment.txt>`.
Keep the empty-runtime bootstrap rule until M1; no fixture fragment enters docs/api.
CI should also build/run Checks via native MTP and run normalizer negative probes.
No shared CI wiring is claimed by this task. For signature families beyond the
foundation/probe coverage, the introducing owner must expand extraction/probes
before shipping (custom compiler attributes, interop/layout, forwarding, explicit
interface dispatch, per-accessor attributes/event visibility). This prototype
cannot establish unrestricted ABI or behavioral compatibility.

Corpus wiring owner implements the concrete outcome shape in its owned
infrastructure paths after review; it must preserve frozen IConformanceCase,
manifest schemaVersion:1, rejection versus runner failure, reviewed exclusions,
and independent expectations. No real corpus data is claimed by this contract.

## Performance evidence and limits

No fresh timings were run. Integrated tree/input experiment reports supply
allocation and provisional single-M4-Pro Arm64 evidence, with corrected scanner
87d101c7e9b09167b2d114f1f33ea5974680617d and unchanged primitives
662b15bb24ce340c301389e037277fd68d9f60ab. Original 62 input/rejection cases are
superseded. Arena saves retained memory but measured traversal/mutation slower;
character-core/bounded decoding favors simplicity/retention, not a blanket speed
claim. Owned DOM text avoids tiny slices retaining large sources; borrowed intern
hits and warm scan/match/callback primitives target 0 B. All setup/persistence/
exception/pool/retention allocations remain separately accountable.
M1/later owners must validate actual XML composition, limits exactly/one-over,
Unicode/location/encoding semantics and cross-architecture release performance.
