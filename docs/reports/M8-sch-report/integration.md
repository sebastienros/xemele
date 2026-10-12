# Integrated reporting verification

Actual tested three-feature integration: `223d094fb93e2a454557822818c06baa296e8702`. Integrated into main at `2a610123e628f9b3b5abf3e5d7733775be889d65` after terminal worker handback `59b81b304aa204eb31722c83df66874f34dfa787`.

Coordinator ran `python3 docs/reports/M8-sch-report/checks.py`, then content `check.py`, then facets `checks.py` in the isolated combined checkout. Reporting42 owned +504 affected tests, content285, and facets804 all passed with zero failures/errors/skips/not-run. These suites overlap; their sum is not a unique test count. Builds had zero warnings/errors. Public API1403 and frozen reporting3/evaluator9/compiler154/content9/facet316 matched. All normal and genuine XML-only consumers passed;179-task planning validation passed.

A full changed-path comparison from the tested revision to main found only orchestration and report/task metadata; source/tests/benchmarks/build/API/consumer/check inputs were unchanged. No rerun of unchanged checks is claimed for the metadata-only merge.

Independent source review accepted the exact serializer/test/golden/API hashes recorded in the task. Coordinator independently inspected the actual allocation result and agreed with `allocations.md`: exact thirteen consumed rows, owned string/UTF8 costs, reused destination excluded, approximate retained/RSS values separately labeled. Report child89187 exited0 with418 identities unchanged, reaped/group absent/log closed. Grantb9e9f7e9, durable host RELEASEcf39961a at2026-10-12T01:51:38.218848Z preceded raw inspection. No zero-allocation, timing, speed, x64 or full corpus claim.

Transient raw output is retained only until verified cleanup; source, compact allocation analysis and identities remain committed. No remote publication.
