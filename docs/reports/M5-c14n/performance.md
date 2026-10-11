# Canonicalization allocation and timing evidence

Measured frozen revision8528b1b48756ac6cdb873b69f335b4326d3d8db1; runtime
implementation86d201877172af32c0afee7a511bb82b281bf8dc. Exact coordinator grant
m5-c14n-8528b1b durably recorded at8387a3f; release095db55. One runner execution
passed initial/final identity0, allocation0, benchmark0, runner0; own-group
cleanup finally reported no_child=true. Benchmark elapsed79.58s, twelve cases
successful. No retries. Actual.NET10.0.11/Arm64, SDK10.0.400, Apple M4 Pro,
macOS15.8.1; timing is informational. DOTNET_TieredCompilation=0,
DOTNET_TieredPGO=0 and DOTNET_ReadyToRun=0 were explicit throughout.

Prepared manifest SHA2564474ed3a7817dc14c919162364bc022dbbeb7dc9e1230960ba52d104b7dfe0c2
covered158 source/generated/config/fixture/test/runner entries,43 binary
entries and3852 full SDK/runtime/fxr entries. Stable effective environment hash:
8340de016d8be396b0ecbad08b83beedc7f3efa7be6d142711a917289b6d4a31
source map SHA256 dc9f0a6db9b4ba4859393299f68566460bc4ba4a9eba367ddea4ec28e2f225cc
binary map SHA256 c6c730135882007d283002d14cb90a2b5cee9d70da6e8e1616b2a221290eef58
runtime map SHA256 959cac98cbb2c75664dfb69e3d0eea9665bde67087a1e8da5fbb0ec0545aa74b

Cold fixture setup3829384B includes DOM parsing and XPath selection construction.
The borrowed scalar encoder's preallocated buffer/delegate/state domain measured
32 warm and32 steady batches,512 groups each,three inputs/encodes per group.
Every batch measured0B; inclusive whole warm0B/16384groups and measured0B/16384groups
(49152 encoding calls each). Warm consumption28884992; total consumed32265856
includes subsequent caller runs. No baseline subtraction. These totals apply
only to the borrowed encoder; canonical preflight deliberately owns dictionaries,
plans, sorting scratch and synthesized attribute values.

| Mode | Selection | Caller-stream bytes /128ops | Owned-output bytes /128ops |
|---|---|---:|---:|
| 0 | full document | 12131576 /128 | 12879344 /128 |
| 0 | exact selected | 21681896 /128 | 24049384 /128 |
| 1 | full document | 12131576 /128 | 12879344 /128 |
| 1 | exact selected | 21681648 /128 | 24049384 /128 |
| 2 | full document | 13748472 /128 | 14750192 /128 |
| 2 | exact selected | 19715568 /128 | 20710896 /128 |

Positive control1048B/one byte[1024]; invalid UTF16 error path38912B/128ops.
Retained64 owned outputs contain183296bytes; whole forced-GC heap427816→798992,
delta371176B. The output array slot container exists before the first heap
snapshot; whole heap delta includes other retained/lazy state and is not an
isolated object-size assertion. Owned-output allocation includes caller-result
MemoryStream growth/ToArray as well as the same canonical preflight.

Both benchmark classes declare [MemoryDiagnoser, ShortRunJob]. Discovery and
execution confirmed exactly one ShortRun job, InProcessNoEmit,three warmups,
three measured iterations,one launch; no CLI job flags. The materialized encoder
baseline uses Replace+UTF8 and returns an owned byte[]; the span encoder writes
into a borrowed caller buffer and returns a count. Same exact bytes, different
ownership shapes; their timings do not establish a like-for-like whole-operation
speedup. Caller stream is a counting sink; output still performs exact encoding.

| Case | Mean | Gen0 | Gen1 | Gen2 | Inclusive allocated bytes /operations |
|---|---:|---:|---:|---:|---:|
| EncodingBenchmarks.OwnedMaterialized [InputIndex=0] | 72.330 ns | 256 | 0 | 0 | 2147483648 /8388608 |
| EncodingBenchmarks.BorrowedSpan [InputIndex=0] | 18.854 ns | 0 | 0 | 0 | 2640 /33554432 |
| EncodingBenchmarks.OwnedMaterialized [InputIndex=1] | 2.929 us | 352 | 4 | 0 | 2950695504 /262144 |
| EncodingBenchmarks.BorrowedSpan [InputIndex=1] | 2.127 us | 0 | 0 | 0 | 2640 /262144 |
| EncodingBenchmarks.OwnedMaterialized [InputIndex=2] | 78.958 ns | 328 | 0 | 0 | 2751466064 /8388608 |
| EncodingBenchmarks.BorrowedSpan [InputIndex=2] | 37.834 ns | 0 | 0 | 0 | 672 /16777216 |
| CanonicalStreamBenchmarks.CallerStream [Mode=0, Selected=False] | 28.671 us | 371 | 46 | 0 | 3105715872 /32768 |
| CanonicalStreamBenchmarks.CallerStream [Mode=0, Selected=True] | 69.048 us | 165 | 33 | 0 | 1387638256 /8192 |
| CanonicalStreamBenchmarks.CallerStream [Mode=1, Selected=False] | 28.883 us | 371 | 46 | 0 | 3105715248 /32768 |
| CanonicalStreamBenchmarks.CallerStream [Mode=1, Selected=True] | 70.150 us | 165 | 33 | 0 | 1387638544 /8192 |
| CanonicalStreamBenchmarks.CallerStream [Mode=2, Selected=False] | 33.313 us | 210 | 26 | 0 | 1759828648 /16384 |
| CanonicalStreamBenchmarks.CallerStream [Mode=2, Selected=True] | 57.604 us | 301 | 50 | 0 | 2523607184 /16384 |

BDN displayed borrowed allocation dashes, while its actual inclusive numerators
were2640,2640 and672B; these positive residuals remain reported without
subtraction or attribution. The dedicated counter probe's exact zero remains
a separate scope/result. BDN displays owned256/11256/328B and caller92.56,
165.42,92.56,165.42,104.89,150.42KB/op; exact inclusive totals above take
precedence over rounded displays.

Raw allocation.json/allocation.log, benchmark.log and the BDN log/reports were
inspected after durable release. Root received exact numerators and raw paths
before cleanup. Only summary/analysis is committed; original raw files remain
until coordinator independent inspection ACK. No measurements are repeated by
functional preservation checks; the Tree allocation method is explicitly excluded.

Coordinator independently inspected and ACK'd all allocation distributions,
exact numerators/denominators and all twelve BDN GC rows. Owned generated raw
cleanup is authorized after this durable evidence commit; no counter reruns.
