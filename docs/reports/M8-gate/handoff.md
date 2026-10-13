# M8-gate canonical offline feed source repair

SOURCE ONLY. Fresh preparation/check/counter execution still withheld.

- Worktree:`/Users/sebastienros/.codex/worktrees/29a0/xemele`; branch`codex/m8-gate`.
- Source closure:3077 files, SHA256`6bb822c2858d015f5549219c4d06276b39ace527c13f6b53ccbe4f7260906f2a`.
- checks.py SHA256:`931b045bdc01867a96b03d6a9e83dba7cff80c78e38b2860e43c889890fba414` (unchanged).
- Packet SHA256:`5b87b6cd0dd50281101c51e2ca7daf790674bb2f58f99df19c9bb9bab64f7185`.
- Contained recipe SHA256:`ec10ad06a7b3efd58e7184a1eb1f3cccdbb3d2560660cb1054e8ee47e79b507d`.
- guard_controls.py SHA256:`15e0be3a0b656b83e62d24e4180e27fa87c03ceca5e8df1070e77e066c090b95`.
- feed_controls.py SHA256:`9a447fb3fc21977a1e064049e6058a48f756ffd309c087587f192f1087965e61`.
- package_inputs.py SHA256:`6633be06e1d36580523caaab64735249fc6a317b84ddaf3124b500acc95d9faa`.

Canonical preflight completes BEFORE feed links. It binds directory-derived ID,
version, casefold target, canonical source path, length andSHA. Every generic alias
needs exactly one same-directory canonical peer and identical bytes; missing peers,
differing bytes, unexpected paths, filename/identity mismatch andcasefold target
conflicts stay fatal. GeneratedXemele.0.8.0-integration is reserved. No first-wins
selection, nuspec parsing or invented version normalization. Whole-cache endpoint
byte guards and actual offline restore remain authoritative. Full catalog stays
active in memory; only concise counts/digests survive, no raw catalog archive.

Read-only audit:7415 archives,7387 unique canonical targets,28 identical aliases,
zero canonical conflicts/unexpected paths. Six generic-name groups contain16
distinct-version cache copies; all match canonical bytes, and onlythese16 were
checked against nuspec metadata. They are separate top-level regular copies,
not embedded archive entries. Exact16 case lengths/SHAs are in source-packet;
case identity digest`c7d4358abc31e6924fa8e4bb0ec6d5ab9c3b28f816727efba270e4bf663439b5`.

Five focused actual-helper filesystem controls are source-only/UNRUN: different
versions with same generic name, identical alias, conflicting alias, missing peer,
andidentity/path mismatch lacking exact counterpart. Tiny owned fixtures are removed;
no product/cache mutation. Existing preparation command now proposes16 controls
(11existing+5new); actual old11 results are not promoted to new16 passing.

Layout command600 seconds; preparation14000work+180reserve. Child work12180+
51cleanup510=12690 leaves1310shared work. Root outer14780/14800, same parent
28c344b25ef8653abc8cff38de60c63f5b36879179c7092d58d94a94cdc36a13 andobserver
23be0a30713915c09f7fbadae61461a66bba3274e853cff8917f20178e44eb99 hard600.
Check/identity/counter bounds unchanged. Initial bindings count in fixed shared
clocks; no deadline reset, retry or expected-behavior weakening.

Root attempt1 remains failed command11/MSB1009 (11normal/1failed/39unrun).
Root attempt2 at9e9c0478f70d83940eb72e5c0e4e36245e16f98b/session57039 remains
failed command40/ambiguous filename (40normal/1failed/10unrun). All18 project
restores/builds and native selection completed only their declared preparation
scope; no package/native-consumer/full-suite/counter pass. Root freshly verified
50groups absent, inspected/deleted raw, and preserved primary record
/Users/sebastienros/github/xemele/docs/orchestration/M8-gate-branch-prepare-attempt-2.json SHA`4bb0f8e98e9c3a197b84692e6ca19ce061cff6be787c32bfb10df644258c7fd2` atcommit
2861fb9595839d16ae6a1598b8d408bcb86c7574. No next-phase certificate/gate credit.

Exact changed paths:
- docs/reports/M8-gate/README.md
- docs/reports/M8-gate/feed_controls.py
- docs/reports/M8-gate/guard_controls.py
- docs/reports/M8-gate/handoff.md
- docs/reports/M8-gate/package_inputs.py
- docs/reports/M8-gate/recipes.py
- docs/reports/M8-gate/source-packet.json

Worker only source reads/hashes/11 ASTs; no builds, imports of workload
entrypoints, tests, controls, probes, counters, chats or actors launched. Task JSON
unchanged. Zero owned jobs/PIDs/sessions/cells. Root remains sole main/primary/state/
integration and actual-counter writer/launcher. Final source review/release required.
