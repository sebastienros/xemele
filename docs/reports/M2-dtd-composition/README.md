# Common-only DTD composition repair evidence

Three nonpublic initialization hooks are frozen in
`docs/decisions/M2/dtd-composition.md`. Public methods and all constructor
signatures are preserved. The parser/model/framer sample compiles explicit
common-only sources, with no decoder/resolver/core/entity implementation.

`verify.py --report-prefix NAME --expected-branch codex/m2-dtd-composition`
records actual SDK10.0.400/executable runtime10.0.11, independent compiled original
20-type constructor/public-method/nullability inventory (including a compiled
nullable mutation), exact3 internal hook types/accessibility/required parameters
and compiled access/default mutations, exact authored composition/rejection
stdout, 27 focused new tests plus4162 predecessor DTD cases and nine pinned
subsets/775 split parses, standalone/main benchmark build/discovery/correctness,
cumulative MTP/direct/discovery5050=4496+255+299 with no skip/failure, fresh copied
BCL242 known-source provenance/hash preservation, public272/registry3388,
planning14/graph122, and committed/staged/working/untracked ownership/whitespace.
Actual outcomes are in each report's summary.json and compressed full raw log;
failures remain failures with later checks unrun. Original parent runtime XML
row identities must be present in the cumulative run. The original successful
performance archive is hash-preserved and never rerun as a new measurement.

The benchmark input has two authored sizes (32/4096 ASCII comment scalars, plus
multilingual/supplementary/newline units). Cold setup+first composition, owned
seed/model output, complete seeded/framed composition, borrowed steady scanning,
positive allocation controls and source/mapping release are separate boundaries.
Prior immutable tables and the live ordinal-deduplicating operation ledger are
initialized before warmed operations. Complete bounded comments reset token
state each call; finite TokenCharacters is unchanged. The scan operation uses
explicit unlimited DocumentCharacters/ExpandedCharacters from initialization to
support repeated steady scanning of one operation. This does not claim unlimited
production defaults or allocation-free setup/model persistence. No new matcher,
SearchValues choice, SIMD, GC/JIT setting or timing threshold is introduced.

`BenchmarkDriver --correctness` validates all authored declaration/particle/
attribute/entity fields, immutable identity, source cursors and effective default
outside timing; scans must return NeedInput on every call. Benchmark classes
carry MemoryDiagnoser and ShortRunJob. Measurement requires coordinator-exclusive
host grant and is recorded separately at an exact source revision, preserving
actual generated build/child/parent exit codes and raw output. This evidence
covers Arm64 common-only composition, not full XML/DOCTYPE/entity conformance,
production core composition, Windows/x64 or AOT.

Retained failures: sandbox NuGet audit access NU1900; first test namespace CS0118;
authored initial focused location/depth oracle mistakes at e5a99fe; benchmark
public return/internal enum CS0050 at57b4e2e; indefinite comment workload correctly
hit unchanged TokenCharacters during correctness at028f8af. All are explicit,
with no passing outcome substituted or runtime quota relaxed.
