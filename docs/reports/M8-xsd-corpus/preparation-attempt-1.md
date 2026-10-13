# Root preparation attempt1

Root executed the accepted b0119e7ed712779723dd2a5c29e9619de83fd328 SOURCE through its family wrapper (work1800/hard1810). Worker did not execute or duplicate preparation. Root session42492 was terminal exit1; durable cleanup was recorded at maincdbfd7575 before logs were inspected.

Commands0..5 completed clean exit0. Command6 `test_supervisor.py` executed25tests:24passed/1failed. The sole failure was `test_unresolved_prelaunch_intent_cannot_report_clean`: expected cleanupError TimeoutError, observed TimeoutExpired from the bounded process-table query. Pending intent and allOwnedGroupsAbsent=false were retained; preparation stopped and remaining commands7..9 did not run. Root found no runtime failure in this attempt. Source correction accepts only TimeoutError/TimeoutExpired while preserving the same false-clean, pending-intent and finite deadline assertions.

Per root inspection, all22registered owned groups were absent, the direct child was reaped, logs closed, and no root pending intent remained. This records root evidence, not new worker checks or a corpus-conformance pass. Helper, runtime, adapter, approval and tested-input binding source are unchanged.

Raw artifacts remain untouched and ignored at `artifacts/M8-xsd-corpus/b0119e7ed712779723dd2a5c29e9619de83fd328-controls-2586a01a6c6c4a43aa00e46f4f59679d/` and root's preparation directory. No raw output is copied into this report. No corrected retry, full campaign or counters executed by the worker. Root review is required before its fresh bounded attempt; task remains in_progress/A2failed.
