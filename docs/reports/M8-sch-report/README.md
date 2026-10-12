# Schematron report source checkpoint

Source revision: `f6db4e76dca6c57ef97f11149348edc3a6a4272c`. All branch acceptance criteria are implemented with applicable owned allocation evidence; integrated verification remains coordinator-owned.

`SchematronReport` implements the exact two frozen static methods. An operation groups fired events by PatternKey and findings by FiredRuleKey, preserving each collection's relative order. Ordinal namespace sorting polls cancellation during long prefix comparisons. Empty patterns/events and absent versus present empty metadata remain distinct. No validation status extensions, synthesized IDs, source node handles, schema access or evaluator rerun are introduced.

Caller fields pass through a borrowed Unicode scalar/escaping loop using the existing XML 1.0 rules (attribute whitespace references, carriage-return references, `]]>` avoidance, astral scalar validation). String output owns StringBuilder chunks and the final string; streaming output reuses the verified bounded WriterOutput UTF-8 encoder/pool and never materializes the complete report. Operation-local dictionary/list/sorted namespace nodes allocate. No speed, zero allocation or cross-architecture claim is made. Stream writes leave the caller stream open and propagate its original exceptions; disposal returns scratch without flushing caller output.

## Actual checks

`python3 docs/reports/M8-sch-report/checks.py` passed at the source revision:

- 42 reporting cases, zero failures/errors/skips/not-run. Independent exact detached golden; key-only joins including repeated RuleKey/context/location and cross-event findings; zero-findings/anonymous patterns; null versus empty metadata; ordinal prefix sorting in en-US/tr-TR/ja-JP; reserved characters and whitespace in all fields; multilingual/astral boundary parity; runtime-created malformed UTF-16 and illegal XML scalars in every emitted field; valid/invalid/incomplete status profile; live facade result detached after DOM mutation; repeatability/concurrency; argument/cancellation/write exception identity; nonseekable stream ownership; cancellation on long text/reference writes and final write.
- 504 verified prerequisite/serialization cases, zero failures/errors/skips/not-run: 35 evaluator, 419 existing compiler/model/source/resource/accounting/M4/M5 checks, plus 50 M1 serialization cases. Counts overlap the evaluator's earlier dependency suites; they are actual executions here.
- Exact compiled fragments: reporting3, evaluator9, compiler154. Full public API1403 = existing1400 plus exactly reporting3.
- Normal standalone consumer and separately built genuine XML-only source assembly consumer passed: detached valid/invalid/incomplete output, M1 XML parsing, M4 XPath, string/UTF-8 parity and open caller streams. Source-linked assembly contains XML prerequisites and Schematron only, with no HTML/CSS source.
- Release SDK10.0.400/runtime10.0.11 builds: zero warnings/errors. Task validator179 and whitespace checks passed.

Early attempts are not passing evidence: test build initially lacked explicit xUnit test cancellation tokens; the first runtime attempt had 3 surrogate fixtures replaced by metadata encoding (23/26 passed). Fixtures were corrected to create malformed UTF-16 at runtime, then expanded to every emitted field. The first consumer build incorrectly placed namespaces in evaluation rather than compile options; corrected before both actual consumer runs. Final committed check passed all cases above.

The allocation-only driver was built in Release with zero warnings/errors after the PARK release. Planned thirteen consumed routes cover cold string/stream output, a positive owned allocation control, paired short/long multilingual/dense escaping/128-event string and UTF-8 output, invalid scalar string/stream, retained sixteen long report strings and process RSS. Thirteen-route counter-disabled direct control passed. Elevated preflight then ran the same prebuilt child with counters disabled, confirmed exit0/reaped direct child/absent owned process group/closed log, and verified418 exact source/runtime/binary/control/supervisor identities. Manifest SHA256 `c365cfe33ed4a766dd6018b997c0d77b44e3795290a21704dbf5b19aec8a8d56`; maximum30s including20s work+10s cleanup. Full runtime10.0.11 and host/fxr files are hashed. One root-granted allocation-only run has now passed after durable host cleanup RELEASE; exact totals, ownership exclusions and approximate retained/RSS counters are in [allocations.md](allocations.md). Source correctness and measured allocation evidence remain separate.

## Independent source review

Coordinator accepted the frozen source checkpoint at clean preparationc44e14e0 with no concrete blockers. Read-only hashes matched locally: serializer `b23f2cec32af3735e5198379a4e72dacf18295eb01bd9d57041e313ee2e1fdb6`, tests `9d581ffe23d8e8d5db231f83cd47e205ee0ae4a4e4a6471cfdb6dd9024a8371e`, golden `c9eeb5f392cb5ea4f0f5b5e6bcf741f811d22afae13c70bae9676210fcd3d298`, API `2708610f32073f3883b22448de4535d3859f463162f5d718c2b1e67347caaaab`. This is review evidence, not new test execution or a performance claim. Prepared inputs remain unchanged.

## Completion

All branch criteria pass. Root reported actual joint integrated checks at `e4bf43e059fd5aa23bfb1b7dc334ac561ee3e7fc` (546report +804facets, overlapping predecessors; API/consumers/planning179, zero warnings/errors). Coordinator owns this integration execution and the final verified decision. No additional source changes or measurement are required by this branch; root review of allocation interpretation and metadata integration remain. Raw artifacts are preserved for root.
