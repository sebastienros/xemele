# M8-cli source and finite correctness recipe

Source release implements internal CLI registration and public schema consumers.
No runtime validator, shared build/registry, public library signature, existing API
baseline, canonical corpus, or allocation path is changed. `samples/M8-cli/README.md`
records the command/output contract. The owned executable links the actual CLI
source and three independently expected public-library profiles. Its fixture
sources are excluded from default test globs and compiled by `Probe.csproj`.

Preparation/check launch requires the coordinator's explicit release. Source-only
`python3 docs/reports/M8-cli/checks.py recipe` prints exact commands, source tree
digest, SDK/runtime module hashes, and environment without importing supervision,
restoring, building, running tests, or launching counters.

After release:

```
python3 docs/reports/M8-cli/checks.py prepare
python3 docs/reports/M8-cli/checks.py check
```

Preparation restores offline from `/Users/sebastienros/.nuget/packages` and builds
the actual Tool, owned regular and XML-only checks, and the existing compiled
public API extractor. Exact SDK10.0.400/runtime10.0.11 are pinned. Build servers
and shared compilation are disabled; single-node builds retain warnings-as-errors.
Each command gets at most240 seconds work and10 seconds terminal cleanup; the
phase total work is1500 seconds plus10 seconds command cleanup and10 seconds
phase reserve. Suggested root outer work/hard deadlines:1530/1540 seconds.

Checks run the two actual owned binaries (120 seconds each), eight phase guard success/failure controls (60 seconds), the small real CLI
controls (120 seconds), the existing full public API comparison (60 seconds),
task validation and diff-check (30 seconds each). Total phase work is540 seconds
plus10 seconds command cleanup and10 seconds phase reserve. Suggested root outer
work/hard deadlines:570/580 seconds. The real CLI controls internally cap their
whole body at105 seconds, children at15 seconds with3 seconds reaping; children
inherit the outer owned process group. They include prior XML/HTML cases and a
closed-stdout SVRL failure. No default test suite or allocation probe executes.

The runner imports the accepted `M8-xsd-corpus/supervision.py` functions unchanged
and owns a copy of its `nested_ownership.py` changing only `OUT` to this task's
artifact root; both original/copy hashes are pinned. Every new session registers
and receives ACK in every ancestor registry before executing; the effective
environment preserves `XEMELE_CORPUS_OWNED_REGISTRIES`. Ancestor registries must
be under `artifacts/M8-cli`. The CLI controls' descendants inherit their registered
group. TERM/INT unwinds through the accepted interruption/cleanup pattern.
`PYTHONDONTWRITEBYTECODE=1` and the interpreter's internal bytecode setting are set
before local imports. Command/phase terminal, binding-failure and deadline receipts
are durable even on failed attempts. Raw logs are inspected and deleted only after
all groups are absent, no intents are pending, children are reaped and logs closed.
Preparation freezes complete output directories (including dependencies, deps and
runtimeconfigs) in `prepared.json`; checks reject drift before and after the phase.
Source, entire SDK/runtime trees and effective environment are checked at both ends
of each phase. Dynamic ancestor registry paths are pinned separately per phase;
stable environment fields must also match preparation. `results.json` and concise
receipts retain command/outcome/boundary/cleanup evidence. No raw archive is retained.
The worker makes no isolation,
host-idle, throughput, timing, or allocation claim. Root alone launches counters.

The true XML-only project compiles selected XML source trees plus all schema
validation and explicit resource integrations, with no reference to the normal
library or any HTML/CSS source. The XML-only binary also checks the resulting
assembly has no HTML/CSS types, then exercises the same schema/CLI/resource/fuzz
cases. Only the regular real CLI controls exercise earlier HTML commands.

Frozen615/257/nullable3 and full93 canonical results are preserved by unchanged
prerequisite source/data; comprehensive cumulative/package/AOT checks belong to
the coordinator's M8 gate. Public1430/55 is rechecked using the verified compiled
extractor; the schema CLI adds no library API rows.
