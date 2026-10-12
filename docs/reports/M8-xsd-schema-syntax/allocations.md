# Allocation evidence

Frozen input/source revision `28e26b68231ca3981702b2e17d7fd445afe42c4c`; accepted runtime unchanged after `54468a6a4daff58c4578b5a46fe48116ccd22e18`. SDK10.0.400/runtime10.0.11, macOS15.8.1 Arm64, tiered compilation/serverGC/concurrentGC disabled. This is an allocation-only correctness repair with no throughput/BDN run, cross-architecture claim or zero-allocation assertion for owned output routes. `[MemoryDiagnoser, ShortRunJob]` benchmark class is supplied but was not launched.

Root alone launched grant `aed90f7b61b688697d689af9178bae1f774d7d22`, exact scope `M8-xsd-schema-syntax-28e26b68-alloc-only`, after all11 fresh actor ACKs. Supervisor30s maximum/20work/10cleanup. Durable root cleanup RELEASE `c3b9438f625dcd941087c83a841bb369fb373884` followed supervisor release at `2026-10-12T06:42:46.282798+00:00`, before worker raw inspection. Children82150/82152 both exit0/reaped/groupabsent/logclosed. All456 source/runtime/binary/control/check identities matched before/after and again during released evidence inspection. Manifest SHA256 `f613780fa3d57c7e0b1f82054c5ccb32f14c24c579f17c6839dfb220b56a1ce8`; supervisor SHA256 `d95f10918f135bfe80a739dc0db85193a8ec73850da0ed9b8f312d883ea8e6eb`. No retries, additional actors, builds or frozen writes under grant.

Each route used16 warmups then64 synchronous consumed operations; fixture generation and report construction were outside warm counter intervals. Disabled controls execute identical route assertions and measured-loop operations with every counter/GC read disabled: all12 zero readings and all15772 consumed units matched. Positive allocation control1048B for1024-byte array. Cold fixture/input initialization2482032B reported separately. Sixteen mixed owned outputs retained delta634208B (1040888-406680); informational live-heap delta, not peak memory or leak evidence.

| Route | Total bytes /64 operations | Bytes/op |
| --- | ---: | ---: |
| compiler-lang-short | 4774912 | 74,608.00 |
| compiler-lang-long-whitespace | 4774912 | 74,608.00 |
| compiler-lang-multilingual-payload | 4774912 | 74,608.00 |
| compiler-lang-empty-invalid | 291840 | 4,560.00 |
| compiler-group-legal | 5042176 | 78,784.00 |
| compiler-global-occurs-invalid | 290304 | 4,536.00 |
| path-dot | 53248 | 832.00 |
| path-long-whitespace | 63488 | 992.00 |
| path-multilingual-name | 65024 | 1,016.00 |
| path-self-invalid | 123392 | 1,928.00 |
| path-descendant-axis-invalid | 123392 | 1,928.00 |
| facade-owned-composition | 89183760 | 1,393,496.25 |

Compiler results own type/declaration/checkpoint graphs, namespace/context tables and definition registries. Short,8192-unit leading+trailing whitespace and multilingual opaque-payload documentation routes each allocate74608B/op: the borrowed language validation adds no length-proportional normalized string or value payload. This comparison observes the complete compiler route, not an isolated allocation-free primitive. Group definition output78784B/op includes persisted expanded content; invalid routes own diagnostics and exceptions4536–4560B/op.

Restricted path programs own parser/list state, retained name strings and arrays: literal dot832B/op, long-whitespace child path992B/op, multilingual child/attribute names1016B/op. Illegal explicit axes own structured diagnostic/exception outputs1928B/op. The public facade route includes XML parsing, content/facet/identity preparation, owned schema publication and one validation result1393496.25B/op; it is representative composition cost, not attributed to the syntax scan alone.

Raw JSON/log outputs remain transient ignored artifacts and are not committed. Frozen inputs preserved for coordinator verification. No later milestone or broad corpus claim follows from these bounded routes.
