# Prepared measurement plan (not measurement evidence)

Measurements require a coordinator-prepared bounded exclusive lease after
independent source review and verified model construction repair adoption.
The driver has 21 cases in exactly one [MemoryDiagnoser, ShortRunJob] job with
3 warmups, 3 iterations, 1 launch. The only mutator is InProcessNoEmit toolchain;
no job flags, dynamic source generation or build runs during the lease.

| Group | Cases | Comparison/ownership |
|---|---:|---|
| Lexical | 4 | Short/long Unicode borrowed vs explicit token materialization, identical consumed offsets/kinds |
| Delimiter | 8 | Short/long Unicode loop, IndexOf, IndexOfAny, cached SearchValues; identical stop-index oracle |
| Vocabulary | 4 | Known maximum-length axis/common-prefix miss; span dispatch vs sequential SequenceEqual |
| Compile | 3 | Owned AST short/long Unicode; invalid Unicode diagnostic (separate allocation boundary) |
| Context | 2 | Owned default/registered static context copying, separate from compile with reused context |

Semantic controls include empty/missing/early/late delimiter hits, all 13 axes,
unknown and case variants, multilingual names, malformed surrogates, quotes,
long vocabulary misses and materialization checksum equality. Setup generates
fixtures outside timing. Discovery and --control keep allocation counters disabled.

Leased --allocation warms 32 x 32 groups of two borrowed inputs before measuring
32 x 32 groups (2048 borrowed scans) and matcher/delimiter work. It preserves each
batch allocation value and the exact total gate (target 0 B), with explicit
materialized token positive controls, 32 owned compiler outputs and 32 registered
context copies. Cold compiler/static context setup is separate from warm work.
There is no global cache; retained AST ownership is described in design.md.
Raw counter JSON/distributions and BDN artifacts remain transient; committed
reports contain only summary tables/analysis and honest failures/limitations.

The pinned host is `/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet`.
After repair, fresh build and clean candidate commit, `freeze.py capture` records
all runtime source/build files, generated files, actual driver binary closure and
SDK 10.0.400 / runtime 10.0.11 / host-fxr files. `freeze.py check` surrounds leased
execution. Expected maximum lease duration: 1200 seconds; each command uses the
actual prebuilt binary. Exact command arrays and manifest digest will be sent
when the final candidate is ready. No measurement has been performed here.

Measurements on the local Arm64 host remain informational for timing and require
later x64 confirmation; microbenchmark search gains do not imply validating
lexer gains or full compile speedups. No prebuilt full XPath evaluator exists.
