# Shared streaming pump

Runtime/test/probe implementation: `b8e5035cf1edfe8b8703a2111d79efdc739e52c7`.
Scoped reviewed correction: `df98050f29526c38b36b7a1eb9c17b59416c0d79`.
Final product SHA256: `6262BA63BEEADF26B711EBEF36A0736D31D9A78DAAD0E782CE2E8A9641AC5153`.
SDK 10.0.400, runtime 10.0.11, macOS 15.8.1 Arm64.

## Consumer protocol

`XmlStreamingPump(options, sourceUri, asyncResources, characterInput)` implements
exactly the frozen sixteen-event bool sink, byte supply/advance and additive async
resource-service interfaces. An internal `SupplyCharacters(ReadOnlyMemory<char>,
bool)` feeds character callers without encoding conversions or invented byte
locations. Public strict/diagnostic adapter policy remains with their owning tasks.
No later public SAX/push/reader adapter is needed to compile this implementation.

Supply retains the supplied owner and its cursor until NeedInput or disposal;
the caller must keep it alive and unmodified. Advance drains existing bounded
projection and core state before decoding/refilling or releasing that owner.
The sink identity is fixed. False accepts one notification and returns Paused;
neither the notification nor its consumed input is replayed. EOF is sticky;
paused final input drains to a sticky Completed/Rejected result. Faults and
disposal reject further use. One operation gate protects supply, advance,
resource servicing, async advance and disposal, including across awaits.
Construction invokes no callbacks, reads or resolver work.

Async mode exposes NeedResource before external work; AdvanceResourceAsync
awaits the verified core's genuine open/frame/read/EOF disposal continuation and
invokes no callbacks. AdvanceAsync loops those services, recreating spans only
inside synchronous AdvanceCore. Callback, cancellation and I/O faults await core
DisposeAsync before propagating the identical primary exception, even if cleanup
fails. A caller manually combining synchronous Advance with resource service
must itself await DisposeAsync after an Advance exception, as required by the
frozen core owner protocol. Synchronous Dispose on an async resource pump rejects;
DisposeAsync is the appropriate owner cleanup boundary. Caller primary streams
are outside pump ownership.

## Projection, locations and bounds

No DOM or input-chunk event queue is constructed. One tag owns ordinary attribute
and namespace rows until the single StartElement callback; rows are cleared at
callback return, including a pause. Core-authored/default provenance is copied,
never inferred. Incomplete values retain immutable M2 parts; injected defaults
use the containing element opening location while the parts keep original DTD
physical locations. Persistent tag names/values/part containers are explicit
allocating owner boundaries, not claimed allocation-free.

Text dispatch borrows core scalar fragments. CDATA start/end remain paired,
including empty sections; EndCData uses the core's actual token-ending location.
A comment or PI retains one quota-bounded lexical body, then delivers one complete
callback. StringBuilder capacity is retained scratch and its final string is an
explicit bounded callback owner. Names, declaration values and projection owners
expire as views when the delivering callback returns.

After DocumentType, an internal pause yields the exact consumed primary prefix
ending at the lexical closing `>`. The pump's original primary scalar cursor
accounts only that consumed prefix using original byte maps and CRLF/surrogate
semantics. Its retained last scalar supplies EndDtd, including after asynchronous
external subset suspension. No URI or byte offset is synthesized from a string
projection. Effective M2 model collections are traversed with collection/index
cursors in frozen order; content particles are projected with an iterative
one-path stack rather than recursion or a list of all declaration notifications.
One bounded declaration string is delivered at a time. Notation provenance is
null. Model/parts retain the original core-owned physical source locations.

Decoder framing uses a six-unit prefix and one submitted byte until declaration
validation permits a body encoding switch. Decode output is 4096 chars and
4096 long byte mappings; surrogate pairs are never split by output capacity.
Parallel prefix storage is six chars/six mappings. These array payloads total
45,116 bytes, separate from array headers/core/decoder/tag/model owners. Tag
arrays begin at eight rows and grow only within the integrated core's per-tag
limits. Comment/PI/model lexical scratch, intern tables and resource owners are
bounded by existing M2 quotas. There is no buffer whose capacity grows with
ordinary streamed body length.

Recovery suppresses successful EndDocument. All fatal core/decoder/declaration
diagnostics are delivered once and registered with the recovery snapshot before
notification; terminal diagnostic identity/order survive pauses. Diagnostic
collections are owned references; a public completion result must take its own
snapshot. Callback exceptions do not become diagnostics.

## Checks

- Both prepared Release projects built with pinned executable, single-node,
  no-server/no-shared-compilation controls: zero warnings/errors.
- At `b8e5035`: 351 selected tests passed, zero failed/skipped; includes 30 pump
  tests plus affected async resources/resolution/entities/encoding/metadata/
  recovery/external declaration/conformance groups. `affected-tests.xml`.
- The same source matrices cover every primary split with every notification
  paused, UTF-8/UTF-16 LE/BE/BOM and character input, multilingual/surrogate/CRLF,
  legacy declaration switches, empty/text CDATA, complete comments/PIs,
  namespaces/default/incomplete values, effective DTD projection, skipped
  references, sticky EOF/fatal diagnostics, faults/cancellation/reentrancy,
  async-only delayed open/read/disposal, manual resource service, gates and
  exact DTD/end/entity locations. Preserve passing unchanged matrices.
- The independent review found two blockers at `b8e5035`: missing fatal decoder
  diagnostic in the recovery snapshot; incomplete default item used DTD value
  location. Both were corrected in `df98050`. Coordinator reviewed the scoped
  correction. Postcommit changed tests: 4 passed, zero failed/skipped,
  `postcommit-correction-tests.xml`; three invalid encoding/recovery identity/
  order cases plus the all-split incomplete default opening/part location test.
  The broader corrected PumpTests class also passed 25/25 before committing its
  identical source; `correction-tests.xml`.
- Direct approved compiled API checker: 639/639 at both revisions; duplicate-
  checked canonical union of every task-owned docs/api fragment. Final checker
  record and product identity: `final-direct-api.json`.
- Official API inventory wrapper failed because this isolated checkout lacks
  the pinned XML corpus fixture `xmlconf/eduni/errata-2e/E14.dtd`. No source,
  corpus manifest or exclusion changed. Coordinator authorized the unchanged
  approved direct checker and has separately verified the official wrapper on
  a hash-provisioned integration checkout.
- Task validator passed: 142 records. `git diff --check` passed.

The broad 351-test selection inadvertently ran inherited resolver/attribute
allocation tests before an exclusive lease. Their correctness results remain
recorded, but their measurement output is not fresh pump performance evidence.
No repeat of those inherited measurements is requested. Initial development
oracles were corrected to count Unicode scalar columns (not UTF-16 units) and
include the existing prolog whitespace event; `initial-tests.xml` preserves the
initial column-oracle failure, not a passing result.

## Completed allocation evidence

Exclusive lease `m3-streaming-pump-df98050` was granted, then revoked when the
coordinator discovered an unrelated host benchmark. The allocation child had
already completed with exit 0; the coordinator explicitly retained its valid
own-process GC counter evidence. The subsequent benchmark child was stopped
with SIGTERM (exit -15) and its pilot timing is invalid. There was no retry,
source/options change or build during the lease. RELEASED/nochildren was sent
before reviewing reports. Raw stdout/stderr and interrupted artifacts survive.

`allocations.json` records the exact final revision, product hash, runtime,
fixture hash and actual process-wide `GC.GetTotalAllocatedBytes(true)` results.
Counters-disabled `harness-control.json` proves the same prepared harness,
but its zero-valued counter fields are not measurements.

| Scope | Work | Actual bytes |
| --- | --- | ---: |
| Byte decode/core/borrowed public Text dispatch | 512 x 4096-byte segments after 128 warmups | 0 |
| Character core/borrowed public Text dispatch | 512 x 4096-char segments after 128 warmups | 0 |
| Escaped allocation positive control | byte[1024] | 1,048 |
| Cold two-fixture setup/owners, including first dispatch | two contexts plus source fixtures | 128,256 |
| Owned end-to-end byte document | 32 x 8 KiB text plus declaration/tag/CDATA/comment/PI | 1,685,504 (52,672/op) |

Cold setup is separate from the warmed primitive, and is not claimed free.
Fixed pump capacities stayed `[4096,4096,6,6,8,8]` after another 16 MiB body.
Full-process retained managed bytes stayed 401,184 before/after that stress;
this is a same-process growth control, not a claim that all 401,184 bytes are
parser-owned. Decoder/core/tag setup, name/value persistence, comment/PI owners,
DTD model projections and async continuation/I/O owners are distinct costs.
No new scan/matcher optimization or throughput improvement is claimed.

## Isolated benchmark completion

Fresh timing-only lease `m3-streaming-pump-df98050-timing`, unchanged pinned
executable/prebuilt probe and source `df98050`: all three InProcessEmitToolchain
cases completed, process exit 0, total 18.75 seconds within the 120-second bound.
No allocation repeat, build, generation, source/options/configuration change or
retry. RELEASED/nochildren was reported before inspecting output. The coordinator
confirmed known unrelated benchmark/test processes absent and the peer worker
parked for this window. Earlier revoked pilot output remains separate and invalid.

BenchmarkDotNet 0.15.8, SDK 10.0.400/runtime 10.0.11 Arm64, concurrent workstation
GC, three warmups and three measured iterations, caller input generation outside
measurement. Fixtures and results are consumed by exact semantic counters.
Raw iterations, distributions and environment metadata:
`benchmark-isolated.stdout.txt`; CSV/Markdown/HTML exports and full log in
`BDN-isolated-df98050/`. CPU model and process-priority adjustment were unavailable
under sandbox permissions (sysctl/priority warnings); all cases still completed.
No affinity/power-control claim is made. Timing is provisional single-host data,
not a comparative optimization or cross-architecture release claim.

| Scope | Payload per operation | Mean | MemoryDiagnoser |
| --- | --- | ---: | ---: |
| BorrowedBytes | 4096 bytes into established text context | 185.724 us | 0 B |
| BorrowedCharacters | 4096 chars into established text context | 145.423 us | 0 B |
| EndToEndBytes | 8 KiB body plus lexical/structural fixture | 284.024 us | 52,673 B |

MemoryDiagnoser reports a dash for zero allocations; the independent process-wide
probe proves exact 0 B with its positive control. The owned end-to-end BDN value
is within one byte/op of the independent 52,672 B/op counter scope. Borrowed
99.9% confidence intervals are wide (111.442–260.006 us for bytes and
27.709–263.136 us for characters); no throughput improvement or regression is
inferred from these three-iteration samples. BDN owned fixture excludes setup
input generation and includes per-operation context, lexical/tag owner creation
and disposal.

## Handoff

A1/A2/A3 pass on the implementation branch. Integrated verification remains the
coordinator's responsibility; this worker leaves `integration_revision` null and
sets `implemented`, not `verified`. Preserve existing same-source passing matrices
and valid allocation/BDN evidence. Broader very-large-stream targets and x64/
Arm64 release confirmation remain performance/release follow-ups. The official
inventory wrapper needs environment-only pinned corpus provisioning; direct
approved compiled API comparison already matches 639 members.
