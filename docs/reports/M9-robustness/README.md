# Robustness evidence status

Current [ordinary corpus reassessment](current-summary.md) preserves the original scope/dispositions after verified M9runtime repairs. Fuzz planning/execution remains explicitly deferred; mandatory duration/coverage/crash-positive gates remain pending.

# Actual initial fuzz qualification

SharpFuzz 2.3.0 compiles on pinned SDK 10.0.400/.NET 10.0.11. Its instrumenter
rewrites only a disposable artifact copy of Xemele.dll. The actual returned names
identify 600 types, not methods or covered branches. The original production DLL
hash stays unchanged. The instrumented XML byte-decoder/parser target successfully
replays three committed seeds (well-formed, internal DTD, truncated).

The original instrumenter labeled the count `methods`; the output label is corrected
to `types` after inspecting the returned qualified type names. This reporting-only
correction does not alter instrumentation, production code or the replay target.
The tested revision and exact finite process receipts remain in qualification.json.

This establishes package/build/instrumentation/single-file replay compatibility.
A subsequent local AFL 2.57b engine smoke completed 41,677 executions and found
370 additional coverage paths (373 total), with zero observed crashes/hangs. The
owned process group exited cleanly. Its failed standalone map-observer attempt is
also retained. Crash-positive detection is still unqualified, no sustained duration
credit is claimed, no branch percentage is established, and other targets remain
unqualified. Fuzz planning/execution is now deferred by direct human request.
The required sustained campaigns and coverage target are still outstanding.

Primary tool documentation: [SharpFuzz](https://github.com/Metalnem/sharpfuzz) and
[its libFuzzer integration](https://github.com/Metalnem/sharpfuzz/blob/master/docs/libFuzzer.md).
Library API inspection used the dotnet-inspect skill; no catch-all exception filtering
or engine `RunAndIgnoreExceptions` path is used.
