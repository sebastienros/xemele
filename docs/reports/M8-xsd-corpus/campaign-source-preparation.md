# M8 corpus campaign SOURCE preparation

Status: SOURCE proposal only. New builds, controls, adapter originals, manifest preparation, full campaign and counters are **UNRUN**. Task stays in_progress / A2 failed. Root alone reviews source, authorizes execution and verifies integration.

Joined verified main a2d3311af5ba00519858716b4cd8f3b2edbd38b2 at 7cec978bf8, preserved supervisor/report preparation at 062458fc5, then joined verified optional main45f83179e2789f7850e50b1ea3fa74eb33942c33 at b9ba0cbd8. Both conflicts were only the owned corpus JSON: earlier evidence/status/A2failed retained, authoritative A2 and required dependencies adopted. Resource55647de5, typee4df6edc, particleb2395ffa and optional6a3a64fa are verified ancestors. Root's checks are integration evidence, not worker reruns.

`supervision.py` reuses six function bodies unchanged from verified particle `measure.py`: members, kill_group, supervise, supervise_command, workload_succeeded, run_commands. Static AST equality checked. A workload succeeds only with reason=completed, exit0, direct child reaped, owned group absent and log closed. Timeout→TERM→exit0 retains TimeoutExpired and fails. Nonzero exit fails even with all rows written. Cleanup has finite waits and bounded process-group inspection.

`run.py` freezes one product+BCL campaign, 93sets/186children and41736rows per engine (41735 originals plus one Unicode supplement). Total deadline3600seconds; each set has at most300work+10cleanup, bounded also by the remaining campaign time. Exclusive UUID directories and exclusive receipt/log writes never replace prior attempts. Every child cleanup receipt is flushed, fsynced and its directory fsynced **before** raw row reads. Process/lifecycle/coverage/harness failure stops before the next child; missing rows are neither padded nor retried. Completed ordinary semantic nonpasses continue through subsequent sets and BCL. Exit0 means **harness completion only**, never conformance. Receipt counts are neutral observations; final dispositions retain original/supplemental/conditional accounting separately.

Frozen identities cover all42270original file pins, inventory/catalog/adjudication/external files, owned adapter/report source, required verified task records, actual successful preparation receipts, built runner/product files and host/runtime10.0.11. Manifest/source/identities are checked before and after execution. Preparation requires clean exact source and the matching successful bounded fresh-build/control receipt; old historical binaries cannot satisfy it. Effective resource/compile/validation limits retain the existing finite caps, including the three exact introspection IDs at20b validation work/60seconds only. No cap increases, allocation counters, leases or foreign-project coordination.

Approved standards/input JSON copies are byte-identical to authoritative main and hash-bound: standards b44cec9757982c7d53cc0555bfc192e6c6172bda958e5a1b8765ca97c7cad4e0; input e82338d5eabd52590a4b0b25e679720edc318795ca216cae99a65d3de51c4063. Guards bind exact13schema+1instance disagreements,9dependent unassessed,9input instances+3indeterminate schemas and earlier time119. Original IDs/oracles/statuses/bytes/nonpasses remain intact. Product-only exact expected/kind/actual/codes guards label approval separately; **never original/profile pass credit or exclusion**. All23 preserved source80 standards witnesses use [] for semantic valid or exactly [XSD.SchemaRestriction] for schema rejection/dependency. Fatal/malformed XML uses exactly [SCHEMA.Syntax], cycles exactly [SCHEMA.ResourceCycle], namespace guard []. Other codes, quotas, denials, timeouts, cancellation, harness outcomes, BCL rows, changed IDs/oracles and promoted original results get no approval. Source-derived profiles are separate: every original nonpass without an exact human gate disposition is listed for root review, even where a distinct profile agrees; no blanket profile/null waiver or automatic gate pass.

Optional adapter mapping is scoped to the four verified original callbacks for anyURIa002/a004, schD7/G8. Match kind=Schema, exact URI/referrer/systemId, original principal SHA and catalog absence. Available pinned schemas retain their source URI. Legacy Resolve(null) remains denial; other absent inputs remain denied; declared-but-missing, changed principal and uncatalogued existing files remain harness errors. No network/instance acquisition. Eight original inputs/baselines and two available neighbor pins are retained in verified-optional-inputs.json. a004's original INVALID/queried mismatch stays nonpassing; only an actual complete VALID product observation can agree with the separate W3C4126 derived VALID profile, without granting a new gate exception.

SOURCE controls:17 supervisor tests (six earlier predicates plus11 actual lifecycle/coverage/receipt/accounting cases), seven additional exact-disposition tests plus existing19 reporting tests; native optional controls for original hashes/statuses/oracles, exact callbacks, independent VALID observation, legacy denial, cancellation, purpose rejection, changed binding/hash, existing uncatalogued and declared-missing files. These counts describe source methods, **not passing executions**. An independent optional source checker and bounded preparation launcher are included, both UNRUN.

Proposed commands, only after root SOURCE acceptance, on the final clean committed revision:

```sh
PYTHONDONTWRITEBYTECODE=1 python3 docs/reports/M8-xsd-corpus/controls.py
# Creates a unique ignored <revision>-controls-<UUID>/controls-cleanup.json.
# Ten frozen commands: fresh Release runner build (no restore/build servers),
# runtime controls, hint guards, optional guards, import check/import controls,
# supervisor controls, existing/new report controls, optional source checker.
# Overall1800seconds, each300work+10cleanup; durable receipts, stop first failure.
```

After actual successful preparation and root's next acceptance, make a unique owned ignored manifest directory and freeze its exact files:

```sh
PYTHONDONTWRITEBYTECODE=1 python3 docs/reports/M8-xsd-corpus/run.py --prepare <absolute-owned-artifact-directory>/manifest.json --controls-receipt <absolute-actual-controls-directory>/controls-cleanup.json
PYTHONDONTWRITEBYTECODE=1 python3 docs/reports/M8-xsd-corpus/run.py --manifest <absolute-manifest-path> --manifest-sha256 <exact-emitted-SHA256>
PYTHONDONTWRITEBYTECODE=1 python3 docs/reports/M8-xsd-corpus/dispositions.py <actual-campaign-directory-name> --engine product
PYTHONDONTWRITEBYTECODE=1 python3 docs/reports/M8-xsd-corpus/dispositions.py <actual-campaign-directory-name> --engine bcl
```

The186official campaign children do not establish a fresh separate explicit-XML application supplement. That current-revision profile build/execution remains a separately authorized follow-up before final gate review. Earlier complete source80, focused overlays, refusal baselines, BCL differences and initial failed controls remain historical evidence; none is replaced by this source proposal.
