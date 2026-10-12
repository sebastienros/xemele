# Immutable preparation handoff

Accepted runtime SOURCE5160cfadb64de8555d58e392df5c949fd465a4a6 and allocation
SOURCE72bd4e876e174b4941f1a0e29b2d2fdd5d2431a0 remain unchanged. Independent
/root/review_m8_model review copied byte-for-byte into allocation-source-review.json.

Frozen manifest: docs/reports/M8-xsd-optional-composition/preparation.json
SHA256389a172594c545289cff9c51b4894434a9c5600aee520b2a099ee678e4405f5c.
471 identities: runtime/probe binaries, pinned runtime/host, tracked runtime source,
owned original byte/copyright/metadata/tests/probe, build inputs, accepted reviews,
reviewed preparation/lifecycle scripts and disabled preparation outputs.
Manifest sourceRevision72bd4e876e174b4941f1a0e29b2d2fdd5d2431a0.

Actual authorized preparation only, first attempt:
- Offline SDK10.0.400/runtime10.0.11 restore/build exit0;0warnings/errors.
- prepare.py disabled control:24routes/16warmups/64samples/40retained and all
  semantic/callback/disposal checks passed; allocation/heap reads guarded OFF.
- lifecycle-check.py actual finite regression: timeout-to-SIGTERM-exit0 remained
  TimeoutExpired failure, next child never started; independent normal child
  completed exit0. Both reaped, owned groups absent, logs closed.
- measure.py --verify-only:471identities/real process-inspection permission pass.
- measure.py --preflight:24disabled routes completed exit0; identities unchanged.
  Preflight and preparation children reaped, owned groups absent, logs closed.
- Planning196 and git diff --check pass; runtime/tests/fixtures/probe/scripts
  immutable. Zero actual allocation-counter or workload-timing executions.

preparation-summary.json retains compact hashes/counts/ownership/lifecycle facts;
raw disabled outputs/logs/regression payload remain ignored artifacts. Keep the
prepared control/lifecycle JSONs and frozen binaries until root before/after
manifest verification finishes: they are identity inputs. No raw per-route or
per-sample payload committed; the durable hash manifest is needed for root launch.

Root alone may later execute accepted measure.py --run with this reviewed manifest
SHA256: total120s/work110s/cleanup10s, disabled control then one actual measurement,
stop at first failure/no retry, durable lifecycle/release before raw inspection.
No isolation/PARK/ACK/lease or foreign-project coordination. Worker has not run
--measure or --run. A3 remains pending actual allocation/retained/ownership evidence.

Root separately reports combined075b033958f321f0046b5b0996de0cfed8a161b0 check
2044unique cases/eightbuilds0warnings-errors/APIs/normal+trueXMLonly consumers pass;
not repeated or represented as worker execution. Root integrates/verifies later.

Zero owned background jobs; both controlling tool sessions finished exit0 and all
four supervised child groups absent. No outside-ownership requests. Writes
relinquished after the handoff commit; no further source work or counters planned.
