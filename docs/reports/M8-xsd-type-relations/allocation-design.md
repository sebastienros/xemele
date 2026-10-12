# Allocation source for review; no probe execution yet

Accepted runtime/tests: `2e1619b35d5f2bc8ef96cb32991603f91b9dbdb8`.
Branch correctness: 1622 affected/85 owned and42 resolver tests,1430 public members,
eight frozen lanes, normal and true XML-only consumers. This design does not
change those sources or tests. The probe is synchronous managed-allocation
evidence, not a throughput benchmark or an optimization claim.

The explicit task-owned project compiles only Fixture.cs and Probe.cs.fixture
against the existing Xemele project. No shared project or runtime edits.
Twenty-five independently specified routes:

| Routes | Work and independent expectation |
| --- | --- |
| 0–7 | Actual shared TypeRelation: identical integer; integer in union; positiveInteger derived member; positiveInteger in24 nested union targets; unrelated date miss; head restriction-blocked member; whole-path intermediate extension block;8192-character multilingual named restriction through nested union. Hits must preserve expected restriction/extension flags; misses stay complete. |
| 8–12 | Internal compiler of unchanged particlesZ012, particlesZ031 and stZ007;32 multilingual attribute names inherited into a restriction with identical binding objects/no inherited wildcard;24 nested union definitions plus8192-character named simple restriction. stZ007 retains t2's anySimpleType datatype. |
| 13–14 | Content compiler produces existing precomputed member arrays; matcher sees actual global member declaration ID and completes, or rejects a restriction-blocked member without work refusal. The route owns a new program/cursor/operation; no matcher changes. |
| 15–18 | Public compile of unchanged particlesZ012, particlesZ031, stZ007 and attZ015. All but schema-only Z031 validate unchanged original instances with complete valid results. |
| 19–22 | Owned schema exceptions: unrelated substitution, mixed extension of simple content, nonempty anySimpleType facets, union-member UPA ambiguity. Each requires XSD.SchemaRestriction; SCHEMA refusal/syntax diagnostics cannot count. UPA additionally checks attribution diagnostic. |
| 23–24 | Precompiled union schema and preparsed64-member valid DOM or64-member plus unexpected-element invalid DOM. Complete outcomes required; invalid result requires XSD.Content. |

Correctness derives from unchanged pinned metadata and the accepted normative
neighbor tests, not solely from BCL agreement. System.Xml discrepancies and the
failed bounded xmllint attempt remain in standards-oracles.md and earlier-failures.json;
none changes the corpus expectations or supplies a new exception.

Cold setup owns original reads, generated fixture strings, all DOMs, dictionary
maps, declaration registries, precompiled schema, and cached graphs. Generation
and parsing are outside each measured route. The setup counter is reported
separately. Each route records its first call after setup,16 unmeasured warmups,
then64 individually sampled operations and their aggregate. Sample arrays/row
objects are allocated outside counters. Returned graph/program/schema/exception/
operation/result objects are consumed and kept alive. Integer consumption avoids
boxing successful relation booleans. No workload timer, Stopwatch, BDN or
throughput comparison executes.

Every route has positive owned-allocation assertions. The iterative relation
allocates a Queue, HashSet, their growth storage and invocation capture state;
these are operation-local scratch, including on identity/miss/blocked branches.
The returned SchemaCompileOperation is a separately owned per-call setup/result
object. Cached type dictionaries/registries belong to fixture setup, not to that
scratch. Compiler routes own declaration/component builders, inherited binding
lists and sealed graph snapshots. Precomputed membership routes add relation
scratch, member arrays, immutable content programs and a consumed cursor.
Facade routes add executable datatype/content/identity composition and owned
validation results for the original instances. Diagnostic routes own exceptions,
stack traces and diagnostic snapshots. Validation routes own per-run state and
result/diagnostic snapshots; schema and DOM parsing are excluded.
There is no zero-allocation primitive claim or baseline throughput claim.

A separate positive control allocates and keeps alive a1024-byte array; measured
allocation must be at least1024 bytes. Six retained categories hold eight objects
each: relation operations, compiler graphs, content programs, public schemas,
schema exceptions and validation results. Full-collection heap readings before/
after each category report coarse incremental process-wide live-heap deltas.
Earlier arrays and fixture inputs remain alive. Relation scratch is not retained
by the returned operation. These deltas are not object sizes, peak-memory ceilings,
or proof of attribution; collection noise can yield negative deltas.48 objects,
finite route/sample counts and the specified32/24/8192/64 fixture dimensions bound
this experiment. Native memory, async allocations, x64/AOT and timing remain
outside its evidence.

In --control every allocation/heap read is guarded and disabled. Identical
semantic paths, live positive array and48 retained objects still execute. All
setup/first/warm/aggregate/distribution/positive/heap fields must be zero; those
zeros are disabled controls, never zero-allocation evidence. Both modes emit
runtime, architecture, OS, GC configuration, consumption and assembly hashes.
Runtime is pinned .NET10.0.11, workstation/nonconcurrent GC, tiering disabled.

After source acceptance, prepare.py performs offline SDK10.0.400 restore/build,
with each command limited to180 seconds and build servers/shared compilation
absent. Its disabled control uses the reviewed process-group supervisor with30
seconds work and10 cleanup reserve. Unique output directories prevent overwriting
preserved artifacts. Preparation verifies semantic controls and freezes runtime/
probe binaries, all runtime source, own and exact amended test source, fixture
source/hashes/copyright, build metadata, consumer, scripts and controls.

measure.py --verify-only and --preflight check frozen identities and real ps
permission/cleanup with counters disabled. lifecycle-check.py exercises an owned
child that times out, exits zero after SIGTERM, and must still fail/stop the
sequence; an independently completed child must pass. No allocation work runs in
that regression. The corrected supervisor requires reason=completed plus exit0,
reaped children, absent owned groups and closed logs. Cleanup uses TERM/KILL,
finite waits and scoped process-group inspection.

Root alone launches --run with the reviewed manifest hash to prevent duplicate
evidence. One prebuilt control then one measurement share110 seconds work and10
cleanup reserve. It stops on first failure, checks identities before/after, and
writes durable lifecycle/release records before raw inspection. No isolation,
PARK, ACK, lease, global-idle condition or foreign-project coordination exists.
The supervisor's monotonic clock enforces lifecycle deadlines only.
Source acceptance precedes any probe build/control/freezing/counters.
