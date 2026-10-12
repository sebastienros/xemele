# RELAX NG allocation evidence

Measured implementation revision: `ec0699e10605c1d89eecfdbe192a7150b81fe1be`.
Host Arm64, SDK10.0.400/runtime10.0.11, Release; tiered compilation/server GC/
concurrent GC disabled for this synchronous allocation-only probe. No latency,
throughput, optimization or full-validator zero-allocation claim. Results include
owned schemas, per-run derivatives/caches/contexts, datatype values, coalesced text,
results and invalid-instance diagnostics. Fixtures/compiled schemas are prepared
before warm validation counters; the compilation route owns its setup each time.
The byte-stream route additionally includes the caller MemoryStream/byte-array,
parser buffer and DOM. These totals are not standalone borrowed-primitive evidence.

Every route had32warmups then64samples ×16operations =1024operations. Counter stops
precede result checks. No arrays/logs/result dumps are committed; the exact raw
samples remain transient for coordinator inspection. All10 known semantic route
results were consumed successfully, with counters disabled before the grant and
enabled only in the exact prebuilt supervised child.

Sample min/max/frequencies below are BYTES PER16OPERATIONS, not per-operation
minima. Means divide exact total by1024; they are arithmetic observations, not
rounded BenchmarkDotNet zero or speed claims.

| Route | Total bytes /1024 ops | Sample min–max | Sample-byte frequencies | Mean B/op |
| --- | ---: | ---: | --- | ---: |
| compile-owned-schema | 58,181,600 | 909,056–909,344 | 909,056 × 57; 909,344 × 7 | 56817.96875 |
| dom-valid | 2,293,760 | 35,840–35,840 | 35,840 × 64 | 2240 |
| dom-invalid-diagnostic | 1,744,896 | 27,264–27,264 | 27,264 × 64 | 1704 |
| coalesced-multilingual-text | 2,605,616 | 40,704–41,264 | 40,704 × 63; 41,264 × 1 | 2544.546875 |
| datatype-regex | 2,457,600 | 38,400–38,400 | 38,400 × 64 | 2400 |
| recursive-dom | 5,873,664 | 91,776–91,776 | 91,776 × 64 | 5736 |
| list-values | 3,162,112 | 49,408–49,408 | 49,408 × 64 | 3088 |
| ambiguous-interleave | 4,612,656 | 72,064–72,624 | 72,064 × 63; 72,624 × 1 | 4504.546875 |
| string-input-dom-and-result | 13,869,056 | 216,704–216,704 | 216,704 × 64 | 13544 |
| byte-stream-input-dom-and-result | 61,095,936 | 954,624–954,624 | 954,624 × 64 | 59664 |

One process-cold compile: 87,208 managed bytes. Positive control:
new byte[1024] consumed 1,048 bytes, including managed overhead.
Separate approximate retained observation after forced collections:16compiled
simple schemas changed managed live memory by 887,160 bytes.
RSS snapshot was 61,505,536 bytes; this includes the whole process/runtime and
is not a schema-only footprint, peak-memory or leak measurement. Cold initialization,
warm allocations and approximate retention/RSS are distinct observations.

Coordinator durable grant `973968a6` for M8-rng-identity-serial-1 began
2026-10-12T02:58:14.749336Z; durable release `ae9cf41ea49a87752a8256acedc987ee9e66ae1a`
at02:58:15.815865Z preceded raw inspection. Exact RNG supervisor status0 in
0.559740833s; sole child8860 exited0 and was reaped,
owned group absent/log closed. All434identities matched before and after; no
mismatches, retries, builds/tests/discovery/BDN or extra children ran in the window.
Source review,89owned/1191predecessor tests, public/frozen API comparison and normal/
XML-only consumers passed independently before freezing.

Manifest SHA256 `5b7d24badda22d79471f6f12ee62684e4e9fb1a159944d36e9186f5c31c1ce9a`.
Supervisor SHA256 `bdb51e0d4183cf06eb71f8df1607869021571f52de992146ee492fe3aed724fd`.
Raw allocations SHA256 `a5a62c9a69e0265d9c32194b29002dd8ef1cc23274c6e1d7d95c1f745ca0f914`;
lifecycle SHA256 `b65b1e2b38caea2ce83521b8112bbc43690b5f34c6cac0cd932418f3a9762914`.
Full identity lists/raw samples stay in ignored artifacts. This compact summary
retains exact denominators/totals/minima/maxima/frequencies and artifact identities.
