# Source-only handoff

Status: **UNRUN** for every build, restore, test, correctness probe, consumer, API
check and allocation measurement. The task stays `in_progress`, all criteria
pending. No runtime output is claimed by this handoff.

Runtime changes are confined to Compiler.cs:

- QName permission checks only `owner.Source.Imports` after chameleon namespace
  adoption; includes/redefines still collect components and retain declaration
  sources. One source-located work charge replaces recursive include traversal
  and its per-reference HashSet allocation. Redefine containment is untouched.
- Syntax validates appinfo/documentation `source` with ComponentCollapse and the
  existing TextBinaryUri validator using a reusable source-located operation.
  Canonical lexical strings stay borrowed; noncanonical whitespace owns a
  temporary collapsed string during the check. Payloads remain opaque. No URI
  retrieval, public surface, frozen lane or shared datatype change is introduced.

The owned fixture manifest binds complete exact original metadata, copyright,
principal and transitive schemas to SHA256. Import cases retain their earlier
compile-valid failures independently; annotation a001 retains its earlier
ResourceDenied observation independently. All original 1.0 INVALID expectations
are preserved; separate principal-import controls never modify original bytes.

Proposed correctness source has 91 theory/fact rows: exact hashes/metadata,
original rejections and principal-import controls, both directions of permission,
namespace-only/missing optional imports, nested and chameleon includes, mutual
imports, declaration order, QName symbol spaces, redefine sources, denial/cycles,
exact resources/work/components, source mutation, cancellation and disposal;
annotation wrappers each cover the two invalid originals, URI positive/negative
forms, whitespace, opaque payload, exact shared work, cancellation mid lexical
work, and redirected byte/character locations.

`check.py --released` is a bounded correctness/API/consumer/control plan, total
840s and per child 180s, offline cache restore, one build node, no build servers.
It runs owned and relevant integrated suites and preserves any existing failures;
no tests/oracles are rewritten. Probe `--control` uses identical semantic routes
with allocation/heap counters disabled. Source-only hold prohibits invoking any
of this until coordinator review and release. Actual counter launch belongs to
coordinator/root exclusively; it requires no isolation/PARK/ACK/lease workflow.

Risk: runtime, assertions and control source have not compiled or executed.
Counters remain pending. Whitespace normalization allocates a temporary string;
changed compiler routes return owned graphs/schemas/diagnostic snapshots and are
not represented as allocation-free. Cross-architecture timing remains a release
follow-up. No later milestone or quota increase is used.
