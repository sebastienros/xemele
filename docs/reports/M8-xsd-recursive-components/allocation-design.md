# Allocation probe source for independent review

Runtime source acceptance: `9e376cd982159b27cbb05c1fa5cf2577bd21e7d2`, compiler
SHA256 `86ec831d782eeea74d938e8bfbefededa7968678a9c55add35a3adf46f9eda24`.
No compiler/test changes since the 1300+42 passing correctness check.

The task-owned project explicitly includes Fixture.cs and Probe.cs.fixture and
references the existing Xemele assembly. This is a synchronous managed-allocation
probe, not a throughput benchmark or optimization claim. It adapts the verified
component-mapping driver and supervised lifecycle without the superseded
isolation/PARK/ACK/exclusive lease protocol. Root is the sole measurement launcher
to avoid duplicate runs; other chats continue their work.

Thirteen independently specified routes:

- Three internal compiler routes using parsed, unchanged original addB077,
  particlesZ010 and particlesEb041 documents.
- Three public facade compile routes using those originals; the Eb041 route
  additionally validates the unchanged original instance against the new schema.
- Internal compilation of 24 genuinely nested inline complex bodies and an
  8192-character multilingual element name with inline complex type.
- Owned diagnostic results for indirect forbidden model-group/base-type cycles
  and an unused deferred-body facet publication failure. Refusal/syntax results
  cannot count as semantic rejection.
- Valid and invalid 96-level recursive instance traversal using one precompiled
  recursive-group schema and preparsed instance DOMs; full result outcomes and
  XSD.Content invalid diagnostics are consumed.

Original schema bytes and copyright are frozen in the existing fixture manifest.
Fixture generation, original file reads, DOMs, cached traversal schema and the
instances belong to cold setup. The probe records that initialization cost
separately. Each route records one first call after setup, 16 warmups and a
64-operation distribution plus aggregate bytes. Distribution arrays and output
row objects are created outside the measured calls. Every returned graph/schema,
exception or validation result is consumed and kept alive for its call.

All routes have positive owned-result allocation assertions. Compiler allocation
includes operation state, declaration/type registration, charged pending entries,
expanded immutable programs and sealed graph snapshots. Facade compilation adds
datatypes/facets/content/identity setup; Eb041 includes a validation result.
Cycle and publication routes own diagnostics, snapshots and exception stacks.
Traversal owns per-run stacks/hooks plus result/diagnostic snapshots, with schema
and instance parsing explicitly excluded. No new zero-allocation primitive is
claimed. Positive control separately allocates a live 1024-byte array.

Four retained categories keep eight compiler graphs, eight public schemas,
eight schema exceptions and eight validation results. Full-collection heap
readings occur before/after each category. Earlier arrays and the entire fixture
stay alive; these are incremental coarse process-wide live-heap deltas, not
per-object sizes, peak-memory ceilings, bounds on every corpus case, or proofs
that all retained memory is attributable to one result. The 96-level/8192-character
inputs and finite counts bound this experiment only. Native memory, x64/AOT,
asynchronous allocations and throughput are outside this evidence.

In --control every allocation counter and GC heap-reading call is conditional
and disabled; first/warm samples, setup/positive/heap fields must all be zero.
The identical semantic paths, positive array and retained objects still execute.
Fixture route consumption and source/probe assembly hashes are reported in both
modes. Environment pins .NET10.0.11, workstation/nonconcurrent GC and disabled
tiering. No Stopwatch, BDN or workload timer is used.

After source acceptance, prepare.py performs offline SDK10.0.400 restore/build
and a 40-second-bounded disabled control, then freezes runtime/probe binaries,
all runtime source, test/fixture/probe source, build metadata, scripts and control
output identities. measure.py --verify-only and --preflight check identity and
actual process-list permission/cleanup with counters disabled. Root-only --run
requires that reviewed manifest hash and launches exactly prebuilt control then
measure once, with 110 seconds work and 10 cleanup reserve. It creates a unique
artifact directory, starts each child in its own process group, stops on first
failure, reaps children, drains owned groups, closes logs and verifies identities
before/after. Durable lifecycle/release records precede raw inspection. Supervisor
elapsed time is lifecycle evidence only. There is no remote push, lease extension,
global-host idle claim or foreign-project communication.

Source review occurs before offline build/freezing. No probe counters, controls,
benchmarks or measurements were executed during source preparation.
