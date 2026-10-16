# Foundation contract

## Build and repository

The runtime targets .NET 10 with no native runtime dependencies. `global.json`
selects the SDK; central package versions and deterministic common build properties
apply to the solution. Nullable analysis and analyzers are enabled. Build, test,
and benchmark tooling uses pinned compatible packages.

The CLI uses an internal partial `CommandRegistry` with optional `partial void`
registration hooks for XML, HTML, and validation commands. This is AOT-compatible
and requires neither reflection-based discovery nor public placeholder methods.

Runtime APIs must have implementations; do not ship throwing declaration stubs.
Compile-only declaration and usage fixtures under `tests/Contracts/` are
nonpackaged models, separate from the actual public [API baselines](../api).
The [compiled API comparison](api-compatibility.md) checks actual assembly
signatures, not just the presence of inventory files.

## Corpus manifests

Each module has a `tests/corpora/<module>/manifest.json`. Required fields are
`schemaVersion: 1`, `id`, `sourceUrl`, immutable `revision`, lowercase SHA-256
`sha256`, `license`, `notice`, `adapterVersion`, `profile`, and `fixtures`.
Each fixture has a repository-contained `path`, SHA-256, and
`classification: accepted|rejected`.

`CorpusManager` discovers manifests, verifies checksums, and provisions an
offline cache under ignored `artifacts/corpora/`. Missing or unverified inputs
fail explicitly. Tests must not derive expected results from Xemele output.
The [conformance contract](conformance.md) defines outcomes and exclusions;
the [discovery guide](../../tools/ApiBaseline/README.md) defines exact validation.

## Reference protocol

Use versioned JSON Lines over stdin/stdout. Requests identify `protocolVersion`,
`caseId`, operation, base64 input bytes, encoding hint, profile/options, and an
offline resource map. Responses have matching IDs, engine/version/build options,
outcome, categorized diagnostics, and normalized operation-specific results.
Runner failures are distinct from parse rejection. Logs go to stderr.

Versioned optional fields extend operation payloads. Pin reference revisions and
toolchains; BCL references identify their actual runtime. Process startup and
IPC are not parser timing.

## Runtime invariants

The [runtime foundation](foundation-runtime.md) specifies arena/value-handle
identity, bounded decoding/scanning, common limits, lifetimes, and errors.
MIT is the project license; third-party code/data retains its own attribution
and notices. Fixture compilation does not prove runtime conformance or performance.
