# Branch correctness

Tested revision: `0e83c98` (provider source identical to `d4d9d7c`).
Command: `python3 docs/reports/M8-datatypes-temporal/checks.py`.

Actual Arm64 .NET 10.0.11 Release run: 50 passing cases (20 owned temporal facts,
18 prior core cases,12 prior model cases), zero errors/failures/skips/not-run.
The script asserts those exact positive counts from XML and checks the runtime.
No allocation tests were selected. Cache-only restores/builds had zero warnings
and errors. The API checker matched 1393 compiled public members. Both normal and
XML-only provider/core consumers executed successfully. Task validation passed
176 records and `git diff --check` passed.

Initial `d4d9d7c` test build failed xUnit1051 because the operation helper accepted
an optional token. `0e83c98` routes ordinary checks to the runner cancellation
token and uses a separate helper for intentional cancellation. No runtime source
changed, and the corrected whole focused script passed. Measurement controls are
separate; no allocation/retention/timing result is claimed here.
