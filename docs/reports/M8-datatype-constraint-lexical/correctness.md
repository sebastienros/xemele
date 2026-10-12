# Constraint lexical candidate correctness

Source candidate: `a58aed481ea2c6831111b73d037f92d1da21a2c1`, isolated branch
`codex/m8-datatype-constraint-lexical`, checkout
`/Users/sebastienros/.codex/worktrees/357f/xemele`.

Pinned SDK 10.0.400, actual Arm64 .NET 10.0.11; cache-only restores and Release
builds, zero warnings/errors. `checks.py` preserves the exact commands and class
selection, excluding every `*WarmBorrowed*` allocation test method. No allocation
counters, BDN, timing probe, or throughput experiment was run.

- 731 selected tests passed: 86 own and 645 datatype/facet prerequisites. Zero
  failures, errors, skips, or not-run cases. Four own facts consume 1,024
  deterministic IEEE bit patterns, 100 date/calendar boundary combinations,
  changed actual union selection, and empty/QName/built-in lists.
- Public compiled API union: 1,410 members match. Frozen Datatypes and Facets:
  316 baseline rows each match unchanged.
- Real normal and true XML-only consumers both pass the integer/pattern and
  decimal/integer-grammar witnesses, including typed fixed-value equality.
- Planning: 182 tasks valid. `git diff --check` passes.

The runner initially asserted an invented lower bound of more than 800 cases;
its actual 731 tests all passed. We corrected the assertion to the independently
inspected exact count and retained the successful run. `--reuse-tests` continues
API/consumer checks using that result; callers must use it only when source and
built tests have not changed. The first draft build required `scoped` on the
writer's borrowed span parameter; the committed candidate includes that repair.

Transient XML and shell logs remain ignored artifacts or /private/tmp, not
committed evidence. Source acceptance, controlled allocation evidence, and
coordinator integrated verification remain pending.
