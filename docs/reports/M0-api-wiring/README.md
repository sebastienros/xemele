# Compiled API wiring

`python -B docs/reports/M0-api-wiring/verify.py` builds the Release runtime and
unchanged approved checker, compiles M1/M2 usage models and runs the foundation
MTP app outside the solution. It compares the stage goldens, preserves M1 within
M2, composes actual fixtures into disjoint fragments with distinct inventory IDs,
and checks missing/added/changed unions against compiled metadata. Real compiled
signature probes cover constraints, interfaces, sealing, nullability, setter
visibility, optional defaults, virtual methods, constants and an added method.
The mutated probe is rebuilt normally in a finally block. Approved fixture/golden
and existing runtime fragment hashes must remain unchanged.

The actual Release Xemele assembly is compared through
`python -B tools/ApiBaseline/compare.py --bootstrap-empty-api --report artifacts/M0-api-wiring/runtime-api.json`.
The flag allows no fragments only when discovery's source-empty runtime rule holds;
the checker must independently confirm zero actual externally visible members.
The fixture API remains outside docs/api and is never advertised as runtime API.
There is no automatic baseline regeneration or tracked union. JSON reports retain
original ID, member, declaration, fragment filename and line number, sorted by
member. The temporary comparison fragment sets id=member as the approved checker
requires; duplicate/conflicting IDs and members fail validation before comparison.
When a report is requested, stdout contains a compact result; the report carries
complete provenance and actual assembly SHA-256. Mismatch/error returns 1.

The normalizer is reused directly from the verified
`docs/reports/M0-contracts/ApiCheck/ApiCheck.csproj`, not copied or moved.
Its version and limitations are frozen in docs/decisions/M0/api-compatibility.md.
Custom compiler attributes, interop/layout/marshalling, type forwarding, explicit
interface dispatch and per-accessor attributes/event visibility require expanded
probes and normalization before any corresponding runtime APIs ship. This is
foundation signature coverage, not unrestricted ABI or behavioral compatibility.
Source usage and contract tests complement metadata checks.

The verifier inherits DOTNET_ROOT/PATH and validates SDK global.json exactly.
CI setup-dotnet supplies the environment on Windows/Linux/macOS. On this host,
use `/Users/sebastienros/Library/Application Support/dotnetup/dotnet` for DOTNET_ROOT
and prepend it to PATH. Builds use --disable-build-servers -m:1
-p:UseSharedCompilation=false; set DOTNET_CLI_USE_MSBUILD_SERVER=0 locally.
The focused verifier writes artifacts/M0-api-wiring/checks.txt and runtime-api.json;
CI uploads these alongside all retained earlier artifacts. No timings are run.
Local execution evidence does not establish hosted Windows/Linux/macOS CI runs.

CI retains planning/discovery, normal solution restore/build, existing MTP
failure-exit checks, corpus app/root CLI and independent bootstrap fixture checks,
and managed package output. Preservation evidence is recorded separately at the
implementation commit. No shared runtime/package/solution changes are made.
