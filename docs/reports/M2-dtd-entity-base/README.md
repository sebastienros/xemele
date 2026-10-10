# DTD entity-base evidence workflow

Preparation requires source review; standalone measurement requires an additional
fresh exclusive grant. Commands below are workflow instructions, not recorded
passing checks. Run with the pinned .NET 10.0.400 SDK / .NET 10.0.11 Arm64 runtime,
DOTNET_CLI_USE_MSBUILD_SERVER=0, and no GC/JIT overrides.

After source review and HOST RELEASE, `python3 -B
docs/reports/M2-dtd-entity-base/measure.py --mode prepare --report preparation-N
--benchmark-report benchmark-N`
builds the isolated driver, executes only counters-disabled correctness and
identity modes, and writes `preparation.json` after strict cleanup. It
uses the uniquely named `Xemele.EntityBase.Measurements.csproj` with its unique
project filename and existing `Xemele.Benchmarks` friend assembly name.
Both generation paths bind explicitly to its absolute path; generated
projects must contain exactly its one ProjectReference before Builder starts.
The wrapper retains that independent binding proof and requires the later
generation to match it. Cleanup takes its directory baseline after parent build
and checks the complete frozen parent closure again after cleanup, preserving
dependency directories restored by the parent build.
Compilation inputs come from actual MSBuild Compile items for both referenced projects.
Imported build configuration, compiler references/analyzers, NuGet assets,
generated assembly/global-using inputs and owned Python drivers are hashed too.
Tracked inputs must equal their actual HEAD blobs. Generated and SDK/package
inputs have explicit owners. Generated queries reuse the exact observed pinned
Builder global properties, verify their evaluated values, and inventory the
actual evaluated assets and NuGet extension paths. Query commands, properties,
exits and bounded error diagnostics survive cleanup. Query process groups are
bounded and reaped, including failures. Same-module PE attribute constructors
are read so the independent child runtime full version must equal the executing
parent runtime build before preparation can pass. The parent output and complete actual shared runtime
directory are frozen along with the dotnet host, SDK, assembly informational
versions and runtime/architecture. Preparation additionally invokes the pinned
BenchmarkDotNet Generator and Builder directly, never Executor or engine, for
the same two immutable declared cases/configuration and exact future artifact
path. PE metadata probing reads the independently generated executable, rebuilt
driver variant and product without running them. Full raw binary closure,
generated code/project/script (including .notcs), actual compiled inputs/imports/
references and complete source versions become the expected manifest. All
generated payloads are then removed. The actual timed child must regenerate
those exact approved inputs/bytes, not adopt its own observed closure as expected.
Correctness/API/package/corpus checks remain
separate required evidence; preparation does not stand in for them.

A fresh explicit exclusive coordinator grant is required for each allocation or
benchmark observation. Use `--mode allocation` or `--mode benchmark`, a fresh
`--report` name, and `--exclusive-grant` with the exact grant reference. Existing
reports/preparation are never silently overwritten. A failure requires review;
this script does not retry. Neither the wrapper nor workload specifies alternate
job arguments, counters, GC/JIT settings, iterations or relaxed tolerances.

Each generated benchmark process validates the approved manifest hash and
compares its complete actual closure, compiled inputs, generated inputs and PE
metadata to the independent expected variant in GlobalSetup before invoking any
fixture correctness workload or measured interval. Failure throws there. It
emits transient actual/expected differences before refusal, then revalidates in
GlobalCleanup outside measured intervals. Proofs include executable, driver,
product and runtime assembly paths/hash/MVID/full informational versions, the
complete generated binary closure and generated project/source input hashes.
The wrapper binds these to the actual Execute command, approved independently
rebuilt variant, exact Git revision and actual runtime, rejects missing or
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
controls/counts, stage-specific failures, truthful passed/failed/unrun phases and
cleanup status. All observed cases, partial measurements, GC/exits, child proofs
and actual/expected file/hash differences are attached before guards can throw;
cleanup cannot erase the actual mismatch. Cleanup failures
force failure. No raw payload/log archives are created.
