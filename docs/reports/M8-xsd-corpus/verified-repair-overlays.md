# Focused verified repair overlays

Coordinator-authorized verified main b3ed1c6439706d7faa001cb31ae2ccf2e00d08e3
was joined without conflicts at 28d1b5e1ac907db4dda650098fc6b867bafed06f.
Component mapping, instance assessment and evaluator continuation are verified at
423da4defd7ec85873f13193dec15cbb4134ae00; resource outcomes at
55647de5bb866b78abddb0cffd1d10ed9aff95f7; counted matcher at
590d642da81125cb451a884014532467a73162fb. These are integrated dependencies,
not private peer inputs. The optional compiler consumer remains unfinished, so
the corpus resolver's legacy null-denial behavior is unchanged.

Exact inputs came from the integrated component manifest (eight targets), instance
provenance (four targets), and counted originals manifest (ten targets). Their
hashes, 22 original IDs/expectations/current and prior statuses, 16 required schema
predicates and hashes of 35 unique upstream files are recorded in owned
verified-repair-overlay-inputs.json. Copies in the owning repair fixtures and the
unchanged original upstream bytes matched the integrated source hashes. No
dependent Saxon instances or neighboring corpus cases were added.

At tested merge28d, the public runner was rebuilt in Release with pinned
.NET10.0.11, no restore, disabled build servers, one build worker, shared
compilation disabled and audit disabled: zero warnings/errors. Runner source
remains 8fe73b947412c965b82afabeb458b061825a3b89921a6251b5bf41119ca2d81f;
runner DLL3202223fe71837c9a8c69ea542ba4481f6c0b3856dc717576f71173858aec6c9;
product DLL77b3f93c4a8c4d98ed78cc3a7c766da339dbe330a0e4db52dc64ecf019744640.
Source and binary identities matched before and after all executions.

The exact command for each group used the pinned dotnet host with
`--fx-version 10.0.11`, XsdCorpus.dll, original upstream root, transient one-group
inventory, original metadata set, product or BCL, and an ignored JSONL journal.
All 22 groups ran sequentially in each engine, giving 44 children and 76 exact
observations. Every child had a 30-second outer deadline, TERM/10-second/KILL
cleanup, and the original 30-second public operation cancellation. No timeout or
cleanup signal occurred. All children exited0, were reaped, had absent process
groups and closed logs; session24766 completed exit0. Exact ID equality and
uniqueness passed for every group, and both engines cover the same 38 IDs.

The public policy remains unchanged: 100 million validation/compile operations,
one million components, 67,108,864 document characters/external bytes, depth1024,
attributes4096, token characters16,777,216, interned names262,144,
external resources1024, inclusion depth128 and diagnostics1024.
No limit was raised, no refusal was credited invalidity, and no measurements ran.

Product has 36 original passes and two original nonpasses across 38 observations:
19 schemas valid, three schemas semantically invalid, nine instances complete
valid and seven complete invalid. All 22 target profiles and all 16 prerequisite
predicates agree. xv001/xv004 remain original VALID versus actual schema-invalid;
the already approved Second Edition profile separately agrees, without a new
exception. All four instance-assessment targets and all ten matcher originals
match their untouched original expectations. Z034_a2/a3/b now finish complete
invalid with one XSD.Content diagnostic each; source80 work refusals and the
matcher owner's intervening Diagnostics1024 refusals remain historical evidence.

BCL independently has 34 original passes and four nonpasses: Z036_b1/b2 complete
invalid despite original VALID expectations, plus policy refusals of the two
XML1.1 schema prologues. These are reference observations, not product oracles.
The product retains the corrected VALID metadata for both Z036_b cases even
though their authored names end in `.i`. No expectation was inferred from names.

The table below preserves immutable source80 and available source09b results
beside the new focused overlay. “Unrun” means absent from that saved overlay;
those earlier journals do not establish selected child exit codes. This is not a
new full corpus result, and old full counts/failures are not erased. Raw command
lists, subset inventories, journals, lifecycle, identities and prior-row copies
remain ignored under artifacts/M8-xsd-corpus/28d1b5e1-focused-repairs.

A2 remains failed/in_progress. Recursive components, ordered hints, type and
particle relations, optional composition and exact input/standards gate policy
remain coordinator work. No still-open hint/resource/boundary originals or full
official suite were rerun. Coordination remains within Xemele only.

| Exact target ID | Original expected | Product80 | Product09b if run | Product28d | BCL28d |
| --- | --- | --- | --- | --- | --- |
| msMeta/Group_w3c.xml#g172/i0:groupN007v.v | valid | instance-invalid / nonpass | instance-invalid / nonpass | instance-valid / pass | instance-valid / pass |
| msMeta/Group_w3c.xml#g173/i0:groupN008v.i | invalid | instance-invalid / pass | instance-invalid / pass | instance-invalid / pass | instance-invalid / pass |
| saxonMeta/XmlVersions.testSet#g0/s:xv001.xsd | valid | schema-valid / pass | schema-valid / pass | schema-invalid / nonpass | reference-policy-refusal / nonpass |
| saxonMeta/XmlVersions.testSet#g3/s:xv004.xsd | valid | schema-invalid / nonpass | schema-invalid / nonpass | schema-invalid / nonpass | reference-policy-refusal / nonpass |
| msMeta/Wildcards_w3c.xml#g175/s:wildK001 | valid | schema-invalid / nonpass | unrun | schema-valid / pass | schema-valid / pass |
| msMeta/Wildcards_w3c.xml#g28/s:wildC030 | valid | schema-invalid / nonpass | unrun | schema-valid / pass | schema-valid / pass |
| msMeta/Wildcards_w3c.xml#g306/s:wildZ010 | valid | schema-invalid / nonpass | unrun | schema-valid / pass | schema-valid / pass |
| msMeta/Group_w3c.xml#g85/s:groupH007v | invalid | schema-valid / nonpass | schema-valid / nonpass | schema-invalid / pass | schema-invalid / pass |
| msMeta/Attribute_w3c.xml#g265/i0:attZ002.v | valid | instance-invalid / nonpass | unrun | instance-valid / pass | instance-valid / pass |
| msMeta/Additional_w3c.xml#g42/i0:addB034.v | valid | instance-invalid / nonpass | unrun | instance-valid / pass | instance-valid / pass |
| msMeta/Additional_w3c.xml#g141/i0:addB136.v | valid | instance-invalid / nonpass | unrun | instance-valid / pass | instance-valid / pass |
| msMeta/IdentityConstraint_w3c.xml#g264/i0:idF018.i | invalid | instance-valid / nonpass | instance-valid / nonpass | instance-invalid / pass | instance-invalid / pass |
| msMeta/Particles_w3c.xml#g742/i0:particlesQ005.v | valid | incomplete-refusal / nonpass | unrun | instance-valid / pass | instance-valid / pass |
| msMeta/Particles_w3c.xml#g844/i0:particlesZ034_a1.v | valid | incomplete-refusal / nonpass | unrun | instance-valid / pass | instance-valid / pass |
| msMeta/Particles_w3c.xml#g845/i0:particlesZ034_a2.i | invalid | incomplete-refusal / nonpass | unrun | instance-invalid / pass | instance-invalid / pass |
| msMeta/Particles_w3c.xml#g846/i0:particlesZ034_a3.i | invalid | incomplete-refusal / nonpass | unrun | instance-invalid / pass | instance-invalid / pass |
| msMeta/Particles_w3c.xml#g847/i0:particlesZ034_b.i | invalid | incomplete-refusal / nonpass | unrun | instance-invalid / pass | instance-invalid / pass |
| msMeta/Particles_w3c.xml#g848/i0:particlesZ035_a.i | invalid | incomplete-refusal / nonpass | unrun | instance-invalid / pass | instance-invalid / pass |
| msMeta/Particles_w3c.xml#g849/i0:particlesZ036_a.i | invalid | incomplete-refusal / nonpass | unrun | instance-invalid / pass | instance-invalid / pass |
| msMeta/Particles_w3c.xml#g850/i0:particlesZ036_b1.i | valid | incomplete-refusal / nonpass | unrun | instance-valid / pass | instance-invalid / nonpass |
| msMeta/Particles_w3c.xml#g851/i0:particlesZ036_b2.i | valid | incomplete-refusal / nonpass | unrun | instance-valid / pass | instance-invalid / nonpass |
| msMeta/Particles_w3c.xml#g852/i0:particlesZ036_c.v | valid | incomplete-refusal / nonpass | unrun | instance-valid / pass | instance-valid / pass |
