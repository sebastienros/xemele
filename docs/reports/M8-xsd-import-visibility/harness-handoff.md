# Harness correction — SOURCE ONLY, all execution UNRUN

Runtime/compiler, tests, fixtures, BenchmarkDotNet and 24-route probe payload
remain exactly at 8cb31895a01fccf945dc2b2ff5a58ef39fd14fab. Original source-identities
and source handoff are historical immutable records; the replacement harness is
bound separately in harness-source.json, and does not require the old check.py
hash. Compiler SHA256 remains 843e59a35be16bd838f31c9b5859223e3043f179a865baf72bbe7d70d566bed3.

The reviewed supervision.py was copied byte-for-byte from corpus source SHA256
407b228d8b4741445eaf928a44aedd4de85aacaac894e6ad23b8ed8071395903. All six functions
are unchanged. nested_ownership.py came from fe1db7d70cc4b10a04aaa5ac8107cd69a95f7a16e21a0888892ef70c5f05a128;
only OUT and REGISTRIES constants now name this task. The functions are unchanged.
There are no runtime imports or build dependencies on the peer checkout.

family.py wraps those algorithms with an outer root registry, per-child 10-second
cleanup reserve inside the child's total bound, a 20-second root drain/receipt
reserve, deadline checks before launch/after cleanup/before publication, and no
next command after any lifecycle failure. Every registered new session publishes
intent to each ancestor before launching, atomic PID/PGID before workload, then
requires complete parent ACK before exec. All receipts precede raw-result reads.
Normal exits also reap/drain groups and close logs. TERM/INT unwinds owned cleanup;
SIGKILL of an intermediate parent leaves groups registered with every ancestor.

check.py removes resume entirely. Required restore/build/tests/API/consumer/control/
planning/diff commands run fresh in one exclusive attempt directory. A source or
lifecycle failure stops subsequent commands. checks-completed.json is published
only after every required check and final git diff check succeeds and the owned
family is clean. Failed attempts retain explicit lifecycle/family error records;
no passing summary is published for failed correctness or diff. The old top-level
fixed XML output/resume path is gone. launch.py is the enclosing root controller;
invoking child scripts directly requires inherited root ownership and review hash.
Check total root bound is 840 seconds, including all cleanup; each child bound is
180 seconds including 10 seconds reserved cleanup, rather than 180 plus cleanup.

Eight SOURCE proving controls in lifecycle-check.py cover normal reaping/group
absence/log closure, timeout SIGTERM->exit0 with no-next, forced intermediate
parent SIGKILL with a separate-session grandchild ACK'd in ancestor registries,
atomic partial ACK bootstrap blocking/exclusive publication, unpublished intents,
late all-absent process snapshots, changed tests/dependency with unchanged runtime
and changed exact commands, and failed correctness/final diff refusing success.
The copied control algorithms use real owned children where lifecycle matters;
source-only claims do not imply any of these controls passed.

Readiness sequence, only after independent coordinator SOURCE PASS:

1. Coordinator writes harness-review.json with result=SOURCE PASS and the exact
   harness-source.json SHA256. The worker never manufactures this record.
2. Root runs `launch.py --released REVIEW_SHA controls`. This produces an immutable
   positive-controls-release.json only after all eight controls and root family
   cleanup succeed. It binds semantic and family receipts and source identity.
3. Root runs `launch.py --released REVIEW_SHA check`; relevant suite/API/normal and
   true XML-only consumers plus the semantic disabled probe run under family control.
4. Root runs `launch.py --released REVIEW_SHA prepare POSITIVE_RECEIPT RECEIPT_SHA`.
   Offline single-node/build-server-disabled preparation builds only the probe,
   records required commands, pins actual runner/product dependency/runtime/host/
   interpreter binaries, runs semantic --control with all counters disabled, and
   freezes source/config/test/dependency/binaries plus successful control/cleanup
   receipts into an exclusive prepared.json. Missing/changed coverage refuses it.
5. Root solely runs `launch.py --released REVIEW_SHA measure PREPARED_JSON SHA`.
   The prebuilt launcher verifies exact disabled/counter commands and whole frozen
   identities before and after each child. Disabled semantic failure prevents the
   counter child. It has 120 seconds total, 100 work and 20 root cleanup/receipt
   reserve; outer root bound is 150 seconds. Counters are synchronous allocation
   counters only; no isolation/PARK/ACK/lease campaign is required. Bootstrap ACK
   in process ownership is unrelated to such a campaign. Raw allocation results
   remain unopened until lifecycle release, then the coordinator inspects them.

Python syntax parsing, exact helper-copy comparisons, runtime/control diff and
source hygiene are source inspection only. Builds/restores/tests/control jobs,
consumer/API runs, readiness preparation, counter runs and measurements are all
UNRUN. No claim that the proposed harness works precedes actual released controls.
