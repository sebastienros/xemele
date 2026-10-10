# M1 contracts evidence

The task freezes declarations and calling boundaries, not shipped runtime XML.
Read `docs/decisions/M1/strict-subset.md` and the four owned specifications.
Exact sources and reviewed model public metadata are under
`tests/Contracts/Fixtures/M1/`. Projects link .cs.fixture sources explicitly and
keep generated bin/obj outside tests/Contracts so solution globs remain clean.

Pinned SDK: global.json 10.0.400. Run from repository root with that SDK:

```sh
DOTNET_CLI_USE_MSBUILD_SERVER=0 python3 docs/reports/M1-contracts/verify.py
```

The script compares committed model baselines; normal validation never rewrites
them. `--emit-reviewed-model-baselines` is an intentional development action for
this contract owner's reviewed new signatures, not CI. It was used once to
create this task's model fragments. No docs/api fragment advertises runtime APIs.
All final commands/outcomes/revisions are recorded in the task JSON and checks.txt;
full raw output is ignored `artifacts/M1-contracts/checks.txt`.

Checks:

- Common/Tree/Input/Syntax/Serializer/Dom compile independently with the exact
  permitted source sets and representative usage. Public snapshots compare using
  the unchanged M0 approved compiler normalizer; all 81 frozen common members
  exactly match the M0 M1 fragment. Input and syntax add internal contracts only.
- Protocol executable verifies synthetic fixed-event callback ordering, pauses
  at every callback, accepted-input accounting, one-time EOF, callback exception
  identity, terminal faults, reentrancy/cancellation, ref-like views, concrete
  struct enumerators and option defaults. Its manually supplied mapping example
  distinguishes 7 UTF-8 bytes, 5 UTF-16 units, 4 raw scalars, normalized CRLF,
  duplicate surrogate offsets and EOF. It performs no actual XML decoding.
- Ten expected compiler failures cover tree/serializer parser isolation, input
  tree isolation, absent later reader/recovery types, retained ref-struct/span
  fields, reference-type sink requirement, explicit byte-count member naming
  and Int64 offset width.
- Three actually compiled public mutations must fail the unchanged normalizer:
  removal of handle readonly, enumerator struct-to-class change, optional
  parameter default true-to-false. Check the particular changed row as well as
  compare failure; no generic fail-only probe hides a different cause.
- Six independent serialization construction/output vectors check UTF-8/
  UTF-16LE/BE byte encoding/BOM arithmetic. Runtime owners must execute the
  construction actions against their serializer. Vector arithmetic is not a
  serializer correctness result.
- Existing M0 foundation executable tests and M1/M2 goldens plus solution build/
  tests preserve earlier milestone behavior. Task validator and whitespace
  checks finish verification. No new benchmark timing is run.

Scope limits: the approved normalizer handles these public readonly/value/
nested-enumerator/enum/record/span/default signatures, and ignores internals.
Internal input/syntax shapes are frozen by exact compiled declaration sources
and negative source probes; they are not claimed as public assembly compatibility
or an approved internal ABI checker. Per-accessor custom compiler attributes,
layout/interop and unrestricted future ref-safety families remain outside M0's
approval. No new public such family is introduced and no shared checker repair
is required for these public declarations. Future production/API baseline owners
must expand probes before shipping a new uncovered family.

Allocation/lifetime: no runtime hot path was added or measured. Compiler-enforced
borrowed views and unboxed enumeration shapes permit, but do not establish,
zero-allocation operation. Setup, scratch, bounded tokens/scopes, owned names/
text, diagnostics and output allocations are explicitly assigned to implementing
lanes in the decision/specs. No benchmark-backed scan optimization is selected
by this declaration task. Actual runtime, full XML corpus, native reference
matrix and cross-architecture performance remain the owning milestones' work.

The first development check reached all M1 contract checks, then failed the M0
restore with NU1900 because sandbox DNS could not reach api.nuget.org for package
vulnerability data. This is recorded as a development failure, not a passing
regression run; final checks rerun with authorized network/IPC access.
