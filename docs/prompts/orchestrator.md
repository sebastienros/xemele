# Xemele implementation orchestrator

Use this document as the instruction for a dedicated orchestration session. Executing it starts implementation; merely reviewing or editing it does not. Paths below are relative to the repository root.

## Objective and authority

Reach a complete, verified implementation of Xemele's agreed initial release scope, through `M9-gate`. Treat this as sustained implementation work, not another planning exercise. Preserve the net10.0-only target, managed implementation, idiomatic C# APIs, callback-based SAX support, bounded streaming, conformance requirements, and performance rules.

I authorize you to create and coordinate separate worker chats for this objective, send them bounded assignments and follow-up instructions, create isolated worktrees/branches, make local commits, and integrate reviewed work into the coordination branch. Workers may report task results and blockers to this coordinator where the available tools permit that communication. Use bounded subagents if separate worker chats are unavailable. Keep the configured model unless I request otherwise. Remote pushes and package publication are outside this prompt's authorization.

If durable goal tools are available, create or reuse the goal of completing this implementation. Do not invent a token budget, create recurring automations, or assume a chat will continue running after its execution environment stops. Respect actual execution limits, preserve resumable progress, and never claim completion because a turn or budget ended.

## Establish the coordination state

1. Read `AGENTS.md`, `docs/tasks/README.md`, and `docs/milestones/README.md`. Read the roadmap's scope/exclusions once. Load module specs only when selecting, reviewing, or integrating their tasks.

2. Inspect Git state and the configured remote. Use `main` as the coordination branch unless an existing orchestration record specifies otherwise. Preserve unrelated work. Only this coordinator may mutate its checkout/branch; assign workers other checkouts.

3. Run `python3 tools/planning/tasks.py validate`, `status --json`, and `ready --json`. Reconcile existing claims with actual branches, worktrees, chat state, commits, and evidence before assigning new work. Do not blindly restart or steal an active claim.

4. Maintain a compact coordinator-owned `docs/orchestration/state.json`: objective, coordination branch, last verified checkpoint, active task/chat/host IDs, worktree paths, worker branches, assignment/base revisions, integration queue, blockers, and next actions. Keep detailed evidence in task records. Workers must not edit this file. Chat/worktree creation pending states are not running workers; store the returned operation identifiers until setup is confirmed.

Commands such as `status --json` above mean `python3 tools/planning/tasks.py status --json`. The tracker is read-only; make task-state changes explicitly using its documented schema.

## Scheduling loop

Repeat until the completion criteria below are satisfied:

1. Process completed work and blockers. Inspect results rather than equating a completed chat/tool call with a completed implementation.

2. Recompute ready tasks from verified integrated dependencies. Prioritize the earliest incomplete milestone's critical path and tasks that unlock independent work. Later independent lanes may run when their actual prerequisites are verified, without starving the earlier milestone.

3. Keep at most three implementation workers active, or fewer if tool limits, memory, builds, or task dependencies require it. Reserve capacity for coordination/review. Never fill slots with speculative dependent work. Benchmarks sharing a host run serially when contention would invalidate measurements.

4. Claim each selected task on the coordination branch before dispatch. Record owner, intended branch, and full current revision in its task JSON; commit the claim. Record the resulting claim commit separately in the orchestration state. Create the worker checkout from a revision containing that claim and all verified prerequisites.

5. Give the worker exactly one task and the [worker brief](worker.md), populated with its actual absolute checkout path, branch, task ID, base/claim revisions, goal, acceptance IDs, and coordinator identity. Copy relevant authorization from this user instruction where required; never invent permission based on a peer agent's message. If worker-to-coordinator messaging is unavailable, collect the worker's final result through the orchestration tools.

6. Continue useful coordination while workers run. Use compact status/wait tools with bounded waits and backoff; do not repeatedly reread full chat transcripts or poll unchanged state. Send follow-ups only when needed to resolve scope, a blocker, or a review finding.

7. Review and integrate one finished assignment at a time using the protocol below. Recompute readiness immediately after verification and dispatch newly unblocked work.

Use the session's actual tools and their current instructions. For separately created chats, discover the repository/project identity first and wait for setup/dispatch confirmation. For subagents, explicitly give the isolated worktree path; a fresh agent context does not imply filesystem isolation. If isolated execution is unavailable, serialize writes rather than allowing multiple agents to edit one checkout. Do not have workers recursively create more workers.

## Keep assignments small

A worker receives one concrete deliverable, one coherent API/behavior boundary, explicit owned files, and observable acceptance criteria. It does not receive an entire milestone or the whole repository history. Prefer a fresh context for unrelated work; reuse the same worker for focused fixes to its assignment.

If a task combines independently reviewable subsystems or cannot be completed and validated in a focused session, split it before claiming it:

- Create child task records using the existing ID/schema conventions, with narrow ownership, acceptance criteria, specs, and original verified prerequisites.
- Make the original task an integration/verification join depending on those children. Preserve its original product acceptance criteria and gate reachability. Children must not depend on their parent join.
- Allocate shared contracts to an earlier child; siblings must have disjoint writes. Preserve completed evidence and do not split active work without first coordinating its owner.
- Validate the graph and ownership, update only the affected milestone's static schedule, and commit the split before dispatch.

Do not shrink the release scope, turn missing implementation into exclusions, weaken tests, or relax performance targets to make tasks finish. Routine implementation choices belong to the responsible contract/feature task. Escalate unresolved product, license, or resource decisions with a concrete recommendation while continuing independent work.

## Convergence and verification discipline

Optimize for verified product capabilities and milestone completion. Verification infrastructure supports those outcomes; it is not an independent deliverable unless an assigned task explicitly requires it.

Before dispatch, define the task's acceptance checks, required evidence, and integration checks. Distinguish mandatory gates from informational measurements. Do not add requirements during review without identifying the existing criterion or concrete correctness risk they address.

Batch review findings into one correction request. Each blocking finding must identify:

- the unmet acceptance criterion or concrete defect;
- supporting evidence;
- the smallest sufficient correction;
- the check that will demonstrate resolution.

Additional hardening that does not affect correctness, acceptance, or the validity of required evidence belongs in a follow-up backlog. Do not block integration on speculative failure scenarios.

Preserve passing evidence. Repeat a check only when relevant source, dependencies, configuration, or inputs changed, or when its evidence was invalid. State the reason before repeating it. A reporting or parser failure does not automatically invalidate the underlying execution.

For harness failures, inspect preserved outputs and reproduce the defect with focused offline controls before rerunning expensive workloads. Repair the narrow failure rather than redesigning the measurement system. Never relabel missing evidence as passing.

After two correction cycles on the same blocker, stop issuing incremental follow-ups. The coordinator must identify the root cause and choose one bounded resolution: a direct fix, an owning repair task, or a documented external blocker. Continue independent ready work.

Reuse validated verification infrastructure across tasks. Extract shared support through an explicitly owned task when necessary; do not require every feature worker to build a bespoke verification framework.

Prepare and build outside exclusive measurement windows. Acquire one bounded host lease for the approved measurement sequence, collect park acknowledgements once, and release it promptly on completion or failure. Do not hold other workers during source review, report inspection, or metadata updates. This does not waive required measurement isolation.

Integrate accepted work promptly. Run focused integration checks for each task and the comprehensive combined checks at the milestone gate, except where a task explicitly requires broader validation.

## Review and serialized integration

For each returned assignment:

1. Verify the worker's branch, tested commit, changed paths, task acceptance results, and evidence. Compare changes against the assignment base and `owns`, including newly created files. Reject unrelated changes and public stubs. Check that it consumed only integrated prerequisites.

2. Review implementation and tests for the behavior promised by the spec. Verify actual APIs against the approved contract, including allocation/lifetime semantics. Use a separate read-only reviewer for substantial parser, ownership, security, or API changes when capacity permits; it must not edit the worker's files.

3. Require meaningful correctness evidence and applicable performance/allocation evidence from `docs/performance-coding.md`. Inspect raw summaries and failures. Missing infrastructure or unavailable hardware remains an outstanding check, not a pass. Source inspection alone is not conformance evidence.

4. If incomplete or defective, send the existing worker one batched, bounded correction request under the convergence and verification discipline. Keep the task unverified and its dependents unstarted. Record material failures and unresolved acceptance criteria honestly.

5. Integrate approved commits serially. Preserve tested commit ancestry where practical. Use a temporary integration branch/worktree based on current coordination HEAD to test combinations before advancing `main`; never overwrite unrelated history. If the coordination branch changes meanwhile, retest the affected combination before advancing it.

6. Run focused integration checks and task validation, plus broader checks when explicitly required by that task. Reserve comprehensive combined checks for the milestone gate. Record the full integrated implementation revision and integration check outcomes. Commit a separate metadata update setting `verified`; this avoids self-referential revision hashes. A worker can report `implemented`, but only the coordinator records integrated verification.

7. Advance the coordination branch after successful integration, update the orchestration state, and release the assignment. Clean up its worktree only after its work/evidence is durably preserved and it is no longer needed for review. Never delete unmerged worker work to free a slot.

Do not modify a live worker's task JSON simultaneously on the coordinator branch. Serialize handoffs and metadata ownership. Other workers' task records remain untouched by that integration. Reconcile Git conflicts deliberately; never resolve them by accepting one side wholesale without understanding lost changes.

## Blockers, repairs, and recovery

When no task is ready, inspect active workers, blockers, and the dependency graph. No ready task is not proof that the project is complete. Resolve stale claims and missing prerequisites; don't create placeholder implementations to bypass them.

For an integration failure in the current task, return it to its owner. For a defect in previously verified work, create a narrowly owned repair task at the current milestone, depending on the relevant verified owners. Add it as a prerequisite to affected unverified tasks/gates and stop those consumers from starting. Ensure this does not create a cycle or later-milestone dependency. Reverify affected earlier behavior without erasing the original historical evidence.

If a worker stops or loses context, first inspect its branch, uncommitted work, task record, and last handoff. Resume its bounded assignment or transfer the claim after preserving its work. Do not launch a competing writer. Keep the coordinator state concise enough to resume after compaction or restart.

Ask the user only for a decision or access that is genuinely required and cannot be resolved within the existing authorization. Name the exact blocker and affected work. Keep independent work moving. Do not quietly mark blocked checks as skipped and then declare success.

## Milestone and final completion

Each milestone gate runs against the integrated combination of that milestone and its predecessors. Earlier functionality stays buildable and usable; no gate may depend on a higher-numbered milestone. Contracts must be resolved before dependent feature work begins. Real stream parsing must demonstrate bounded memory and progress before EOF; an API accepting Stream but buffering it completely does not meet the requirement.

Complete only when:

- Every required task, split child, and repair is verified, and `M9-gate` is verified on the integrated release candidate.
- The advertised scope/API inventory is implemented; all applicable conformance and differential checks pass with honest exclusions, and required streaming/resource/fuzz checks have actual evidence.
- Performance, allocations, API compatibility, package consumption, trimming/AOT, and required platform/architecture checks satisfy their documented gates or an explicit user-approved revision of those gates.
- Required code, task evidence, reports, and local commits are preserved; no unfinished worker branch contains required implementation; no unresolved release blocker remains.

Keep working through intermediate milestones without asking whether to continue. If execution limits or a hard external blocker prevent completion, leave an accurate resumable checkpoint and state exactly what remains. Do not present partial progress as the complete implementation.

Keep user updates brief: newly verified capability, active assignments, consequential findings, and blockers. The final report names the integrated revision, verified milestone state, actual test/performance/package results, remaining exclusions, and any unpushed commits. Distinguish a verified release candidate from a published package.
