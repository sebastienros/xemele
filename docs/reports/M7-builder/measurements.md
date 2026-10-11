# Released measurement checkpoint5d4de99

Actual.NET10.0.11Arm64; pinnedSDK10.0.400. RootdurableGRANT m7-builder-5d4de99;
durableRELEASE confirmed before raw inspection. Sole sequence initial/allocation/
72ShortRunNoEmitBDN/final exit0; nochildtrue; source3904/binary145/runtime3852 unchanged.
Manifest a0daa28b3784e8d844ddfeca832059d29b5015972cf1e832ca3088de153b0dd2.
Complete effective child environment preserved, including opaque per-key secret/
context digests. Exact-wrapper positive0/unknown-envnegative1/finalpositive0.

96batchesx1024 borrowed prescan+byte/char label+cached scan operations =98304operations,0B.
Cold first owned parse35080B, positive1048B, cachedSearchValues/class init48B,
prepared linear label oracle JSON/owned fixtures152120B (separate setup).

These are inclusive operation allocations, not an allocation-free DOM claim.
Byte and stream fixtures include a UTF8 meta declaration absent from character/
fragment fixtures; API-lane ratios are ownership observations, not equivalent-
input parser speedups. Setup strings/bytes/context/closures are outside counters.

| Domain | Total B | Operations | B/op |
|---|---:|---:|---:|
| cold-first-owned-document-inclusive-static-initialization | 35080 | 1 | 35080.000 |
| positive-control | 1048 | 1 | 1048.000 |
| cold-benchmark-cached-SearchValues-and-fixture-class-initialization | 48 | 1 | 48.000 |
| prepared-linear-label-oracle-fixture-JSON-and-owned-tables | 152120 | 1 | 152120.000 |
| allocating-prescan-baseline-short-hit | 31744 | 128 | 248.000 |
| owned-character-document-short-hit | 997376 | 128 | 7792.000 |
| owned-byte-document-short-hit | 6366976 | 128 | 49742.000 |
| owned-stream-object-transport-and-document-short-hit | 7426816 | 128 | 58022.000 |
| owned-contextual-fragment-short-hit | 989184 | 128 | 7728.000 |
| owned-deep-clone-output-short-hit | 216064 | 128 | 1688.000 |
| allocating-prescan-baseline-short-miss | 32768 | 128 | 256.000 |
| owned-character-document-short-miss | 1003520 | 128 | 7840.000 |
| owned-byte-document-short-miss | 6373120 | 128 | 49790.000 |
| owned-stream-object-transport-and-document-short-miss | 7432960 | 128 | 58070.000 |
| owned-contextual-fragment-short-miss | 995328 | 128 | 7776.000 |
| owned-deep-clone-output-short-miss | 216064 | 128 | 1688.000 |
| allocating-prescan-baseline-long-late-hit | 31744 | 128 | 248.000 |
| owned-character-document-long-late-hit | 5388288 | 128 | 42096.000 |
| owned-byte-document-long-late-hit | 10826240 | 128 | 84580.000 |
| owned-stream-object-transport-and-document-long-late-hit | 11886080 | 128 | 92860.000 |
| owned-contextual-fragment-long-late-hit | 5380096 | 128 | 42032.000 |
| owned-deep-clone-output-long-late-hit | 293888 | 128 | 2296.000 |
| allocating-prescan-baseline-long-miss | 0 | 128 | 0.000 |
| owned-character-document-long-miss | 5211136 | 128 | 40712.000 |
| owned-byte-document-long-miss | 10581504 | 128 | 82668.000 |
| owned-stream-object-transport-and-document-long-miss | 11640840 | 128 | 90944.062 |
| owned-contextual-fragment-long-miss | 5171200 | 128 | 40400.000 |
| owned-deep-clone-output-long-miss | 171008 | 128 | 1336.000 |
| allocating-prescan-baseline-dense | 542720 | 128 | 4240.000 |
| owned-character-document-dense | 3539712 | 128 | 27654.000 |
| owned-byte-document-dense | 8868608 | 128 | 69286.000 |
| owned-stream-object-transport-and-document-dense | 9928448 | 128 | 77566.000 |
| owned-contextual-fragment-dense | 3523584 | 128 | 27528.000 |
| owned-deep-clone-output-dense | 1281024 | 128 | 10008.000 |
| allocating-prescan-baseline-multilingual | 52224 | 128 | 408.000 |
| owned-character-document-multilingual | 1057536 | 128 | 8262.000 |
| owned-byte-document-multilingual | 6486784 | 128 | 50678.000 |
| owned-stream-object-transport-and-document-multilingual | 7546624 | 128 | 58958.000 |
| owned-contextual-fragment-multilingual | 1048576 | 128 | 8192.000 |
| owned-deep-clone-output-multilingual | 234496 | 128 | 1832.000 |
| buffered-foster-N=256 | 4673088 | 8 | 584136.000 |
| buffered-adoption-N=256 | 4862144 | 8 | 607768.000 |
| buffered-foster-N=512 | 10297856 | 8 | 1287232.000 |
| buffered-adoption-N=512 | 9662848 | 8 | 1207856.000 |
| buffered-foster-N=1024 | 24714816 | 8 | 3089352.000 |
| buffered-adoption-N=1024 | 19265336 | 8 | 2408167.000 |
| buffered-foster-N=2048 | 66136896 | 8 | 8267112.000 |
| buffered-adoption-N=2048 | 38469376 | 8 | 4808672.000 |

Retained inclusive heap: before949760B, after3927272B, delta2977512B for128
documents including the array/model/tokenizer/runtime roots; not isolated node storage.

## Unresolved deterministic growth defect

Foster8-operation totals N256/512/1024/2048 =4673088/10297856/24714816/66136896B.
Successive ratios2.204/2.400/2.676 reveal a quadratic existing-text copy term.
Characters appends previous.Value, and FlushText materializes the accumulated body
text at every interleaved TD/body location change. The single-location buffer does
not preserve growing foster text across these switches. Reported to root before
further source edits. This checkpoint is not implemented/verified.
Adoption totals4862144/9662848/19265336/38469376B grow approximately linearly.

## Timing applicability

All72BDN cases passed in561.29s; MemoryDiagnoser,3warmup/3measurement/1launch;
NoEmit produced no builds/children. Original raw logs/CSV and frozen probe binaries
preserved under ignored artifacts/M7-builder/run1 before any future changes.
Only summary/analysis is tracked. OneArm64 host; no x64/release performance claim.
High process priority was denied; tiny scan baselines and long-input confidence
intervals are noisy. No autonomous tuning or rerun.
Borrowed prescan removes248/256/408/4240B allocations on matching fixtures;
short-hit mean112.116ns vs78.789ns allocating baseline; dense1462.561ns vs2387.472ns.
It is an allocation improvement with mixed timings, not a universal speedup.
Generated static label matching is allocation-free and usually faster than the
linear owned-table oracle; both byte/char paths validated against228 originals.
Scalar scan wins when the first byte is markup; IndexOf/cachedSearchValues both
approximately16-17ns for long late-hit/miss vs scalar231-238ns. SearchValues has
no clear advantage over simpler IndexOf for this one-byte set; retain simpler
production matching pending any separately authorized change. No hashing/customSIMD.
