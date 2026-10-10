# Validated fresh-tree construction boundary

`XmlDocument.BeginDomConstruction()` returns an internal `XmlDocument.DomBuilder`
only when the document owns exactly its empty document node and no members or
structural history. Construction is single-threaded and exclusive. Public general
mutation is unchanged; outside structural mutation or an unexpected arena count
invalidates the builder. Multiple builders acquired before work do not cooperate:
the first commit invalidates the other.

The consumer creates one node using existing validated `Create*` APIs, then calls
`AppendChild(parent, child)` immediately. The child must be the new final slot,
same owner, detached, with no children or members. Parent must be the document
node or a previously appended element. This excludes cycles without parent walks,
fragments, foreign/default handles, reuse and detached subtrees. The builder keeps
one root bit, so arbitrarily many document comments/PIs cause no repeated root
scan. Text/CDATA are legal under elements only. Empty documents remain legal.

`AppendAttribute(element, localName, value, namespaceUri = null, prefix = null)`
and `AppendNamespaceDeclaration(element, prefix, namespaceUri)` accept unique
expanded attribute keys / declaration prefixes guaranteed by validated syntax.
**Uniqueness is the caller's internal precondition**, not a public unchecked API.
They still validate XML names/scalars and reserved namespace bindings. They call
the original `AppendMember` primitive, retaining its version/capacity/name-quota
preflight and ordered identity / postcommit `NotifyMutation` behavior. Child
attachment calls the original `Link` primitive and notification. Tree owns no
parser type and ignores input-only depth/character/member limits.

The builder retains only its owner, scalar arena counts/version/root flag and
diagnostic counters. All work per child/member is bounded apart from linear
name/value validation and amortized arena/dictionary growth. Existing intern
lookup uses .NET's ordinal dictionary and borrowed alternate probes; owned names
are allocated only on actual misses. The future DOM consumer must probe borrowed
callback names before materializing missing strings, and own any text at its
persistence boundary. No borrowed spans are retained by this boundary.

`ChildChecks` counts child validations reaching primitive attachment;
`MemberChecks` counts validated member attempts reaching primitive append.
The count tests cover 500/12000-depth trees, 64/1024 member pairs and 4001 document
children. They complement direct source review: no ancestor, root-list or member
key loops exist in the boundary or selected primitives. Failure attempts can
increment counters but never commit structural membership.

The independent sample first records an owned syntax-event model, disposes the
parser, then replays it into Tree. It is an explicit model probe, not DOM factories
or the future streaming DOM adapter. No input/DOM/serializer peer code is needed.

Benchmark cases compare identical public/validated construction at 256/2048 deep
nodes, member pairs and document comments. Fixture names are outside timing;
returned documents consume results. Cold setup, warm helper, owned arena growth,
full owned construction and retained memory are separately probed. Timing is
provisional single-arm64 evidence pending the coordinator's exclusive grant and
later end-to-end DOM confirmation / x64 release checks. Check results will be
recorded at the committed source revision after actual execution.

## Correctness and allocation evidence

At `4a5102bd7c1894e69f5d6be2a67fba839e9500ad`, `verify.py` passed all
11 builder tests and 169 solution tests without skips, Tree 14/Input 10/Syntax 70
regressions, Foundation 11, explicitly rebuilt foundation and all M1 declaration
models, and 38 model protocol assertions. Actual fragment comparison and the
approved Tree golden both matched the unchanged 192 Common+Tree public members.
The owned syntax model replay and fresh isolated local-feed package consumer
passed. No future DOM or serializer runtime was present or required. For combined
integration, `verify.py --public-golden tests/Contracts/Fixtures/M1/Serializer.golden.txt`
allows the coordinator's approved 215 union while mandatory fragment comparison
remains in force. No empty public fragment is created for this internal repair.

The allocation probe measured cold document/builder setup 1560 B and separately
generated fixture names 35568 B. After warm-up, 4096 helper child attachments
allocated 0 B; their separate node arena growth cost 1387712 B. The member regression
preallocates arenas and warms valid repeated names across distinct elements, then
measures 1000 unique attribute/declaration pairs: 0 B. Full durable construction
of 512 attribute/declaration pairs allocated 271280 B, including arena/dictionary
growth, owned intern names and builder/setup. A 32-document batch retained-memory
GC estimate was 229444 B/document, excluding shared fixture strings; this is an
estimate rather than an exact layout or release target. `checks.txt` captures
commands and outcomes; raw output is ignored under `artifacts/M1-tree-dom-builder`.

Initial attempts: the supplied SDK path was a directory (corrected to its
`dotnet` executable); sandbox NuGet audit DNS and MTP local IPC were blocked and
rerun successfully with authorized escalation. One test incorrectly expected a
cycle error from a text container; existing kind validation correctly rejected
first. The test was corrected to attempt a cycle on an element. No runtime
change was needed. No failed/skipped check is represented as passing.

## Completed measurements

The complete 12-case measurement used source
`4a5102bd7c1894e69f5d6be2a67fba839e9500ad`: SDK 10.0.400, runtime .NET 10.0.11,
Apple M4 Pro Arm64, macOS 15.8.1, concurrent workstation GC. Each workload exited
0 with three measured iterations; no duplicate configurations were discovered.
Host high-priority scheduling was denied for every workload, so these are ordinary
priority provisional measurements. The coordinator parked other heavy work.

| Shape/count | Public mean | Validated mean | Public allocated | Validated allocated |
| --- | ---: | ---: | ---: | ---: |
| Deep/256 | 70.251 µs | 9.783 µs | 82.83 KB | 82.89 KB |
| Document/256 | 56.953 µs | 8.399 µs | 82.83 KB | 82.89 KB |
| Member pairs/256 | 183.097 µs | 31.875 µs | 120.49 KB | 120.55 KB |
| Deep/2048 | 2333.795 µs | 91.351 µs | 619.97 KB | 620.04 KB |
| Document/2048 | 4467.203 µs | 78.457 µs | 619.97 KB | 620.04 KB |
| Member pairs/2048 | 10698.230 µs | 290.910 µs | 951.33 KB | 951.40 KB |

These results support the count/source evidence: construction removes ancestor,
root and duplicate-member scans while keeping durable storage. Validated full
construction adds one 64 B builder object. The larger cases include substantial
arena growth and GC costs; they are not zero-allocation claims. Confidence ranges
and raw output remain in `bdn/`; actual parser-to-DOM and x64 confirmation belong
to later integration/release tasks.

Annotation-only commit `8a5c714cabd1375f568a6b40b39628fba5b32453` puts
`[MemoryDiagnoser, ShortRunJob]` on the benchmark class. Its Release driver build
passed with zero warnings/errors, and class-configured discovery confirmed the
same 12 cases in one configuration. No runtime or test source changed. The
coordinator accepted the original complete runtime measurements as applicable.
Overlapping coordination instructions caused two later partial repeats to be
cancelled; both compressed consoles are retained and neither is passing evidence.
An initial rejected CLI configuration never started timing. Future invocation:
`dotnet run --project benchmarks/Xemele.Benchmarks -c Release --no-build --no-restore -- --filter '*DomBuilderBenchmarks*' --artifacts artifacts/M1-tree-dom-builder/bdn`.
