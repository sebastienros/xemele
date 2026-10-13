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
No native coverage-guided engine has run, no campaign duration has accrued, no
branch coverage percentage is established, and other major targets remain unqualified.
The required sustained campaigns and coverage target are still outstanding.

Primary tool documentation: [SharpFuzz](https://github.com/Metalnem/sharpfuzz) and
[its libFuzzer integration](https://github.com/Metalnem/sharpfuzz/blob/master/docs/libFuzzer.md).
Library API inspection used the dotnet-inspect skill; no catch-all exception filtering
or engine `RunAndIgnoreExceptions` path is used.
