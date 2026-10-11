# M5 cumulative gate evidence

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
environment. All tracked task JSON records are included; only volatile environment bookkeeping is excluded. Runs use direct
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

Final preparation includes the verified Contracts build repair. The initial
solution build failure is preserved at `artifacts/M5-gate/initial-solution-build-failure.log`:
nested M5 API tooling was included without Roslyn references. The owner's
repair excludes that tooling and nested fixture build outputs. No failure is
silently treated as passing.

The coordinator authorized the owned build flag
`-p:RuntimeFrameworkVersion=10.0.11` on restore and build, with
`--no-incremental` to regenerate outputs. Initial property-only and
restore-plus-property controls both found stale 10.0.0 runtimeconfigs:
the SDK incremental configuration input cache omits that property. Forced
regeneration passed the actual output assertions for all three test DLLs
and BCL reference DLL. Earlier allocation child harnesses bind
the actual host path/hash and launch `dotnet <test.dll>`; their generated,
frozen runtimeconfigs explicitly request 10.0.11 and `DOTNET_ROLL_FORWARD=Disable`
prevents a different runtime. Their own provenance checks compare child and
parent runtime identities. Direct gate executions still include
`--fx-version 10.0.11`. No shim bypasses allocation provenance.

The single exclusive full-suite run completed and was durably released before
raw inspection. All 6797 tests passed:5932 unit, 384 conformance, 481 differential;
zero failed/skipped/errors/not-run, with actual .NET 10.0.11 in every XML.
Initial/final 8666 source, 335 binary and 3852 runtime identities matched manifest
`633dee53146304a14d69ca7e4d19a6c3ae6f58dd884504dbc683499908245de7`;
all suite exits and wrapper exit were 0, finally cleanup reported no children.
The exact committed revision was `8a0c9d893eb000e2995d454b792d3d0130cc17ba`.
Its prepared binaries carry informational revision `c2eb65f820cdbfe1f6d52b76a36ec14e059c3c42`
because later preparation commits only changed owned documentation/harness
controls. The full closure recorded those actual binaries. Root and worker
independently matched all 12 allocation report/request pairs and their host,
test, product and corelib hashes against that leased manifest.

Inherited allocation regressions are summarized separately. Real measured
bytes are 0 for tree, streaming, PI, DTD framing, resolver and all 3 encoding
labels. Warm real setup is 368 B PI/432 B streaming, 0 B others. Positive measured
controls are64000 B tree/framing, 7680000 B streaming, 6400000 B PI, 640000 B
resolver and each encoding; warm positive tree 64000 B/PI 64368 B/stream 77168 B
remain inclusive. This proves those narrow existing regressions on current
SourceCore, not new cold/retained/full-operation throughput performance.

After release, original grammar initially stopped before any cases on
driver/product informationalversion, then reference informationalversion.
Authorized ordinary postlease rebuilds at fixed8a0 corrected the entire
identity closure without weakening the original oracle. Final 2585 cases
passed:1394 accepted/1191 rejected, 3386 original hashverified resources,
all 1394 callback-pause models, 409 original output models and 15 unchanged authored
effective 1.0 overrides. BCL accepted 671/rejected 1914, matched 644 independent
accepted models; 771 reference disagreements retain the existing independent
normative resolutions. The original grammar oracle is not a claim of new
DTD-validation corpus coverage; M5-corpus supplied that verified dependency.

Fixed seed 1295276112 fuzz passed768 generated/768 reuse/128 invalid/32 mutation/
32 quota/64 cancellation. Official inventory matched 948 declarations. Local
unpublished `1.0.0-m5-preview` package contains net10.0 runtime assets only;
DLL SHA`cc75de18f50cb833c60e95e208c8d23ddf9c385d518a657ebe7ae36084a9dea0`
is identical to the postlease API-tested DLL. Its initially absent-cache
external consumer used only the local feed and identical public source, with
output identical to the source demo. Package SHA
`e6b7fffa966dd5df2d92177721789a4c33a13166f9da15d2e96c6f224e3b00ba`.
No Friend/repository references, catalogs, HTML or later schemas were added.
No benchmark or historical wholeoperation timing was rerun.
Raw results remain ignored for coordinator inspection; only analysis and
summaries will be committed. The local package is retained for coordinator
copying before checkout cleanup.

Earlier M3 streaming timing evidence is historical after M5 SourceCore
repairs. This gate proves cumulative bounded functionality and makes no
updated throughput/allocation claim for those old streaming measurements.
