# Validation result and operation model

Implementation `0472c1e2afe07dba4a9c3c103c6bd9424036de84`; prepared measurement
revision `c564f5a2a48af0170b0b5b41a6afe814ca8dae2a` changes only stable environment
identity capture. Public declarations exactly match the frozen Validation fixture:
getter-only owned ValidationResult snapshots and init-only immutable ValidationOptions.
No schema implementation or later milestone dependency is introduced.

The internal ValidationOperation snapshots/validates limits at entry, uses the minimum
of both nullable work caps, checks cancellation with the original operation token,
and refuses counter overflow. Exact work/diagnostic caps succeed. One-over work or
diagnostic retention yields incomplete `VALIDATION.Limit`; diagnostic overflow
replaces the final retained entry with Fatal so the total remains within the cap,
including cap one. Refuse supports `DTD.IncompleteInput` without invented locations.
Each operation owns counters/diagnostics; schemas may reuse immutable options.
Consumers must stop traversal when ChargeOperations/AddDiagnostic returns false.
Refusal is terminal; Complete creates an owned result snapshot and checks cancellation.

## Focused verification

Pinned SDK 10.0.400. Direct executable calls use the pinned dotnet plus
`--fx-version 10.0.11`; test runner and public sample proved actual .NET 10.0.11.

- Release test/sample/probe/API-checker builds: zero warnings/errors. Restore used
  `--source /Users/sebastienros/.nuget/packages --ignore-failed-sources -p:NuGetAudit=false -m:1`;
  builds used `--no-restore -m:1 -p:UseSharedCompilation=false --disable-build-servers`.
  Default/parallel restores stalled; exact own processes were stopped and serialized
  local restore succeeded. No stalled command is counted as a passing check.
- Test executable `-class Xemele.Tests.M5ValidationModel.ValidationModelTests -noColor`:
  18 passed, zero failed/skipped at implementation revision. Covers owned snapshots,
  outcome invariants, exact/minimum/unlimited caps, representational overflow,
  caps 1/2/128, cancellation boundaries/original token and 32 concurrent isolated runs.
  Earlier two nullable-long InlineData conversion failures were corrected to long
  literals before the implementation commit; the final 18-case run passed.
- `python3 docs/reports/M5-validation-model/check_api.py`: approved compiled extractor
  matched all 921 runtime members against the full API union, including this task's
  24 frozen ValidationOptions/ValidationResult entries. This check is independent of
  corpus provisioning. An initial extraction accidentally included the future
  DtdSchema.Validate row; corrected before implementation commit, no shipped stub.
- Public sample: `.NET 10.0.11; cap=42; valid=True; invalid=True/False; incomplete=False/False`.
- `python3 tools/planning/tasks.py validate`: all 152 records valid.
- Global API/corpus discovery was attempted but unavailable in this fresh worktree:
  missing `tests/corpora/xml/xmlconf/eduni/errata-2e/E14.dtd`. No corpus result is claimed;
  the compiled API comparison above passed without unrelated corpus discovery.

Broader milestone preservation and composed DTD validation belong to integration/gate.

## Allocation and benchmark summary

Exactly one prepared measurement run under coordinator grant
`m5-validation-model-c564f5a` (main lease `be87524`, durable release `ccfb734`).
Initial/final full identities matched: 152 source/generated/config/fixture/runner files,
43 actual binary files, 3,852 complete SDK10.0.400/runtime10.0.11/host closure files.
Prepared manifest SHA256 `a807e52426a509c75ee2f93895d795f16871c8573d1cee938b3531f206e560f1`.
Stable effective environment captured PATH/LANG/LC_ALL/LC_CTYPE/MallocNanoZone/
LD_LIBRARY_PATH plus DOTNET_/COMPlus_/CORECLR_/DYLD_ keys and plan overrides.
Plan fixes RollForward Disable, TieredCompilation=0, TieredPGO=0, ReadyToRun=0.
Probe enforces actual FrameworkDescription `.NET 10.0.11`; process architecture Arm64.
BDN host: Apple M4 Pro, 14 logical/physical cores, macOS Sequoia15.8.1
(Darwin24.6.0), RyuJIT armv8.0-a, BenchmarkDotNet0.15.8.

Controls/discovery/builds happened outside the lease. Plan bounded allocation to 20s,
four benchmark cases to 300s and total lifecycle to 400s. Runner did no builds or
generation. Allocation and benchmark exits 0; initial/final identity exits 0;
finally cleanup scoped to own process groups, no_child=true; runner exit 0.
Lease released before raw inspection. Worker and coordinator inspected actual raw
counters/logs before cleanup. Four benchmark reports succeeded; benchmark run 24.1s.

| Domain | Actual bytes / denominator | Interpretation |
|---|---:|---|
| Cold fixture initialization | 776 / 1 | Owned options/diagnostic fixtures, outside warm work |
| First operation setup after controls | 216 / 1 | Owned operation and limits snapshot |
| Warm charge distribution | all 32 batches = 0 B | 4,096 charges/batch; initialization separate |
| Steady charge distribution | all 32 batches = 0 B | 4,096 charges/batch |
| Inclusive steady primitive total | 0 / 131,072 | 0 B/charge; consumed warm+steady = 262,144 |
| Owned operation setup | 6,912 / 32 | 216 B/operation |
| Owned one-diagnostic result snapshots | 6,656 / 32 | 208 B/snapshot, includes transient construction |
| Diagnostic overflow + result | 18,944 / 32 | 592 B/refusal operation |
| Retained whole-heap observation | 215,048 → 518,824 B | Difference 303,776 B with 1,024 snapshots retained |

Retained result array allocated before the initial heap observation. GetTotalMemory(true)
observes the entire managed heap, including runtime roots and probe lifetime effects.
The observed difference is **not** an isolated per-result footprint or subtractable
wrapper overhead; no adjustment or narrower retained-size claim is made.
Retention is semantically bounded by diagnostic count and owned snapshots.

All benchmark classes have `[MemoryDiagnoser, ShortRunJob]`; no CLI job flags.
Prepared InProcessNoEmit runs actual host 10.0.11, one ShortRun, three warmup/three
measurement iterations/one launch. Latency is informational on this Arm64 host.

| Benchmark | Mean | StdDev | BDN displayed allocation | Actual inclusive GC bytes / operations |
|---|---:|---:|---:|---:|
| WarmWorkCharge | 1.121 ns | 0.0102 ns | dash | 0 / 536,870,912 |
| OwnedOperationSetup | 15.383 ns | 0.0253 ns | 216 B | 7,247,759,280 / 33,554,432 |
| OwnedResultSnapshot | 32.397 ns | 0.3119 ns | 208 B | 3,489,662,896 / 16,777,216 |
| DiagnosticOverflow | 72.740 ns | 0.1372 ns | 592 B | 4,966,057,904 / 8,388,608 |

The raw inclusive owned GC numerators retain their tiny extra bytes; no subtraction
was applied. Warm charging is 256 operations per invocation; denominator above is
normalized operations. There is no token scan/match primitive in this shared model,
so SearchValues/vocabulary alternatives are inapplicable. End-to-end validation
allocation/latency confirmation is required in the DTD validation/composition lane.
Cross-platform/hardware/AOT/trimming evidence remains a later release gate.
Only this summary and measurement machinery are committed; raw results were transient.

## Combined integration

Verified combined revision `7c495266447fff73c76aee8cfce08b68db7addcb`:36focused shared-model+standalone-metadata tests pass on actual.NET10.0.11,921compiled members match,153tasks validate,Release0warnings/errors,whitespace clean. Source-only independent reviewer approved. Successful unchanged shared-model allocation evidence was reused; no remeasurement. Commands:

```sh
'/Users/sebastienros/Library/Application Support/dotnetup/dotnet/dotnet' --fx-version 10.0.11 tests/Xemele.Tests/bin/Release/net10.0/Xemele.Tests.dll -class Xemele.Tests.M5ValidationModel.ValidationModelTests -class Xemele.Tests.ValidationMetadata.ValidationMetadataTests -noColor
python3 docs/reports/M5-validation-model/check_api.py
python3 tools/planning/tasks.py validate
git diff --check
```
