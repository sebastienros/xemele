# Source-only allocation plan

Status: source review requested. No probe build, control, preparation, counters,
heap readings, WarmBorrowed, BDN or timing execution has occurred for this task.
Coordinator must accept these sources and release preparation before
`prepare.py --source-accepted`. A fresh exclusive root grant bound to the prepared
manifest SHA256/latest-start/absolute-expiry is required for measurement.

Reuse the accepted M8-xsd-facade prepared allocation infrastructure, pinned
Release SDK 10.0.400/runtime 10.0.11, Arm64, tiering/server/concurrent GC disabled.
The supervisor is copied with task/project/environment substitutions only; it
retains process-group termination, direct child reap, group absence, closed logs,
before/after frozen SHA256 identities and durable release-before-inspection.
Proposed lease bounds are 30 seconds total, 20 work, 10 cleanup reserve.
Latest start is grant +60 seconds; absolute expiry is grant +90 seconds. These
are proposal bounds only; no grant exists. No retries or throughput gate. Build/control/freeze/preflight take place outside the lease.

Probe source: `benchmarks/Xemele.Benchmarks/M8-schema-resource-outcomes/`.
Three primitive routes compare direct legacy denial with adapted denial and
result factory/copy invariants on an already-owned resource. Preparation owns
request/resolver/resource initialization; each route consumes its result with
16 warmups and 1,024 operations. These routes target zero steady-state bytes.
The factory route does not cross a resolver boundary or repeatedly transfer a
resource; its fixture disposes the original wrapper exactly once at completion.

Five owned routes exercise optional unavailable, resolved DOM, legacy refusal,
malformed input and shared principal/external parsing. Each operation constructs
a fresh context with bounded state; successful callbacks create fresh wrappers
and streams, which the loader disposes. Sixteen warmups and 64 measured
operations per route, fixture generation outside measurement. These routes own
context/request/scope/diagnostic/DOM allocations and do not claim zero bytes.
Record cold fixture setup separately, a positive byte-array control, and retained
heap snapshots with 16 live external documents. No async measurement is involved.

`--control` follows the same bounded workload with every allocation/heap reading
disabled; zero fields are placeholders, never allocation evidence. Preparation
checks control invariants and freezes binaries, runtime, sources, check/probe/
supervisor scripts, API, samples and control output. Supervisor preflight runs
only the disabled control under owned-process cleanup checks. Measurement runs
control then measure using only frozen prebuilt inputs. Inspect raw results only
after durable lifecycle release. Report owned bytes/op, primitive bytes/op, cold
setup, positive control and retained observations with runtime/architecture/GC
configuration and identities. Timing/BDN remains unexecuted and informational;
no new token/scanning optimization or hardware-sensitive throughput claim exists.
