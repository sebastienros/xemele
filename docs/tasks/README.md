# Task workflow

## Read only what you need

1. Run `python3 tools/planning/tasks.py ready` (or `show M1-tree`).
2. Read that task's JSON and its `specs`; read a dependency's handoff only if relevant.
3. Use [milestone plans](../milestones/README.md) for scheduling, [roadmap](../roadmap.md) for product scope. Neither is a live checklist.

Each `docs/tasks/<id>.json` is the sole status source for that task. No generated status table is checked in. All implementation tasks begin `planned`; these documents are not implementation evidence.

Commands: `status` summarizes all tasks; `ready` lists executable tasks; `show ID` prints one record; `validate` checks the dependency graph, paths, metadata, and evidence rules. Optional `--milestone M3` filters `status`/`ready`. Use `--json` for machine-readable output on those two commands.

## State and evidence

| State | Meaning |
| --- | --- |
| `planned` | Unclaimed. Ready only when all dependencies are `verified` and the blocker list is empty. |
| `in_progress` | Claimed on the coordination branch and being implemented. |
| `blocked` | Claimed work cannot proceed; record a concrete cause and next action. Dependency blocking of unstarted tasks is derived, not manually maintained. |
| `implemented` | All acceptance criteria implemented on a branch; relevant checks passed there; awaiting integration verification. |
| `verified` | Integrated on the coordination branch and verified there; only this state unlocks dependents. |

Each acceptance criterion has a stable ID, status (`pending`, `passed`, or `failed`), and evidence references. A partly completed task stays `in_progress`; its criteria show exactly which behavior exists. Use `implementation` for concise file/symbol references. Test evidence records the command, outcome, revision, and report path or concise output. Human reviews are recorded as review evidence, never as test runs.

`owner` is a human/session identifier, `branch` is its branch name, and `base_revision` is the integrated revision it started from. `integration_revision` is the full commit hash containing the implementation; a follow-up metadata commit records it, avoiding a self-referential commit hash. A verified milestone gate represents a tested integration checkpoint, not just arithmetic completion of its tasks.

## Claims and isolation

Use one named coordination branch (initially `main`). A coordinator serializes claims: update the selected task to `in_progress` with owner/branch/base revision and integrate that metadata before dispatching work. Other sessions fetch this branch before selecting work. A claim existing only on a private branch is not a reservation. When no coordinator is present, serialize claim merges before starting; Git alone does not provide distributed locks.

Each worker uses its own branch and worktree based on the coordination branch after its claim. Separate sessions must not edit the same checkout. Do not start against a dependency merely because its branch is available; integrate and verify it first. Read-only exploration can happen earlier, but cannot be reported as an implementation start.

The task owns its JSON record implicitly. `owns` lists all other allowed write paths (trailing `/` owns a subtree; a filename owns only that file). Feature tasks own separate source, test, fixture, benchmark, sample, and API-baseline fragments. SDK default source globs avoid concurrent project-file edits. A task may create folders under its ownership without touching a shared index.

Shared files have a single task owner at each stage. Reuse of paths is allowed only when the later task transitively depends on the earlier owner. The validator rejects ownership overlap between unordered tasks. Passing that check prevents planned path collisions, not semantic integration errors.

Do not casually edit another task's API, project configuration, package versions, README, solution, or workflow. Record a precise change request in `handoff`; route it to the owner, or have the coordinator create a small repair/contract task with explicit dependencies and ownership. A worker must not expand its own scope to bypass this rule.

## Completion and handoff

1. Implement only the task contract; add tests in its owned paths and update each acceptance criterion honestly.
   For runtime hot paths, follow the [performance coding rules](../performance-coding.md) and record allocation/benchmark evidence before verification. Keep benchmarks and reports inside the task's owned paths.
2. Record implementation references and check/review evidence at a known revision. Set `implemented` when all criteria pass.
3. Coordinator integrates the branch, reruns applicable checks on the combined tree, records integration evidence, and sets `verified`.
4. Dependents start from that integrated revision. End a milestone only through its gate task.

Handoff fields: `summary`, `checks`, `remaining`, and `requests`. Record failures and skipped checks explicitly. Reports belong under task-owned documentation paths or ignored `artifacts/<task-id>/`; concise evidence in JSON should survive removal of transient artifacts. Do not bulk-edit completed tasks during routine feature work. Reopening a verified task requires reassessing its dependents; validation will flag dependents whose prerequisites cease to be verified.

Evidence record shape (replace placeholders with actual values; do not copy this as a passing record):

```json
{
  "id": "integration-tests",
  "kind": "integration",
  "revision": "<full tested Git commit hash>",
  "command": "<actual command executed>",
  "result": "passed",
  "summary": "<durable outcome, including counts and relevant limitations>",
  "artifact": null
}
```

`kind` is `check`, `review`, or `integration`; `result` is `passed`, `failed`, or `skipped`. Criteria refer to evidence IDs. The validator checks metadata consistency; it cannot establish the truth of a claimed test run. The coordinator checks actual output and that the implementation revision is integrated before setting `verified`.

## Contract changes and additive delivery

Listed shapes are design contracts, not claims of shipped APIs. Each milestone's contract task must resolve every marked decision, publish a precise API baseline, and compile representative usage before implementations start. No public method that throws `NotImplementedException` is shipped to reserve a future feature.

Later milestones add capabilities through new modules or explicitly owned extension files. They must not be needed to compile, test, or use earlier deliverables. Safe rejection of unsupported features is an earlier-milestone behavior with tests. If a later requirement would force a breaking change, stop that dependent work, record the contract issue, and agree on a compatible extension before proceeding.
