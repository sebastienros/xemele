# M5 bounded source instance validity provenance

Implemented/tested runtime source `d96ed87af7b73af3e82744310c71c100732d1635`; final prepared/measured source and harness `c800f18472c1796847f99b7939cfbe26ab603672`. This is isolated branch implementation, awaiting coordinator integration verification.

The parser retains one first owned DTD.Content witness and one immutable original declaration reference for Element Valid restrictions erased by expansion. EMPTY references include empty general entities and predefined/numeric references. Children-content direct character-reference whitespace and reparsed entity replacement character-reference whitespace are invalid; internal declaration '&#32;' already becomes legal literal XML S, while '&#38;#32;' constructs a reference in replacement text. No raw lexical/per-node sidecar collection is added, and the nonvalidating parser continues accepting these well-formed source inputs. The optional internal completion hook publishes once before EndDocument, only on nonrecovering completion; unchanged nonnegative original completion revision and applicable declaration category guard consumption. Mutation/restoration, cloned/manual trees never reuse the source witness. ANY/mixed category changes on otherwise unchanged source refuse with Fatal DTD.IncompleteInput: ignoring the only first witness could conceal another erased nested restriction. Equivalent applicable categories can use the first invalidity proof; current-tree and standalone checks continue.

For original E3e13, fully read internal parameter declarations prove a symbolic general reference undeclared. FromDocument permits compilation only when all sidecars are proved absent in the complete original associated model; Validate returns complete invalid DTD.UndeclaredEntity at the first occurrence. The sidecars remain symbolic and intact, with no invented empty replacement or content traversal using it. A different associated compiled model, declared-unexpanded entity, unread declaration, incomplete attributes, missing/changed completion proof, or recovery remains incomplete. The decision clarification is owned in docs/decisions/M5/validation-canonicalization.md; missing ordinary defaults on manual trees retain the earlier contract.

Primary source: [XML 1.0 Fifth Edition Element Valid](https://www.w3.org/TR/2008/REC-xml-20081126/#elementvalid) and [Entity Declared](https://www.w3.org/TR/2008/REC-xml-20081126/#vc-entdeclared). BCL controls agree on literal S, non-S, mixed and ordinary EMPTY text. The corpus's earlier source audit records that BCL also erases/accepts the three E15 lexical references; normative source expectation takes precedence, with no exclusions.

## Functional checks and compatibility

Pinned dotnet executable `/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet`; SDK10.0.400. Every executable uses argv `--fx-version 10.0.11`, and tests/probe assert actual .NET10.0.11/Arm64.

- Release test and probe builds zero warnings/errors. Offline cache-only restore succeeds; NuGetAudit=false, server-disabled no-restore builds.
- 58 focused cases passed:41 new plus17 owned older provenance cases. Includes12 SHA-pinned original E15a/g/h/E13 cases over character, one-byte sync and async input, paired literal/reference controls, first-only100violations, nested changed-category counterexample, all EMPTY reference kinds, non-S ordinary invalidity, mutation/restoration/clone/manual, associated/separate E3 models, unknown/unread/declared/recovery/incomplete-attribute cases, cancellation/quota, all splits with every callback paused, optional-hook exactlyonce/order and cancellation before publication.
- 2270 relevant preservation cases pass: parser exact quota/source/composition/grammar/nonvalidating, prior compiler/current validation/source provenance, syntax/intern/callback views, new attribute and parameter validity repairs. This selected count overlaps the17 old focused cases; no unique combined total claimed. No broad suite or allocation tests ran.
- Full API union948 compiled members matched; public API unchanged.159 task records validate and diff check passes.
- Counters-disabled probe controls verify public FromDocument+Validate outcomes and10,000-reference bounded streaming completion. Discovery verifies16 ShortRun NoEmit cases with3warmups,3iterations,1launch; no build/generation or CLI job flags.

One intermediate test build failed xUnit1026 because the category-switch theory's old Boolean parameter was unused; corrected to expected completeness. A subsequent accidental execution used the previously built47-case assembly, so it is not claimed as checking the new tests. Final build and58-case execution succeeded before the source commit. No measurements failed or were retried.

## Bounded work, ownership and field applicability

New DOM-only declaration lookup borrows tag QName scratch, uses indexed existing declaration storage, charges parser EvaluationWork per candidate and per bounded256-UTF16-unit comparison chunk, and checks cancellation between chunks. This adds explicit semantic work for DOM DTD source provenance; exact3work succeeds/2fails for the authored lookup boundary. The no-DTD and ordinary non-provenance sink paths add no lookup work or diagnostic allocations. Existing token/character/depth/name/source budgets remain in force. Unresolved-reference proof scans charge validation work and lexical comparisons, guard ContentRevision and preserve the first location; quota refusal remains VALIDATION.Limit.

Core and XmlDocument each add two nullable reference fields (16logical payload bytes apiece on Arm64); each existing bounded parser Frame adds one immutable declaration reference (8logical bytes). These are logical field payloads, not claims about aligned object/array allocation deltas. Frames remain depth-bounded existing transient parser storage; no per-document-node collection. First invalid source allocates at most one owned XmlDiagnostic per Core, published to document metadata; declaration/URI references are immutable owned model data, never input/resource owners. Valid source adds no diagnostic object. Owned DOM/model/default/index/source setup, results, sidecars, diagnostics and retained storage allocate and are measured separately below. Exact isolated diagnostic heap size and aligned field delta were not measured.

## Exact prepared exclusive measurements

Grant durable main `1ec62782058162133170cd9d4fa4b812abee5fcc`, lease `m5-dtd-instance-provenance-c800f18`; durable release `2768718b5d98108834b9bd1bf8c8fd075ed2b009`. Exactly one prepared sequence with1050s total cap: allocation60s, benchmark900s, initial/final identity35s each. Runner/phase exits all0; initial/final identities0; no_child=true. Full manifest SHA256 `cacbb23e3a4d0066fd0e3714b84ef17515e4da7db9b4c0543c1841517d564f7e` covered170 source/generated/config files,43 binaries and3852 full SDK/runtime/host files plus effective environment. TieredCompilation/TieredPGO/ReadyToRun0, explicit actual .NET10.0.11 Arm64. Both classes declare MemoryDiagnoser+ShortRunJob, NoEmit, no CLI job options. All16 cases succeeded. Ordinary-sink versus optional-proof sink is an in-revision semantic-output control, not a prior-revision timing comparison or speedup claim.

All counters below are inclusive. Probe label concatenation occurs before end-counter argument evaluation in first/warm/owned domains and is included. No wrapper/control/setup subtraction and no warm-zero or isolated-first-diagnostic-size claim. Steady domain labels are prepared outside their intervals. Five steady domains each have32 independently observed batches of4096operations; all160 numerators are0. Callback scalar/name counts and ScanResult outcomes are consumed.

| Steady borrowed domain | Batches | Inclusive total bytes | Total operations | Per-batch bytes min/max |
|---|---:|---:|---:|---:|
| steady-borrowed-reference-False |32|0|131072|0/0|
| steady-borrowed-reference-True |32|0|131072|0/0|
| steady-borrowed-name-<a/> |32|0|131072|0/0|
| steady-borrowed-name-<p:a/> |32|0|131072|0/0|
| steady-borrowed-name-<p:名/> |32|0|131072|0/0|

| Inclusive first/warm/owned domain | Numerator bytes | Denominator operations |
|---|---:|---:|
| fixture-initialization | 221168 | 1 |
| positive-control | 1048 | 1 |
| stream-setup-False | 19048 | 1 |
| first-reference-False | 64 | 1 |
| warm-borrowed-reference-False | 80 | 1000 |
| stream-setup-True | 19048 | 1 |
| first-reference-True | 136 | 1 |
| warm-borrowed-reference-True | 80 | 1000 |
| first-owned-name-<a/> | 88 | 1 |
| warm-borrowed-name-<a/> | 72 | 1000 |
| first-owned-name-<p:a/> | 344 | 1 |
| warm-borrowed-name-<p:a/> | 72 | 1000 |
| first-owned-name-<p:名/> | 344 | 1 |
| warm-borrowed-name-<p:名/> | 72 | 1000 |
| owned-dom-no-dtd | 1125656 | 100 |
| owned-dom-literal | 2921840 | 100 |
| owned-dom-charref | 2918456 | 100 |
| owned-dom-entity-literal | 2968072 | 100 |
| owned-dom-entity-reparsed | 2996096 | 100 |
| owned-dom-empty | 2265656 | 100 |
| owned-dom-missing | 2986240 | 100 |
| owned-validate-literal | 56072 | 100 |
| owned-from-document-literal | 320080 | 100 |
| owned-validate-charref | 80072 | 100 |
| owned-from-document-charref | 320080 | 100 |
| owned-validate-missing | 87272 | 100 |
| owned-from-document-missing | 144880 | 100 |

Retained whole-heap observation:454920 bytes before,1112256after, inclusive delta657336 bytes for100retained documents. Retained array, source fixture, options and runtime roots were allocated after the baseline and are INCLUDED. This is an approximate whole-heap domain, not an isolated or per-document retained footprint.

### Benchmark summaries

Timing/error is informational ShortRun evidence on this one Arm64 host. DOM construction has necessary persistence/setup allocations. Rounded allocation dashes on borrowed cases are not literal zero: exact positive GC records follow.

```

BenchmarkDotNet v0.15.8, macOS Sequoia 15.8.1 (24H32) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a

Job=ShortRun  Toolchain=InProcessNoEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method        | Scenario        | Count | Mean      | Error     | StdDev    | Gen0   | Gen1   | Allocated |
|-------------- |---------------- |------ |----------:|----------:|----------:|-------:|-------:|----------:|
| **ParseOwnedDom** | **charref**         | **8**     |  **8.750 μs** | **0.1917 μs** | **0.0105 μs** | **3.4943** | **0.0305** |  **28.57 KB** |
| **ParseOwnedDom** | **charref**         | **128**   | **28.134 μs** | **0.2924 μs** | **0.0160 μs** | **3.6011** | **0.0610** |  **29.51 KB** |
| **ParseOwnedDom** | **empty**           | **8**     |  **5.741 μs** | **0.3806 μs** | **0.0209 μs** | **2.7084** | **0.0153** |  **22.15 KB** |
| **ParseOwnedDom** | **empty**           | **128**   | **22.348 μs** | **1.9099 μs** | **0.1047 μs** | **3.0518** | **0.0916** |  **24.96 KB** |
| **ParseOwnedDom** | **entity-literal**  | **8**     |  **8.825 μs** | **0.4306 μs** | **0.0236 μs** | **3.5553** | **0.0305** |  **29.06 KB** |
| **ParseOwnedDom** | **entity-literal**  | **128**   | **29.017 μs** | **0.3879 μs** | **0.0213 μs** | **3.9978** | **0.0610** |  **32.81 KB** |
| **ParseOwnedDom** | **entity-reparsed** | **8**     | **10.313 μs** | **0.1511 μs** | **0.0083 μs** | **3.5553** | **0.0305** |  **29.13 KB** |
| **ParseOwnedDom** | **entity-reparsed** | **128**   | **50.717 μs** | **2.0312 μs** | **0.1113 μs** | **3.9673** | **0.1221** |  **32.87 KB** |
| **ParseOwnedDom** | **literal**         | **8**     |  **7.768 μs** | **0.4117 μs** | **0.0226 μs** | **3.4790** | **0.0305** |   **28.5 KB** |
| **ParseOwnedDom** | **literal**         | **128**   | **11.727 μs** | **0.3221 μs** | **0.0177 μs** | **3.6011** | **0.0305** |  **29.44 KB** |
| **ParseOwnedDom** | **missing**         | **8**     |  **8.701 μs** | **0.2783 μs** | **0.0153 μs** | **3.5400** | **0.0763** |  **28.99 KB** |
| **ParseOwnedDom** | **missing**         | **128**   | **49.675 μs** | **2.0911 μs** | **0.1146 μs** | **5.7983** | **0.6104** |  **47.83 KB** |
| **ParseOwnedDom** | **no-dtd**          | **8**     |  **1.149 μs** | **0.0958 μs** | **0.0053 μs** | **1.3447** | **0.0401** |  **10.99 KB** |
| **ParseOwnedDom** | **no-dtd**          | **128**   |  **5.234 μs** | **0.3852 μs** | **0.0211 μs** | **1.4572** | **0.0458** |  **11.93 KB** |

```

BenchmarkDotNet v0.15.8, macOS Sequoia 15.8.1 (24H32) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a

Job=ShortRun  Toolchain=InProcessNoEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                  | OptionalProof | Mean     | Error     | StdDev   | Allocated |
|------------------------ |-------------- |---------:|----------:|---------:|----------:|
| **BorrowedScalarCallbacks** | **False**         | **196.0 ns** |  **95.73 ns** |  **5.25 ns** |         **-** |
| **BorrowedScalarCallbacks** | **True**          | **207.8 ns** | **544.78 ns** | **29.86 ns** |         **-** |


| Case | Inclusive GC allocated bytes | Operations | Inclusive bytes/operation |
|---|---:|---:|---:|
| Scenario=charref, Count=8 |1917371560|65536|29256.768188476562|
| Scenario=charref, Count=128 |990219040|32768|30219.086914062500|
| Scenario=empty, Count=8 |2973299080|131072|22684.471740722656|
| Scenario=empty, Count=128 |837490720|32768|25558.188476562500|
| Scenario=entity-literal, Count=8 |1949963688|65536|29754.084594726562|
| Scenario=entity-literal, Count=128 |1100888096|32768|33596.438476562500|
| Scenario=entity-reparsed, Count=8 |1954682280|65536|29826.084594726562|
| Scenario=entity-reparsed, Count=128 |551460512|16384|33658.478515625000|
| Scenario=literal, Count=8 |1912635120|65536|29184.495849609375|
| Scenario=literal, Count=128 |1975716272|65536|30147.037841796875|
| Scenario=missing, Count=8 |1945200064|65536|29681.397460937500|
| Scenario=missing, Count=128 |802446680|16384|48977.458496093750|
| Scenario=no-dtd, Count=8 |5901388704|524288|11256.005676269531|
| Scenario=no-dtd, Count=128 |1601178528|131072|12216.022705078125|
| OptionalProof=False |4320|4194304|0.001029968262|
| OptionalProof=True |4656|4194304|0.001110076904|

Independent source/harness review approved d96ed87 and c800f18. Coordinator independently inspected every allocation JSON line including160steady batches, positive first/warm/owned totals, retained wholeheap, all16 inclusive GC records, actualruntime banners and both summaries. Only summary tables/analysis are committed. After durable summary commit57e0c7c860c30e9090ca053efcfc4a1968012d89 and the coordinator's conditional cleanup acknowledgment, only generated allocation/benchmark raw logs and BDN result files were deleted; ignored prepared identity metadata remains. No runtime ownership requests are open; coordinator integration verification remains.
