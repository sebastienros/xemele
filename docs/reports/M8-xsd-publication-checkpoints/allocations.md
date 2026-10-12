# Owned compilation allocation results

Accepted runtime/correctness revision:
`233aa3cf738ffffe4ae7b52e37e8ddb96eb9c8a7`.
Prepared probe/source revision: `d5f1174213c74d2bf5815e3ad683b7527d28b760`.
Measurement manifest SHA256:
`a9cb5fd88f5ad32780c22066c4993511950de73f23f1df8c45baffe91be8f88f`.

Root was the sole launcher under durable grant
`9707173cdaa6ea8720b3831d7b0aa2acefcc01f7`, followed by durable host RELEASE
`d1ff39c3b9fb6d53400aae10e7398249ec66b1b6`. Both publication children exited
zero, were reaped, had no remaining process-group members, and closed their
logs. All 444 identities matched before and after. No raw allocation result was
opened before host RELEASE. There were no retries, extensions, builds or tests
under this grant.

Host: Arm64, macOS 15.8.1, SDK 10.0.400, .NET 10.0.11, workstation GC.
`DOTNET_TieredCompilation=0`, `DOTNET_gcServer=0`, `DOTNET_GCConcurrent=0`.
The prebuilt synchronous allocation-only driver measured 64 calls after 16
warmups per route. Allocation counters apply only to synchronous work on the
calling thread. No BenchmarkDotNet or timing campaign ran.

| Owned route | Aggregate bytes / 64 calls | Bytes / call |
| --- | ---: | ---: |
| Legacy graph from previously parsed document | 4,912,128 | 76,752 |
| Shared principal string parse/compile | 5,807,616 | 90,744 |
| Unused model checkpoint | 6,242,816 | 97,544 |
| Unused attribute checkpoint | 6,304,256 | 98,504 |
| Zero-self-reference redefine checkpoints | 11,649,536 | 182,024 |
| Self-reference redefine checkpoints | 11,824,384 | 184,756 |
| Aggregate scalar quota rejection | 5,381,632 | 84,088 |
| Stream scalar/expansion exact cap | 13,009,152 | 203,268 |

Cold corpus/legacy-document setup plus one shared parse/compile allocated
126,952 bytes. The separate 1024-byte positive control allocated 1,048 bytes.
Sixteen unused-model compiled graphs remained rooted across collections:
approximate live heap delta 1,204,656 bytes. This is a heap observation including
managed bookkeeping, not a precise object-size or RSS claim. All expected
checkpoint counts/source identities/refusal diagnostics were consumed; the
fixture's final consumption value was 28,671.

Compilation persists immutable type/definition/binding/context snapshots and
owns its transient parsing/builder/resource state. Checkpoints deliberately keep
otherwise discarded constraints available to later content/facet/facade checks.
The allocations include these owned boundaries and the fixture's independent
result checks. Legacy and shared routes perform different principal parsing
work and are not comparable optimization baselines. There is no borrowed-zero,
throughput, latency, cross-architecture release, or async allocation claim.

The counter-disabled controls exercised identical routes with all allocation
and heap fields zero; those zeros mean counters were disabled. Raw output stays
in ignored `artifacts/M8-xsd-publication-checkpoints/measurement-1/`.
The raw measured result SHA256 is
`d07b79ae789f0713d68ae3b8ef59bc16216bafad417c939fa12e4587e7b335d0`.
Only this summary and analysis are committed.
