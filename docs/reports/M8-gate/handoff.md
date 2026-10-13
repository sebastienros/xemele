# M8-gate pinned ILLink host source repair

SOURCE ONLY. Fresh preparation/check/counter release remains with root.

- Worktree:`/Users/sebastienros/.codex/worktrees/29a0/xemele`; branch`codex/m8-gate`.
- Source closure:3079 files, SHA256`46bddcd748c85e95ddd98549e4f663594707c8d5e6e4ccbadb982c187868753c`.
- checks.py SHA256:`931b045bdc01867a96b03d6a9e83dba7cff80c78e38b2860e43c889890fba414` (unchanged).
- Packet SHA256:`78dc58760c3f3a2162973d16cf01752a7b85b323bffacd95b1bfc8e68326eb9b`.
- Recipe SHA256:`ec10ad06a7b3efd58e7184a1eb1f3cccdbb3d2560660cb1054e8ee47e79b507d` (unchanged).
- Wrapper template SHA256:`f059dceb06a661d5790aac35f7380ad699c073afc467ae8ee6f68d8f15107259`.
- Expected in-memory rendered wrapper SHA256:`359190cdf414c76b2db158ca8a73851f1522237373f1959a79dc6c001a3e0d25`.
- Linker input pins SHA256:`903f5ec1f29ad7eb7f0b5273fc0c706567b481a21573517f5bad1add2dccf3d5`.
- package_inputs.py SHA256:`38c249ac2edc468778f184eee6a992deb0d0d620f1524404da554ae0c51b7622`.
- identities.py SHA256:`b21a4291f472aea4de96e27de89b13ae12695f84f644923e3ad78826e7cc80ad`.

Consumer BeforeTargets=_RunILLink selects owned linker-host/dotnet with ToolPath/
ToolExe properties, requiring SDK DOTNET_HOST_PATH. Layout renders/binds pinned
Python/dotnet/exact private ILLinkDLL+deps/runtimeconfig/task hashes; wrapper
execs pinned dotnet --fx-version10.0.11 with unchanged original linker arguments,
Disable roll-forward, inherited deadline and process group. Original tool/cache/
runtimeconfig bytes, ToolTask operation and warnings-as-errors stay unchanged.
Freeze/check binds rendered bytes/mode/consumer target/private tool identities.
No new commands or deadlines; exact package/fresh-private-cache/offline/noProjectRef/
DLL-equality/managed-trim-AOT expected outputs preserved. Wrapper itself UNRUN.

Primary source review:
[ILLink LinkTask](https://raw.githubusercontent.com/dotnet/runtime/v10.0.11/src/tools/illink/src/ILLink.Tasks/LinkTask.cs)
constructs original DLLargs;
[MSBuild ToolTask](https://raw.githubusercontent.com/dotnet/msbuild/main/src/Utilities/ToolTask.cs)
uses explicit ToolPath/ToolExe before default generated tool path. Installed
10.0.11 targets supply properties and only reset them without DOTNET_HOST_PATH.
NativeAOT local targets RunILLink=false/directExec native tools/ilc; no runtimeconfig
in that toolfolder. Source review only; NativeAOT build/run/support stillUNRUN.

Root attempt4 at24e694b8e0946085fe5c876ee570e04ecc59a2ac/session47687 actually
exited1 command46 trimmed publish:MSB6006/dotnetexit150 andNETSDK1144 because
illink runtimeconfig requested10.0.11-servicing.26373.116 withDisable versus pinned
installed10.0.11. Record46normal/1failed/4unrun stays failed. CPM-corrected managed
restore/build andtrim restore passed only declared preparation scope; managed
consumer output checks did not run. Root freshly verified56groups absent and
inspected/deleted raw/failed metadata/partialexternal, keeping canonical caches.
Durable failure record SHA`b59f70d9732e8252f8d663631fb82b109e54723d1d2071fa5bda4f9a0d62c561`; cleanup SHA`688a29c1ea595b71fc7dd4b7272c374a28465c9cfe9d7067fa722c6866adcee8`.
Earlier attempts1/2/3 and39/10/7unrun counts remain. No certificate/gate credit.

Exact changed paths:
- docs/reports/M8-gate/Consumer.csproj.template
- docs/reports/M8-gate/LinkerHost.py.template
- docs/reports/M8-gate/README.md
- docs/reports/M8-gate/handoff.md
- docs/reports/M8-gate/identities.py
- docs/reports/M8-gate/linker-inputs.json
- docs/reports/M8-gate/package_inputs.py
- docs/reports/M8-gate/source-packet.json

Source-only11 PythonASTs plus in-memory wrapper AST/hash reconciliation;
no generated layout, wrapper, imports of workload entrypoints, builds, tests,
controls, probes or counters ran by worker. Task JSON untouched. Zero owned jobs/
PIDs/sessions/cells. Root sole primary/main/state/integration/launch writer.
Commit clean thenidle; final root review needed before any fresh executable release.
