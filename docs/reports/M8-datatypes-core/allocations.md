# Datatype core measurement

Measured binary-source revision c999c2cf9d836990c0342445ed68bc844e14bd79;
accepted runtime source ff628c8836f07f372c9c37ea3008e6bc62ef234c unchanged.
Supervisor/preparation revision0a481ed761b338cb279438053fd964dd846db74a.
Pinned SDK10.0.400/runtime10.0.11 Arm64, Apple M4 Pro, macOS15.8.1, workstation GC.
Raw allocation/BDN output stays ignored and transient for coordinator inspection;
only this summary, aggregated distributions and source/binary/input identities persist.

The coordinator's durable M8-datatypes-core-measurement-1 grant preceded the exact
`preparation.json` two-command sequence, supervised by
`python3 docs/reports/M8-datatypes-core/measure.py --granted 7a45ad5280f0f3b367fd70e67629b6a48733baa195bfeba754ff7bd879a77099`
with approved elevated process-inspection permission. The total bound was600seconds,
590work/10cleanup. Actual elapsed55.418255750seconds. Children98723/98725 exited0,
were reaped, their groups were absent, logs closed; all254prepared file identities
matched before and after, including copied libraries/dependencies/configs, pinned
host/runtime/CoreLib and fixture/source/harness. Lifecycle was reported before raw
inspection, and the coordinator's durable RELEASE ACK preceded opening results.
One sequence only; no retry or generated build. `measurement-summary.json` records
aggregate results and the existing preparation manifest retains binary identities.

Each thin-probe route used32warmups, then64consumed individually sampled operations.
No asynchronous work is measured. All64samples in each route had the same byte count:

| Route | Managed bytes/op | Samples |
| --- | ---: | ---: |
| Borrowed metadata lookup | 0 | 64/64 zero |
| Borrowed Unicode scalar measurement | 0 | 64/64 zero |
| Borrowed short normalization | 0 | 64/64 zero |
| Borrowed 1,024-unit normalization | 0 | 64/64 zero |
| Owned short normalized string | 40 | 64/64 at40 |
| Owned 1,024-unit normalized string | 2,072 | 64/64 at2072 |
| Schema operation setup | 512 | 64/64 at512 |
| Owned empty list snapshot/view/value | 88 | 64/64 at88 |
| Owned union wrapper | 40 | 64/64 at40 |
| Owned retype of an owned list | 40 | 64/64 at40 |

Cold fixture/metadata/first long normalization and pool initialization totaled42,720B.
This inclusive boundary contains permanent tables, fixtures, operation and output;
it is not a per-token cost. A separately consumed1,024-byte positive-control array
allocated1,048B. Warm long scratch had no additional measured allocation beyond
its owned string. Retaining256owned long strings plus their owner array produced
an inclusive GC.GetTotalMemory estimate from177,056to1,253,880B (delta1,076,824B).
That aggregate estimate includes runtime/pool/GC effects and is not an exact
per-string retained-size assertion; the explicit construction allocation remains
2,072B/string. Empty owned strings are canonical and outside the nonempty route.
No family parsing/comparison, nonempty list payload or end-to-end schema workload
is claimed here; their evidence belongs to later composition/family tasks.

One [MemoryDiagnoser, ShortRunJob] class ran exactly8prebuilt in-process NoEmit
cases (four fixtures by two methods), one launch, three warmups/three iterations,
without CLI job flags or code generation. All reports succeeded and reported0B.

| Collapse fixture | Minimal scalar loop mean | Core mean | Core/baseline |
| --- | ---: | ---: | ---: |
| Short ASCII | 29.78ns | 51.84ns | 1.74 |
| Multilingual/supplementary | 76.48ns | 130.96ns | 1.71 |
| 1,024-unit ASCII | 3,412.36ns | 6,290.77ns | 1.84 |
| Dense XML whitespace | 3,341.84ns | 4,962.51ns | 1.48 |

These provisional single-Arm64 timings do not demonstrate a speed optimization.
The comparison consumes identical valid collapse outputs and2Nscalar work charges,
but the minimal baseline supplies only collapse and assumes a sufficient output;
core also measures exact output size, implements all three rules, validates rule,
capacity and overlap, and exposes scalar counts. It is an allocation/cost diagnostic,
not a claim that the baseline is a drop-in implementation of the complete helper
contract. The straightforward core remains unchanged; no SearchValues/token-hash
optimization or custom SIMD is introduced. Composition and x64 confirmation belong
to the release gate; no release-wide throughput result is inferred.
