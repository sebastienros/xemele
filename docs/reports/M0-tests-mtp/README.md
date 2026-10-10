# MTP infrastructure migration

The central package pin is `xunit.v3.mtp-v2` 4.0.1. `global.json` retains SDK
10.0.400 with roll-forward disabled and selects `Microsoft.Testing.Platform`.
Each test shell is now an executable; the package supplies its generated entry
point, MTP registration, and transitive platform dependencies.

`python3 -B docs/reports/M0-tests-mtp/verify.py` runs clean bootstrap checks,
workflow-equivalent commands, existing discovery tests, actual transitive package
inventory, package metadata inspection, and planning validation. Run in an
isolated checkout with the pinned SDK available; this clears project `bin`/`obj`
outputs. Full output is recorded under `artifacts/M0-tests-mtp/`.

CI and bootstrap share `check_runner.py`: discover each project's smoke test,
execute the solution with native MTP `--solution` syntax and xUnit-supplied
`--report-xunit-trx`/`--report-xunit-xml`, check each smoke test's reported success,
then run each smoke class with `XEMELE_MTP_SMOKE_FAIL=1`. Controlled assertion
failures must produce exit 2 and the expected failure message; unexpected runner
errors or successful controlled runs fail verification. Per-run reports and
transcripts are saved under `artifacts/test-results/mtp-*/` and uploaded by CI.

All smoke tests carry `Category=Infrastructure`. The script counts these
separately from product tests: runner smoke success does not establish product
conformance. Discovery reports, XML and TRX retain assembly identity. Ordinary
solution execution remains unfiltered so later product tests run normally.

Historical passing task evidence and committed transcripts are preserved. The
updated bootstrap writes its new transcript/package under `M0-tests-mtp`.
No runtime hot path changes are made; allocation/benchmark evidence does not
apply. Local workflow-equivalent execution does not attest hosted CI on all OSes.

Primary references:

- [xUnit v3 project setup](https://xunit.net/docs/getting-started/v3/cmdline)
- [MTP native dotnet test options](https://learn.microsoft.com/dotnet/core/tools/dotnet-test-mtp)
- [Requested package metadata](https://www.nuget.org/packages/xunit.v3.mtp-v2/4.0.1)
