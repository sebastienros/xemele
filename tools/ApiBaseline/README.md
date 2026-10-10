# Task fragment discovery, version 1

Run `python -B tools/ApiBaseline/discover.py --root <repository>`. The default
repository is derived from the script location. Success writes one deterministic
JSON summary to stdout; invalid input writes `discovery error: ...` to stderr and
returns 1. No central index is created or tracked. Discovery does not fetch data.

## API fragments

Discover `docs/api/<task-id>.txt`, sorted by filename. UTF-8 text starts with the
literal `xemele-api-v1`. Every subsequent line is a JSON object with exactly
`declaration`, `id`, and `member` string fields, in alphabetical field order,
without JSON whitespace. Use Python's `json.dumps(entry, ensure_ascii=False,
sort_keys=True, separators=(',', ':'))` to produce canonical lines. All fields
are nonempty single-line strings. Entries are sorted lexicographically by `id`.
Each fragment must contain at least one real declaration.

`id` is a stable entry identifier. `member` is the canonical fully qualified
member identity shared by every fragment producer: use XML documentation member
IDs (`T:`, `M:`, `P:`, `F:`, `E:`), including parameter types for overloads. Return
types, accessibility, nullable annotations, constraints and default values belong
in the C# `declaration`, not the member identity. Producers must use the same
member identity for revisions of a member. Duplicate IDs or member identities
fail, including matching declarations and conflicting declarations under distinct
IDs. Unknown headers/fields, duplicate JSON fields and noncanonical lines fail.
The discovery tool treats declarations as contract text; it does not parse C#.

No baseline is shipped for the empty M0 runtime. Missing fragments fail by
default. CI uses `--bootstrap-empty-api`, which permits zero fragments only if
`src/Xemele/Xemele.csproj` exists and the runtime has no `.cs` sources outside
`bin`/`obj`. As soon as runtime sources exist, actual baselines are mandatory.
This flag does not exempt invalid or empty existing fragments.

Discovery validates declaration inventory, not compiled API compatibility.
M0-contracts must define declaration extraction/normalization and compare the
compiled assembly's public member IDs and normalized declarations against these
fragments (missing, added or changed members must fail). Consume fragments
in-memory using these identities; do not generate a tracked central index.
Contract usage fixtures additionally compile intended call shapes. No assembly
comparison has been run or claimed by M0-ci.

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
planning validation, restore, Release build/test and runtime pack on Windows,
Linux and macOS. Hosted executions require repository CI; local validation
records its actual host and does not imply the other platforms ran.
