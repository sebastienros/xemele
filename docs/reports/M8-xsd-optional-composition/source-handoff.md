# Optional XSD composition — runtime SOURCE handoff

Implementation `5160cfadb64de8555d58e392df5c949fd465a4a6` on
`codex/m8-xsd-optional-composition`, isolated checkout
`/Users/sebastienros/.codex/worktrees/4607/xemele`.

Only Compiler.cs changes at runtime: verified TryLoadOptional for include/import
and redefine with no non-annotation children, after existing syntax validation.
The existing charged Children traversal classifies redefine; substantive
simpleType/complexType/group/attributeGroup stays mandatory Load. Explicit absence
continues before loader/compiler document/source/cache/include registration;
source.Imports namespace permission remains. No new model/shared/public API,
catch-and-recover, fake schema, oracle edit, or changed successful-resource cap.

`runtime-source-identities.json` freezes exact compiler/shared loader/contract and
all owned test/fixture hashes. Original fixtures copied bytewise from pinned
w3c/xsdtests revision `7bc3365c652a322f3d762021b3879eb92dae7e30` preserve copyright,
metadata snippets/IDs/expected/current/prior fields. Metadata source file hashes
are retained; no network requests were made. Exact lexical missing references are
listed in the manifest; any other original reference must match the exact local
C-schema file. Streams preserve original bytes and caller ownership.

Baseline: unchanged compiler `6d7f6a67...` at claim `6df0921b...`, with the initial
owned observation scaffold before runtime changes. Five rows passed as observation
checks: four originals all refused SCHEMA.ResourceDenied, instances unassessed.
These are not validity passes. `original-baseline.json` preserves commands/output.

Branch originals now compile; a002, schD7 and schG8 instances complete VALID match
authored expectations. a004 original instance INVALID remains frozen: observed
complete VALID is an explicit original disagreement and is never credited as an
original validity pass. A separate named test proves separately derived VALID
from the accepted W3C4126 disposition and pinned group documentation, with the
same unchanged schema/instance bytes. `focused-summary.json` preserves both.

68 owned observation/control rows cover annotation-only/substantive redefine,
missing then available SAME URI include/import, imported namespace permissions,
ordinary required type/group/attribute-group references, default/explicit/legacy
null/no-resolver denial, syntax before callbacks, malformed/wrong-kind/wrong-NS
resolved schemas, legal import cycles/refused include/redefine cycles, substantive
resolved redefine, aggregate exact/one-short external bytes/scalars/resources/
inclusion depth/work/components, evaluation and max-work ledgers including
principal parse, cancellation before callback/after absence/acquisition/read,
caller IO/parse exceptions, primary versus cleanup exceptions, successful disposal
failure, redirected final URI/xml:base/referrer, byte provenance and source mutation.

Correctness: 1777 affected rows (68 owned and 87 particle included once), 42 resolver,
77 datatype, all disjoint = 1896 rows. Prior resource/compiler/content/derivation/
evaluator/facade/type/particle/recent repairs passed. Public1430, eight frozen lanes,
additive46 and emitted compiler/model/evaluator baselines passed. Normal and true
XML-only facade/relations consumers passed. Planning196 and git diff --check passed.
Sources remained unchanged while check.py built and ran; implementation commit
created during the run pins the exact same compiler/tests/fixtures. This is source
identity evidence, not a repeated check. Actual command exits are recorded in
correctness-summary.json; raw log/XML remain ignored artifacts for root inspection.

Earlier failures are retained: initial scaffold CS0649 build failed; init-only fault
properties fixed it. First focused run66/67 failed because depth positive fixture
hit resource-capacity guard first; independent depth control now uses two resource
slots while retaining exact depth2 versus one-short1. Runtime did not change for
these fixes. No exclusions, weakened original expectations or acceptance changes.

A1/A2 passed; A3 pending actual bounded allocation. Classification's owned temporary
Children list and optional request/loading/graph ownership require measurement;
no zero-allocation primitive, timing or global-idle claim is made. No allocation
proposal/source/build/control/preparation/counters has begun. Root SOURCE review
and subsequent staged authorization are required. Task remains in_progress with
integration_revision null. No outside-ownership requests; all finite correctness
commands returned/reaped, controlling exec exited0; zero owned background jobs.
