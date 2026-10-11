# Compiler performance evidence

Measured once under exclusive grant `m4-compile-73eb77a-prepared-sequence` at
73eb77a50fea0c7e738f83d8757c4b50e5c9d2c3. The reviewed production source is
1943b1b87b7e8245139e20e6bd9f5b65ccd7ff98; subsequent commits only prepare the lease.
SDK 10.0.400, actual .NET 10.0.11 (10.0.1126.37416), native Arm64 RyuJIT,
Apple M4 Pro (14 cores), macOS 15.8.1. Every class declares
[MemoryDiagnoser, ShortRunJob]; one NoEmit toolchain-only mutator preserves
3 warmups, 3 iterations, 1 launch. No CLI job flags/build/restore during the lease.

Allocation exited 0, then all 25 timing cases succeeded in 203.28 s; wrapper exit
0, initial/final identities 0, no_child=true. Root independently confirmed all
144 source/generated inputs, 43 actual binary files and 3852 complete SDK/runtime/
host files unchanged, manifest SHA256
4a52301a863a00f513e35af2f993e6a08d06f5a6ac50527e3ca30b0b9cccfe14. Release was reported
before inspecting results. There were no failed measurements or retries.

## Exact counter domains

| Domain | Actual managed bytes | Scope |
|---|---:|---|
| Cold setup | 12808 | First static context/core metadata + compiler/owned short AST; prepared fixture generation excluded |
| Warm primitive batches | 0 | Each of 32 batches, 32 groups of two inputs; all min/max/sum 0 |
| Steady primitive batches | 0 | Each of 32 batches; all min/max/sum 0 |
| Inclusive steady gate | 0 | Entire measured batch loop plus distribution stores; 2048 borrowed lexer scans, fixed matcher, delimiter, polled colon/ordinal comparison and bounded decimal conversion work |
| Token materialization positive control | 32776 | One long Unicode lexical scan with a new owned string for every token |
| Owned short compiler outputs | 113664 / 32 = 3552 per output | Reused static context; drafts, stacks, static resolution enumeration and immutable AST/string/collection ownership included |
| Owned registered contexts | 101632 / 32 = 3176 per context | Reused caller options; namespace/signature snapshot and charges included |

Consumed checksums match semantic preparation: combined warm/steady 2373031936;
long materialized scan 1158273. The exact gate is independent of BenchmarkDotNet's
rounded allocation display. This is synchronous per-thread work with no async or
external callbacks in the measured region. Positive owned boundaries confirm the
counter was active. Fixtures/arrays, cached delimiter SearchValues construction
and their initialization occur outside the primitive intervals; that experimental
SearchValues setup cost is not a production lexer allocation claim.

## Latency summaries (informational)

| Primitive / case | Straightforward baseline mean | Production/candidate mean | Managed B/op display |
|---|---:|---:|---|
| Lexer, short core-call expression | Materialized 201.9 ns | Borrowed 135.3 ns | 360 vs reported 0 |
| Lexer, long Unicode path | Materialized 14496.6 ns | Borrowed 9148.8 ns | 32776 vs reported 0 |
| Axis vocabulary, longest hit | Sequential SequenceEqual 11.336 ns | Span dispatch 2.485 ns | Both reported 0 |
| Axis vocabulary, common-prefix miss | Sequential SequenceEqual 14.994 ns | Span dispatch 2.817 ns | Both reported 0 |
| Decimal, short | Framework 18.06 ns | Bounded/polled 65.91 ns | Both reported 0 |
| Decimal, long | Framework 15931.43 ns | Bounded/polled 11736.96 ns | Both reported 0 |

| Delimiter-only case | Loop | IndexOfAny | Cached SearchValues | Repeated IndexOf |
|---|---:|---:|---:|---:|
| Short/early hit | 1.260 ns | 2.792 ns | 2.519 ns | 10.963 ns |
| Long Unicode/late hit | 3772.053 ns | 706.265 ns | 227.402 ns | 1370.411 ns |

All delimiter candidates have identical stop-index oracles and report 0 B/op.
The long loop has stddev 326.38 ns and a very wide ShortRun confidence interval;
other timing samples also have only three measured iterations. No repeatability
or cross-hardware claim follows from these numbers. The validating production
lexer keeps its simple scalar loop: the multi-stop microbenchmark does not
validate Unicode or poll cancellation during a whole-span search. Short inputs
also favor the simple loop. Existing span vocabulary dispatch remains simple
and allocation-free. Bounded decimal conversion costs more on short input;
its preserved rounding and cancellation guarantees, rather than this one timing
comparison, justify the bounded implementation.

| Owned boundary | Mean | BDN allocated display |
|---|---:|---:|
| Short core-call compile with reused context | 776.8 ns | 3552 B |
| Long Unicode path with numeric predicates | 56427.1 ns | 197765 B |
| Long literal with invalid surrogate diagnostic | 24137.6 ns | 704 B |
| Default/null options context | 429.4 ns | 3.14 KiB (~3216 B) |
| Registered context from reused options | 508.9 ns | 3.10 KiB (~3176 B) |

Context cases include different option-creation boundaries: default/null creates
its default options, while the registered case receives prepared options. Their
allocation difference does not imply that registrations reduce snapshot cost.
Owned compile figures include all transient parser/resolution bookkeeping, not
only retained AST storage. Invalid input allocates a bounded exception/diagnostic,
never echoes expression text, and has no partial AST result.

## Inclusive GC diagnostic accounting

BDN's 18 cases displayed as no allocated bytes actually have positive inclusive
harness diagnostic totals. The following durable summary preserves those totals
without treating a dash/rounded per-op figure as exact zero. GC generation
collection counts were zero for all these primitive cases.

| Primitive case | Inclusive GC bytes / diagnostic operations |
|---|---:|
| Borrowed lexer long / short | 2640 / 65536; 2640 / 4194304 |
| Delimiter long loop / IndexOfAny / SearchValues / IndexOf | 3312 / 262144; 3648 / 1048576; 3648 / 4194304; 3648 / 524288 |
| Delimiter short loop / IndexOfAny / SearchValues / IndexOf | 3648 / 268435456; 3312 / 134217728; 3648 / 268435456; 3648 / 67108864 |
| Vocabulary hit sequential / dispatch | 3648 / 67108864; 3648 / 268435456 |
| Vocabulary miss sequential / dispatch | 3648 / 33554432; 3648 / 134217728 |
| Number long framework / bounded | 3648 / 32768; 3648 / 65536 |
| Number short framework / bounded | 3648 / 33554432; 3648 / 8388608 |

The seven allocating BDN cases likewise include harness overhead, and are not
identical to direct counter intervals:

| Owned diagnostic case | Inclusive GC bytes / operations |
|---|---:|
| Materialized lexer long / short | 2148007936 / 65536; 1509951792 / 4194304 |
| Compile invalid / long / short | 23073088 / 32768; 3240186592 / 16384; 3724920240 / 1048576 |
| Context default / registered | 6744444480 / 2097152; 3330281024 / 1048576 |

All 25 BDN cases emitted a warning that High process priority could not be set
(permission denied); they completed at normal priority. Timing is provisional
local Arm64 evidence. M4 composition must confirm whole-query behavior and M9
must confirm x64/Arm64 release performance. No full evaluator is used here.

Retained ownership is explicit: AST nodes retain owned names/literals/snapshotted
collections and context URI strings, not the full expression or drafts. The
compiler retains no mutable operation cache; immutable 27-core signature metadata
is process-wide. No separate retained-heap census was measured, and none is
claimed. Raw logs, BDN output and counter distributions were transient ignored
artifacts; only these summary tables and analysis are committed.
