# Combined integration verification

Tested revision: `72969a5a40794b35975d8b46a07c8252b9da67cb`.
Worker handoff: `9774676260d1b1c6e020e8737efbb5dfd09a8fcd`.

Reused current verified syntax `check.py` because integration adds its three
prior identity-neighbor rows to the original affected 1110 inventory. This
passed 1146 rows (33 syntax +1113 affected), plus42 resolver cases, compiled
public1417 and all frozen APIs, normal and source-linked true XML-only
consumers. Five Release builds completed with zero warnings/errors.

Ran the same pinned .NET10.0.11 test executable separately with `-namespace
Xemele.Tests.M8XsdAttributeFixed` (45) and `-namespace Xemele.Tests.M8Datatypes`
(77), both with `-method- *WarmBorrowed* -parallelMode none -noLogo -noColor
-result-xml <owned transient output>`. Parsed each actual XML result and
required exact row counts, unique names, pinned runtime, all Pass and zero
errors/failures/skips/not-run. Rechecked saved predecessor inventories at1146
and42. Total1310 passes. No allocation-only assertion was run by these checks.
Planning188 and diff checks passed.

Allocation evidence was already measured on frozen139b9cf98 with identical
Derivation.cs runtime bytes. No new measurement or performance claim. Original
baseline and the two test-witness correction failures remain in task history.
Raw generated results/logs are transient and discarded after verification;
this summary and identities survive cleanup.
