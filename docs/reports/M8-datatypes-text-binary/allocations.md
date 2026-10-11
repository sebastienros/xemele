# Accepted single-run allocation/performance evidence

Root granted one prepared sequence at2026-10-11T23:12:53.912416Z and recorded
RELEASE82de8fe36ed8bd073825e80c786544c01eddbed6 before raw inspection. Production
f7a0a83e8de4e5739d95e089eeacce32ba6e7449, prepared checkpoint119453ee636ed3fd2d2f8cf9e9b510d4b281fbac,
manifestc7b3aee7896c7efb68d225b6a488dbfc71296074fb9b81d55f5f098b9c3627bf.
All444 identities matched before/after. Both direct children35615/35617 exited0,
were reaped, groups absent and logs closed;68.27719699998852 seconds within600s.
No retries, generated builds, raw inspection before release or other measurement.

SDK10.0.400/.NET10.0.11, Arm64 RyuJIT, Apple M4 Pro14 logical/physical cores,
macOS15.8.1, workstation GC. Probe warms each route32 times then samples64 calls
using current-thread counters and consumes results. These synchronous primitives
retain no async state. Caller input/values/operation setup is outside warm routes.

| Probe route | Every one of64 samples (B/call) |
|---|---:|
| Text/binary/QName/boolean equality and hashes (8 routes) | 0 |
| Stored scalar length | 0 |
| Invalid base64/static error | 0 |
| Owned short text (`根😀 value`,9 UTF-16 units/8 scalars) | 112 |
| Owned long text (`根😀`+4096 ASCII,4099 units/4098 scalars) | 8288 |
| Owned hex and base64 (5 octets each) | 88 each |
| Owned prefixed QName (local `根`) | 152 |
| Owned URI (`../根😀`) | 104 |
| Owned boolean | 56 |
| Operation setup | 512 |

Private arrays and value nodes are the explicit retained ownership boundary.
QName additionally has a transient boxed namespace enumerator; the immutable
resolved namespace string is shared. No arrays, pools, spans or maps escape as
mutable storage. Simple scalar/octets loops and pinned static name tables remain;
no speculative SearchValues/token generator/SIMD optimization was introduced.

Cold fixture setup80472 B includes first-use metadata/provider/operation/corpus
and explicit fixture static initialization, not the entire process startup.
Positive control `byte[1024]` allocated1048 B. `GC.GetTotalMemory(true)` snapshots
for256 retained long values were221848→4490376 B, inclusive delta4268528 B.
This is process-wide heap evidence, not isolated payload accounting. For context,
256×8288 B steady parse allocations plus an estimated2072 B reference array total
2123800 B; the observed inclusive delta differs. No attribution or leak claim is
established from this single snapshot pair. No pool is used by this provider.

BDN0.15.8: exactly ten `[MemoryDiagnoser,ShortRunJob]` cases, InProcessNoEmit,
launch1/warmup3/iteration3. Fixture generation/setup is untimed. Baseline and
provider use identical immutable scalar fixtures, consumed equality results and
same semantic shape/scalar work charges. High-priority setup was denied in all
ten cases; timings are provisional, not a broad optimization/release claim.

| Case | Method | Mean ns | 99.9% error ns | Rounded B/op |
|---|---|---:|---:|---:|
| long | BaselineEquals | 6944.97 | 1329.049 | 0 |
| long | ProviderEquals | 5767.30 | 589.952 | 0 |
| long | ProviderHash | 6545.38 | 602.097 | 0 |
| long | OwnedParse | 16679.07 | 210.191 | 8288 |
| long | InvalidSuffix | 9740.26 | 4354.002 | 0 |
| short | BaselineEquals | 19.20 | 2.589 | 0 |
| short | ProviderEquals | 31.86 | 1.080 | 0 |
| short | ProviderHash | 26.08 | 1.321 | 0 |
| short | OwnedParse | 63.88 | 0.214 | 112 |
| short | InvalidSuffix | 38.23 | 7.218 | 0 |

The long equality ratio is0.83; short1.66. Keep the straightforward implementation;
this single synthetic in-process run does not establish a repeatable speedup.
Raw GC counters differ from rounded rows and must remain visible:

| Case/method | Exact GC allocated bytes / operations | Bytes beyond rounded-product |
|---|---:|---:|
| long baseline | 0 /131072 | 0 |
| long equality | 2976 /131072 | 2976 |
| long hash | 2688 /131072 | 2688 |
| long owned | 271594456 /32768 | 13272 |
| long invalid | 2976 /65536 | 2976 |
| short baseline | 2976 /33554432 | 2976 |
| short equality | 2976 /16777216 | 2976 |
| short hash | 2976 /33554432 | 2976 |
| short owned | 939562328 /8388608 | 38232 |
| short invalid | 2976 /16777216 | 2976 |

Residual bytes are unattributed. Rounded zero BDN rows do not prove literal zero
inclusive totals; the separate64-sample probe directly establishes its warm-call
zero-allocation distributions. Gen0 collections: long owned32, short owned112;
all other generation counts zero. [Machine summary](measurement-summary.json)
preserves frequencies, exact counters, source/input/payload identities and hashes
of the ignored raw artifacts. Only summary/analysis is committed.

Follow-up belongs to composition/release: representative complete datatype/facet/
validator workloads, broader malformed/miss/corpus timing, retained object-graph
accounting and attributable BDN residuals, repeatability and x64 evidence. These
are explicit limits of this family microbenchmark, not missing family behavior.
