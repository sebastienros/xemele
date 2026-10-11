# Correctness and preparation

Candidate runtime/source commit: 802a2a0e746820cb73f388b6804e9740fafacdd5.
Pinned SDK 10.0.400 / .NET 10.0.11, local macOS Arm64.

- Release runtime/test build uses `-m:1 -nr:false -p:UseSharedCompilation=false`.
- Focused namespaces `Xemele.Tests.M4Navigation` and `Xemele.Tests.M4Model`:
  55 passed, 0 failures, 0 skips. Navigation includes two differential vectors
  over every context and thirteen axes, exact quotas, mutation stability,
  declared/default/duplicate IDs, incomplete provenance and cancellation.
- `python3 tools/planning/tasks.py validate`: 148 tasks validated.
- `python3 docs/reports/M4-model/check_api.py --dotnet <pinned-dotnet>`:
  actual 893 members; model 173; preserved earlier 720; two corruption controls
  rejected. Navigation adds zero public members.
- Standalone friend-seam sample and fresh packed production consumer:
  `logical text=12.5; retained attribute=v; language=en; axes=13`.
  Isolated consumer global.json/cache, Release build with 0 warnings/errors.
  Pack reported existing missing-readme advisory; restore printed macOS
  CSSM_ModuleLoad diagnostic but completed successfully.

Initial test builds correctly rejected direct mutation of temporary struct
handles and omission of xUnit cancellation tokens. Those test issues were fixed.
The first focused run had 51/52 passes: external-reference fixture was rejected
by the XML parser before navigation. It now uses the verified unresolved-reference
pattern. The cancellation boundary probe then exposed a missing completion
checkpoint; fixed before candidate, now 55/55 passes. No excluded tests or relaxed
acceptance criteria conceal these failures.

Counters-disabled control: 65,536 warm and measured primitive groups,
consumed=1,703,936 in both, input items=389, input chars=6,123. It checks
normalization, repeated snapshots and identical linear/indexed order semantics.
No GC allocation counters are called in control mode. Discovery confirms exactly
10 benchmark cases in one declared ShortRun InProcessNoEmit job, 3 warmups,
3 iterations, 1 launch. Fixture and expected-value work is outside measurement.

`measurement-plan.json` records exact command arrays and a maximum 930-second
exclusive sequence (allocation 30, benchmark 900); `run_measurements.py` checks
frozen source/generated inputs/binary closure and pinned runtime hashes before
and after. No build, generation, CLI job flags or unrelated measurements occur
in that sequence. Raw counters/logs/results stay in ignored artifacts; only
summary/analysis will be committed after actual granted runs.

Allocation and timing: not run, pending independent review and explicit prepared
exclusive coordinator lease. No claimed passing allocation evidence yet.
