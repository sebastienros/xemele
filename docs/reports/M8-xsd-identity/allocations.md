# Reviewed identity allocation evidence

Tested/accepted runtime source36b7f1379988da6b9c1e5915b513ccc33b48a3a4;
frozen package975d34a72494dc0bdb7b9b939c1b11f243b562cd. The coordinator,
not this worker, executed the one exact prebuilt allocation-only child under
M8-rng-identity-serial-1 grant973968a68a3b6d37c8a24866a3b2c739ad316652.
Durable RELEASEae9cf41ea49a87752a8256acedc987ee9e66ae1a preceded raw
inspection. Child8867 exited0, was reaped, its group absent and log closed;
all199 prepared identities and the manifest matched before/after. Supervisor
elapsed0.320725167seconds is lifecycle accounting, not engine latency.
The identity supervisor reserved20seconds work+10cleanup within30total; serial
host lease capped100total including root cleanup. No retry/build/test/discovery/
BDN/new actor or raw inspection occurred inside the lease.

Pinned .NET10.0.11, Arm64/macOS15.8.1; tiered/server/concurrentGC disabled,
roll-forward disabled. Fixture input/parser/type setup occurred before each
route's warm observation loop. Each route warmed32 times, then consumed64
synchronous per-thread allocation observations. Every row numerator equalled
the sum of its observations; each histogram below was constant across all64.

| Boundary | Bytes per observation | Sum bytes / operations |
| --- | ---: | ---: |
| Short restricted path hit | 0 | 0 / 64 |
| Short restricted path miss | 0 | 0 / 64 |
| Typed integer equality | 0 | 0 / 64 |
| Long multilingual namespace path hit | 0 | 0 / 64 |
| Long multilingual namespace path miss | 0 | 0 / 64 |
| Owned immutable identity preparation | 3216 | 205824 / 64 |
| Owned valid typed-hook run | 3944 | 252416 / 64 |
| Invalid hooks and diagnostics | 3632 | 232448 / 64 |
| Fresh initialized session setup | 952 | 60928 / 64 |

There are576observations:320borrowed zero and256owned positive. The positive
1024-byte allocation control observed1048bytes; cold fixture/program/input/type
setup observed304208bytes and is distinct from warmed routes. Hook routes use
preparsed documents and typed effective values; they include owned session/frame/
row/tuple/table state, operation/result objects and delivered final-content
records. They exclude parser and datatype parsing setup. The invalid fixture
omits an ineligible own key table and includes diagnostics; its lower bytes than
the valid fixture do not indicate a speed improvement.

Informational whole-heap retained bytes were276392 before,423768 after32owned
programs,484904 after32initialized open sessions. Differences147376 and61136
include reference arrays and whole-heap effects; these are not portable per-
program/session costs. Shared schema/document/type values belong to fixture
setup, not copied per primitive operation. Logical component/work quotas charge
before growth; finite default work bounds table growth and comparisons. Null
work caps deliberately remove that work/memory bound; no separate byte quota
or unrestricted-memory promise is made.

The exact aggregate histograms and lifecycle/environment metadata are preserved
in measurement-summary.json; no raw arrays/logs/result JSON are committed.
Raw transient outputs remain untouched for coordinator inspection/cleanup.
No BDN class, throughput/latency/improvement, whole-validator zero-allocation,
conformance corpus or cross-architecture release claim follows from this probe.
