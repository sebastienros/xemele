# Actual recursive-component allocation evidence

Root ran the accepted prebuilt control then allocation process once, without
an isolation, PARK, ACK or exclusive-use gate. Durable cleanup `cc2f59ce6`
preceded worker raw inspection. Both children (53183/53185) completed, exited0,
were reaped, left no owned process-group members and closed their logs. All457
source/runtime/probe/control identities matched before and after. Supervisor
lifecycle elapsed time is cleanup evidence only, not workload timing.

Runtime source: `9e376cd982159b27cbb05c1fa5cf2577bd21e7d2`. Built fixture/probe
source: `822e5a756f9d82c5becd14c2ded7c3a9b132ff99`; supervisor amendment
`e458a2d4b8088fec8e678cd24d242fa2e1316b8d`. SDK10.0.400/runtime10.0.11,
macOS15.8.1 Arm64, workstation GC, tiering/concurrent GC disabled. Source,
fixture/probe, preparation/control and timeout regression hashes are preserved
in preparation.json and measurement-summary.json. No worker launched counters,
and no build or test was repeated during this evidence update.

The counter-disabled process executed the same1085calls/26095consumed units,
including13first calls,16warmups and64sampled calls per route and32retained
objects. All disabled allocation/heap fields are zero placeholders, never an
allocation-free claim. The measurement process consumed identical outcomes.
Every one of832owned samples was positive; each aggregate equals the sum of its
64samples. Refusal cannot count as a semantic rejection.

| Owned route | First call B | Warm mean B/op | Sample range B |
| --- | ---: | ---: | ---: |
| compiler-original-addB077 | 115056 | 113936 | 113936–113936 |
| compiler-original-particlesZ010 | 90840 | 90840 | 90840–90840 |
| compiler-original-particlesEb041 | 88952 | 88952 | 88952–88952 |
| facade-original-addB077 | 2461736 | 2.46077e+06 | 2460376–2461712 |
| facade-original-particlesZ010 | 1829144 | 1.8281e+06 | 1827808–1829144 |
| facade-original-particlesEb041 | 1942672 | 1.9426e+06 | 1942288–1943624 |
| compiler-nested-anonymous-24 | 229056 | 228432 | 228432–228432 |
| compiler-long-multilingual-name-8192 | 81048 | 81048 | 81048–81048 |
| diagnostic-forbidden-model-group | 52400 | 52160 | 52160–52160 |
| diagnostic-forbidden-base-type | 51656 | 51656 | 51656–51656 |
| diagnostic-deferred-publication | 253168 | 251853 | 251832–253168 |
| validation-recursive-96-valid | 535720 | 535720 | 535720–535720 |
| validation-recursive-96-invalid | 538472 | 538408 | 538408–538408 |

Cold fixture initialization allocated3452776B, separately from first/warm calls.
The live1024-byte array positive control allocated1048B. Fixture initialization
owns file reads, generated schema text, parsed documents, cached traversal schema
and96-level instances. Compiler costs own operation state, registration, charged
pending-body queue entries, expanded immutable content and final graph snapshots.
Facade costs additionally own datatype/facet/content/identity setup; the Eb041
facade route includes unchanged-original validation. Rejection paths own diagnostic
snapshots and exception stacks. Preparsed traversal routes own per-run state and
result/diagnostic snapshots. No new primitive0B or optimization-speed claim exists.

| Retained category (8 objects each) | Whole-heap delta B |
| --- | ---: |
| compiler-graphs | 581672 |
| public-schemas | 2039824 |
| schema-diagnostics | -39688 |
| validation-results | -4944 |

All32objects and the fixture remain reachable through the readings. These full-GC
process-wide deltas are coarse illustrations, not individual object sizes or
peak/retained-memory bounds. The -39688B diagnostic and -4944B validation-result
deltas reflect GC/lifetime noise; they cannot imply negative, zero, or borrowed
ownership costs. Independently measured allocation samples for those routes are
strictly positive. The positive graph/schema deltas likewise are not precise
per-result retained sizes. This experiment bounds fixture input/count only; it
does not bound every schema, prove a release target, or measure native memory.

Evidence covers synchronous managed allocation only. Other threads were free to
work; no globally-idle-host, foreign-project exclusion, throughput, x64, AOT or
asynchronous allocation claim is made. Raw logs/distributions stay in ignored
artifacts; the committed report contains summary tables and analysis only.
