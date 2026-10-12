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
