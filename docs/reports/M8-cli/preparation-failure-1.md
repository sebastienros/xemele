# First root preparation failure

Tested source: `54e536cd3f42d85ce713a3b94cdc65fbe4e0ecd4`.
Root's finite preparation passed Tool restore/build and regular probe restore.
The regular probe build (command3, exit1) failed with compiler CS0028 at
`ResourceCases.cs.fixture(14,27)`: the schema-text helper named `Main(string,string)`
was considered an invalid entry point under warnings-as-errors.

The owned source fix renames that helper and all call sites to `PrimarySchema`.
No follow-up executable work has been run by the worker. Four later preparation
commands and all seven checks remain unrun. The original failed root receipts
`prepare-c2dadc7d0fc3483382ca0235ce739b8a*.receipt.json` and `prepared.json` are
preserved unchanged locally; the owned ignore rules keep these durable execution
receipts out of the source commit. The failure is not reclassified as passing.

The recipe now retains up to8 distinct compiler error/exception lines, each
capped at512 characters, and binds the original failed command/arguments/exit
as `primaryFailure`. Missing later binaries appear as `postPhaseBindingFailure`
and cannot replace the failed workload as the primary outcome. Existing ownership,
deadlines, inspection, publication reconciliation and root certification remain.
