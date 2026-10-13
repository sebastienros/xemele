Implemented on the worker branch; integration verification is pending. Root
accepted actual proposed evidence at main e81dd0ae5 for frozen packet
cc9b87472ee23a1f420f67f4ca3d7784f2aaa461, runtime source
f71e1ebfa95609b831000b79c3d7e8d885d5258f and Availability.cs SHA
c5ccdab54580f735bf030906951fe2b7ca02ae163036cf98963a53f2888db1e3.
This handoff changes only the task record and owned reports; it is not newly
tested code. Historic source manifests and their tested identities stay intact.

AvailabilityBuilder tracks whether this invocation has registered an unavailable
reference. Before that event, Unavailable polls cancellation/prior refusal with
Charge(0) and returns false without constructing closure. Later missing
registration disables this shortcut immediately. Full charged registration,
partial closure, Seal and immutable publication remain unchanged. All93 added
API declarations equal the independently verified canonical projection.

Root actual15 controls and24 check commands passed704 independently selected
methods/2222 cases, including16 owned cases and unchanged694/2206 predecessor
cases. These prove exact dangling QNames, no fabricated types, strict compiler
rejection, incomplete required consumption, corrected keyInfo and document-local
ID uniqueness, late registration, reverse chains/diamonds/use separation,
registration/publication caps, cancellation/refusal with null caps, and immutable
complete publication. Public1430/55 fragments, frozen615/257, full93/13 joins,
normal and true XML-only consumers, disabled6 routes/254 consumed and planning
passed. Four schema-original INVALID oracles/nonpass observations and four
original missing-instance nonassessments retain zero disposition original/gate
pass credit. Their separately approved dispositions are not new test waivers.

Original annotZ002 remains391301 bytes, SHA
fff58be90b04ace4117786f19d8a450ed81b0e074f9c67d9316a3275a028b6cd.
Root reproduced baseline SCHEMA.Limit and proposed VALID with the unchanged
100M work/1M components/64MiB document-character limits. Original compilation
is outside allocation intervals. No quota, metadata or oracle was rewritten.

Actual Arm64 .NET10.0.11 Release allocation observations, same pinned probe and
fixtures; all calls consumed, first operation plus8 explicit warmups then32
repeated calls per route:

| Route | Baseline warm B/call | Proposed warm B/call | Baseline32-call work | Proposed32-call work |
| --- | ---: | ---: | ---: | ---: |
| Resolved query8 | 1224 | 0 | 21184 | 0 |
| Resolved query32 | 5112 | 0 | 850496 | 0 |
| Resolved query64 | 10256 | 0 | 6190144 | 0 |
| Public compile8 | 418307.5 | 367313.75 | not queried by graph counter | not queried by graph counter |
| Public compile32 | 685675.5 | 455635.5 | not queried by graph counter | not queried by graph counter |
| Public compile64 | 1019743 | 554517.25 | not queried by graph counter | not queried by graph counter |

Proposed query first and warm thread/global bytes are all0. Their retained-growth
charges are0 versus baseline320/1088/2112 across32 calls. Cold fixture ownership
is49496B versus49472B (+24B); positive control4120B in both. Coarse retained-owner
heap delta is4705456B versus6496600B. The192 retained slots contain96 repeated
builder references and96 compiled schemas, plus8 separate public schemas; this
is not a distinct-object/live-memory census. Public compile rows include owned
results. No timing, host isolation, global-static initialization, x64 or release
performance claim. Disabled mode calls zero allocation counters/collections.

Fresh preparation3 commands/disabled6 routes254 consumed and actual6-route
allocation passed. Root launched every actual phase. All registered families
were closed/reaped/logs closed/absent;341 controls,442 checks,5 preparation and52
measurement raw files deleted, single-use preparation consumed and removed.
Concise report hashes, receipts and limitations are in branch-evidence.json and
baseline-evidence.json; no raw archives are retained here.

Remaining: merge on primary, rebind the implemented task and primary path/input
inventories with fresh SOURCE review, then fresh root integrated controls/check
and final planning validation. Only root may record verified/integration_revision.
See integration-rebind-plan.md. Branch evidence may support integrated allocation
only after root proves identical runtime/probe/fixture/toolchain/result inputs.
Worker has zero active owned jobs/PIDs/sessions/cells and launches no phases.
