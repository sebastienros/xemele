# Complete-library composition measurements

Accepted source: fb8fc3b5b4ef1d83da85679c296a48b5863dde32. Prepared evidence
fdc7ff8fa06dcd178e94a60140ad2f0fc706eaca; manifest456 identities,
4cd80164c24e5551468139806ef42282f1ff9fb3325c4152555e9b39419cd959.
Root alone executed under grant20b411ee5cc0a62624891252fd5c5e8693813699,
M8-datatypes-measurement-2; durable RELEASE
2fb89c1d697b6817e69078616f95cb220adbba58 at2026-10-11T23:57:29.356012Z
preceded all raw inspection. Supervisor38.26007733s, both children exited0,
reaped/groups absent/logs closed; every identity stable before/after. Root also
independently inspected all distributions and GC counters. No further run occurs.

SDK10.0.400/runtime10.0.11, Release, Arm64 RyuJIT, macOS15.8.1, Apple M4 Pro
(Mac16,8;14cores), workstation GC. Fixed fixture generation, pre-parsed values,
namespace context and shared provider set are outside warm calls. Probe is
synchronous, uses per-thread managed allocation counters, consumes results,
32warm calls then64samples per route. Timing uses prebuilt InProcessNoEmit,
MemoryDiagnoser+ShortRunJob,3warmup/3measurement iterations/1launch. All six
priority elevation requests were denied; timing is informational. There are no
async-counter claims, x64 results, repeatability or universal speed claims.

## Warm and owned boundaries

All512 observed warm samples in these eight routes were exactly0B: canonical
registry lookup/type query, RNG registry lookup, missing type lookup, decimal
value equality, list hash (twice per invocation), invalid boolean, empty built-in
list, invalid Unicode. Core's former list-hash enumerator allocation was fixed
only with explicit later ordered-owner authorization; emitted IL has indexed
get_Item/get_Count and no GetEnumerator. Original failed measurement remains
honest and distinct in measurement-failure-1.md; it had no persisted byte count.

Each owned route below had64identical samples. These allocations belong to
immutable results, private snapshots, temporary composition references/adapters,
or setup; they are not claimed allocation-free.

| Route | Warm bytes/call | First route bytes |
| --- | ---: | ---: |
| Owned normalization | 32 | 32 |
| Short built-in list | 472 | 472 |
| Long built-in list, warmed pool | 1496 | 4792 |
| Canonical decimal through composer | 96 | 96 |
| RNG decimal alias (owned retype/adapter) | 160 | 160 |
| CreateList, one retained item | 96 | 96 |
| CreateUnion | 40 | 40 |
| Retype | 40 | 40 |
| Registry setup, all45built-ins | 34168 | 34168 |
| Long string through warmed pool | 1136 | 1136 |

First fixture construction allocated72568B, including fixture/static metadata,
providers, context and pre-parsed values; whole-process startup is excluded.
The first missing-type lookup allocated88B versus0B warm. The first pooled
long-list route allocated4792B versus1496B warm (3296B additional first-call
allocation); that difference is not attributed to a specific pool/runtime object.
The positive1024-byte-array control allocated1048B.

Whole-process GC.GetTotalMemory(true) snapshots with256 retained long lists were
230984B before and993640B after, difference762656B. These are approximate
inclusive heap observations, with library/runtime/cache effects, not an isolated
retained-size measurement or a per-result allocation claim. Ownership tests
establish that only immutable payloads/list snapshots survive; original spans,
namespace maps, temporary reference arrays and pooled scratch are not retained.

## Materializing oracle and exact GC accounting

The baseline owns normalized text, Split tokens and a reference array, parses the
same verified atomic types, then calls core CreateList. Composition uses borrowed
normalized scratch/scans. Controls compare validity, exact value equality/hash
for short/multilingual/long inputs and an invalid suffix. Both use unlimited
budgets; their extra normalization/scratch work differs. No custom matching,
SearchValues, SIMD or hash-scanning optimization is claimed.

| Case | Method | Mean | 99.9% error | Rounded allocation |
| --- | --- | ---: | ---: | ---: |
| invalid | Split oracle | 325.8ns | 12.78ns | 392B |
| invalid | composition | 291.0ns | 76.62ns | 240B |
| long | Split oracle | 18349.8ns | 2719.33ns | 3672B |
| long | composition | 17247.8ns | 2171.20ns | 1496B |
| short | Split oracle | 384.0ns | 21.79ns | 600B |
| short | composition | 368.5ns | 7.50ns | 472B |

Timing confidence intervals overlap. Ratios0.89/0.94/0.96 describe this single
ShortRun and establish no repeatable speedup. Allocation differences reflect the
explicit materialization boundaries; invalid list parsing can allocate prior
atomic items and temporary references before rejecting the entire result.

Every raw GC numerator/operation denominator is retained below, including
residuals above the rounded Allocated column. These residuals are not attributed
to a particular cause and are not described as zero.

| Case/method | Raw bytes / operations | Exact bytes/op (decimal) | Residual bytes beyond rounded column |
| --- | --- | ---: | ---: |
| invalid/Split | 822139024 / 2097152 | 392.0264358520508 | 55440 |
| invalid/composition | 503352944 / 2097152 | 240.01738739013672 | 36464 |
| long/Split | 120334800 / 32768 | 3672.32666015625 | 10704 |
| long/composition | 49025248 / 32768 | 1496.1318359375 | 4320 |
| short/Split | 1258378064 / 2097152 | 600.0414199829102 | 86864 |
| short/composition | 989924688 / 2097152 | 472.03287506103516 | 68944 |

Measurement-summary.json preserves exact fractions/counters, all per-route
min/median/max/first-call values, runtime/input/assembly/manifest/raw-file
identities and lifecycle. Only summaries and analysis are committed. Prior
family evidence is unchanged. This complete-library workload is provisional
composition evidence, not a full schema-validation/cumulative conformance gate;
end-to-end validator and cross-architecture/repeatability confirmation remain
with their later owning tasks. There is no remaining M8-datatypes measurement
blocker, and no allocation/timing rerun is authorized.
