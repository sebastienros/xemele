# M1 common runtime

The shipped common public surface exactly matches the 81 approved Common model
rows. Its arrays are owned constructor snapshots; only immutable records escape.
There are no recovery, DTD, resolver, parser, decoder or tree dependencies.

## Internal consumer contract

Each operation constructs its own `OperationBudget`, validating options/limits
before cancellation or input work. The copied options/limits, limit array and
counter array are setup allocations. No state is global or shared between
operations. A consumer must propagate the same budget across any nested work.
`TryCharge` returns false without modifying counts on quota or representation
failure; consumers create their own terminal module diagnostic. It never throws a
parse exception, and no helper catches callback, cancellation, I/O or OOM errors.

Quota names/widths match XmlResourceLimits. DocumentCharacters and
ExpandedCharacters count scalars; ExternalBytes counts original bytes;
EvaluationWork counts candidate visits/transitions (Int64). Depth, EntityDepth,
InclusionDepth count simultaneous nesting; AttributesPerElement counts attributes
including namespace declarations; TokenCharacters counts normalized token scalars;
InternedNames counts distinct owned namespace/name strings; ExternalResources
counts successful opens (Int32). Persistent allocation/work must follow a successful
charge. Consumers release nesting on exit, reset token/tag counters between their
scopes, and never reset cumulative quotas. Consumer lifecycle/state machines and
Unicode decoding validation belong to the later enforcing lanes.

`BeforeWork` precedes segments of at most 4096 units. Each consumer also calls
`CheckCancellation` at operation entry/resume and before external work or callbacks.
Checks happen at most 4096 work units apart. The common primitive does not execute
or catch callbacks, so callback-thrown XmlParseException cannot become a syntax
rejection through these helpers.

`ScalarLocation` accepts already validated Rune scalars, tracks normalized CR/LF
including split CRLF, and counts astral scalars and tabs once. The caller captures
Current before consuming the scalar and supplies its first original-byte offset,
or null for character input; the normalized CRLF location is captured at CR.
The decoder owns byte/UTF-16 counts and mapping, separate from scalar locations.
TryAdvance rejects Int32 overflow without mutating state. Consumers issue their
module's location-capacity diagnostic. Public XmlLocation preserves the approved
record contract (including zero unknown), without extra constructor validation.

DiagnosticBuffer allocates lazily and retains at most N diagnostics, replacing
slot N with common.diagnostics-limit upon message N+1, including N=1. Exactly N
messages are retained unchanged. Later messages are ignored; snapshots are owned
read-only arrays. Null still obeys Int32/List representational capacity and OOM
propagates. Public constructors copy the diagnostics supplied by their caller;
parsers use bounded retention before constructing public results/exceptions.

## Verification and measurements

Run `python3 docs/reports/M1-common/verify.py` with pinned SDK 10.0.400 on PATH.
Reports record known implementation revision; no peer report or golden is written.
Benchmark harness is isolated here to avoid default source glob collisions:
`dotnet run -c Release --project docs/reports/M1-common/Xemele.CommonBenchmarks.csproj -- --filter '*' --artifacts docs/reports/M1-common/bdn`.
ShortRun compares a straightforward guarded counter to the general primitive;
there is no optimization/speed claim. Setup and owned rejection snapshots are
separate cases. `--setup-allocation` records fresh-process construction allocation,
warm setup allocation and a GC heap-delta retention estimate separately.
All timing is provisional on this host; no XML composition exists. Parser/tree
consumers must provide composition evidence; x64/arm64 confirmation and longer
runs remain release requirements. No static lookup tables or pool setup exists.

## Recorded results

Final source revision: `937def8c23313ecdd087537906d08e81297baae7`. `checks.txt` records all commands,
SDK/host, 10 focused, 64 solution and 11 foundation passing tests, exact compiled
API comparison, models, pack and both sample consumption modes. No skipped tests.
Runtime/tests/API/sample source is unchanged from reviewed `25c8375`.

Final ShortRun results are `bdn/results/CommonBenchmarks-report-github.md`, with
raw BDN/console logs and CSV beside it. Baseline/general counters: 0 B/op and
0.5540/0.9931 ns; these tiny noisy timings do not establish a throughput claim.
Setup 680 B/op; exception construction with an already owned diagnostic 208 B/op
(unthrown, excludes message construction/stack-trace capture); terminal retention
216 B/op. Warm counter/location test measured 0 B across 10,000 iterations.
Separate fresh-process construction: 2696 B; standalone warm/escaping setup about
704 B/op. Different harness contexts are reported separately. Whole-process GC
heap-minus-fragmentation estimate is about 535 B retained per operation, excluding
the preallocated reference array. The GC.GetTotalMemory delta was inflated and is
raw informational telemetry, not the retained-size conclusion.

Timing order is explicit: final verification session 68314 returned exit 0 before
final BDN session 52910 started. No builds/tests overlapped final timings. The
previous successful session 92467 likewise followed verification session 52844's
exit 0, but its cancelling increment/decrement baseline optimized below measurable
latency; `bdn-initial`/`benchmark-initial.txt.gz` preserve it as superseded evidence.
The first BDN driver failure timed zero cases; its log is preserved separately.
No parser composition, x64 result or broad public performance victory is claimed.

Raw console/BDN/failure logs are gzip compressed without changing their bytes;
read them with gzip -dc. Generated Markdown/HTML has trailing spaces removed to
pass the repository whitespace check. CSV values are unchanged.
