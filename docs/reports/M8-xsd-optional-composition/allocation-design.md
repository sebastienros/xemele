# Allocation SOURCE proposal — optional XSD composition

Runtime SOURCE PASS reviewed `5160cfadb64de8555d58e392df5c949fd465a4a6` by
`/root/review_m8_contract`; exact independent review is copied bytewise into
runtime-source-review.json. Branch correctness/evidence `db4635c8216493d51ca7c7b42f12e4febed1cfaa`
records1896 disjoint passing rows and preserved original disagreement. Root
independently checked actual XML and command exits, and prepares combined
candidate075b033958f321f0046b5b0996de0cfed8a161b0 separately. Runtime/tests/fixtures
remain byte-identical to accepted5160. This stage is source/design only: no C#
probe build, controls, lifecycle regression, freezing, preflight or counter run.

Task-owned Optional.Probe.csproj explicitly compiles Fixture.cs and
Probe.cs.fixture against accepted Xemele; no shared project edits. Twenty-four
finite synchronous routes use fixed fixture sizes and independent expectations:

| Routes | Operation / semantic checks |
| --- | --- |
|0–3|Fresh internal compiler graphs for explicitly unavailable include, import, empty redefine, annotation-only redefine (multilingual documentation). Root declaration remains; exactly one explicit schema-kind callback, zero streams/documents from absence.|
|4–5|Fresh graphs with same-URI missing then available include/import; both callbacks occur, the available T definition has the expected effective namespace, one stream disposed once. Import permission is retained. No principal parsing in graph routes; child byte parsing is per operation.|
|6|Public resolved substantive simpleType redefine remains mandatory: effective minInclusive2 accepts2 and rejects1. Newly allocated child/resource parsed/disposed once; schema and consumed result ownership included.|
|7|Substantive redefine explicit Unavailable fails SCHEMA.ResourceDenied, one callback and no stream.|
|8|Optional composition with shared MaxOperations1 refuses SCHEMA.Limit before resolver; no callback. This is an independently diagnosed work refusal, not absence or validity.|
|9–11|Optional legacy-null, explicit-Denied import, and no-resolver annotation redefine remain SCHEMA.ResourceDenied; exact callback counts1/1/0 respectively. Legacy ResolveResult adapter invokes Resolve once.|
|12–14|Resolved malformed child -> SCHEMA.Syntax with disposal; resolved wrong namespace -> XSD.SchemaRestriction with disposal; missing import then required type -> XSD.SchemaRestriction with no stream. No failures become absence.|
|15–16|Per-call token cancelled after Unavailable or after returned resource. OperationCanceledException retains exact token; respectively zero/one streams, resolved stream disposed once before propagation.|
|17–20|Public byte-stream compilation of unchanged original a002/a004/schD7/schG8 with exact missing lexical references and expected URI/referrer/kind. Local C dependencies use setup-cached original bytes; per-call principal/resource wrappers and parsing are counted. Exact callbacks3/3/2/2, streams0/0/1/1, complete VALID original instances consumed. a004 is ONLY separately derived W3C4126 VALID: frozen authored INVALID vs observedVALID remains explicit original disagreement in manifest/focused-summary; not a metadata validity pass.|
|21|Public same-URI missing then available include; effective xs:int accepts both2 and1. Two callbacks/one disposal; schema and validation results consumed.|
|22|Precompiled original a004 schema with preparsed unchanged original instance; complete separately derivedVALID result. Schema/DOM setup outside route. No claim that original authoredINVALID matches.|
|23|Resolved optional child read throws per-call owned XmlParseException CALLER.Read; disposal also throws IOException. Exact primary exception object survives, stream disposed once, no schema publication.|

Cold setup owns source strings/DOMs, cached original schema and included bytes,
original URIs/instance DOMs, and precompiled a004 schema. Cold allocation is measured
separately and includes constructor's setup compile; it is not amortized into warm
routes. There is no fixture creation inside steady-state routes except explicitly
owned per-call resolver/stream/resource/exception/cancellation/operation state.
Schema compiler graph builders, retained dictionaries, contexts, immutable output,
and temporary charged Children lists (including added redefine classification)
are operation-owned. No loader/cache state crosses independent compiles.

One first call after setup per route,16 unmeasured warmups, then64 individual
current-thread allocation samples and their aggregate. Sample/report arrays
allocate outside counters. KeepAlive retains each returned graph/schema/exception/
result through the counter boundary; incidental validation results are consumed
and kept alive through their checks. Semantic controls, resolver identity checks
and stream-accounting state are included in bytes/op; this does not isolate an
allocation-free primitive. Every route should have positive owned allocation.
No Stopwatch, workload timer, BenchmarkDotNet throughput, or zero-allocation claim.
The 1024-byte managed-array positive control must observe at least1024B.

Five retained categories hold8 results each: compiled graphs; public schemas;
schema diagnostics; cancellation/caller exceptions; validation results(total40).
Full-GC heap readings before/after each category retain previous arrays/fixtures.
Deltas are coarse process live-heap differences, not attributed object sizes,
peak bounds, native-memory measures or precise retained-memory attribution.
Negative deltas remain visible. Fixed24 routes/64 samples/40 retained outputs bound
the workload; async,x64,AOT,timing and broader corpus remain outside this evidence.

--control runs identical routes/checks/positive/retained objects while every
allocation and heap read is guarded off. Cold/first/warm/aggregate/positive/heap
values must be0. Disabled zeros are never allocation evidence. Both modes emit
runtime/architecture/OS/GC/consumption and runtime/probe assembly SHA256 identities.
Pinned SDK10.0.400/runtime10.0.11, tiering disabled, workstation/nonconcurrent GC.

After independent allocation SOURCE acceptance, prepare.py requires an owned
allocation-source-review.json with resultSOURCE PASS and exact reviewed source
commit. It verifies no runtime/test/fixture diff from5160, no allocation-source diff
from reviewed commit, and builds offline from local NuGet cache: restore/build each
180s maximum, servers disabled, single-worker/shared-compilation false/audit false.
Its disabled control uses accepted supervisor30s work+10s cleanup. Unique output
directories preserve earlier outcomes. Freeze dotnet/runtime/probe binaries,
all tracked runtime sources, exact owned tests/fixture/copyright/metadata/probe,
build configuration, independent source reviews and preparation control/lifecycle.

Supervisor body is byte-identical to accepted particle/type infrastructure.
Success requires reason=completed AND exit0, direct child reaped, owned process
group absent and log closed. Timeout-to-SIGTERM-exit0 remains failure and prevents
the next child. The lifecycle regression, after acceptance only, runs precisely
that owned timeout control plus an independent normally completed child, with
finite TERM/KILL/wait/group cleanup. Counters disabled throughout regression.

Root alone later executes --run <reviewed manifest SHA256>: one prebuilt disabled
control followed by one allocation measurement, total120s with110s work/10s cleanup,
no retry. Frozen hashes are verified before/after; first workload failure stops
sequence. Durable fsynced lifecycle/release records precede raw-result inspection.
--verify-only and --preflight check real process permissions and disabled counters
only after preparation. No isolation/PARK/ACK/lease/global-idle or foreign-project
coordination condition. Worker never launches actual counters.

Raw outputs/logs/XML stay ignored artifacts/M8-xsd-optional-composition, inspected
only after lifecycle release. Commit compact aggregate/min/max/ownership/lifecycle
summaries, no per-sample or all-passing-row payloads. Source review/design/source
hashes are permanent. All execution and performance evidence remains pending.
