# M6 contracts verification

Run from repository root:

```sh
python3 tests/Contracts/Fixtures/M6/check.py --dotnet '/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet'
python3 tools/planning/tasks.py validate
```

`--emit-baselines` is only for the contract owner creating/updating the reviewed
canonical API snapshots; normal verification compares rather than overwrites.
`checks.txt` records actual focused output. Task JSON identifies the exact tested
implementation revision; evidence metadata is committed separately.

Each lane is a separate nonpackaged executable referencing integrated Xemele and
only its own declarations. Catalog, XInclude and Interop have canonical compiled
`xemele-api-v1` golden files. The Roslyn checker is an owned copy of the earlier
M5 contract checker, with .fixture sources so parent default source globs remain
unchanged. Missing-member negative controls confirm comparisons reject drift.
CLI links the unchanged actual M0 registry and executes its static contract probe
and unknown-command code; it has no new public assembly API.

Every lane executes actual prior XML parsing and deep import APIs and checks
Environment.Version=10.0.11. Restore uses an explicit local source with auditing
disabled; build forces RuntimeFrameworkVersion=10.0.11 and --no-incremental;
execution uses the pinned host with --fx-version 10.0.11. Runtimeconfig version
and disabled roll-forward are checked. New M6 extern declarations are compile
models only and never executed. The vector inventory is future acceptance input,
not runtime catalog/include/adapter conformance evidence. Earlier owned files,
source, package/build files and contract fixtures are checked untouched against
the coordinated claim. The initial exploratory Interop build reported CS0824
(extern constructor warning treated as error); fixture-only suppression was added,
without any runtime-source change. The initial long-running runner loaded its
source before the generated-file exclusion was fixed, then failed the final
authored-source glob assertion on generated obj files; all preceding builds,
runtime demonstrations and API controls succeeded. The final assertion now
excludes generated bin/obj. Catalog alone was rebuilt after its private constructor
change; other compiled fixture sources were unchanged after their successful
builds. `--verify-existing` rechecks those prepared local binaries without
reclaiming a rebuild and is used for the committed-revision final comparison.
A fresh checkout should use the default full command above.

No benchmark, allocation or timing workload is involved in this documentation/
compile-model task. No package publication, corpus cache changes or actual AOT
smoke is asserted. M6 CLI/samples/gate own implementation and package/AOT evidence.

Outside ownership: src/Xemele/Resolution/Core/Resources.cs requires the ordered
resource-kind repair described in the M6 decision. Existing enum baseline/fixtures
and M2 behavior must be preserved; undefined enum requests must still reject.
