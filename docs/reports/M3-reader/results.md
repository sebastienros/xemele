# Measured results

Frozen probe/benchmark source: `0e81744ffc718ef9e7b45f80992295d0e55208d7`.
Reader runtime source: `cb2a561f658b2a99b95d2206e83186391c1a91ea`.
Pinned SDK 10.0.400 / .NET 10.0.11, macOS 15.8.1 Arm64, 14 logical CPUs,
workstation GC. CPU model was unavailable to the sandbox/BDN; no model claim.
Both bounded prebuilt commands completed with actual child exit code 0 under
coordinator lease `m3-reader-0e81744-measure`. Python subprocess.run waited for
the direct children; InProcessNoEmit produced no build/benchmark child executables.
The lease was released before report inspection. No retries or option changes.

| Scope | Actual managed allocation |
| --- | ---: |
| Cold cursor owner and first 4096-char growth | 9,056 B |
| 100,000 warmed current copies, checksum 409,600,000 | 0 B total |
| 1,000 owned-string positive-control copies | 8,216,000 B total |
| Reader setup for generated 1 MiB / 8 MiB | 57,064 / 57,016 B |

The primitive counter covers Clear, Text capture and borrowed current access
on bounded preallocated cursor state, with no fixture creation inside measurement.
It excludes context creation, I/O setup, string-valued owned rows and diagnostics.

| Generated body | Tokens | Active peak live delta | Source bytes consumed |
| --- | ---: | ---: | ---: |
| 1 MiB | 1,048,580 | 8,984 B | 1,048,583 |
| 8 MiB | 8,388,612 | 8,712 B | 8,388,615 |

The source stream generates bytes using constant bounded state, never a corpus
buffer. Sampling runs while the reader is positioned, every 256 KiB and at EOF,
using full GC with the reader kept alive. The consumed character counts, terminal
None state and caller stream readability were asserted. Token and document input
quotas were explicitly unlimited only for this finite test. Full 1 GiB qualification
was not run here; the coordinator retained it as mandatory M3-corpus work.

Exactly six BDN cases executed in one fixed InProcessNoEmit job (3 measurement
iterations, 1 warmup, 1 launch, 1024 invocations, unroll 1). Raw results are in
`benchmark.txt`; isolated allocation/memory output is `allocation-memory.txt`.

| Case | Characters | BDN inclusive allocated/op |
| --- | ---: | ---: |
| OwnedCopyBaseline | 8 | 40 B |
| BorrowedCurrentCopy | 8 | 2 B |
| EndToEndOwnedSetupAndRows | 8 | 57,506 B |
| OwnedCopyBaseline | 4096 | 8,218 B |
| BorrowedCurrentCopy | 4096 | 2 B |
| EndToEndOwnedSetupAndRows | 4096 | 57,464 B |

The BDN 2 B/op result is preserved and is not presented as zero. Its difference
from the isolated 0 B primitive measurement has not been independently attributed;
BDN inclusive instrumentation and runtime scope differ from the direct counter.
There is no asserted attribution subtraction. No comparative performance or
regression claim follows from these short, informational observations on a shared
host with unrelated activity. BDN emitted minimum-iteration-duration warnings.
Actual callback-copy zero evidence is the independent counter with its positive
control; end-to-end setup/row ownership costs remain reported separately.

Correctness and API checks: 44 focused reader/pump tests pass; standalone sample
passes; approved extractor matches all 39 frozen Sync reader/token rows and 678
composed runtime rows. Relevant Release runtime/test/sample/probe/parent benchmark
builds pass with 0 warnings/errors. Planning validation passes for 145 tasks.

Earlier preservation check on this isolated branch: 211/212 pass; the one failure
is `Syntax.StreamingTests.InternalAbiMatchesApprovedViewsAndCallbackShape`, which
expects four states despite the already verified dependency's additive
NeedResource=4. Coordinator reported owning repair M3-scan-abi integrated/verified
at `dbfb42ac39751f03065f42534f38332c27dcca27`, with 69 syntax tests and the focused
ABI check passing. This worker neither changed that test nor imported a peer
branch. Coordinator must repeat preservation with that verified repair during
reader integration; the isolated failed check is not marked passed.

Full API inventory/corpus discovery command failed before assembly comparison:
missing `tests/corpora/xml/xmlconf/eduni/errata-2e/E14.dtd`. The independent approved
extractor comparison passed all compiled rows. Corpus provisioning belongs to its
owner/coordinator; no corpus file was changed or exclusion added.
