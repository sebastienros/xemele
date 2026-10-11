# Parsed HTML text persistence repair

The parser-only CreateParsedText/SetParsedText/ImportParsed lanes exempt only
already-charged merged text from the per-token cap. Public CreateText, Value,
Clone and Import keep their existing quota checks. Parsed copies still enforce
name/attribute/comment/PI quotas, immutable owners, template ownership and
attachment/depth checks. The builder must charge tokens/input/expansion before
using these internal members and buffer contiguous text to avoid repeated copies.

Pinned SDK10.0.400/runtime10.0.11 Release builds passed with zero warnings/errors.
29 actual facts passed (4new,16PI,9model); unchanged official API1230 matched,
3388fixtures/bootstrapFalse, all3386 original cache resources copied and source/
destination SHA-verified. Planning169 and diff checks passed.

Initial28/29 run failed because the new witness incorrectly counted text as an
element-depth level. Corrected to preserve existing element-only depth semantics;
all29 passed. No production correction was needed for that failure.

Prepared revision 7d95ed6d896e631a238a41d12b394f751920bdd3; manifest
302d893e55ab9b4a8a590cf6e9543c8349ec541e93d563fdcc8facc61fbc5578.
Complete233source/generated/probe fixtures,10binary,3852SDK/runtime/host and
full effective child environment frozen. Positive/final-positive identity controls
passed; unknown-env control rejected. Root exclusive lease ed15cc5, RELEASE
1590463 before raw inspection. Initial/allocation/final exits0, no children.
8batches4096 updates (32768assignments)0B; positivecontrol1048B;
public ownedtext256000B/1000 and parsed ownedtext256000B/1000;
parsed deeptextcopy376088B/1000 includes existing CopyCore work-stack setup.
Prepared strings/closures created outside counters; these numbers do not claim
DOM creation or cloning is allocation-free. This internal repair reuses existing
DOM creation/copy behavior; no new scanner, matcher or timing optimization.
Builder end-to-end accumulation/composition benchmarks remain required in its task.
No new timing, retained-heap or x64 claim; existing M7-model measured ownership
boundaries remain applicable to unchanged constructor/storage.
