# M6 adapter results

Completed private branch behavior: real forward streaming XmlReader adapter and
immutable read-only BCL XPathNavigator capture. The reader owns exposed strings,
attributes and namespace declarations; default leaveOpen preserves the underlying
reader, and the primary stream remains caller-owned even when leaveOpen=false.
Logical text classification coalesces one run with one underlying lookahead;
CDATA framing and source URI changes retain boundaries. Navigator clones share
captured rows, names, namespace scopes and typed/xml:id indexes, with independent
cursors. No live document handles remain in the completed capture. Inherited BCL
XPath evaluation is separate from Xemele's query engine/limits.

Correctness at3a0ba1e4e9fc99f96fadf816050a5ca102dc4d49:23 focused tests all passed,
zero errors/failures/skips, actual10.0.11. Six sync/async chunk1/2/4096 cases each
check seven BCL logical-text/whitespace/xml:space/CDATA event sequences. Other
checks cover all required cursor members, attribute positions/depth/misses,
namespace scopes, empty events, EOF/error, ownership, asynchronous overlap,
caller I/O/cancellation, redirected entity effective xml:base, Unicode,
coalesced navigator text, snapshot mutation isolation, clone identity, typed IDs,
incomplete rejection and unsupported mutations. Independent public sample passed;
actual57adapter API rows exactly equal the frozen golden. Full private-base
compiled API union1005=prior948+57 matched. Later integration adds resource-kind
rows outside this branch; this private count is not a combined-main claim.

The runtime source/generated/binary hashes remained unchanged for the subsequent
owned Python/plan/docs corrections. Prepared measurement revision was
4b797dda27080f48151cfa2c6bb896cc739934bb. Frozen manifest SHA256
`db2f1ae5f6ea05b4c41ca8d02678618ada39ee01af07f5f38c0f8b07c6fcc6f5` covered
5084source/config/generated files,150binaries,3852SDK/runtime/host files, host
identity and COMPLETE effective child environment. Only proven volatile SHLVL was
explicitly pinned; agent-specific CODEX_THREAD_ID remains correctly frozen.
Secret/other environment values are neither printed nor persisted; safe per-key
hashes allow diagnosis. Host:Apple M4 Pro, Arm64,14cores,48GiB, macOS15.8.1;
SDK10.0.400, actual.NET10.0.11. Explicit --fx-version10.0.11, runtimeconfigs10.0.11,
roll-forward disabled, tiered compilation/PGO/ReadyToRun disabled.

The first lease at8c5cd74 ran once and stopped BEFORE measurement:initial/final
identity1, wrapper1, runs[], nochildren. Safe diagnosis proved solely SHLVL
restored the exact previous complete environment hash; Python child startup
changed0keys. Durable release preceded diagnosis. Exact intended sole-command
--identity-control positive checks passed; unknown environment key rejected.
One additional identity control correctly rejected an inadvertent concurrent task
metadata edit at final check; a fresh sequential postcommit control passed.
No failed measurement was retried under that lease.

Corrected durable lease m6-adapters-4b797dd was granted on main532e477. The exact
wrapper ran ONCE:initialfreeze0, allocation0, benchmark0, finalfreeze0, wrapper0,
nochildren=true; own process groups cleaned in finally. Total cap1250seconds
(initial40/allocation90/benchmark1080/final40 with cleanup reserve). BDN reported
111.08seconds for16cases. Durable release mainca11f7e preceded raw inspection.
All16cases have successful nonzero statistics and actual10.0.11 runtime headers;
oneNoEmitShortRun job,3warmups/3iterations/1launch, [MemoryDiagnoser, ShortRunJob]
on every benchmark class, no CLI job flags. No generated/build subprocess ran.

Scenarios:0=one short ASCII child;1=64 repeated ASCII names;2=64 distinct names
with multilingual/non-BMP values. Inputs include attribute/namespace access and
entity markup. Fixture string/byte generation, DOM parsing and BCL XPathDocument
setup are outside timing. Reader operations include their own transport/parser/
adapter construction, owned events and disposal. Snapshot operations start from
an already completed DOM. Warm cursor operations consume name and value lengths.
BCL reader/traversal and XPath scalar controls passed independently.

## Allocation boundaries

Inclusive GC totals are authoritative. Async totals use
GC.GetTotalAllocatedBytes(true) with genuinely suspending input; no per-thread
async zero-allocation claim. Warm cursor/attribute operations use an existing
snapshot/current owned event and allocate no output. All32batch numerators were0;
inclusive total0B/8192groups (32×256), each group includes a full scenario1cursor
walk plus attribute/value positioning and property/namespace reads. Positive
control1048B proves the counter was active. Cold three-fixture setup297856B
includes owned corpus generation, DOM/BCL captures and initialization.

| Owned operation | Scenario | Inclusive bytes | Operations | Bytes/op |
| --- | ---: | ---: | ---: | ---: |
| Reader events/setup | 0 | 7,724,032 | 128 | 60,344 |
| Reader events/setup | 1 | 14,212,096 | 128 | 111,032 |
| Reader events/setup | 2 | 16,378,880 | 128 | 127,960 |
| Snapshot construction | 0 | 920,104 | 128 | 7,188.3125 |
| Snapshot construction | 1 | 26,476,232 | 128 | 206,845.5625 |
| Snapshot construction | 2 | 27,818,696 | 128 | 217,333.5625 |
| Async reader, process-wide | 0 | 7,855,728 | 128 | 61,372.875 |
| Async reader, process-wide | 1 | 14,343,168 | 128 | 112,056 |
| Async reader, process-wide | 2 | 16,509,952 | 128 | 128,984 |
| Malformed input/owned errors | — | 7,526,400 | 128 | 58,800 |

Retained16scenario1snapshots:heap804408→2229304B, delta1424896B, measured with
fullGC and snapshots kept alive. Root aggregate value characters13312=16×832;
this is not the complete stored-character count. Heap delta is process-level,
not an exclusive object-size attribution or a retained-DOM claim. The final
consumed checksum was18016256. Owned names/text may share immutable DOM strings;
construction allocates metadata/aggregate values and drops the temporary M4
capture. Retained capture cost remains separate from cursor zero allocation.

## Informational timing

| Operation | Scenario | Mean | 99.9% error | BDN allocated/op |
| --- | ---: | ---: | ---: | ---: |
| BCL reader | 0 | 526.3ns | 13.08ns | 2.81KiB |
| Adapter reader | 0 | 7058.9ns | 114.32ns | 58.93KiB |
| BCL reader | 1 | 6867.0ns | 869.85ns | 14.16KiB |
| Adapter reader | 1 | 173274.7ns | 946.54ns | 108.43KiB |
| BCL reader | 2 | 10658.9ns | 229.88ns | 27.33KiB |
| Adapter reader | 2 | 206185.1ns | 6285.42ns | 124.96KiB |
| Snapshot construction | 0 | 1.936μs | 0.0192μs | 7.02KiB |
| Snapshot construction | 1 | 64.399μs | 7.1845μs | 201.99KiB |
| Snapshot construction | 2 | 68.431μs | 6.8806μs | 212.23KiB |
| BCL cursor | 0 | 25.188ns | 0.3110ns | 32B |
| Captured cursor | 0 | 5.494ns | 0.0950ns | dash; GC primitive0B |
| BCL cursor | 1 | 1242.358ns | 18.9275ns | 6288B |
| Captured cursor | 1 | 211.269ns | 207.1336ns | dash; GC primitive0B |
| BCL cursor | 2 | 1231.838ns | 43.6953ns | 6672B |
| Captured cursor | 2 | 208.201ns | 51.6910ns | dash; GC primitive0B |
| Owned rejection | — | 8.119μs | 0.2242μs | 57.49KiB |

The adapter reader is substantially slower/more allocating than this BCL reader
baseline; no whole-reader throughput/allocation advantage is claimed. Captured
cursor properties reuse previously owned names/aggregate values, while BCL's
chosen property accesses materialize strings. Lower observed warm cursor means
must be weighed against measured setup/retention, not presented as a universal
navigation algorithm improvement. Scenario1cursor error is almost its mean;
three-iteration timing is informational. BDN could not raise process priority
(permission denied); every case still completed. Expected rejected-input
exceptions belong to the owned-error benchmark. No SearchValues/hash/SIMD
optimization or hardware-independent release claim is introduced. x64 and
broader real-corpus/end-to-end composition remain later integration/release work.

Raw inspected after release:allocation JSON/log, all four CSV/Markdown BDN
summaries and benchmark log.16case shape/statistics/runtime headers were checked.
Raw remains ignored under artifacts/M6-adapters for coordinator independent review;
only these summary tables/analysis are committed. Allocation JSON SHA256
`f6d7db18c31ddc8441a0a9c9b9abb7e3273e49fdd8ef05374373b63beeed3a30`;
benchmark log SHA256
`8642e72db1fd4eb3363a0fcf12b63492206ec7a634e1d09fab0f84e0ad983c15`.
No raw deletion, new test or measurement repetition accompanied reporting.
