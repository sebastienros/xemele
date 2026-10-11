# Integrated serializer verification

Integration revision `fa3bb3ab86bc445976d19ad720c125dd190e63ce`. Actual pinned .NET 10.0.11 / SDK 10.0.400 on Arm64. Focused serializer, legacy, builder, tokenizer, model, PI, parsed-text and XML serialization execution passed 1,622 tests with zero errors, failures, skips or not-run cases. Test assembly, sample and independent API checker built offline in Release with zero warnings/errors.

Official compiled API comparison matched 1,253 members, preserving 1,235 predecessors and adding exactly 18 frozen members. Immutable corpus inventory 3,388, bootstrapFalse; missing/added/changed negative controls rejected. Public sample passed all three APIs, both modes, outer/template/fragment, caller UTF8 stream and earlier XML. All 18 owned tracked runtime/test/fixture/probe/sample/API files match measured source. Planning validation passes 169 records.

Final measurement evidence was independently inspected only after durable release0520b27: 231 allocation rows, 214 warmed borrowed zero rows/213,760 operations, and all 49 selected NoEmit timing cases. Preserve first-phase valid historical measurements and explicit timeout. See measurements.md for cold/owned/retained costs, raw GC numerator/denominator, noise and Arm64-only limits. No further measurement rerun.

The isolated merge initially rejected a textual owner-label difference; coordinator owner, branch and base were preserved, metadata repaired and validated, and the local merge amended before main advanced. No unresolved ownership or product request remains.
