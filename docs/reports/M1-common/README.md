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
