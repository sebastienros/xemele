# M5 cumulative gate preparation

The isolated gate starts from verified dependency revision
`084c3a49659f4df8bf482b5459078db212ed572c`, with durable claim
`8619a7882b5e22bd26e9789d76a612be1b6ef7fc`. Only this report subtree
and the gate task record are owned. No product changes are included.

`verify.py` reuses the M4 gate's ordinary checks. `prepare` restores from the
local cache, builds serially with build servers disabled, and runs the owned
public source demo. `run_tests.py --exclusive-grant` is a separate run-only
phase: three direct test DLL executions, each limited to 300 seconds, and a
1000-second total deadline including initial/final identities and own process
group cleanup. The flag does not grant authority. Only the coordinator's
exact durable exclusive grant permits it. The allocation regressions in the
full suites must remain inside that lease. `inspect_tests` parses results only
after durable release. No benchmark or historical throughput run is scheduled.

`freeze.py` records all tracked files, ignored original corpus resources,
generated project state, all checkout binary outputs, SDK 10.0.400, complete
runtime 10.0.11 and installed host closure, and the stable effective runtime
environment. Volatile task bookkeeping is excluded. Runs use direct
`dotnet --fx-version 10.0.11`; tests additionally use invariant culture and
no parallelism. XML results must prove actual .NET 10.0.11. The demo asserts
`RuntimeInformation.FrameworkDescription` itself.

After release, run `grammar`, `fuzz`, `api`, and `package` independently.
The grammar adapter pins both original driver children to 10.0.11 without
changing the original model or resource verification. Official API discovery
uses the 3386 copied original ignored files inside this checkout; the API
host shim pins the approved extractor. The local unpublished package must
contain only net10.0 runtime assets with a DLL identical to the API-tested
assembly. Its external consumer uses an initially absent package cache and
only a local feed; its public source and output must match the source demo.

Preparation currently passes the source demo and all auxiliary builds.
The first solution restore passed, but the build failed CS0234 because the
Contracts project includes `Fixtures/M5/ApiCheck/Program.cs` without that
nested tool's Roslyn references. This is an outside-ownership repair request,
not an ignored failure. No full tests, allocation regressions or timing
measurements have run. Raw results remain ignored for coordinator inspection;
only analysis and summaries will be committed. The local package is retained
for coordinator copying before checkout cleanup.

Earlier M3 streaming timing evidence is historical after M5 SourceCore
repairs. This gate proves cumulative bounded functionality and makes no
updated throughput/allocation claim for those old streaming measurements.
