# Namespace local-name conformance correction

Runtime/spec/tests revision: f3fe9d2843cde1e7bcd52334374e7aba9707d04e.
Cumulative verification and allocation additions: b53dfce146fa8d5a9141985567111b8bf02e63f2.
Claim/audit base: 99944ee86788414e5750a1e6eecf8a38d76baf17; implementation base:
aac9fa0e656bb07a0d60d1b4561ba307061a27fb. macOS15.8.1 Arm64,
SDK10.0.400/.NET10.0.11; Release, build servers off, one MSBuild worker,
shared compilation off. No timing run performed.

[W3C Namespaces1.0 section3 and QName productions](https://www.w3.org/TR/xml-names/#ns-decl)
reserve the xmlns prefix, xmlns namespace URI, and namespace declaration attribute
spellings. They do not ban an element's local name xmlns or the local part of a
qualified ordinary attribute. TreeValidation.Expanded and serializer traversal
now distinguish elements from ordinary attributes. The plain unprefixed,
no-namespace xmlns attribute is rejected by public mutation, checked construction
and output validation; declarations remain separate handles. Namespaced ordinary
attributes with empty preferred prefix remain valid logical models; Repair
supplies a prefix and Strict rejects absent attribute bindings. All existing xml
binding, name/scalar, uniqueness, root, quota/capacity/version checks remain.
Public API unchanged; trusted persistence helpers, syntax and DOM code unchanged.

NamespaceNameTests uses independent XDocument parsing to confirm legal and
forbidden cases before trusted replay; public, checked and trusted construction
serialize via both Strict/Repair and string/stream transports. Cases include
plain/default/prefixed/xml-bound elements, p:xmlns ordinary attributes, empty
preferred attribute prefix, declarations, invalid reserved bindings, precommit
atomicity, clone/import, and corrupted plain declaration versus legal qualified
attribute. The two earlier incorrect negatives were replaced with actual
reserved-prefix/URI cases; all other negatives remain.

## Checks at b53dfce

- Solution Release build: zero warnings/errors; cumulative tests244/244
  (prior223 plus21 namespace cases). Focused namespace21, Tree14, Serializer46,
  checked builder11, trusted persistence8, Input10, Syntax70.
- Foundation checks11; explicitly rebuilt FoundationM1/M2/ApiProbe compare
  139/173/8 members. Rebuilt Common/Tree/Input/Syntax/Serializer/Dom models
  match their approved goldens; protocol38 assertions.
- Actual runtime215 matches approved Serializer golden and mandatory canonical
  fragments. Source sample and fresh isolated local-feed NuGet sample pass
  independent construction and BCL round trips. Package restore uses a disposable
  isolated RestorePackagesPath.
- Committed/working/untracked owned-path audit and diff whitespace checks pass.
  Required planning validation separately passes106 tasks (planning-validation.txt).
- Warm context validation:0B for3000 expanded-name checks after20 warm batches.
  Existing borrowed intern/traversal test passes both focused and full solution
  without changing its warmup, measurement or zero threshold. Coordinator-reported
  peer failure4368B in an unmerged DOM branch is not reproduced here; it remains
  a peer observation, not proof of its cause. Integration should rerun the test.
- Stream.Null allocation-only probe:2104B/op for both1KiB and1MiB text, measured
  over10 operations after20 warm operations with fixed namespace shape. Owned
  tree/text, first-use JIT/static/pool setup are excluded explicitly. Serializer
  scope/encoder state allocates per operation; allocation does not grow with text.
  Existing approved Tree and Serializer timings remain applicable because the
  scan algorithms and workloads are unchanged; see ../M1-serialize/README.md and
  its preserved bdn/ results. No new speed claim or cross-host measurement.

verification-raw.txt retains commands and output; checks.txt is the compact
run record. initial-focused.txt records20 passing tests before the allocation
case was added. Initial sandbox MTP IPC failure is recorded separately and the
escalated run passed. Historical reports were preserved.

## Additional failure and follow-up

The cumulative launcher additionally invoked the tools/planning unit suite,
which failed3 of14 tests: gate omission, unordered path overlap, and bootstrap
independent-lane expectations. Those tests hardcode old M0 task states; this
claim starts from integrated M0 tasks. The failure is preserved in
planning-unit-failure.txt and verification-raw.txt. The launcher now runs the
required planning validator only; the unrelated unit failure is not claimed
passing or fixed. Coordinator should route a repair of tools/planning/test_tasks.py
baseline assumptions to its owner. No namespace implementation checks failed.

DOM/corpus owners must accept <xmlns/>, default/qualified elements whose local
name is xmlns, and ordinary p:xmlns attributes; keep declarations separate,
reject reserved xmlns prefix/URI, retain xml binding rules, and never add a
blanket syntax or DOM LocalPart rejection. Integrate and rerun applicable checks
before setting this task verified. No push, main merge or peer code consumed.
