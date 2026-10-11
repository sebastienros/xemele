# M3 contract declaration checks

This report family is scoped to the M3-contracts freeze. Exact declarations are
`tests/Contracts/Fixtures/M3/*.cs.fixture`; they are nonpackaged and contain
consumer-test fake bodies. No runtime streaming/writer API was added.

Run `python3 docs/reports/M3-contracts/check.py` from the repository. The script
restores with local SDK/cache and NuGetAudit disabled for deterministic offline
contract checking, runs independent Sync and Async consumer fakes, compiles DOM
async partials alongside integrated runtime sources, and requires the negative
projects to fail specifically with CS4007 (borrowed span used after await) and
CS8345 (borrowed callback view stored on a class). It also runs the task validator.
Detailed transient command output goes to `artifacts/M3-contracts/contract-check.txt`.

Sync excludes all public async fixture declarations. Both consumer projects
reference the actual integrated M2 library rather than a model of its types.
DOM source composition is a separate nonpackaged assembly because partial types
cannot extend a type in a referenced assembly. Its sources are the integrated
runtime, not a later worker branch. The fixture introduces only the approved
async DOM signatures. This proves source feasibility while preserving the real
library's earlier public signatures; it does not implement the methods.

Executable fake checks cover all sixteen handler/delegate/property signatures,
minimal subclass/lambda equivalence, indexed/concrete borrowed collection shape,
individual attribute/namespace-to-start-tag composition, explicit default metadata,
callback exception identity, accepted-event pause/resume without replay and EOF.
The async fake pump exercises owned memory across actual stream ReadAsync, then
synchronous span callbacks, plus a suspended resource continuation. Fake buffers
allocate freely: they are not runtime allocation/performance evidence.

The specification fixes exact DTD effective-model ordering/lexical projection,
unknown attribute/content representation, recovery result path, strict/push/reader
states, token access/lifetimes and writer state/flush/async/indentation behavior.
Compiler rejection is language evidence, not proof of runtime buffer retention.
Runtime chunk-split, source quotas, encoding, reentrancy, external resource disposal,
1 GiB memory, sync/async equality and allocation acceptance remain feature tasks.

Owning follow-ups: M3-attribute-metadata must expose actual default provenance;
M3-async-resources must provide NeedResource/AdvanceResourceAsync and resumable
external-source framing/refill/async cleanup. The precise affected files and
invariants are in `docs/decisions/M3/streaming-writer.md`. No unverified repair
code is used by the fixture builds. No dependency on M4/M5 is introduced.
