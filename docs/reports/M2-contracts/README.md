# M2 independent contract models

Run with SDK10.0.400 and DOTNET_CLI_USE_MSBUILD_SERVER=0:

```sh
python3 -B docs/reports/M2-contracts/verify.py --report-prefix tested
```

Every attempt uses a new owned report directory and retains the compressed
command transcript, summary, actual source revision and failure/unrun scope.
Baseline preparation additionally uses --models-only
--emit-reviewed-model-baselines; it is not cumulative runtime verification.
The normal run compares committed goldens, executes snapshot/disposal/default/
result invariants, requires specific compiler failures for eleven boundary probes,
and detects a real compiled public-API mutation. It restores clean model outputs
and preserves common81 plus foundation resource signatures by metadata identity.
Internal DTD/core signatures are compilation contracts, not public APIs; public
model baselines use the existing xemele-api-v1 extractor without changing it.

Resolver and DTD lanes each use foundation common without M2 dependencies.
Encoding uses foundation common plus the unchanged decoder interface.
Entities composes independent DTD/resolver declarations and M1 tree/sink models;
Recovery composes resolver/options/M1 tree/strict factory declarations. None
references the runtime assembly, shared test/build projects or unmerged code.
The executed Checks model does not invoke any grammar/core/recovery declaration.

The cumulative run provisions existing private corpus data from its authorized
cache (no publication), rebuilds the BCL runner, runs preserved M1-contracts
verification (all independent models/goldens, protocol38, negative/ABI probes,
foundation11 and solution), compares actual API222, and records runtime247,
conformance255 and differential299 XML results:801 distinct solution cases.
Planning14 and validator110 plus committed/staged/working/untracked ownership
and whitespace audits are required. Output reports distinguish model checks from
real earlier-milestone runtime tests. Existing model/reports are never rewritten.

No M2 XML conformance, live I/O policy, repair, additional encoding behavior or
allocation/performance pass is claimed. No timing slot is used. Windows/x64/AOT
and full XML remain later unrun gates. Source Options/Common.cs requires the
explicit coordinator-owned partial/split repair in the decision/handoff before
additive options can be implemented in their assigned paths.

Coordinator integration runs set --expected-branch to the actual integration
branch and --audit-base to its pre-integration revision. Claim/prerequisite
ancestry remains independently required; branch/base overrides never relax owned
path checks. The resolved SDK executable and fresh local BCL runner are pinned;
all242 actual reference records must match its unchanged SHA256 and tested source
build. Caller XEMELE_DOTNET/runner overrides cannot produce reference evidence.
The initial tested4d5ebd7 attempt preserved all M2/M1 model passes but failed
NU1900 at foundation NuGet vulnerability lookup under the network sandbox;
subsequent runtime/API/planning checks in that attempt were unrun.
