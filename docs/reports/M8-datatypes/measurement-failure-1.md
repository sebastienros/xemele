# First composition measurement failure

Root executed the prepared supervisor only after durable serialized grant
M8-sch-datatypes-serial-2, then durably released at main
6b35e04d057e7ec516a967fd52836a0aa46da1dd, 2026-10-11T23:44:30.974440Z,
before inspecting raw results. This task inspected outputs only after RELEASE.
Manifest8301db41daddce4cee4ee5b528cb1d4147a3e16ddd9d65a7eb41d16132294ff9
covered453 identities, stable before and after. Lifecycle reports status1,
child47920 exit-6, direct child reaped/owned group absent/log closed,
elapsed0.37743375007994473s. Command0 failed at route4, library list hash twice;
BenchmarkDotNet did not start. This is a failed check, not allocation success.

The original probe threw before writing the distribution. The retained raw
failure directory artifacts/M8-datatypes/measurement contains command-0.log and
lifecycle.json; it has no allocations.json. No prior per-operation byte count is
invented. The original preparation.json and raw failure are preserved separately
from the new measurement-2/preparation-2 paths.

Source/IL diagnosis, with counters disabled: Core/Values.cs used foreach over
list.Items exposed as IReadOnlyList. The real Release IL had callvirt
IEnumerable<XmlDatatypeValue>.GetEnumerator at0024 and Current/MoveNext/Dispose
calls. ReadOnlyCollection.GetEnumerator delegates to the backing IEnumerable.
This accounts for an allocating enumerator path; the failed probe proves only
that its exact0B target was exceeded, not the byte count.

The coordinator explicitly authorized the later ordered composition owner to
replace exactly that hash loop with indexed traversal, preserving seed17,
multiplier31, item order, recursive dispatch/work and retained snapshots. Prior
core/family task records and historical evidence remain untouched. One new
composition regression independently checks the frozen ordered fold and exact
work/one-short behavior. The owned IL inspector rejects a GetEnumerator call in
the corrected hash method. The probe now saves actual prefix distributions,
first-route bytes and failure metadata before asserting unchanged zero targets.
No allocation/timing retry is permitted outside a fresh grant.
