# ShortRun harness configuration validation

Implementation revision: `2e77234f797450c50da335a2d20a9d4e5968c41d`.

All three entry points use `Job.ShortRun.AsDefault()` when no direct CLI job option
is present. Explicit `--job`, `--job=...`, `-j` and attached short-option forms
bypass the custom default and let BenchmarkDotNet choose the requested job.
There are no customized launch,
warmup, iteration-count or iteration-time overrides. The existing Tree JSON
exporter and all workloads/correctness/allocation commands are preserved.
BenchmarkDotNet 0.15.8 cached XML documentation describes `AsDefault` as specifying
custom default settings for the console arguments parser. `dotnet-inspect` was
attempted twice via dnx (latest and cached 0.26.0); both failed because NuGet DNS
was unavailable. Local package documentation and compiled/runtime checks provide
the configuration evidence instead.

The first implementation (104ed1b870ab1bab528c230eebbab6f743af5ec8) passed
default ShortRun validation, but its explicit Dry check failed: it still ran
ShortRun. Its complete outputs are retained under `initial-104ed1b/`. The
[versioned BDN parser source](https://github.com/dotnet/BenchmarkDotNet/blob/v0.15.8/src/BenchmarkDotNet/ConsoleArguments/ConfigParser.cs)
confirms `GetBaseJob` chooses a global default before the selected CLI job.
The corrected revision conditionally supplies the custom default. The repeat
Dry check selected and executed exactly one Dry case without a ShortRun job.
It is configuration validation only; its timing is not performance evidence.

Commands executed against the implementation revision (from the worker checkout):

```sh
dotnet build experiments/Tree/Tree.csproj -c Release
dotnet build experiments/Input/Input.csproj -c Release
dotnet build benchmarks/Xemele.Benchmarks/Xemele.Benchmarks.csproj -c Release
dotnet experiments/Tree/bin/Release/net10.0/Tree.dll --self-check
dotnet experiments/Tree/bin/Release/net10.0/Tree.dll --memory
dotnet experiments/Input/bin/Release/net10.0/Input.dll check
dotnet experiments/Input/bin/Release/net10.0/Input.dll allocations
dotnet benchmarks/Xemele.Benchmarks/bin/Release/net10.0/Xemele.Benchmarks.dll --help
dotnet experiments/Tree/bin/Release/net10.0/Tree.dll --help
```

Raw output is stored in the adjacent named text files. All three builds succeeded
with zero warnings/errors. Tree correctness passed eight construction fixtures
and identity, growth, cycle, sibling, deep traversal, culture and lookup checks.
Tree memory checks retained the expected backing-buffer ownership behavior and
reported zero bytes for warm borrowed lookups. Input correctness passed 83,095
assertions. Input allocation/source-collectability regressions passed; cold
initialization and owned conversion allocations remain explicitly reported.
Bootstrap help retains the working shell (there are no central benchmark cases).

These checks validate harness configuration on .NET SDK 10.0.400/runtime 10.0.11,
macOS 15.8.1 Arm64. They do not introduce comparative performance conclusions or
release evidence. ShortRun uses fewer samples and is the user-selected convention
for future assignments; new work should use these defaults unless the user
explicitly selects another job. Historical experiment reports and their recorded
job definitions/revisions remain untouched. No full matrix was rerun.

Focused job validation commands (only one benchmark case selected per command):

```sh
dotnet experiments/Tree/bin/Release/net10.0/Tree.dll --job Dry --filter '*Text.RetainedView*32*' --artifacts docs/reports/M0-bench-shortrun/tree-dry --exporters json
dotnet experiments/Tree/bin/Release/net10.0/Tree.dll --filter '*Text.RetainedView*32*' --artifacts docs/reports/M0-bench-shortrun/tree-short --exporters json
dotnet experiments/Input/bin/Release/net10.0/Input.dll benchmark --filter '*LiteralBenchmarks.ByteLiteral*xml*' --artifacts docs/reports/M0-bench-shortrun/input-short --exporters json
```

The default cases are Tree `Text.RetainedView(Length: 32)` and Input
`LiteralBenchmarks.ByteLiteral(Token: <?xml)`. Both reported `ShortRun` with
LaunchCount=1, WarmupCount=3, IterationCount=3 and exactly one executed benchmark.
The explicit Dry case reported only `Dry`, with one executed benchmark.
Tree ShortRun completed in 43.49 seconds total; Input ShortRun in 23.59 seconds
total. Dry completed in 3.06 seconds total.
Raw output and full JSON exports are retained alongside this report.
BenchmarkDotNet could not elevate process priority on this host. This and the
small sample count limit timing interpretation; no performance selection is
made. The coordinator granted the timing slot; a shared atomic mkdir lock at
`/private/tmp/xemele-benchmark.lock` serialized these runs.
