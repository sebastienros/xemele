# Foundation contract — M0

Decision owner: `M0-contracts`. Runtime implementation begins in M1.

## Build and repository

`M0-build` creates `Xemele.slnx`, SDK pin for .NET 10, central package versions, deterministic common props, nullable/analyzers, `.editorconfig`, `.gitignore`, and the empty `src/Xemele/Xemele.csproj`. No native runtime dependencies. Build/test/benchmark tools use pinned compatible packages. SDK default globs include task-owned source files; no feature worker edits project files.

Bootstrap also creates empty test projects (`Xemele.Tests`, `Xemele.Conformance.Tests`, `Xemele.Differential.Tests`, `Xemele.TestUtilities`), benchmark/fuzz/tool project shells, and their references. Executable shells can contain a working help-only entry point; no fake library implementation. Source files belong to feature tasks. Use explicit `Program.cs` only in bootstrap and route later commands through a registry convention approved here.

Proposed CLI convention: an internal partial `CommandRegistry` declares optional `partial void` registration hooks for XML, HTML and validation commands. Bootstrap invokes these hooks; absent implementations are erased by the compiler. Each later command task implements its own hook in its owned folder. This is AOT-compatible, needs no runtime reflection, and lets the early help-only CLI compile without later command types. These internal hooks are not public placeholder functionality.

Parallel lanes after bootstrap: CI, corpus manager, reference runners, tree experiments, and input experiments. Each writes its own outputs. `M0-contracts` joins experiments, reviews the API specs, and records decisions; `M0-gate` joins all lanes.

## Corpus manifests

Each module owns `tests/corpora/<module>/manifest.json`. Required fields: `id`, source URL, immutable version/commit, SHA-256, license/notice, adapter version, specification profile, and fixture entries with expected classification. Missing/unverified downloads fail explicitly. Cache bytes under `artifacts/corpora/`. No global manifest requiring all feature branches to append rows.

`CorpusManager` discovers module manifests, verifies checksums, and provisions an offline test cache. `IConformanceCase` is test infrastructure, not runtime API:

```csharp
public interface IConformanceCase
{
    string Id { get; }
    string Profile { get; }
    ConformanceOutcome Execute();
}
```

The contract task defines concrete outcome/schema types and per-case failure/exclusion reporting. Tests must not bootstrap their expectations using Xemele output.

## Reference protocol

Use versioned JSON Lines over stdin/stdout. Each request has `protocolVersion`, `caseId`, `operation`, base64 input bytes, encoding hint, profile/options, and offline resource map. Each response has matching IDs, engine/version/build options, outcome, categorized diagnostics, and operation-specific normalized tree/events/query value or output bytes. Runner failures are distinct from parse rejection. Use stderr for logs. Native runner process timing is never parser timing.

Operations are added by module task-owned adapters. M0 requires only one known XML fixture per pinned reference; later module tests extend protocol payloads through versioned optional fields. Record exact libxml2/xmloxide commits from the roadmap and pin build toolchains. BCL reference reports the actual runtime.

## Decisions that unblock M1

- Node public representation and handle equality/lifetime rules, measured against object nodes.
- Input representation and resumable scanner/sink contract; decoding and text event lifetimes.
- Exact common diagnostics, limits, options, cancellation, source-location units, resolver result ownership.
- API file ownership, baseline-fragment format, CLI discovery convention, and test naming.
- Project license decision and provenance for imported code/data; if license choice is unavailable, record it as a distribution blocker without stopping independent experiments.

API sketches must be expanded into task-owned declaration/usage fixtures under `tests/Contracts/Fixtures/<module>/`, compiled in a separate contract-check project that is not packaged. These fixtures can use test implementations; runtime packages must not ship stubs. Contract approval records the fixture revision and decisions; dependent workers consume that exact version.
