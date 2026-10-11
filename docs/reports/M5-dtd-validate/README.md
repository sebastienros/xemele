# M5 DTD instance validation

Tested and measured implementation revision: `455000b57f583ea132488b7fcc240c54e37ed15f`. Isolated branch `codex/m5-dtd-validate`; local implementation only, coordinator integration verification remains outstanding.

The exact frozen public DtdSchema Compile/FromDocument/Validate surface adds four members (948 total). Compile reads an external-subset body through the verified M2 parser and supplied resolver only, sharing scalar source/name/entity and compiler work in one ValidationOperation. FromDocument uses the retained combined model without resolution. Original first duplicate-declaration diagnostics take precedence; retained unread parameter evidence refuses compilation at its original location.

Validation inspects current decoded lexical QNames, namespace declaration attributes, content models, required/fixed/type constraints, ID uniqueness and forward references, notation/unparsed entities, and standalone dependencies. It adds no attributes, normalizes no values, fetches no resources and changes no DOM nodes. Immutable schemas can be reused concurrently for separate documents. ID/IDREF bookkeeping owns necessary retention. Every traversal/lookup has bounded work and cancellation; captured nonnegative ContentRevision guards mutations, and recovery/unresolved/incomplete attributes or unread declarations yield Fatal DTD.IncompleteInput.

For standalone external element-only whitespace, unchanged complete parsed input must retain the exact source completion revision and original effective external children-content declaration. Numeric-reference whitespace can therefore be trusted; mutation/restoration or unknown/different original declarations make the result incomplete. CDATA remains a distinct structural node: XML 1.0 Fifth Edition VC Element Valid forbids it in children content, including whitespace; EMPTY also forbids comments/PIs. These content rules are tested against System.Xml controls and the primary specification: https://www.w3.org/TR/2008/REC-xml-20081126/#elementvalid .

## Checks

Pinned executable `/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet`, SDK10.0.400; every executable uses `--fx-version 10.0.11`, and tests/sample/probe assert actual `.NET 10.0.11`, Arm64.

- Release tests/sample/probe builds: zero warnings/errors, offline cache restore with NuGetAudit=false; server-disabled no-restore builds.
- 89 focused cases passed; 2091 prior parser/budget/source/composition/grammar/nonvalidating/compiler/declaration/source-completion/unread-completeness cases passed, allocation methods excluded. No failed/skipped/not-run cases.
- Full API union:948 compiled members matched; own API fragment is docs/api/M5-dtd-validate.txt.
- Counters-disabled executable sample verifies forward IDREF success, type/duplicate-ID errors, quota incompleteness and no default augmentation. Probe discovery verifies14 ShortRun NoEmit cases, three warmups/iterations and one launch.
- Planning156 records validate; final diff check is recorded in task evidence.

Earlier provisional checks honestly failed: missing discovery output argument; test called nonexistent ParseAsync (corrected to LoadAsync); direct undeclared-PE expected code corrected to existing DTD.UndeclaredEntity. These occurred before the tested revision and successful final checks. No measurement failed or was retried.

## Prepared exclusive measurements

Coordinator grant `m5-dtd-validate-455000b`, durable grant `fa712f0`, release `bef7626`. One sequence, maximum1050seconds: allocation60seconds, benchmark900seconds, initial/final identity35seconds each. Existing NoEmit harness never builds/generates; scoped own-group cleanup finally. All phase exits0, initial/final identity0, no_child=true. Manifest SHA256 `612fc368b6962d6547f48add9d9d9655ec037318c50b00a5a00e529ba06f0a78` covers190 source/generated/config,50 binary and3852 full SDK/runtime/host files and effective environment. TieredCompilation/TieredPGO/ReadyToRun0; explicit runtime10.0.11. Both benchmark classes declare [MemoryDiagnoser, ShortRunJob], no CLI job flags. All14 cases completed successfully.

Allocation counter domains use no subtraction of controls or setup. Fixture initialization includes initial QName pool growth. Warm borrowed QName buffers retain/dispose pooled scratch; they do not retain lexical sidecars.

| Domain | Inclusive numerator bytes | Denominator |
|---|---:|---:|
| fixture-initialization | 59936 | 1 operations |
| positive-control | 1048 | 1 operations |
| warm-borrowed-qname-0 | 0 | 100000 operations |
| warm-borrowed-qname-1 | 0 | 100000 operations |
| warm-borrowed-qname-2 | 0 | 100000 operations |
| warm-borrowed-qname-3 | 0 | 10000 operations |
| owned-valid-id-index | 91200 | 100 operations |
| owned-invalid-diagnostics | 138400 | 100 operations |
| owned-quota-diagnostic | 68000 | 100 operations |
| owned-schema-compilation | 1628424 | 100 operations |

Retained whole-heap approximation:359512 bytes before,526512 after, inclusive delta167000 bytes for100 retained invalid ValidationResults. This separate live-heap domain includes collector/global effects and is not a per-result exact retained size; no control subtraction.

Valid no-ID complete validation allocates368 B/call at both8 and128 elements for owned operation/result infrastructure. IDs and diagnostics grow owned allocations; quota refuses early with680 B/call. Compiler output is owned and allocates. Zero-byte claims apply only to the four warmed borrowed QName counter domains, not full Validate/Compile calls. Time/error below is ShortRun informational evidence without a prior facade baseline or speedup claim.

```

BenchmarkDotNet v0.15.8, macOS Sequoia 15.8.1 (24H32) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a

Job=ShortRun  Toolchain=InProcessNoEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method             | Scenario | Mean     | Error     | StdDev    | Gen0   | Gen1   | Allocated |
|------------------- |--------- |---------:|----------:|----------:|-------:|-------:|----------:|
| **CompileOwnedSchema** | **ascii**    | **3.317 μs** | **0.0100 μs** | **0.0006 μs** | **1.3161** | **0.0267** |  **10.77 KB** |
| **CompileOwnedSchema** | **unicode**  | **3.303 μs** | **0.0947 μs** | **0.0052 μs** | **1.3161** | **0.0267** |  **10.77 KB** |

```

BenchmarkDotNet v0.15.8, macOS Sequoia 15.8.1 (24H32) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a

Job=ShortRun  Toolchain=InProcessNoEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method             | Scenario | Count | Mean        | Error       | StdDev    | Gen0   | Gen1   | Allocated |
|------------------- |--------- |------ |------------:|------------:|----------:|-------:|-------:|----------:|
| **ValidateCurrentDom** | **ascii**    | **8**     |  **1,602.5 ns** |    **92.25 ns** |   **5.06 ns** | **0.0439** |      **-** |     **368 B** |
| **ValidateCurrentDom** | **ascii**    | **128**   | **24,078.3 ns** |   **749.51 ns** |  **41.08 ns** | **0.0305** |      **-** |     **368 B** |
| **ValidateCurrentDom** | **ids**      | **8**     |  **3,109.4 ns** |   **186.79 ns** |  **10.24 ns** | **0.2251** |      **-** |    **1888 B** |
| **ValidateCurrentDom** | **ids**      | **128**   | **47,799.3 ns** | **1,228.97 ns** |  **67.36 ns** | **1.7700** | **0.0610** |   **15120 B** |
| **ValidateCurrentDom** | **invalid**  | **8**     |  **1,897.0 ns** |    **71.66 ns** |   **3.93 ns** | **0.1678** |      **-** |    **1416 B** |
| **ValidateCurrentDom** | **invalid**  | **128**   | **27,584.7 ns** | **3,779.84 ns** | **207.19 ns** | **1.7700** | **0.1221** |   **15048 B** |
| **ValidateCurrentDom** | **prefixed** | **8**     |  **2,127.2 ns** |   **564.73 ns** |  **30.96 ns** | **0.0420** |      **-** |     **368 B** |
| **ValidateCurrentDom** | **prefixed** | **128**   | **29,960.5 ns** |   **688.83 ns** |  **37.76 ns** | **0.0305** |      **-** |     **368 B** |
| **ValidateCurrentDom** | **quota**    | **8**     |    **142.0 ns** |    **18.00 ns** |   **0.99 ns** | **0.0811** |      **-** |     **680 B** |
| **ValidateCurrentDom** | **quota**    | **128**   |    **145.3 ns** |     **8.61 ns** |   **0.47 ns** | **0.0811** |      **-** |     **680 B** |
| **ValidateCurrentDom** | **unicode**  | **8**     |  **1,665.8 ns** |    **12.29 ns** |   **0.67 ns** | **0.0439** |      **-** |     **368 B** |
| **ValidateCurrentDom** | **unicode**  | **128**   | **25,014.0 ns** | **3,282.88 ns** | **179.95 ns** | **0.0305** |      **-** |     **368 B** |


Exact inclusive MemoryDiagnoser GC allocation numerators/denominators were inspected, including fractional bytes hidden by rounded tables. No overhead subtraction:

| Case | GC allocated bytes | Operations | Inclusive bytes/operation |
|---|---:|---:|---:|
| Scenario=ascii, Count=8 | 192937984 | 524288 | 368.000000000 |
| Scenario=ascii, Count=128 | 12061600 | 32768 | 368.090820312 |
| Scenario=ids, Count=8 | 494930848 | 262144 | 1888.011352539 |
| Scenario=ids, Count=128 | 247726080 | 16384 | 15120.000000000 |
| Scenario=invalid, Count=8 | 742394784 | 524288 | 1416.005676270 |
| Scenario=invalid, Count=128 | 493095840 | 32768 | 15048.090820312 |
| Scenario=prefixed, Count=8 | 96470960 | 262144 | 368.007507324 |
| Scenario=prefixed, Count=128 | 12061264 | 32768 | 368.080566406 |
| Scenario=quota, Count=8 | 2852129696 | 4194304 | 680.000709534 |
| Scenario=quota, Count=128 | 2852129696 | 4194304 | 680.000709534 |
| Scenario=unicode, Count=8 | 192940960 | 524288 | 368.005676270 |
| Scenario=unicode, Count=128 | 12061600 | 32768 | 368.090820312 |
| Scenario=ascii | 2890228880 | 262144 | 11025.348205566 |
| Scenario=unicode | 2890228544 | 262144 | 11025.346923828 |

Only summary tables and analysis are committed. Ignored raw allocation/benchmark logs, BDN result files and full manifest are retained for coordinator independent inspection until cleanup acknowledgment. No later milestone or active peer implementation was consumed. No open runtime ownership requests remain; coordinator integration checks and raw cleanup remain.
