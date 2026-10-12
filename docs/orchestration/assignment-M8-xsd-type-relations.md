# Worker assignment: M8-xsd-type-relations

Task: M8-xsd-type-relations — Correct inherited XSD attributes and type relations
Goal: Correct existing XSD1.0 union substitution, complex simple-content inheritance, effective attribute restriction and vacuous anySimpleType content restriction using unchanged original witnesses.
Checkout: /Users/sebastienros/.codex/worktrees/99ab/xemele
Branch: codex/m8-xsd-type-relations
Base revision: e59e96489f48d4ffb3e1a56a8a916db8e475aaf2
Claim revision: 34961be37c2a4a26e39b6a0cd2922766e71626f2
Worker chat: 01a13085-3620-7602-9211-4f452d7ff5b4
Coordinator: 01a12c6c-b333-71d3-877f-0d186618e21d (local), sole main/state writer.
Human authorization: “Authorize remaining M8 worker chats”; gpt-6.1-sol/high isolated worktrees. “Yes, authorize all M8 coordination messages”.
Current policy: Never communicate or synchronize with other projects. Allocation evidence needs no isolation, PARK, ACKs, exclusive usage or coordination lease. Root sole probe launcher avoids duplicate evidence only; meaningful identities/controls/counters/finite cleanup retained. No M9, remote push/publication, new workers or broad exclusions.

Verify repository root, branch, base/claim ancestry and verified recursive-components/fixed-restriction dependencies before edits. Read AGENTS.md, tasks README, worker prompt, your task JSON/listed common+schemas specs, performance rules and relevant source/tests only. Wait for explicit ACTIVATE. Own only the following paths plus your task JSON; do not alter shared projects/APIs/model/matcher/evaluator/other tasks or orchestration.

Owned paths:
- src/Xemele/Validation/Xsd/Compilation/Compiler.cs
- src/Xemele/Validation/Xsd/ContentModels/Compiler.cs
- tests/Xemele.Tests/M8-xsd-type-relations/
- tests/fixtures/M8-xsd-type-relations/
- benchmarks/Xemele.Benchmarks/M8-xsd-type-relations/
- docs/api/M8-xsd-type-relations.txt
- samples/M8-xsd-type-relations/
- docs/reports/M8-xsd-type-relations/

Acceptance criteria:
- M8-xsd-type-relations-A1: Accept original particlesZ012/Z015/Z020 by implementing union-member simple derivation in compiler substitution validation and runtime substitution membership; preserve head block/final and unrelated-type rejection, nested union/member-derived cases and shared work. Runtime membership must use the union-aware relation in the content-model compiler, retaining existing precomputed member arrays and shared charging; prove newly admitted member UPA participation. No matcher/evaluator/model/API changes.
- M8-xsd-type-relations-A2: Accept original particlesZ031: distinguish simple type definitions from complex types with simple content; inherit simple content and attributes for an empty complexContent extension, reject added element particles and preserve derivation rules.
- M8-xsd-type-relations-A3: Restore unmentioned base attribute uses during restriction, except effective overrides/direct prohibitions. Omit prohibited declarations from attribute-group components. Prove original attZ015, ctE019/ctF013/ctG001/ctG013, particlesZ002 and Suncombined009 test.11.v/test.3.v/test.5.v. Preserve constraints, contexts, locations, required/default/fixed behavior and direct/group prohibition distinction; do not inherit base wildcard as a shortcut.
- M8-xsd-type-relations-A5: Accept unchanged XSD1.0 originals stZ007, stZ047 and stZ055 by retaining inherited anySimpleType as the content datatype only for complex simpleContent restriction with no inline simpleType and no facets. Keep the complex component distinct with its actual BaseType and Restriction, and preserve final, attribute and other restriction checks. Explicit simple-type restriction of anySimpleType, nonempty facets and a complex base forbidding restriction still reject; inline simple types follow ordinary validation. This implements Second Edition 3.4.2 case1.2 and identical-type derivation, not the XSD1.1 fresh anonymous datatype rule.
- M8-xsd-type-relations-A4: Preserve original baselines and independent standards oracles; pass focused owned/affected checks, public/frozen APIs, normal/XML-only consumers, bounded actual allocation evidence and planning validation. No XSD1.1 union-facet rule imported; disputed stZ073b remains separate.

Ownership correction review is docs/orchestration/review-M8-xsd-type-relations-scope.json: runtime membership is independently built in ContentModels/Compiler.cs; changing compilation acceptance alone is insufficient. Existing precomputed member arrays and block/work semantics remain. No matcher/evaluator/model or frozen-interface change required. Actual original baseline first; separately specified public neighbors, nested/member-derived unions, blocked/final/unrelated and newly admitted UPA members. Preserve inherited binding/default/fixed/required contexts and prohibition distinctions, empty complex-content extension versus actual simple definition, no new XSD1.1 datatype rule.

Original source cache: /private/tmp/xemele-xsdtests-w3c pin7bc3365c652a322f3d762021b3879eb92dae7e30. Locate exact IDs/unchanged expectations/source hashes in relevant msMeta/Sun metadata, freeze own fixtures/provenance/copyright. Do not infer expectations from .v/.i names. Preserve every earlier failure and no refusal-as-semantic-invalid credit. Independent oracles and original source pin required; disputed stZ073b remains separate and only exact time_maxInclusive007_119 exception is approved.

Checks: pinned .NET10.0.11 Release, SDK10.0.400, offline ~/.nuget/packages, disable build servers/single worker/shared compilation false/audit false. Reuse existing focused own check infrastructure without changing shared files. Run actual owned/affected compiler/content/evaluator/facade/datatype regressions including latest seven verified repairs, public1430/all8 frozen/emitted APIs, normal and true XML-only consumers, meaningful work/cancellation/location boundaries, python3 tools/planning/tasks.py validate and git diff --check. Checks finite, controls and cleanup explicit; no unrun success. Before runtime source acceptance send focused source/test review handoff. Before building a new allocation probe send source/design/hashes for acceptance; then prepare offline prebuilt/disabled-control/manifest/finite lifecycle. Actual counters root launches; never count control zeros as zero evidence. Separate setup/first/warm owned allocations/retained coarse heap; no unrelated throughput runs or timing claims.

Commit implementation, then truthful evidence metadata separately. implemented requires all actual branch criteria passed; integration_revision remains unset and verified belongs to root. No private peer dependencies or unfinished particle/optional composition consumption. Concise final exact revisions/checks/remaining/outside requests, clean checkout, zero owned jobs; relinquish writes and wait focused followup.

Focused ownership amendment after actual affected checks: add only tests/Xemele.Tests/M8-xsd-content/DerivationTests.cs with direct verified M8-xsd-content dependency. Correct two stale A3 expectations: required base attribute omitted locally remains inherited and valid; incompatible changedType extension must reject, with schema construction enclosed in Assert.Throws so earlier compilation rejection is accepted. Preserve semantic rejection and observed two failed checks. No other test/source/contract ownership expands.
