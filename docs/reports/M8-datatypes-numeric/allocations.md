# Numeric allocations and provisional performance

Accepted/tested source `9dee538394f076df86ea12ea282126fbe39da8f2`;
preparation `74c5bf92eee366dd6a23be0f907163cfc641c13c`.
Frozen manifest SHA256
`0efa1f6acf38bb562cdc6b0e747e8c5fef5cd593e3a6a070d9e2a4f2138fdae7`.
The sole approved supervisor ran once, allocation probe followed by ten prebuilt
ShortRun InProcessNoEmit BDN cases. Lifecycle: status0, 71.7018669169629seconds,
438 identities unchanged before/after; children11035/11037 both exit0, reaped,
groups absent and logs closed. Raw inspection waited for durable RELEASEACK.
Coordinator independently confirmed distributions, residuals and lifecycle.

Apple M4 Pro, Arm64, macOS15.8.1, SDK10.0.400, runtime10.0.11, workstation GC;
BenchmarkDotNet0.15.8 with MemoryDiagnoser/ShortRunJob, three warmup and measured
iterations, one launch. No generated builds, retries or other measurement workload.
Counter-disabled controls consumed all routes/methods; discovery asserted ten
NoEmit cases; elevated process and identity preflights passed.

## Per-thread probe

Every route: 32 warm operations then 64 samples. Each distribution was constant:

| Route | Bytes per sample |
| --- | ---: |
| Decimal short/4096-digit comparison; short/long hash | 0 |
| Float comparison/hash; invalid lexical; digit measures | 0 |
| Owned decimal `12345.6789` (nine significant digits) | 112 |
| Owned decimal: 4096 copies of `1` | 8280 |
| Owned double `-1.23456789012345678e-200` | 56 |
| Owned zero `-0.000` | 64 |
| Operation setup | 512 |

These finite warmed probe samples support the borrowed no-allocation boundary;
they do not prove zero allocations for every future process/runtime execution.
Cold fixture setup measured67040B, including static metadata/input initialization,
provider/context/operation and persisted fixture values. Positive1024-byte array
control measured1048B. There is no pooling in Numeric; bounded IEEE scratch is
stack storage and decimal arrays belong to retained values.

For256 retained4096-digit decimals, GC.GetTotalMemory(true) changed214864→4479296B
(delta4264432B). This is a process heap estimate including the retaining array and
collector effects, not an exact size for the sole retained payload graph. The
separate per-thread owned parsing result8280B/value is the observed construction
allocation. No global provider cache is introduced.

## BDN summary

Short input is canonical `12345678`, eight digits; long input is4096 `1`s.
Comparison differs only in the final digit (`9`); invalid parsing appends `x`.
Thus BDNshort owned104B and probeshort owned112B use different array lengths.
Baseline compares canonical positive integral characters with identical charged
character work; it omits core/provider joins and does not claim full parser scope.

| Method | Short mean ± SD (ns) | Long mean ± SD (ns) | Displayed B/op short / long |
| --- | ---: | ---: | ---: |
| Scalar baseline comparison | 18.88 ± 0.019 | 6446.96 ± 34.196 | 0 / 0 |
| Provider comparison | 31.30 ± 0.036 | 6902.25 ± 8.144 | 0 / 0 |
| Provider hash | 26.06 ± 0.247 | 5956.07 ± 53.138 | 0 / 0 |
| Owned parse | 67.48 ± 0.141 | 13360.36 ± 21.812 | 104 / 8280 |
| Invalid suffix | 31.40 ± 0.079 | 6065.11 ± 35.333 | 0 / 0 |

Displayed BDN zeros are rounded. Exact raw GC aggregate bytes/operations:

| Case/method | Bytes / operations |
| --- | ---: |
| Long baseline | 0 / 131072 |
| Long comparison | 3224 / 131072 |
| Long hash / invalid suffix (each) | 2976 / 131072 |
| Long owned parse | 542661336 / 65536 |
| Short baseline / hash (each) | 2976 / 33554432 |
| Short comparison | 3224 / 16777216 |
| Short invalid suffix | 2976 / 16777216 |
| Short owned parse | 872450968 / 8388608 |

Borrowed residuals are preserved as unattributed; no universal BDNzero claim or
assumed harness cause. BDN high-priority setup was denied in all ten cases, so
these single-host ShortRun timings are informational. Provider joins add about
12.42ns for short comparison and455.29ns for long. No bulk-scanning/token or
SearchValues optimization is claimed; straightforward loops preserve exact work
charging and cancellation. End-to-end composer confirmation and x64 evidence
remain downstream. Raw logs/results were preserved through coordinator independent inspection,
then removed with other ignored numeric transient outputs after evidence
acceptance. Concise summaries/analysis/identities remain committed; prepared
source and binaries remain unchanged for coordinator worktree cleanup.
