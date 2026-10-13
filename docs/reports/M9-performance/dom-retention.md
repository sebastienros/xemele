# Frozen representative DOM retention

All ten primary fixtures were frozen before measurement. Exact bytes, origin/license, semantic digests and operation semantics are in benchmarks/baselines/release/fixtures.json and operations.json. Three consumed parse/held-full-GC/released-full-GC cycles per fixture and engine; input bytes stay outside the retained graph delta. Retained 9 MiB controls are recorded separately.

| Fixture | Xemele median retained bytes | BCL median retained bytes | Ratio |
| --- | ---: | ---: | ---: |
| feed-small | 50952 | 26296 | 1.9376 |
| svg-small | 230288 | 177008 | 1.3010 |
| deep-medium | 977648 | 799448 | 1.2229 |
| entity-medium | 2930272 | 2398736 | 1.2216 |
| xhtml-medium | 3195712 | 3988032 | 0.8013 |
| build-medium | 5868560 | 7300504 | 0.8039 |
| soap-large | 15739784 | 13395000 | 1.1750 |
| configuration-large | 23774904 | 29545704 | 0.8047 |
| business-large | 84018824 | 91021664 | 0.9231 |
| text-large | 33688168 | 33485200 | 1.0061 |

The predeclared aggregate ratio is 0.935969; required maximum is 0.75. **Target failed.** No fixture is excluded and the per-fixture values remain visible.

The direct pinned native/Rust workers qualified all20 fixture/engine pairs for real DOM parsing, common-model output, UTF-8 serialization roundtrip and compiled XPath results. A finite Rust quota adjustment and entity API mapping were required; original failed attempts remain retained. Managed XPath qualification independently hit the configured100million work limit on entity-medium and remains unresolved.

Local retention results do not establish dedicated-machine timing acceptance. Correctness/control preparation failures are retained separately from target failure; the final memory metric uses held-versus-released heap to avoid pre-parse pool/cache changes.
