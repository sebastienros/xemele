# Correctness and compatibility evidence

Tested implementation: `5dae4fb3eeb06d882016cae111fe5c28c5c76e9e`.
Local host: macOS 15.8.1 Arm64, SDK 10.0.400, runtime .NET 10.0.11.

Release builds of tests, standalone probe and sample passed with zero warnings
and errors. Restore used the existing local NuGet package cache. Commands used
`DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER=1`, `--disable-build-servers`,
`-p:BuildInParallel=false -m:1 -p:UseSharedCompilation=false -p:NuGetAudit=false`.
The test/probe builds used `--no-restore`; sample restore used
`--source /Users/sebastienros/.nuget/packages`.

```
dotnet tests/Xemele.Tests/bin/Release/net10.0/Xemele.Tests.dll -namespace Xemele.Tests.Push -noColor -result-xml docs/reports/M3-push/focused.xml
```

17 passed, zero failures/skips. Covers every partition of `<r/>` and
`<r>x</r>` (136 partitions total), every two-chunk split and one-byte chunks for
three documents, complete event/attribute/default/namespace/DTD equality, copied
caller input, immediate notifications, all lifecycle and concurrency guards,
completion and ordinary callback fault identity, diagnostic identity,
cancellation, resolver I/O/cleanup identity and ownership, fixed scratch capacity.

```
dotnet tests/Xemele.Tests/bin/Release/net10.0/Xemele.Tests.dll -namespace Xemele.Tests.Sax -namespace Xemele.Tests.StreamingPump -namespace Xemele.Tests.Reader -namespace Xemele.Tests.Callbacks -class- '*AllocationTests' -noColor -result-xml docs/reports/M3-push/preservation.xml
```

58 passed: 33 pump, 20 SAX, 5 callback/view tests. The Reader namespace in this
command has no tests; the actual ReaderRuntime namespace was run separately.
Allocation-observation tests were deliberately excluded pending the serial lease,
not counted as passing or replaced by the counters-disabled control.

```
dotnet tests/Xemele.Tests/bin/Release/net10.0/Xemele.Tests.dll -namespace Xemele.Tests.ReaderRuntime -noColor -result-xml docs/reports/M3-push/reader-preservation.xml
dotnet tests/Xemele.Tests/bin/Release/net10.0/Xemele.Tests.dll -class Xemele.Tests.Syntax.SyntaxTests -noColor -result-xml docs/reports/M3-push/syntax-preservation.xml
```

11 reader and 53 M1 strict-syntax tests passed with zero failures/skips.
The initial combined namespace/class filter selected zero tests; it was replaced
by these separate nonempty runs. The final reports confirm actual test counts.

Approved checker:
`/private/tmp/xemele-m3-integration/docs/reports/M0-contracts/ApiCheck/bin/Release/net10.0/ApiCheck.dll`.
Emit of the runtime assembly yielded 711 public member rows. Five XmlPushParser
rows form `docs/api/M3-push.txt`. All task fragments were composed into an
untracked temporary union, sorted by member with id=member, duplicate rejection,
and canonical JSON. `ApiCheck.dll compare` on the Release runtime assembly and
that union passed: **Matched 711 compiled members**. This compares the actual
assembly and earlier public surface; full official corpus discovery belongs to
coordinator integration and was not claimed here.

```
dotnet samples/M3-push/bin/Release/net10.0/PushSample.dll
```

Output was start root, five scalar text notifications spelling hello, end root,
complete. Arbitrary scalar-boundary text splitting is permitted by the contract.

```
XEMELE_PUSH_REVISION=5dae4fb dotnet docs/reports/M3-push/Probe/bin/Release/net10.0/Push.Probe.dll --control docs/reports/M3-push/control.json
python3 tools/planning/tasks.py validate
```

Control passed all assertions including 16 MiB text and owned end-to-end
completion counts. Its GC counters were disabled: zero report fields are
placeholders, not allocation or retained-memory observations. Planning validation
passed all 147 tasks. The first control used the short implementation revision; after the probe-only
correction, the counters-disabled control was rerun at full revision
adc750dec4a48b91870f789396ba05741fd88495. The final control.json preserves that
revision and the identical runtime assembly SHA-256. Active measurements and
benchmark guards/results are recorded in performance.md.

Before the frozen revision, one invalid-byte test expected rejection during a
single nonfinal byte Append. The decoder's encoding sniff waits for more input or
EOF, so the test was corrected to verify rejection at Complete. An unsupported
BenchmarkDotNet UnrollFactor attribute was removed after a probe compile error.
Three original non-single-node restore/build attempts stalled and were terminated
with exit137; subsequent scoped single-node offline builds passed. None of these
attempts is reported as a successful check.
