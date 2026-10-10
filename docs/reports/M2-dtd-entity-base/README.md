# DTD entity-base evidence workflow

Source candidate only until the coordinator releases HOST HOLD. Preparation and
measurement commands below are instructions for that later phase, not recorded
passing checks. Run with the pinned .NET 10.0.400 SDK / .NET 10.0.11 Arm64 runtime,
DOTNET_CLI_USE_MSBUILD_SERVER=0, and no GC/JIT overrides.

After source review and HOST RELEASE, `python3 -B
docs/reports/M2-dtd-entity-base/measure.py --mode prepare --report preparation-N`
builds the isolated driver, executes only counters-disabled correctness and
identity modes, and writes `preparation.json` after strict cleanup. Compilation
inputs come from actual MSBuild Compile items for both referenced projects.
Imported build configuration, compiler references/analyzers, NuGet assets,
generated assembly/global-using inputs and owned Python drivers are hashed too.
Tracked inputs must equal their actual HEAD blobs. Generated and SDK/package
inputs have explicit owners. The parent output and complete actual shared runtime
directory are frozen along with the dotnet host, SDK, assembly informational
versions and runtime/architecture. Correctness/API/package/corpus checks remain
separate required evidence; preparation does not stand in for them.

A fresh explicit exclusive coordinator grant is required for each allocation or
benchmark observation. Use `--mode allocation` or `--mode benchmark`, a fresh
`--report` name, and `--exclusive-grant` with the exact grant reference. Existing
reports/preparation are never silently overwritten. A failure requires review;
this script does not retry. Neither the wrapper nor workload specifies alternate
job arguments, counters, GC/JIT settings, iterations or relaxed tolerances.

Each generated benchmark process emits transient provenance in GlobalSetup and
GlobalCleanup, outside measured intervals. It includes actual executable, driver,
product and runtime assembly paths/hash/MVID/full informational versions, the
complete generated binary closure and generated project/source input hashes.
The wrapper binds these to the actual Execute command, known prepared production
and driver binaries, exact Git revision and actual runtime, rejects missing or
changed before/after proofs, and checks the final on-disk child closure. Exactly
two distinct cases, three Actual and three Result observations per case, one GC
record and one zero child exit are required. Raw operation counts must agree;
finite positive durations and nonempty result rows are mandatory. Both generated
restore/build exits and the parent exit must be zero. Process groups have bounded
total/build/child deadlines and are terminated/reaped on failure.

Allocation results distinguish cold setup plus correctness oracles, warmed owned
immutable-model output, warmed complete document construction, and a positive
allocation control. These owned output paths have no zero-allocation claim.
Timing is informational on this host; no cross-architecture speedup or release
claim follows. Historical composition measurements are preserved and never rerun.

Raw logs, CSVs and generated child projects/binaries are removed before durable
summary delivery. Summaries contain identities, selected numeric observations,
controls/counts, stage-specific failures and cleanup status. Cleanup failures
force failure. No raw payload/log archives are created.
