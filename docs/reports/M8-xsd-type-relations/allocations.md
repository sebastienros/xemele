# Type relation allocation evidence

Root executed `python3 docs/reports/M8-xsd-type-relations/measure.py --run ae8a7853fe4cb3a8b7f3bd7e43744c1810f1750aabd27788ce5e282995f28da0` against accepted probe source `e3a4b91bbe56148c01a2085fc3a19b2a1b0c0d7c` and accepted runtime/tests `2e1619b35d5f2bc8ef96cb32991603f91b9dbdb8`. Preparation `2f8bb02677c53a5ae47f8162ff8796b8cbeb48b3` froze505 identities; the manifest hash above matched before and after. Actual counters ran once, following one disabled control. Cleanup commit `1932d68760dc57431fd43b5f7874c7f717cd3ad3` preceded raw inspection.

Environment: .NET 10.0.11, Arm64, macOS 15.8.1; workstation GC, concurrent GC/tiering disabled. Synchronous per-thread managed-allocation readings only. No workload timing, throughput comparison or zero-allocation claim.

Cold fixture setup: **4,187,016 B**, including original reads, generated fixtures, parsed DOMs, cached maps/graphs and validation schema. Positive live1024-byte array control: **1,048 B**. Each route has one first call,16 warmups,64 measured calls. All1600 warm samples and25 first calls were positive; aggregates equal sample sums. Identical control and measurement consumption: **25,196**, with48 retained objects in each. Disabled counters produced zeros only as controls.

| Route | First call B | Warm mean B/op | Warm min–max B/op |
| --- | ---: | ---: | ---: |
| relation-identity | 1,008 | 1,008.000 | 1,008–1,008 |
| relation-union-member | 1,472 | 1,472.000 | 1,472–1,472 |
| relation-member-derived | 2,504 | 2,504.000 | 2,504–2,504 |
| relation-nested-union-24 | 25,272 | 25,272.000 | 25,272–25,272 |
| relation-unrelated-miss | 2,456 | 2,456.000 | 2,456–2,456 |
| relation-head-blocked | 1,472 | 1,472.000 | 1,472–1,472 |
| relation-whole-path-blocked | 1,008 | 1,008.000 | 1,008–1,008 |
| relation-multilingual-name-8192 | 27,088 | 27,088.000 | 27,088–27,088 |
| compiler-original-particlesZ012 | 147,360 | 141,392.000 | 141,392–141,392 |
| compiler-original-particlesZ031 | 114,664 | 114,664.000 | 114,664–114,664 |
| compiler-original-stZ007 | 112,048 | 111,984.000 | 111,984–111,984 |
| compiler-inherited-32-attributes | 156,984 | 156,856.000 | 156,856–156,856 |
| compiler-nested-union-24 | 183,344 | 183,344.000 | 183,344–183,344 |
| content-precomputed-union-members | 31,016 | 30,944.000 | 30,944–30,944 |
| content-precomputed-blocked-members | 29,456 | 29,408.000 | 29,408–29,408 |
| facade-original-particlesZ012 | 2,117,880 | 2,116,678.000 | 2,116,344–2,117,120 |
| facade-original-particlesZ031 | 1,504,600 | 1,504,818.500 | 1,504,568–1,505,904 |
| facade-original-stZ007 | 1,667,352 | 1,667,623.375 | 1,667,352–1,668,128 |
| facade-original-attZ015 | 1,558,288 | 1,558,538.500 | 1,558,288–1,559,624 |
| diagnostic-unrelated-substitution | 61,984 | 61,768.000 | 61,768–61,768 |
| diagnostic-mixed-simple-extension | 52,504 | 52,448.000 | 52,448–52,448 |
| diagnostic-anySimple-facet | 269,616 | 268,321.750 | 268,280–269,616 |
| diagnostic-union-upa | 304,616 | 304,593.750 | 304,552–305,888 |
| validation-members-64-valid | 167,664 | 167,681.500 | 167,664–168,224 |
| validation-members-64-invalid | 170,416 | 170,360.750 | 170,352–170,912 |

The direct relation owns a per-call SchemaCompileOperation, Queue, HashSet, growth storage and invocation capture state. Cached maps/registries belong to cold setup; scratch expires after each call. Compilation owns builders and sealed graphs; membership routes add precomputed arrays/programs and consumed cursor state. Public originals own schema composition and, where specified, validation results. Diagnostics own exception/stack/diagnostic snapshots. Instance routes exclude parsing and cached schema setup; they own run state and complete results. The bounded32-attribute/24-union/8192-character/64-member cases verify these particular ownership boundaries.

Six incremental coarse live-heap observations keep8 objects per category while retaining earlier categories and the fixture:

| Category | Objects | Coarse heap delta B |
| --- | ---: | ---: |
| relation-operations | 8 | 42,440 |
| compiler-graphs | 8 | 651,096 |
| content-programs | 8 | 252,184 |
| public-schemas | 8 | 1,718,136 |
| schema-diagnostics | 8 | 16,432 |
| validation-results | 8 | -11,512 |

The **−11,512 B** validation-result delta is preserved as process-wide GC noise. These readings are not object sizes, peak-memory bounds, proof of attribution, or evidence of zero/negative allocation cost. Native/async/x64/AOT behavior and CPU timing are outside this experiment.

Root control child60436 and measurement child60438 both completed with exit0. Both were reaped, owned groups absent and logs closed;505 identities matched before/after. Terminal lifecycle/release ledgers were durable before root inspected raw output. The prior timeout-to-SIGTERM-exit0 regression correctly failed and prevented the next child. Supervisor deadlines are120 seconds total/110 work/10 cleanup; elapsed supervision is lifecycle evidence only.

The accepted original and normative semantic assertions cover hit/miss/nested/blocked relations, compiler/public originals, inherited attributes, vacuous urtype, mixed rejection and UPA; no BCL discrepancy or resource refusal is promoted into conformance evidence. Earlier baselines and failed-check records remain. Raw transient outputs are preserved pending the coordinator’s cleanup direction.
