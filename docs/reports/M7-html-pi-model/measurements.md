# PI owned persistence allocation evidence

Measured revision `ab5b8d918251d7da4b9f4497f80ed2251c89e7c2`; actual
SDK10.0.400/runtime .NET10.0.11, Arm64. No BDN timing optimization was introduced
or claimed. The standalone synchronous thread counter is scoped to explicit owned
factory/graph boundaries and existing-node mutation; fixture strings are prepared
outside warm measurement intervals. No later parser/selector implementation is
required or measured.

| Domain | Numerator bytes | Denominator | Interpretation |
| --- | ---: | ---: | --- |
| Cold document + first owned PI |1112|1|Document/name dictionary/node setup included|
| Positive allocation control |1048|1|1024-byte array plus object overhead|
| Existing PI Value mutation |0|16 batches ×4096|0 B per operation after1000 warmups|
| Owned PI, existing target |1048576|4096|256 B/node; target/data strings reused|
| Owned PI, new target |87768|256|342.84375 B/op inclusive first dictionary backing storage and growth on a newly prepared owner; input strings prepared beforehand|
| Owned PI graph |2020800|100|20208 B/graph; root/template and65 PIs included|
| Retained whole heap |3934000 delta|100 retained documents|Inclusive heap delta; array/runtime roots included, not exact per-node retained memory|

The probe consumes nodes/owners using KeepAlive, includes NUL/terminator/supplementary
Unicode data, repeated and distinct targets, document/root/template placement,
and positive allocation control. The retained before/after heap counts were346128
and4280128 bytes. No throughput, x64 or end-to-end parser claim is made; composed
engine validation remains with later owning tasks.

Root granted exclusive lease m7-html-pi-model-ab5b8d at coordination289022d.
Prepared manifest SHA256
`ebac06408e7a59aebd55f31fb6213af6ff7df3e18601c6bdda6ebcc08f687f4a`
covered3633 sources/generated/corpus files,10 binaries and3852 SDK/runtime/host
files, plus host and full effective child environment (opaque SHA/per-key hashes;
SHLVL1). Root independently matched closure; only session CODEX_THREAD_ID differs
between coordinator and worker captures. Exact intended wrapper controls accepted
positive/final-positive, rejected unknown environment and proved no-child.

Total bound220 seconds reserves initial35, allocation60, final35 and conservative
cleanup. Sole prepared command executed exactly once, no builds/tests/source
writes/retries or log inspection during lease:
`python3 docs/reports/M7-html-pi-model/run_measurements.py --exclusive-grant`.
Lifecycle was
`{"exit_code":0,"final_freeze":0,"initial_freeze":0,"no_child":true,"phase":"complete","runs":[{"exit_code":0,"phase":"allocation"}]}`.
Initial/final manifest SHA remained identical; groups were killed/reaped/inspected,
including identity children. Root durably released at de2b4c0 before this worker
or root inspected raw allocation output. Raw outputs remain ignored under
artifacts/M7-html-pi-model, with only this concise analysis committed. No measured
runtime/probe input changed after the run.
