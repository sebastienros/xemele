# Conformance outcomes and fixture contract

Test infrastructure distinguishes Accepted, Rejected, RunnerFailure, and Excluded
outcomes. Results retain case ID, profile, expected classification, actual status,
diagnostics, and reason. RunnerFailure and Excluded require a nonempty reason
and are never counted as conformance success. A reference crash, missing cache,
or unavailable dependency is not parse rejection.

Exclusions identify the fixture, reason, tracking issue, and profile and require
review. Applicable unimplemented cases remain failures. Accepted/Rejected results
are compared with pinned independent expectations, never expectations generated
from Xemele output.

Manifests use `schemaVersion: 1`, `id`, `sourceUrl`, immutable `revision`,
lowercase 64-digit `sha256`, `license`, `notice`, `adapterVersion`, `profile`,
and a nonempty `fixtures` array. Each fixture has a repository-contained `path`,
SHA-256, and `classification: accepted|rejected`. Source hashes identify the
immutable source payload; fixture hashes identify exact provisioned bytes.
Optional metadata may hold case IDs and adapter-specific expected output.

Provisioning and parser execution are separate operations. Successful checksum
verification or cache provisioning does not establish standards conformance.
See the [discovery guide](../../tools/ApiBaseline/README.md) for validation rules
and [testing requirements](../testing.md) for behavioral coverage.
