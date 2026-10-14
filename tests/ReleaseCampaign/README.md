# Local fuzz campaigns (AFL + SharpFuzz)

One AFL campaign per target. Nothing in this folder starts a fuzzer unless you run
`local_xml.py run`, `resume` or `qualify` yourself.

| Target | Driver | Exercises |
| --- | --- | --- |
| `xml` | `XmlDriver` | XML byte decoder/parser with internal DTD processing |
| `html5` | `TargetDriver` | `Html5Parser` (bytes, string, fragments), HTML serializer |
| `html-legacy` | `TargetDriver` | legacy `HtmlParser` (bytes, string, chunked stream), serializer |
| `stream` | `TargetDriver` | `XmlReader`, `XmlSaxParser` (sync/async), `XmlPushParser`, `XmlDocument.LoadAsync`, all with fuzzed chunk sizes |
| `xpath` | `TargetDriver` | `XPathExpression.Compile`/`Evaluate` |
| `xsd`, `rng`, `schematron` | `TargetDriver` | schema compilation and validation, with includes served from the input |
| `xinclude` | `TargetDriver` | `XIncludeProcessor`, `XmlCatalog`, `CatalogResolver` |
| `serialize` | `TargetDriver` | `XmlSerializer`, `XmlAsyncSerializer`, `XmlCanonicalizer` (all modes) |
| `writer` | `TargetDriver` | `XmlWriter` driven by an operation script decoded from the input |

Multi-part inputs (schema + instance + resource, document + include + catalog) are split at
NUL bytes. Resources are served from the input; nothing reads the disk or network. Only the
exceptions that the contract documents for malformed input are tolerated, so any other
exception or hang is a finding. A session covering one target does **not** certify the full
M9 robustness gate (see "Whole gate" below).

## Layout

| Path | Purpose |
| --- | --- |
| `prepare_xml.py` | Builds the disposable bundle (never starts a fuzzer). |
| `local_xml.py` | User-run front end: `qualify`, `run`, `resume`, `status`. |
| `XmlDriver/` | `xml` target (`XmlDocument.TryParse` with DTD parsing). |
| `TargetDriver/` | All other targets, selected by its first argument. Replays files when not run under AFL (use an uninstrumented build). |
| `CrashControl/` | Same driver plus a deliberate crash, to prove crashes are detected. |
| `Instrumenter/` | SharpFuzz instrumenter, applied only to copies of `Xemele.dll` under `artifacts/`. |
| `seeds/<target>/` | Initial corpus per target. |
| `CoverageCases/`, `CoverageControl/` | Coverage checks, unrelated to the campaign. |

The bundle is written to `artifacts/M9-robustness/manual-xml/` (gitignored).

## Requirements

- macOS with `make`, `clang`, `curl` and network access (to download AFL once).
- .NET SDK 10.0.400 and runtime 10.0.11 in
  `~/Library/Application Support/dotnetup/dotnet`, or set `XEMELE_DOTNET_ROOT`.

## Steps

Run from the repository root.

```sh
# 1. Prepare the bundle: build, instrument, download and build the pinned AFL, hash everything.
python3 tests/ReleaseCampaign/prepare_xml.py

# 2. Crash-positive control (at most 90 s). Required before any campaign.
python3 tests/ReleaseCampaign/local_xml.py qualify

# 3. Start a new campaign for one target (--hours greater than 0, at most 24; default target xml).
#    Stop early with Ctrl+C.
python3 tests/ReleaseCampaign/local_xml.py run --target html5 --hours 4

# Check statistics and recorded hours at any time (starts nothing).
python3 tests/ReleaseCampaign/local_xml.py status

# Continue the retained queue later.
python3 tests/ReleaseCampaign/local_xml.py resume --target html5 --hours 4

# Every target at once, N campaigns in parallel (default: CPUs - 2). Targets that already have
# a campaign are resumed automatically.
python3 tests/ReleaseCampaign/local_xml.py run --target all --hours 24 --jobs 6
```

## Rules enforced by the script

- Every bundle file and runtime file is checked against `manifest.json` before each command.
- `run` and `resume` refuse to start unless `qualification.json` says `qualify` passed for
  the exact current bundle.
- `run --target T` refuses when its campaign folder (`campaign` for `xml`, `campaign-T` for the
  others) already exists; use `resume` to keep findings. To start from scratch, move or delete
  that folder deliberately. `--target all` resumes existing campaigns by itself.
- `resume` refuses if the recorded AFL process is still alive.

## Results

Under `artifacts/M9-robustness/manual-xml/`:

- `campaign[-<target>]/queue`, `crashes`, `hangs`, `fuzzer_stats`
- `sessions/<timestamp>.log` and `.json`: AFL output and a record of each session (these feed `status`; do not delete them if the hours are evidence)
- `qualification.json` and `control-*/`: the crash-positive control

## Whole gate

`status` sums the wall-clock hours of the retained session records per target and compares
them with the 24 hours that [docs/testing.md](../../docs/testing.md) requires per major
parser/validator target. It also shows the crash and hang counts. That covers only the
duration part. The gate (task `M9-robustness`) also needs:

- every finding minimized into a committed regression case and replayed;
- the corpora, differential, coverage (at least 90% of core branches), cancellation and
  resource-budget evidence, which come from the other suites, not from this folder;
- the evidence report under `docs/reports/M9-robustness/` (revision, duration, seeds,
  findings, no unresolved release-blocking defect).

The list of targets is this folder's reading of "major parser/validator target"; the spec does
not enumerate them.

## After changing source

If `Xemele`, a driver or a seed changes, rerun `prepare_xml.py`, then `qualify`. Preparation
refreshes the generated parts of the bundle and keeps the AFL engine, the campaign folders and
the session records (it refuses while an AFL process of the bundle is running). The old
qualification does not carry over, but `resume` still continues the retained queues with the
rebuilt drivers.
