# M8 contract verification

The check entry point is `tests/Contracts/Fixtures/M8/check.py`. Supply the pinned
SDK 10.0.400 host and runtime 10.0.11:

```sh
python3 tests/Contracts/Fixtures/M8/check.py --dotnet '/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet'
python3 tools/planning/tasks.py validate
git diff --check
```

`--ownership-base <full revision>` selects the known preintegration revision for
the same owned-path assertion; default is the claim
`e0357391da5232e67b4e1de09d6aac52d732275f`. A coordinator separately inspects the
complete worker diff against that claim. `--emit-baselines` is an explicit authoring
operation, never the normal comparison check.

Eight independent nonpackaged declaration consumers use a source-built XML-only
prerequisite assembly. Its explicit Compile list excludes HTML/CSS and later
schema implementations. Each actual runtime probe exercises M2 request fields,
existing resource kinds/exactly-once disposal, M4 XPath, and M5 DTD validation,
owned result snapshots and incomplete/fatal invariants. M8 methods are compiled
as usage consumers but never invoked; no runtime schema implementation or stub
ships. The dependency assembly is inspected for absence of HTML/CSS types.

The owned extractor includes public and internal declarations, uses dependency
metadata to resolve value types/nullability, compares exact canonical member rows,
and rejects separately mutated member presence, nullable, init-only and internal
work-width metadata. Inventory checks enforce every built-in datatype/facet,
identity family and 2016 properties as well as per-entry ownership/disposition.
The 410 inventory entries are required scope/exclusions, not executed corpus cases.

Allocation/performance measurement is inapplicable: this task modifies no runtime
hot path. Corpus pinning, actual validator coverage and cumulative gates remain
with their implementation owners. The required ordered Schema=5 resolver change
is an ownership request, not an implemented runtime enum member in these probes.

## Exploratory runs

The first authoring run compiled eight consumers, passed prerequisite probes and
24 mutation controls, then failed because inventory.json was not written yet.
The next authoring run passed with the inventory. Subsequent declaration refinements
add datatype composite/measure hooks and prepared identity programs; only checks
at the final recorded implementation revision establish the completed task.
