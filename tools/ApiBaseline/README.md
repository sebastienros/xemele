# API and corpus discovery, version 1

Normal CI uses `dotnet run --project tools/Build -c Release --no-build -- verify`.
The Python tooling below remains available for specialized maintenance checks.
Run `python -B tools/ApiBaseline/discover.py --root <repository>`. The default
repository is derived from the script location. Success writes one deterministic
JSON summary to stdout; invalid input writes `discovery error: ...` to stderr and
returns 1. No central index is created or tracked. Discovery does not fetch data.

## API fragments

Discover `docs/api/<module>.txt`, sorted by filename. UTF-8 text starts with the
literal `xemele-api-v1`. Every subsequent line is a JSON object with exactly
`declaration`, `id`, and `member` string fields, in alphabetical field order,
without JSON whitespace. Use Python's `json.dumps(entry, ensure_ascii=False,
sort_keys=True, separators=(',', ':'))` to produce canonical lines. All fields
are nonempty single-line strings. Entries are sorted lexicographically by `id`.
Header-only join fragments are allowed alongside a nonempty validated union.
An aggregate with no declarations fails, including with the bootstrap flag.

`id` is a stable entry identifier. `member` is the canonical fully qualified
member identity shared by every fragment producer: use XML documentation member
IDs (`T:`, `M:`, `P:`, `F:`, `E:`), including parameter types for overloads. Return
types, accessibility, nullable annotations, constraints and default values belong
in the C# `declaration`, not the member identity. Producers must use the same
member identity for revisions of a member. Duplicate IDs or member identities
fail, including matching declarations and conflicting declarations under distinct
IDs. Unknown headers/fields, duplicate JSON fields and noncanonical lines fail.
The discovery tool treats declarations as contract text; it does not parse C#.

Missing fragments fail by default. The `--bootstrap-empty-api` option permits zero fragments only if
`src/Xemele/Xemele.csproj` exists and the runtime has no `.cs` sources outside
`bin`/`obj`. As soon as runtime sources exist, actual baselines are mandatory.
This flag does not exempt invalid or empty aggregate fragments. Current CI
requires the complete runtime baseline without this option.

Discovery validates declaration inventory, not compiled API compatibility.
The compiled checker extracts and normalizes declarations and compares the
compiled assembly's public member IDs and normalized declarations against these
fragments (missing, added or changed members must fail). Consume fragments
in-memory using these identities; do not generate a tracked central index.
Contract usage fixtures additionally compile intended call shapes.

## Corpus manifests

Discover `tests/corpora/<module>/manifest.json`, sorted by path. Required schema:

- `schemaVersion`: integer 1.
- `id`: nonempty, globally unique corpus identifier.
- `sourceUrl`: HTTP(S) provenance URL; no download is performed.
- `revision`: immutable upstream version/commit, never main/master/latest/HEAD.
- `sha256`: lowercase 64-digit checksum of the immutable source payload.
- `license`, `notice`: nonempty license identifier/text and attribution notice.
- `adapterVersion`, `profile`: nonempty adapter and specification profile.
- `fixtures`: nonempty array of objects with `path`, `sha256`, `classification`.

Fixture paths are relative to the manifest directory, may reference an offline
cache within the repository, must stay within the repository after symlink
resolution and must exist as regular files. Each path is globally unique.
Classification is `accepted` or `rejected`. Every fixture checksum is verified
against physical bytes; the source payload checksum is format-validated here.
CorpusManager owns source download/checksum verification and provenance review.
Additional manifest/fixture fields may describe adapters and expected outputs.
No manifests means no provisioned corpora yet; this is reported explicitly as
zero rather than a conformance success. Discovery does not execute fixtures.

## Checks

`python -B -m unittest discover -s tests/Xemele.TestUtilities/Build -p 'test_*.py' -v`
uses temporary synthetic fragments and fixtures only. None is shipped as a
runtime API baseline or real corpus. The weekly/manual test-data workflow runs
these legacy maintenance checks. `.github/workflows/build.yml` uses managed
validation, restore, Release build/test and runtime pack on Windows,
Linux and macOS. Hosted executions require repository CI; local validation
records its actual host and does not imply the other platforms ran.

## Compiled comparison

Build the approved `tools/ApiBaseline/Checker/ApiCheck.csproj` and
`src/Xemele/Xemele.csproj` in Release, then run
`python -B tools/ApiBaseline/compare.py --report artifacts/api/runtime-api.json`.
Comparison validates discovery first, composes member/declaration pairs in memory,
and sends one temporary canonical fragment to the unchanged approved extractor.
Original inventory IDs and source/line provenance remain in the JSON report; the
comparison representation uses id=member. No API fragment is rewritten. Matching
members with distinct IDs still fail as duplicates; conflicting declarations fail.
Missing, added or changed compiled rows return 1, including a nonempty actual
assembly paired with a bootstrap-empty inventory. Missing fragments require the
explicit bootstrap flag and existing source-empty runtime project.

`--root`, `--assembly` and `--checker` support isolated checks; defaults point to
this repository's runtime/checker Release outputs. `--report` writes full provenance
and prints a compact result; without it stdout is the complete JSON report.
The checker scope is docs/specs/api-compatibility.md, including required
future signature-family expansion. The CI verifier and focused reproduction live
in tools/ApiBaseline/; contract fixtures are not shipped runtime baselines.

## Clean-checkout CI prerequisites

The SDK selected by `global.json` is the only ordinary build/test prerequisite.
Test-project builds run the managed `tools/Build` corpus provisioner automatically.
It bounds and verifies the immutable archive download, extracts exactly 3,386
inventoried resources into ignored manifest paths, and checks every fixture byte.
The archive cache stays under `artifacts/test-data/`; subsequent builds can run
offline after NuGet restore. Existing corrupt bytes fail explicitly.

Test projects declare runtime 10.0.11 with roll-forward disabled; this runtime is
supplied by the pinned SDK. No runtime installer or generated-config rewrite is
part of the normal build. The canonicalization reference is a pinned test-only
NuGet dependency, restored through NuGet's normal cache resolution.

`prepare_corpus.py`, `prepare_references.py`, and the Python API/CLI audit scripts
remain maintenance tools. The weekly/manual test-data workflow builds Jing and
libxml2 at the frozen RNG revisions, checks runner source hashes, and records
actual platform binary/class identities. Its snapshot generator captures only
independent-engine output and binds it to all input hashes. Normal managed tests
verify these snapshots and the existing oracle/limitation accounting, without
starting native or Java processes. Live mode is explicit through
`XEMELE_LIVE_REFERENCES=1`. See `docs/testing.md` for the full workflow.

Frozen runner sources are exempt from checkout line-ending conversion so their
hashes remain identical on Windows. External reference build outputs stay ignored
under `artifacts/M9-ci-maintenance`; compact reviewed result baselines are tracked
beside the RNG inventories. The maintenance workflow proposes draft PRs rather
than accepting updates silently or changing upstream pins automatically.

Allocation probes require full matching compiled Git revisions, not matching
package-version prefixes. Runtime and test assemblies can have different package
versions while their complete versions and binary hashes remain recorded and
validated in parent/child provenance.
