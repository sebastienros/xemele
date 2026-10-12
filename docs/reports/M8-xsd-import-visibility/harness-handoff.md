# Harness correction — SOURCE ONLY; all execution UNRUN

Runtime/compiler, tests, original fixtures, BenchmarkDotNet and the 24-route probe
remain exactly at 8cb31895a01fccf945dc2b2ff5a58ef39fd14fab. Compiler SHA256 remains
843e59a35be16bd838f31c9b5859223e3043f179a865baf72bbe7d70d566bed3. Historical
source-identities and source handoff are immutable. The current owned harness is
separately pinned in harness-source.json; only the coordinator can create an actual
harness-review.json with SOURCE PASS and the manifest's exact hash.

supervision.py is the exact accepted 407b228d8b4741445eaf928a44aedd4de85aacaac894e6ad23b8ed8071395903
copy. nested_ownership.py retains the fe1db7d70cc4b10a04aaa5ac8107cd69a95f7a16e21a0888892ef70c5f05a128
template's functions, adapting only OUT and REGISTRIES. No peer checkout dependency.
Registered sessions publish intent, atomic PID/PGID, and complete ancestor ACK
before exec. Normal and abnormal child completion reap/drain groups and close logs.
A lifecycle failure prevents the next command. TERM/INT unwind root cleanup;
intermediate-parent SIGKILL leaves descendants owned by enclosing registries.

All passing candidates use finite publication: crossing the hard deadline
quarantines the candidate and removes its acceptance path. Final acceptance also
requires the enclosing controller-release.json, binding source/review, payload,
whole-family cleanup and terminal-cleanup receipts. Check has 840 seconds total;
prepare 440; controls 100; measurement 150 enclosing/120 inner (100 work/20 cleanup).
Each normal correctness/build child has 180 seconds including 10 cleanup seconds.
No resume, build overlap exemption, or previously failed attempt reuse.

## Artifact-policy correction

The content-addressed binary archive is removed. No runtime, host or generated
binary bytes are copied into evidence. Actual active files are hashed before and
after execution, with directory membership and fresh argv/lifecycle receipts.
Every command, including builds, verifies all previously executed binary inputs;
a later rebuild changing their bytes or membership fails. Test/extractor/probe
builds and normal/XML-only consumers use distinct active output roots. Restore
and build receive matching SDK UseArtifactsOutput/ArtifactsPath and global
OutputPath flags; intermediate directories separate referenced projects. The
API union uses the same canonical full-row validation as the established M5
checker, with an actual extractor/runtime in the owned active roots. All 53 API
files remain bound by membership and hashes. SDK output relocation is source-only,
unverified; see [SDK artifacts output](https://learn.microsoft.com/en-us/dotnet/core/sdk/artifacts-output).

After verified whole-root group absence and closed logs, terminal_cleanup removes
owned active bin/obj outputs, raw XML/API/control/counter results, derived union,
control scratch and raw logs. This runs on success, ordinary failure and signal
interruption. Canonical sources, original fixtures, package caches and installed
runtime/host files are preserved. Only concise outcomes, lifecycle receipts and
hash/membership identities survive. Deleted files cannot be rechecked later:
terminal receipt consumers explicitly verify recorded deletion and surviving
inputs; they never interpret summaries as active-byte readiness. Any newly restored
file at a deleted identity is rejected. Failed or late enclosing controllers still
cannot authorize a child candidate. Cleanup is refused while any owned group may
remain. SIGKILL of the outermost controller cannot run a Python finally block;
the coordinator must recover/drain that root before terminal cleanup. Absence or
expiry alone never supplies a passing controller receipt.

Prepared probe outputs are active only while awaiting their one measurement or
explicit discard. Preparation deletes its raw disabled-control result after
publishing a concise semantic summary; binaries remain actual inputs, fully pinned
with dependency/runtime/host membership. Measurement/discard atomically claim the
preparation before work; claimed preparations cannot be reused. Root compares
active identities and membership before and after each disabled/counter child.
After the entire enclosing family is clean, root alone reads allocation output,
validates 24 routes/32 operations, positive control, consumption and 28 retained
results/seven categories, publishes concise cold/first/warm aggregates and retained
heap deltas, then removes raw output and prepared compiled files. Allocation
summary and release identities are bound by final controller acceptance. No raw
allocation distributions or binary payload archives remain.

## Release sequence

1. Independent coordinator SOURCE PASS for the exact manifest.
2. Root `launch.py --released REVIEW_SHA controls`: all eleven proposed controls
   and enclosing cleanup must pass. The controls cover real normal/timeout/forced
   parent-death families, partial ACK, missing intent, late absence/publication,
   late enclosing acceptance, source/command/API/binary/result mutations, failed
   correctness/diff and no-next. Finite source test seams now cover terminal
   deletion for success/failure/interruption, restored-byte rejection, and refusal
   while ownership remains uncertain. All controls remain UNRUN.
3. Root `launch.py --released REVIEW_SHA check`: fresh owned/affected correctness,
   safe resolver42, datatypes77, full/frozen/additive API checks, normal/true XML-only
   consumers, disabled probe, planning and final diff. A concise terminal receipt
   replaces the transient XML/API outputs after their actual validation.
4. Root `launch.py --released REVIEW_SHA prepare POSITIVE_RECEIPT SHA`: fresh offline
   single-node probe build and disabled semantic control; active prepared.json.
5. Root solely invokes `launch.py --released REVIEW_SHA measure PREPARED_JSON SHA`,
   or `launch.py --released REVIEW_SHA discard PREPARED_JSON SHA` when abandoning
   prepared outputs. No isolation/PARK/ACK/lease campaign is introduced.

Ten Python files parsed statically; planning and diff/source-copy checks only.
No harness imports, executable controls, builds, restores, tests, consumers,
extractors, preparation jobs, counters or measurements have run. Runtime and all
acceptance criteria remain pending integrated verification. Freeze this source
packet for review; no readiness or control pass is implied by syntax/hash checks.
