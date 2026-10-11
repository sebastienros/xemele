# Async composition verification

The verification join adds no runtime members. Its public surface is the already
verified child fragments M3-async-writer, M3-async-serialization and M3-async-input;
no duplicated or empty public API fragment is retained. Official compiled inventory
matches all 720 members on the combined tree.

At `8cc0104f44b7ab84ed0262d409d35760942e6f33`, Release tests build with zero warnings/errors. The combined
AsyncComposition/AsyncInput/M3AsyncWriter/M3AsyncSerialization run passes 98 cases,
zero failures/errors/skips: 14 join cases, 31 input, 19 writer, 34 DOM serializer.
The join's independent output vectors cover UTF-8 and both UTF-16 byte orders,
BOM on/off and mixed-content indentation. Generated output feeds delayed async-only
SAX, reader and DOM input. All twelve async writer members produce the same owned
DOM/save result. Controlled streams actually yield and input returns at most three
bytes; synchronous I/O throws. Cancellation, primary I/O/callback identity,
reader fault/current invalidation, no implicit serializer flush, explicit writer
flush and caller ownership are asserted. All handler/delegate Invoke return types
are void. Test fixtures own their bytes/events; no borrowed view survives an await.

The standalone sample compiled and ran at 10251a8bdc148ed9337b7a16499367abc4a40dbe:
DOM async load/save, SAX, reader (9 tokens), and forward async writer agree:
`<r>hello</r>`. Subsequent edits affect tests only; sample/runtime closure unchanged.

Earlier attempts are not passes: initial 28428c1 test/sample compile had wrong
CDATA override signatures and instantiated abstract XmlSaxHandler; 10251a8 tests
compile failed two missing test cancellation tokens. The stale assembly selected
zero tests and proves nothing. At 83075ac, 11/14 tests passed; three expected strings
omitted preserved prolog LF in indented SAX/reader output (failed XML retained).
The checker rejected the temporary empty API fragment; the join has no new API,
so that file was removed rather than duplicating child IDs. Final actual checks
resolve these consumer/harness issues without runtime edits or gate changes.

## Preserved child measurement evidence

No runtime scan/transport implementation changes occur in this join. Relevant
actual evidence remains in each verified child's task and reports; it is referenced
rather than rerun. These measurements distinguish borrowed primitive work from
owned setup, linked tokens, real asynchronous continuation costs and BDN scope.

| Verified child | Actual isolated evidence | Other scopes/limits |
| --- | --- | --- |
| M3-async-writer runtime8011920, diagnostic824470c | 80000 default-runtime API calls and1000 whole batches0B, positive280B/group | linked80B/call, bounded8192scratch; historical helper128B failures retain unproven CLR attribution; six BDN cases inclusive9.67–13.63KiB |
| M3-async-serialization runtime1747815/probe7cee8bb | 48000 scalar validation calls both counters0B, positive512000B | short/long1384B/document setup, real suspended14888.8B/document,8192scratch, retained16808B and completed owner released; six BDN cases; original wrapper ps failure retained, probe exit unprinted/inferred only |
| M3-async-input runtime3679687/probe9e9da61 | 4096 buffered characters process-wide0B, positive4120B | real1024 suspended short reads320B/call;32active samples through8MiB peak8576B; four BDN cases report buffered2B, no subtraction/zero claim for that scope |

All raw BDN results and known failures remain honest. Shared-host latencies are
informational, with no speedup/regression/repeatability claims. Full1GiB adapter
qualification, corpus equivalence and fuzzing remain mandatory M3-corpus work;
release x64/Arm64 hardware qualification remains M9. No requirement is waived.
