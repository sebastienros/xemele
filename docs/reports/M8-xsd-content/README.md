# Internal XSD content join

The owned deliverable is `XsdContentCompiler : IXsdContentCompiler`,
`XsdContentMatcher : IXsdContentMatcher`, isolated `IXsdContentCursor` instances,
and opaque immutable programs. No public schema facade is supplied.

Every compile operation checks all compiled types, including unused anonymous
old/new model-group and attribute-group redefine carriers. It validates UPA,
element-declaration consistency, occurrence/compositor constraints, particle
restriction/extension correspondence, extension-first derivation history, and
effective attribute-use/wildcard restrictions. Legal substitution closure uses
separate element and ancestor-type blocks. Namespace constraints are finite
sets or complements, with the absent namespace represented by the empty string.

Programs retain sealed declaration particles and owned arrays exposed only as
read-only spans. Cursor configuration banks belong to one run and allocate
lazily under its supplied operation. Repetition counters grow with actual
iterations; bounds never expand into automaton states. Nullable repeated bodies
count consumed iterations and permit empty padding. Dominance merges only
otherwise-identical configurations with fewer nullable-body iterations.
Cold cursor/setup, bank growth, compiler scratch/results and diagnostics are
explicit owned allocation boundaries; warm allocation evidence is pending.
XSD intensional subset/representability is checked separately from mathematical
matching/overlap. Expanded counter arithmetic reserves construction first and
uses polled byte steps, including the seeded overflow path.

Structural operations, retained elements, traversed edges, state copies and
integer algebra are charged before their work/persistence, with cancellation
checks throughout. Particle traversal, restriction mappings and nullability
use explicit work stacks/graphs. Large compilation arithmetic uses charged
byte steps; steady-state counter comparisons inspect bit length before comparing
actual small counters with large bounds. UInt64 exhaustion retains an arbitrary
precision fallback rather than truncating counts.

Rules follow [W3C XSD Structures Second Edition](https://www.w3.org/TR/2004/REC-xmlschema-1-20041028/),
sections 3.4.6, 3.8.4/6, 3.9.4/6 and 3.10.6. Empty choice groups admit no sequence
unless their particle allows zero repetitions; empty sequence/all groups admit
empty content. Fixed repetitions have guarded boundaries rather than naive
unfolding or over-broad follow sets.

## Required later joins

The declaration graph retains every element/attribute ID, type, fixed/default
lexical constraint, namespace context and actual source location. Datatype/facet
finalization must check typed element fixed-value equality and lexical/value
validity, attribute binding/declaration constraint consistency, ID/NOTATION/ENTITY
rules and simple-type/facet definition validity, including unused declarations.
Content checks enforce structural fixed-constraint presence and the explicit
attribute-use string restriction. No definition-only datatype registry is
pretended to execute values. The later evaluator consumes strict/lax/skip policies,
validates text/attributes/xsi values, and dispatches identity hooks; this cursor
accepts only child element-name sequences.

## Development checks before review

Release runtime/test builds: zero warnings/errors. Latest owned selection:
84 cases passed on .NET 10.0.11. One case contains an additional 63-word exhaustive
independent nullable-repetition language oracle. Initial compilation fixes were
required for a ref-parameter local-function capture and an internal enum in a
public test signature. A later combined test run exposed a synthetic
RecurseAsIfGroup normalization loop and terminated with stack overflow; fixed
by preserving synthetic groups and implicit substitution members. Subsequent
81- and 84-case runs passed. XML-only consumer, frozen/public API and affected
prerequisite checks are being prepared; no unrun checks or measurements pass.

## Current review/harness state

Source correction `f098c72fd2f94bea18c46194de1795ea26439a4d` addresses the
bounded review batch. Actual 102 owned cases: 101 passed and one required
negative failed; see [review-corrections.md](review-corrections.md) for the exact
lost compiler operand and ordered owner request. Source closure review and the
verified compiler repair remain pending. Task-record writes were transferred
to the coordinator; source/tests/reports remain worker-owned.

The probe payload and BDN payload build successfully on SDK 10.0.400/runtime
10.0.11. Counter-disabled control consumed ten routes with 32 warm/64 samples;
five BDN semantic methods passed without timing/counters, and discovery asserted
exactly five MemoryDiagnoser ShortRun InProcessNoEmit cases. No allocation or
timing run occurred. `prepare.py` requires the mandatory negative and all 204
combined focused cases to pass before freezing; `measure.py` checks exact
identities/process permissions and absolute grant expiry, reaps every process
group, closes logs and emits lifecycle RELEASE before any result inspection.
The ten-route probe distinguishes name lookup/matching and three warmed cursor
shapes from lazy cursor construction, owned initialization/bank growth, all-type
compilation, actual XML/compiler/content composition and UPA diagnostics.
Retained programs and initialized cursors are observed separately. Dictionary
and borrowed-name methods have the same five-candidate membership/ID semantics;
cursor workloads have separate grammar semantics, so their baseline ratios are
not comparative optimizations. No new SearchValues/vocabulary optimization is
introduced. Real facade composition and x64 confirmation remain later-owner
release checks; this is provisional synchronous Arm64 evidence preparation.
