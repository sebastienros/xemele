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
short ASCII, 4096-byte ASCII with trailing newline, and multilingual/CRLF input.
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
