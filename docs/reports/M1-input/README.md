# M1 input runtime

`XmlInputDecoder` implements the approved internal Input model independently of
syntax/tree/DOM. It selects UTF-8/UTF-16 BOM/signature encoding, rejects UTF-32 and
EBCDIC signatures, validates strict Unicode, emits complete scalars as raw UTF-16
units, and maps each unit to the scalar's original first byte (both surrogate
halves share one offset). BOM bytes count but emit nothing; newlines remain raw.
Diagnostics track normalized scalar locations through the valid prefix.

Four inline carry bytes are the only retained source data. Detection peeks when
possible; unfinished detection/scalars copy bounded bytes with their source
position. Contiguous invalid sequences remain unconsumed; previously accepted
carry counts cannot be rolled back. Output and mapping capacity checks precede
state changes. Final is sticky; completion/rejection/fault are terminal. Dispose
is idempotent and subsequent operations/properties reject use. Cancellation is
checked at entry and each scalar, with the original token propagated on fault.
Primary bytes use an independent checked Int64 mapping counter. ExternalBytes
counts external resources and does not limit the primary source; its enforcement
belongs to the external transport boundary. Scalar/document/token grammar quotas
belong to syntax/composition. Common validates/snapshots configuration at setup.

Declaration labels use direct ASCII-case-insensitive full equality with no
string copies or culture dependency. ValidateDeclaration is exactly once after
detection, including after completion of short input; mismatch/unsupported label
is terminal. A BOM-less UTF-16 signature requires explicit matching LE/BE.
The decoder does not locate/parse XML declarations. Composition must submit at
most one new byte at a time until declaration presence/absence is known and
reconciled, before decoding body bytes or forwarding body callbacks. The focused
framing regression demonstrates mismatch precedence with malformed following
body bytes across transport sizes, without depending on an unmerged parser.

The compiled internal surface is recorded in `internal-contract.txt`; the
verification probe compares actual public members of these internal types,
parameter names/defaults, enum values and type flags to the approved Input model.
No public API fragment is invented: the public assembly remains exactly the
81 approved common declarations. The source and isolated NuGet-only samples
compile the actual interface and verify UTF-8 BOM counts/mappings/declaration.
They use an explicitly named internal friend assembly, not a public parser API.

Run `python3 docs/reports/M1-input/verify.py` with pinned SDK 10.0.400 on PATH,
DOTNET_CLI_USE_MSBUILD_SERVER=0. It checks Release solution/focused/full/foundation
checks, actual common ABI, all M1 compiled model lanes/protocol assertions,
nonempty API discovery/comparison, Python build tools/bootstrap corpus, source
and packaged samples, benchmark harness compilation, allocation regression,
planning validation and whitespace. Shared historical reports are not written.

The measurement harness is `Xemele.InputBenchmarks.csproj`; its filename equals
its assembly name, and it links only the owned benchmark source. ShortRun uses
short ASCII with trailing newline, 4096-byte ASCII with trailing newline, and multilingual/CRLF input.
GlobalSetup checks the straightforward strict Rune oracle against actual output,
first-byte mappings and checksum. Steady UTF-8 cases begin after detection and
compare full scalar validation/emission/mapping/location/count semantics; the
general decoder also checks its operation state/capacity/budget. There is no scan,
hash or SearchValues optimization and no speed claim. Lifecycle setup and owned
malformed diagnostic are separate cases. Fixture generation is outside timing.

`--allocation` reports fresh-process construction, warmed construction and a
GC heap-minus-fragmentation retention estimate excluding the reference array.
Decoder/options/limit snapshots, budget arrays and ScalarLocation are operation
setup; no buffers, pools or lookup tables grow. Error diagnostic/message ownership
is separate from the zero-allocation warm decode regression. All measurements
are provisional on this Arm64 host. Actual parser composition and x64/Arm64
release confirmation remain future composition/release evidence.

Initial failures were corrected: the xUnit cancellation-token analyzer rejected
default tokens in lifecycle tests; an overly broad replacement briefly changed
XmlLocation arguments; the sandbox denied MTP named-pipe IPC. The first focused
run also caught test assumptions about leading BOM text and pending output before
new-input counter enforcement. All were fixed; none are passing evidence. Raw
initial logs are retained under ignored `artifacts/M1-input/`.

Independent review corrected an initial primary-byte charge against ExternalBytes.
Focused regression now proves ExternalBytes=1/3/4 does not reject primary bytes,
and direct counter-capacity injection proves checked original-byte overflow.

The first package-only sample restore selected the preexisting same-version
NuGet cache from an earlier common package, so the internal decoder namespace
was absent. The corrected probe uses an isolated temporary RestorePackagesPath
and only this task's freshly packed feed; no package version/shared file changes.
Raw failed output is retained as artifacts/M1-input/package-cache-failure.txt.

Final correctness source bcd2dffa6e890ccfbd4e52e7f923a07e5e7ac17e: focused10/10,
solution74/74, foundation11/11 with no skipped tests; internal41/public81 rows,
all model/protocol checks, Python/bootstrap, source+fresh packaged samples pass.
Warm mixed decode is 0 B across10000 calls. Construction is2808 B fresh process,
816 B/op warmed; informational retained heap-minus-fragmentation648 B/op excludes
the preallocated reference array. Durable checks.txt keeps
command/results/summaries; raw output is ignored artifacts/M1-input/verification.txt.


## Exclusive ShortRun evidence

Runtime and focused-test source is unchanged from reviewed/checked
bcd2dffa6e890ccfbd4e52e7f923a07e5e7ac17e. Final benchmark source is
7e7161f60014898ebca0b3dc916f886056247b21 (only the short corpus newline changed).
The coordinator parked tree/syntax builds, tests and benchmarks and held its
exclusive lock. Harness-only repair build session1873 exited0 before final BDN
session52861 started; generated BDN build completed before workload measurements.
All eight final cases succeeded, including GlobalSetup output/mapping/checksum
oracle checks. No unrelated build/test overlapped timing. The coordinator was
notified of release immediately after process completion. Full raw console and
BDN logs remain ignored at artifacts/M1-input/benchmark-console.txt and bdn/;
durable BDN Markdown/CSV reports are in this report's bdn/ folder.

Apple M4 Pro Arm64, macOS15.8.1, SDK10.0.400/runtime10.0.11, BDN0.15.8;
ShortRun (one launch, three warmup/measurement iterations), MemoryDiagnoser.
Short source is `<r/>` plus LF (5 bytes); long source is4095 ASCII characters plus
LF (4096 bytes); multilingual is128 repetitions of é/astral/CRLF/中 (1408 bytes,
768 UTF16 units). All fixture generation/clone/oracle setup is outside timing.

| Corpus | Straightforward mean | Decoder mean | Warm allocation |
|---|---:|---:|---:|
| Short | 8.969 ns | 57.233 ns | 0 B/op both |
| Long ASCII | 7220.297 ns | 34943.817 ns | 0 B/op both |
| Multilingual | 1530.262 ns | 6764.661 ns | 0 B/op both |

Construction:46.81 ns,816 B/op; malformed source plus construction and owned
terminal diagnostic:66.60 ns,888 B/op. The diagnostic adds72 B in this harness;
its message/code are static literals, and no exception is thrown. Construction
includes options/limits snapshots and common budget/location state. ShortRun
latencies are informational, with wide confidence intervals for tiny cases.
The general decoder is slower than the assumed-complete UTF8 oracle; it also
checks operation state, arbitrary output capacity, carry/final progress and
checked counters. No optimization or speed victory is claimed. Invalid UTF16,
chunk boundaries and declarations are correctness-test coverage, not a timed
UTF16/composed-parser performance claim.

Initial BDN run at8a88afb1b8548b07ef276d2320a24fe9f83db4ec attempted eight cases;
seven produced results and the short oracle process exited255 after legitimate
Int32 scalar-column overflow. The outer driver exited0 despite that failure;
inspection caught it, so it is not a full passing run. Raw output/reports remain
artifacts/M1-input/benchmark-initial-failed-console.txt and bdn-initial-failed/.
Only the benchmark fixture changed to append LF; no runtime limit was weakened.
The final short oracle printed536870913 workload calls and the decoder134217729;
with at most one further67108864-call diagnostic iteration plus setup, each
newline counter remains belowInt32.MaxValue. All final cases produced samples.
The complete retry supersedes initial partial timings.
