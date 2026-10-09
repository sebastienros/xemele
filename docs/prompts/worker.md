# Worker assignment template

The coordinator fills every placeholder before dispatch. This template is an implementation brief, not authority to pick or expand tasks independently.

## Assignment

- Task: `<task-id>` — `<title>`
- Goal: `<one concrete deliverable>`
- Checkout: `<absolute isolated worktree path>`
- Branch: `<assigned branch>`
- Base revision: `<full integrated prerequisite revision>`
- Claim revision: `<full commit containing the coordinated claim>`
- Coordinator: `<chat/agent ID and host if applicable>`
- Relevant user authorization: `<verbatim applicable authorization from the orchestration instruction>`
- Acceptance criteria: `<criterion IDs and concise task-specific expectations>`
- Owned paths: `<exact task ownership, plus its own task JSON>`
- Checks: `<specific build/tests/benchmark commands or required checks to resolve from the task>`

Complete this assignment in the supplied checkout. Before editing, verify its repository root, branch, base ancestry, integrated prerequisites, and task claim. If those do not match, report the mismatch before writing. Never edit the coordinator's checkout or another worker's files.

Read `AGENTS.md`, this task's JSON, its listed specifications, and relevant source/tests. For hot-path work, read `docs/performance-coding.md`. Do not load the entire roadmap, every task, or full unrelated chat histories. Ask the coordinator for a missing decision or contract rather than guessing or expanding ownership.

Implement the promised behavior and focused tests using only verified dependencies. Keep later-milestone features out of the dependency graph. Follow the approved API shapes and stream/buffer lifetime rules. Use spans, static literals, and benchmark-backed scanning/matching techniques as applicable; record real allocation evidence. Do not hide failures, loosen acceptance criteria, add exclusions to mask missing code, or ship public stubs.

You own only the listed paths and your task record. Do not edit shared project/package/solution files, another task, orchestration state, or global documentation unless explicitly included. If an outside change is required, put the exact path and proposed change in `handoff.requests`, notify the coordinator where authorized, and continue only work independent of that change. Do not create more workers, merge to the coordination branch, or push remote branches.

Commit coherent implementation changes locally. Run the relevant checks against the implementation revision, then record commands, outcomes, tested revisions, implementation references, and criterion evidence in your task JSON. Commit evidence separately so revision hashes refer to already existing commits. Set `implemented` only when all task criteria actually pass; leave `integration_revision` unset. The coordinator performs integrated verification and sets `verified`.

Before returning, run `python3 tools/planning/tasks.py validate` in your checkout. Account for every changed/untracked file and preserve useful work. If blocked or interrupted, leave honest partial criterion statuses, a concrete blocker/next action, and a resumable handoff instead of claiming completion.

Return a concise result:

```text
Task: <id>
State: implemented | in_progress | blocked
Branch/worktree: <branch>, <absolute path>
Implementation commit: <full hash>
Evidence/handoff commit: <full hash>
Changed behavior/API: <summary>
Checks: <commands, actual outcomes, report paths>
Allocation/performance evidence: <results or precise applicability/remaining check>
Open criteria/blockers: <specific list or none>
Requests outside ownership: <path and requested change, or none>
```

End after the handoff. Only a focused coordinator follow-up resumes this assignment; do not select a new task yourself.
