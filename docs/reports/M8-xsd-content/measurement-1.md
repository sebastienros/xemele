# Preserved first measurement failure

Accepted source `ccb384811d47099f931854c7b2a79c962d4ba2d1`, prepared tip
`4637ebe8ae4f23cacf826cfd248c61cb96bee264`, manifest SHA256
`806a9dc5ad3e673a85ee9093f7da154059bfe172077ffbe6a557e419f2b6aa90`.
Root grant `c7c7ca49` began at 2026-10-12T01:27:44.273488Z with expiry
01:37:44.273488Z. Root durable cleanup RELEASE `f2e5dae0` preceded inspection.

The supervisor exited 1 after 0.31723220797721297 seconds. The first child
PID 85004 exited -6; all 213 identities matched before and after. The child
was reaped, its process group was absent, and its log was closed. The second
BenchmarkDotNet command was never started; there was no retry.

`artifacts/M8-xsd-content/measurement/command-0.log` records
`Allocation ownership control failed: warm wildcard cursor` at probe line 26.
The original probe throws before serialization, so `allocations.json` is absent.
No byte count or distribution is available and none is inferred from the failure.
The original lifecycle and log remain in the original directory.

Post-release source and IL inspection show `ContentNamespaces.Allows` obtains
an `IReadOnlyList<string>` from `XsdWildcard.get_Namespaces`, then calls the
enumerable interface's `GetEnumerator`, `Current`, `MoveNext` and `Dispose`.
The frozen list is a read-only wrapper over
an owned `List<string>` (`SchemaSnapshots.List`); interface enumeration boxes
the list enumerator on each wildcard match. Other content traversal helpers
run during owned compilation and are outside this warm matcher correction.

The bounded owned correction replaces only this loop with indexed borrowed
reads over the same immutable list, preserving order, short-circuit matching,
ordinal equality and per-item operation charge/cancellation checks. The probe
adds each completed observation row before ownership checks and persists all
completed distributions before throwing; unrun positive/retained fields remain
null. A new output directory `measurement-2` preserves the first attempt.

After the active unrelated host hold releases: build the changed source/probe,
confirm indexed calls and absence of enumeration in IL, rerun relevant 285-case
correctness/API/XML-only checks, refresh disabled controls and five-case NoEmit
discovery, commit and freeze new identities, obtain changed-source closure and
a fresh all-host root grant. No new allocation/timing claim exists yet.

Correction source `127d15b2fedd1675572bad17b4822c80ee94d241` now passes the
actual 285-case correctness/API/XML-only check (zero test failures/errors/skips,
1,400 public/nine frozen members, Release builds zero warnings/errors, planning
179). The IL now calls `get_Item` and `get_Count` with no enumeration calls;
before/after IL is retained in `artifacts/M8-xsd-content/wildcard-*-il.txt`.
The unchanged test subtree is `efe2a8c3f65d8bb75aaded5f5e033fd059e38ec7`;
corrected ContentModels subtree is `5331aa4bd823d48b55beb53b7bd2f1ec9960e570`.
Ten disabled probe routes, five BDN semantic controls and exact five-case
MemoryDiagnoser ShortRun InProcessNoEmit discovery pass. Both payloads rebuild
with zero warnings/errors. No actual allocation/timing measurement ran again.

Fresh attempt-2 manifest: 40,775 bytes, SHA256
`8bcc6182e3e89db9f8a3e627cc1f58e3c635b6d89f3a072a352eb7f0d1fb52a9`.
Supervisor: 7,428 bytes, SHA256
`b77dee00b003416eb10999c2dbe8788619a2dfff6b9c69adeb54d5d4115ca552`.
All 213 identities and elevated process-list permission pass. Bounded source
closure and a fresh all-host root grant are still required.
