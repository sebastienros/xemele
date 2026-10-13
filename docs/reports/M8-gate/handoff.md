# M8-gate final source repair handoff

SOURCE ONLY. All gate executable phases remain UNRUN; root alone releases and launches.

- Isolated worktree: `/Users/sebastienros/.codex/worktrees/29a0/xemele`; branch `codex/m8-gate`.
- Serialized claim: `e8230dfb967aa15cea120f831a35a40e4ce15807`.
- Source closure: 3076 files, SHA256 `d8aa885d474f5763084a497deabef5fe292e061cdf15b6ae111de6dcd77f5985`.
- checks.py SHA256: `931b045bdc01867a96b03d6a9e83dba7cff80c78e38b2860e43c889890fba414`.
- guard_controls.py source SHA256: `de912080876c30072057b66f3efc9ecd51709a454946b7586cda693fc4842a6d`.
- Packet SHA256: `a6fccf2fb1b3723036f77c58fad48a7c27f5d1d785d9b9d21f250350cbf321f2`.
- Recipe SHA256: `2fc2714cbd8a596880f4eeae07a00f6f130b8c206cc2e4619643c5681399f0cb`.

The recipe hash is UTF-8 of json.dumps(packet['recipe'],sort_keys=True,
separators=(',',':'),ensure_ascii=True), with no trailing newline. It hashes the
actual contained recipe object, including commands/counts/bounds.

Final terminal printing and its post-print hard-deadline check now occur inside
publication reconciliation. Late output makes exit1 and authoritative passed=false;
exactly one failed reconciliation is attempted. The added late-terminal-output
synthetic control is UNRUN. Source proposes11 synthetic preparation guards plus
three archive controls; it does not reuse root's nine control results as gate credit.

Accepted parent SHA1af448c7a5ebda57d0091f15cde4877531a9caa647c8e45fcb25a6c4af377602
has outer work/hard:prepare12860/12880,check3860/3880,counter2160/2180.
Inner phases remain51commands12500work+180reserve,18commands3500+180,
4rootcounter commands1800+180. Allfive identity controls retain180innerwork+
40reserve,225outerwork/235outerhard; controller1250work/1290hard within1400
recipe work. Deadlines never reset.

Source inspection only:10 Python ASTs parsed and source/script/authority hashes
refreshed. No build, restore, provisioning, discovery, control, test, probe or counter
was run by worker. Task record unchanged; no gate criterion passing/implemented.

Exact changed paths for this repair:
- docs/reports/M8-gate/README.md
- docs/reports/M8-gate/checks.py
- docs/reports/M8-gate/guard_controls.py
- docs/reports/M8-gate/handoff.md
- docs/reports/M8-gate/source-packet.json

All prior M7 type/identity preservation, twelve Schema-resource positives,
WarmBorrowed inclusion, API1430/55/eight frozen lanes/615/257/nullable3/canonical93,
primaryCLI attribution and historical failures/EPIPE limitation, originalXML
resources/earlier behavior, external local package/trim/AOT expectations, and
source-bound XSD/RNG/Schematron evidence remain. Original155 dispositions retain
144false/11null and zero original/gate credit; annotZ002 remains unwaived. NoEmit
has only real+positive modes; compatible disabled evidence remains separately bound.

Remaining: root source acceptance/release, actual finite preparation/checks/counters,
integrated verification, checkpoint custody, transient cleanup and terminal transfer.
Worker owned jobs/PIDs/sessions/cells:zero. Root retains sole primary/main/task-state/
integration and actual-counter custody. No chats, agents, dispatch or publication.
