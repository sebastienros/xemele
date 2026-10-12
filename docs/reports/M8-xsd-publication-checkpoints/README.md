# XSD publication checkpoints

Implementation: `233aa3cf738ffffe4ae7b52e37e8ddb96eb9c8a7`.

`XmlSchemaCompiler.Compile(XmlDocument, SchemaCompileContext)` is an additive
internal composition join. It uses the supplied context's options, operation,
and resource loader, preserving the principal parser's aggregate scalar and
expansion ledgers across includes/imports. The frozen `IXsdCompiler` method
continues to create an independent loader for already supplied documents.
There is no public API change and no prerequisite or facade source modification.

Every model/attribute group retains an anonymous immutable type checkpoint under
its existing `#model-group:` / `#attribute-group:` carrier identity. Displaced
versions retain their generated redefine name, original source location, local
IDs, and binding/declaration namespace snapshots. Zero-self-reference redefine
still links its current checkpoint to its original checkpoint by restriction;
repeated expansion shares the same checkpoint identity. The generated names
contain `#` and cannot be authored as NCNames for public `xsi:type` selection.

The standalone tests prove typed integer/QName fixed-use equality witnesses and
joint ID uses survive in otherwise unused carriers. The subsequent facade owner
must inspect these retained bindings for fixed/declaration consistency and ID
count before publication; this worker does not implement the facade. Verified
content compilation actually rejects inconsistent unused and displaced model
groups, including both redefine forms. Legal neighboring cases are included.

## Correctness at the implementation revision

Run `python3 docs/reports/M8-xsd-publication-checkpoints/checks.py` using SDK
10.0.400 / runtime 10.0.11, Release, offline package cache, one build node,
build servers and shared compilation disabled. At the implementation revision:

- 34 owned, 894 affected, 42 safe resolver cases: 970 passed; zero failed,
  errors, skipped, or not run. No WarmBorrowed or allocation counter tests.
- Public API: 1410 matched; frozen compiler: 257; shared model: 316.
- Task-owned real consumer passed against normal and true XML-only assemblies.
- Planning: 184 task records valid; Git whitespace check passed.
- All builds reported zero warnings and errors.

Scalar caps cover ASCII and supplementary Unicode in principal string/stream
input plus includes, exact cap and one over. Expansion caps combine predefined
and supplementary numeric references across principal/include. Tests also cover
shared work exhaustion, cancellation before external work/after resolver
transfer, caller/read/resolver exception identity, exactly-once transferred
resource disposal, and caller streams left open.

Failure history: the initial build found two xUnit1051 test token omissions;
corrected before the passing run. The first check script ran the earlier
XML-only sample against the normal assembly and hit its explicit rejection of
HTML/CSS types. A task-owned consumer now validates both configurations without
changing the earlier sample or weakening its XML-only assertion.

Coordinator-reported bounded contract source-only review accepted the runtime
and tests at `233aa3cf` with no concrete blocker; reviewer ran no checks.
Allocation preparation at `d5f11742` passed: eight disabled-control routes,
444 frozen identities, and real supervisor TERM/KILL/reap/group/log-close
preflight. Root-granted allocation measurement passed after durable host RELEASE:
eight owned routes, positive control and 16 retained graphs; see
[allocation results](allocations.md). No timing or zero-allocation claim is made. Raw correctness output lives only in ignored
`artifacts/M8-xsd-publication-checkpoints/checks/` and temporary logs.
