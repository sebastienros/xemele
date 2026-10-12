# Worker assignment: M8-xsd-instance-hint-ordering

Task: M8-xsd-instance-hint-ordering — Validate XSD instance hint order without fetching resources
Checkout: /Users/sebastienros/.codex/worktrees/a053/xemele
Branch: codex/m8-xsd-instance-hint-ordering
Base revision: 69691f493fd2b0d5ce382b2f2cbcfb154dd8d202
Claim revision: b5544af4913420f9e13ba058d6ab65d830d53e0c
Worker chat: 01a1305d-4dd2-7f93-8c9e-ee2e5cfdc8c0
Coordinator: 01a12c6c-b333-71d3-877f-0d186618e21d (local)
Human authorization: Authorize remaining M8 worker chats; gpt-6.1-sol/high isolated worktrees. Main/state writes belong only to M8 root.
Newer direct human instruction: Never communicate or synchronize work with other projects. Only Xemele chats/reviewers coordinate. No foreign messages including farewell/cancellation, no global host-idle claim.
Model: gpt-6.1-sol; reasoning high. Maximum three concurrent implementation workers, no recursive workers, no later milestone, remote push or publication.

Read AGENTS.md, docs/tasks/README.md, docs/prompts/worker.md, this task JSON and listed specs, docs/performance-coding.md and relevant owned source/tests. Own only your JSON and listed paths; no unverified private peer dependencies. Verify exact checkout/branch, claim ancestry and all prerequisites verified before edits. Do not write until explicit ACTIVATE.

Acceptance criteria:
- M8-xsd-instance-hint-ordering-A1: Freeze the explicit project interpretation in the owned decision before runtime edits: check global validation-root hint ordering independently of fetching, including skipped subtree names/hints without enabling skipped type/content/lexical assessment. Preserve that this skip corner is a project interpretation, not a W3C-adjudicated oracle. Reproduce and preserve the seven exact original failures/statuses/hashes from the coordinator triage.
- M8-xsd-instance-hint-ordering-A2: Add only SchemaHints.cs and minimal Evaluator.Enter hook. A per-validation ordinal set tracks earlier physical element/attribute namespace names. Check every hint namespace against preceding tags before recording the entire current tag; same-tag order is irrelevant. Empty namespace is real; namespace declarations, QName values and hint tokens/locations do not establish use. Report one source-located ordering error per offending hint attribute, preserve normal typed values/identity hooks, no fetching or document mutation.
- M8-xsd-instance-hint-ordering-A3: Complete all seven unchanged original instances as invalid using explicitly compiled original first-hint schemas. Verify same-tag permutations, first-use and prior attribute-only use, unqualified attributes, repeated/multiple hints, non-use declarations/QName values, explicit skip interpretation and preserved skip behavior, lexical errors, recovery after a content failure and zero resolver calls. No invalid-pass credit from a refusal or missing prerequisite.
- M8-xsd-instance-hint-ordering-A4: Preserve charged borrowed scanning, ordinal comparisons, bounded per-run retention and no global state. Prove exact/one-short work/diagnostic limits, cancellation inside long tokens/comparisons with null caps, source locations, repeat/concurrent isolation and immutability. Pass affected evaluator/identity/facade checks, unchanged public/frozen APIs, normal and true XML-only consumers, planning validation and actual allocation evidence through existing infrastructure under a fresh root lease.

Owned paths:
- src/Xemele/Validation/Xsd/Evaluation/Evaluator.cs
- src/Xemele/Validation/Xsd/Evaluation/SchemaHints.cs
- docs/decisions/M8/instance-hint-ordering.md
- tests/Xemele.Tests/M8-xsd-instance-hint-ordering/
- tests/fixtures/M8-xsd-instance-hint-ordering/
- benchmarks/Xemele.Benchmarks/M8-xsd-instance-hint-ordering/
- docs/api/M8-xsd-instance-hint-ordering.txt
- samples/M8-xsd-instance-hint-ordering/
- docs/reports/M8-xsd-instance-hint-ordering/

Checks: actual original baseline/repaired outcomes and independent negative/neighbor oracles; relevant compiler/evaluator/content/facade tests, exact public1430 and all8 frozen API lanes, normal and true source-linked XML-only consumers, planning validation and diff check. Reuse existing task-owned check support and .NET10.0.400/runtime10.0.11 offline toolchain. Correctness must be bounded, preserve genuine failures, never count unrun checks or refusals as semantic invalid passes.

Prepare all allocation fixtures/commands/binaries and counter-disabled control outside a fresh root measurement grant. No counters, heap probes, timing, WarmBorrowed or BDN without explicit separate root grant. Root sole measurement launcher; lifecycle cleanup and durable RELEASE precede raw inspection. Record allocation ownership/setup/positive/control/retained memory separately. No gratuitous benchmark reruns.

Commit implementation then truthful evidence separately. implemented means all branch criteria actually pass; verified is coordinator only. No runtime fallback stubs, relaxed caps or hidden exclusions. Finish with concise exact revision/status/checks/remaining/outside requests; relinquish writes and wait for focused followup.

The accepted bounded design remains in docs/orchestration/brief-M8-xsd-instance-hint-ordering.md and exact original triage in M8-xsd-instance-hint-ordering-triage.json. Read both. Freeze the owned decision before runtime edits; skipped-subtree global hint bookkeeping is explicit project interpretation, not independently W3C adjudicated. Preserve every other skipped assessment behavior, no-fetch, and original seven INVALID oracles.

Human policy update verified2026-10-12: allocation evidence needs no isolation, PARK, ACKs, exclusive host/thread usage or coordination lease. Retain meaningful controls, identities, actual counters and bounded cleanup; coordinate root execution to avoid accidental duplicate evidence, not to gate other threads. Earlier exclusive-grant instructions in this historical assignment are superseded. Throughput/timing requirements unchanged; never communicate or synchronize other projects.
