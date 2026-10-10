# Measured matrix summary

All means are nanoseconds per whole operation. Three short iterations; provisional single-host data. Raw full JSON retains workload samples, error and standard deviation. Single-delimiter controls have different semantics.

## ChunkMatchBenchmarks (24 measured cases)

Tested revision: `662b15bb24ce340c301389e037277fd68d9f60ab`.

| Method | Mean range (ns/op) | Allocation range (B/op) |
|---|---:|---:|
| ByteHash | 2.873–16.679 | 0–0 |
| ByteLength | 2.956–8.140 | 0–0 |
| CharHash | 2.851–15.818 | 0–0 |
| CharLength | 2.886–9.042 | 0–0 |

ByteHash/ByteLength ratio across identical parameter sets: min 0.972, median 1.639, max 2.049; below 1 is faster; 0 subnanosecond denominator(s) omitted (empty-harness resolution); all raw cases retained.

CharHash/CharLength ratio across identical parameter sets: min 0.923, median 1.625, max 2.074; below 1 is faster; 0 subnanosecond denominator(s) omitted (empty-harness resolution); all raw cases retained.

## InputBenchmarks (56 measured cases)

Tested revision: `87d101c7e9b09167b2d114f1f33ea5974680617d`.

| Method | Mean range (ns/op) | Allocation range (B/op) |
|---|---:|---:|
| ByteToByteCore | 38.824–22580.982 | 0–0 |
| ByteToByteCorePaused | 39.098–22702.254 | 0–0 |
| ByteToCharacterScratch | 32.635–17738.708 | 0–0 |
| ByteToOwnedString | 30.813–8454.167 | 80–7704 |
| StringToCharacterCore | 19.841–4768.230 | 0–0 |
| StringToCharacterCorePaused | 20.791–4583.998 | 0–0 |
| StringViaOwnedUtf8 | 54.759–24749.846 | 56–6168 |

## LiteralBenchmarks (20 measured cases)

Tested revision: `662b15bb24ce340c301389e037277fd68d9f60ab`.

| Method | Mean range (ns/op) | Allocation range (B/op) |
|---|---:|---:|
| ByteArray | 0.000–0.000 | 0–0 |
| ByteLiteral | 0.000–0.000 | 0–0 |
| CharArray | 0.174–0.191 | 0–0 |
| CharLiteral | 0.000–0.211 | 0–0 |
| ConstantCollection | 0.000–0.000 | 0–0 |
| ConstantIntCollection | 0.000–0.000 | 0–0 |
| MutableCollectionControl | 0.248–0.400 | 0–0 |
| MutableLocalCollection | 0.188–0.191 | 0–0 |
| RuntimeCollectionControl | 0.216–0.223 | 0–0 |
| RuntimeLocalCollection | 0.163–0.201 | 0–0 |

## MatchBenchmarks (96 measured cases)

Tested revision: `662b15bb24ce340c301389e037277fd68d9f60ab`.

| Method | Mean range (ns/op) | Allocation range (B/op) |
|---|---:|---:|
| ByteHash | 0.260–11.398 | 0–0 |
| ByteLength | 0.000–5.782 | 0–0 |
| CharHash | 0.272–11.845 | 0–0 |
| CharLength | 0.449–5.874 | 0–0 |

ByteHash/ByteLength ratio across identical parameter sets: min 1.350, median 1.971, max 6.788; below 1 is faster; 15 subnanosecond denominator(s) omitted (empty-harness resolution); all raw cases retained.

CharHash/CharLength ratio across identical parameter sets: min 1.325, median 2.208, max 5.906; below 1 is faster; 12 subnanosecond denominator(s) omitted (empty-harness resolution); all raw cases retained.

## RejectionBenchmarks (6 measured cases)

Tested revision: `87d101c7e9b09167b2d114f1f33ea5974680617d`.

| Method | Mean range (ns/op) | Allocation range (B/op) |
|---|---:|---:|
| ByteCore | 2873.665–3031.394 | 1160–1184 |
| CharacterScratch | 2873.497–3008.604 | 1160–1184 |

## ScanBenchmarks (80 measured cases)

Tested revision: `662b15bb24ce340c301389e037277fd68d9f60ab`.

| Method | Mean range (ns/op) | Allocation range (B/op) |
|---|---:|---:|
| ByteCached | 0.207–34.814 | 0–0 |
| ByteIndexOfControl | 0.000–32.927 | 0–0 |
| ByteSimple | 0.000–659.907 | 0–0 |
| ByteSmallSet | 0.000–67.554 | 0–0 |
| CharCached | 0.939–30.765 | 0–0 |
| CharIndexOfControl | 0.466–38.401 | 0–0 |
| CharSimple | 0.000–399.494 | 0–0 |
| CharSmallSet | 0.464–96.032 | 0–0 |

ByteCached/ByteSimple ratio across identical parameter sets: min 0.045, median 0.055, max 0.851; below 1 is faster; 2 subnanosecond denominator(s) omitted (empty-harness resolution); all raw cases retained.

CharCached/CharSimple ratio across identical parameter sets: min 0.071, median 0.076, max 0.926; below 1 is faster; 2 subnanosecond denominator(s) omitted (empty-harness resolution); all raw cases retained.

Total: 282 benchmark cases. No parser throughput claim.

