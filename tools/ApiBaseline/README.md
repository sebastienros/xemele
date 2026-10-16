# API and corpus discovery, version 1

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
runtime API baseline or real corpus. `.github/workflows/build.yml` runs discovery,
restore, Release build/test and runtime pack on Windows,
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

`prepare_corpus.py` reuses the bounded, hash-pinned XML archive provisioner and
copies its exact 3,386 resources into the ignored manifest paths. Discovery still
checks every fixture byte. `--archive` supports an existing pinned offline archive.

After building, `prepare_runtime.py` installs runtime 10.0.11 if needed and pins
generated net10.0 application runtimeconfigs to it with roll-forward disabled.
SDK selection stays governed by global.json.

`prepare_references.py` builds Jing and libxml2 at the frozen RNG source revisions,
checks runner source hashes, records actual platform binary/class hashes, and
restores System.Security.Cryptography.Xml 10.0.11 for independent canonical checks.
It requires Java/Javac, CMake and a C compiler. The original engine calls and
reference limitation/oracle assertions remain in use. CI exports the generated
identity file and Java executable through environment variables; historical local
defaults remain supported. Outputs stay ignored under artifacts/M9-ci-maintenance.
