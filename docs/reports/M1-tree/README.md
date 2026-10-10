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

Deep text traversal, import and clone are iterative. General insertion cycle checks
walk ancestors. Copy preflights complete source topology, limits/capacities/version
budget, then directly links fresh detached copies and appends known-unique members
in source order. It never calls public insertion or scans destination member keys.
Internal counters record actual preflight/copy node and member visits plus links;
tests cover 500/5000-depth chains and 64/1024-member lists without latency thresholds.
Indexers may walk links; linked foreach avoids
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
the same checksum, representative 64-item owned tree construction, document clone/tree import and
512-attribute/512-declaration import. Setup and
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

Independent review identified quadratic copy work in the initial implementation:
public insertion walked ancestors and member copying used linked integer indexers
and destination key scans. The final copy path uses direct private primitives only
for fresh copies after full preflight, preserving quota, version and notification
semantics. General public mutation validation is unchanged. Prior passing correctness
results do not establish acceptable copy complexity.

## Recorded final results

Stable source: `abdf9db8ade0b1e13fcbc8d45bdfdd941f35288f`. Final verifier session
17439 exited 0 before exclusive timing session 59530 began. The coordinator parked
input, syntax and its own builds/tests for this window. All ten benchmark processes
exited 0 with three actual workload results each; global elapsed time was 70.25 s.
The full console is retained as `bdn/benchmark-console.txt.gz`; Markdown/CSV tables
are in `bdn/`. Uncompressed BDN artifacts and verifier output remain ignored under
`artifacts/M1-tree/`. No build/test overlapped timed workloads.

Final verification: Release build zero warnings/errors; 14/14 focused tree,
78/78 full solution and 11/11 foundation tests, no skips. Exact runtime matches
192 approved Common+Tree members; 111 owned Tree rows compose with 81 unchanged
Common rows. Foundation 139/173-row models, all six independent M1 public lanes,
38 protocol-model assertions, 16 Python checks, bootstrap corpus, source samples,
runtime pack and isolated package-only tree consumer pass. `checks.txt` is concise;
`runtime-api.json` preserves actual compiled compatibility/provenance. Task validation
and owned-path/whitespace audits pass.

On Apple M4 Pro Arm64, macOS 15.8.1, SDK 10.0.400/.NET 10.0.11, ShortRun measured:

| Workload | Mean | Managed allocation/op |
| --- | ---: | ---: |
| Linear intern oracle, padding 8 / 80 | 140.78 / 143.48 ns | 0 B |
| Borrowed ordinal probe, padding 8 / 80 | 18.62 / 50.68 ns | 0 B |
| Indexed / linked 64-child traversal | 2629.1 / 175.3 ns | 0 B |
| Owned 64-item tree construction | 10.293 us | 52,560 B |
| Owned document clone | 9.597 us | 52,784 B |
| Import 64-item tree into fresh owner | 9.643 us | 52,928 B |
| Import 512 attributes + 512 declarations | 81.833 us | 435,240 B |

Lookup operations each batch three probes: first hit, last hit and miss among 64
interned names. Padding is the configured suffix length, not total name length.
Lookup/traversal pairs have identical semantic checksums. Construction and copy
are distinct persistence workloads; their BDN ratios against traversal are not
semantic speed comparisons. Three measurements on one Arm64 host are provisional,
with substantial ShortRun confidence intervals; no cross-host/release speed claim.

Per-thread allocation regressions measure 0 B over 1,000 complete child/attribute/
namespace traversals plus borrowed probes and separately 10,000 borrowed intern
hits. First-process representative construction and warm construction both allocate
52,560 B/tree. Heap-minus-fragmentation estimate is 18,408 B retained/tree, excluding
the preallocated handle array; GC estimates are informational. A batch of 1,000
detached repeated-name nodes allocates 281,320 B including cumulative arena growth.
One new interned name plus detached node allocates 320 B including dictionary growth.
Unique names, slots, member state, common budget setup, growth arrays and copied
results allocate; warm navigation/name lookup does not. Text aggregation allocates
its builder and result separately.

Historical per-slot objects used 24,728 B allocated and approximately 21,544 B
retained for the same construction fixture. Value arenas trade more cumulative
array-growth allocation for lower retained fixture storage, while keeping stable
handle identities. These single-process retention estimates are not a release
memory guarantee. Any surviving node/attribute/declaration retains the complete
document arena, including detached/removed slots and interned names. Actual parser/
output composition and x64 confirmation remain later-task/release evidence.
