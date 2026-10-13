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

Checks run the two actual owned binaries (120 seconds each), the small real CLI
controls (120 seconds), the existing full public API comparison (60 seconds),
task validation and diff-check (30 seconds each). Total phase work is480 seconds
plus10 seconds command cleanup and10 seconds phase reserve. Suggested root outer
work/hard deadlines:510/520 seconds. The real CLI controls internally cap their
whole body at105 seconds, children at15 seconds with3 seconds reaping; children
inherit the outer owned process group. They include prior XML/HTML cases and a
closed-stdout SVRL failure. No default test suite or allocation probe executes.

The runner imports the accepted `M8-xsd-corpus/supervision.py` functions unchanged,
sets this checkout and task artifact directory, and verifies each direct child
reaped, whole owned process group absent, and log closed. Raw logs are read for
actual outcomes and deleted after terminal cleanup; no raw archive is retained.
Preparation freezes source/runtime/environment and actual binary hashes in
`prepared.json`; checks reject any input/binary drift. `results.json` retains only
concise command/outcome/boundary/cleanup evidence. The worker makes no isolation,
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
