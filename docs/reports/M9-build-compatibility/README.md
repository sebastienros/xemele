# Root build and hosted CI compatibility repair

The new M9 usage Program.cs was incorrectly included by parent Contracts.csproj's
SDK source glob, producing actualCS8805. Rename it to the established
Program.cs.fixture convention and include it explicitly in its own executable
project. Its C# contents and product API/runtime are unchanged. Both parent
Contracts and standalone usage must compile; the existing usage output must pass.

The earlier generic build workflow also triggers on push. Cap each job20minutes
and keep its complete check steps for normal branches/PRs. Exclude only the
reserved codex/m9-os-validation push from that generic workflow: the owned M9
release workflow supplies the explicit full unit/package/trim/AOT matrix there.
This prevents duplicate generic jobs on the requested bounded validation push.
The validation branch runs at most3jobs,20minutes each (60runner-minutes maximum),
with no main update, package publication or extra release requirement.

This is one smallest owning build repair for a demonstrated failure and the human's
requested hosted time bound. Historical contract evidence remains unchanged;
metadata source equivalence alone did not reveal the parent project glob defect.
Root library plus standalone compilation/execution and ownership validation are
the proving checks. No allocation/timing workload needs replay for this file layout
and workflow change.
