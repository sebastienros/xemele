# M1 tree runtime

The exact approved 111-row tree surface composes with 81 Common rows. Tree code
has no input, syntax, DOM factories, output, DTD or index dependency. The sample
constructs and imports trees independently of parsing and serialization.

## Ownership and invariants

Document-owned value-slot arenas contain never-reused Int32 slots. Nodes, attributes and
namespace declarations have separate slot domains and strong owner references.
Detached nodes/removed member handles remain valid; removed members retain their
original owning element. Any retained handle deliberately retains the whole
owner, including detached nodes, removed members, their text and interned names.
This prevents dangling handles but retains the complete arena until every handle
and the document are unreachable. There is no disposal or slot reclamation.

Contiguous value slots contain linked child/member order. Array growth uses 1.5x
capacity; handles remain stable by index, and no movable storage reference escapes
publicly. Mutation primitives hold internal refs only while their arena cannot
grow; text replacement reacquires slots after allocating its new text. Structural changes
preflight the checked Int64 global mutation version and report primitive changes
through the optional erased partial hook. Values/prefix updates preserve membership
identity and enumerator validity. Fragment transfers validate every prospective
child and the resulting document root count before any detachment. All default
handles reject access except IsDefault, equality and hashing.

Construction enforces the copied common limits' InternedNames quota plus finite
slot/version capacities. Input-only quotas do not apply to independent constructed
trees. Preflight counts distinct new nonempty names across local name/URI/prefix;
empty names share static literals. Persistent allocation follows successful quota
checks. Intern probes borrow ReadOnlySpan<char> and use .NET's ordinal dictionary
alternate lookup; a miss materializes one owned string. Names and URIs remain in
the bounded document table after removal. Existing values are immutable strings
owned by document storage, with no pooled or caller-buffer views. XML Name/NCName
and scalar validation uses Rune decoding and direct comparisons, without delimiter
scanning optimizations or custom hash/SIMD machinery.

Deep text traversal, import and clone are iterative. Insertion cycle checks walk
ancestors; cloning a chain therefore has quadratic validation work in depth,
while requiring bounded call stack. Indexers may walk links; linked foreach avoids
repeated indexed scans. Copy operations allocate owned destination slots, a
bounded missing-name preflight set, and interned strings on distinct-name misses.
Text aggregation allocates StringBuilder capacity and the final owned string.
OutOfMemoryException propagates; allocation failures do not promise transaction
rollback. Validation rejection preserves observable tree structure and values.

## Checks and measurements

Use pinned SDK 10.0.400 and `DOTNET_CLI_USE_MSBUILD_SERVER=0`:
`python3 docs/reports/M1-tree/verify.py`. Raw verification goes to ignored
`artifacts/M1-tree/verification-raw.txt`; durable results stay in this directory.
The verifier builds/tests the actual runtime, compares its 192 members with the
approved Tree model, verifies task fragments, preserves foundation/model/common
checks, runs source and package-only consumers, and records allocation boundaries.
No peer historical report is rewritten.

The isolated benchmark project has the same project/assembly filename and links
only this task's benchmark source. `--allocation` measures first construction,
warm tree construction, heap-minus-fragmentation retention, repeated borrowed
probes, persistent repeated-name slots and a new interned name. GC retention
estimates are informational and exclude the preallocated handle array.

Timing requires the coordinator's exclusive window after all builds/tests end:
`dotnet run --project docs/reports/M1-tree/Xemele.TreeBenchmarks.csproj -c Release --no-build -- --filter '*' --artifacts artifacts/M1-tree/bdn`.
ShortRun compares short/long borrowed first/last-hit and miss batches with a
linear ordinal oracle (three probes per operation), linked/indexed traversal with
the same checksum, and representative 64-item owned tree construction. Setup and
fixture generation are outside timed lookup/traversal. Construction intentionally
includes its owner/slots/names/text setup and persistence. Timing is provisional
single-host Arm64 evidence, with parser/output composition and x64 release
confirmation owned by later deliverables.

Initial sandbox test restore failed NU1900 because api.nuget.org DNS was blocked;
authorized network retry succeeded. The initial focused run passed 8/10; a wrong
checksum and a quota test using an invalid binding were corrected, rather than
changing runtime behavior to satisfy them. Failed checks are not passing evidence.

The first known-revision verifier reached the package consumer, then found the
previous Common-only package in the global NuGet cache under the same development
version. The consumer now uses an isolated RestorePackagesPath; source/API tests
already passed before that harness failure. Raw initial failure remains in ignored
artifacts/M1-tree/verification-initial-cache-failure.txt.

After the first complete verifier passed, storage was tightened from per-slot
objects to contiguous value arenas before any timing. Physical array layout is not
contract-frozen; this layout follows the approved arena retention direction. Prior
object-slot check output is retained only as historical evidence under ignored
artifacts, not as allocation/timing evidence for the final source.
