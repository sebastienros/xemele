# Test-infrastructure outcome and fixture contract

Keep the frozen IConformanceCase unchanged: Id, Profile, Execute returning
ConformanceOutcome. Concrete representative declarations live in
Conformance.cs.fixture (namespace Xemele.Testing); the corpus/reference owner
implements infrastructure in its own paths. These declarations do not introduce
a dependency on the empty public runtime. The outcome snapshots a read-only
list of infrastructure diagnostics: Code, Message, optional Resource, one-based
Line/Column (zero unknown). Status is Accepted, Rejected, RunnerFailure, Excluded.
RunnerFailure and Excluded require a nonempty reason. Exclusion requires a reviewed
module exclusion entry with fixture id, reason, issue and profile, never a silent
successful skip. Reports preserve case id, profile, expected classification,
actual status, diagnostics and reason. Accepted/Rejected compare to the pinned
expectation; RunnerFailure/Excluded are separately counted and never conformance
success. No reference crash or missing cache can be reported as parse rejection.

Manifests use the exact established schema, not the original prose spelling:
`schemaVersion: 1`, `id`, `sourceUrl`, immutable `revision`, lowercase 64-digit
`sha256`, `license`, `notice`, `adapterVersion`, `profile`, nonempty `fixtures`.
Each fixture has repository-contained `path`, lowercase SHA-256 and
`classification: accepted|rejected`. Id/path uniqueness, checked bytes and source
provenance follow tools/ApiBaseline/README.md. Additional optional metadata can
hold `caseId` and adapter-specific expected output; no renamed required field,
third classification or changed checksum meaning is introduced. Manifest source
sha256 verifies the fetched immutable source payload; fixture sha256 verifies
exact provisioned fixture bytes. Reports/exclusions live in the module's owned
paths, not a global shared registry. Expected output comes from pinned standards/
independent reference adapters, never Xemele output. No actual corpus is provisioned
or executed by this foundation task.
