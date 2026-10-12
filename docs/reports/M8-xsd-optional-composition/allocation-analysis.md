# Actual allocation evidence — optional XSD composition

Root executed one accepted prebuilt control/measurement sequence against prepared
`d2b4cd848fe028a512fd68505f9faddfeb5f49be`; allocation source remains72bd4e876,
runtime/tests/fixtures5160cfadb. Manifest SHA256
`389a172594c545289cff9c51b4894434a9c5600aee520b2a099ee678e4405f5c` froze471identities.
Root session51051 terminal exit0. Children83458/83460 completed exit0, reaped,
owned groups absent, logs closed. Durable cleanup commit
`2dfb30ab958cb221a4978067a3de72db9f78e311` preceded raw inspection.
Identity checks matched before/after; worker performed static hash/arithmetic checks
only in this final stage and never launched actual counters.

Arm64/macOS15.8.1, SDK10.0.400/.NET10.0.11, workstation/nonconcurrent GC, tiering off.
Cold setup968416B separately owns schema strings/DOMs/cached original bytes, URIs,
precompiled a004 schema and its setup compiler graph. Positive1024-byte array
observed1048B. One first call,16 warmups and64 samples per route:24 positive first
calls,1536 positive warm samples. Aggregates equal sample sums. Disabled mode
guarded off every counter/heap read and emitted zero; both modes consumed4702
results and retained40, with identical runtime/probe hashes. Disabled zero is not
allocation evidence. No timings, throughput comparison or zero-allocation claim.

| Route | First call B | Warm mean B/op | Warm min–max B/op |
| --- | ---: | ---: | ---: |
| 0 graph-optional-include-absent | 102056 | 100620 | 100616–100872 |
| 1 graph-optional-import-absent | 100728 | 100732 | 100728–100984 |
| 2 graph-empty-redefine-absent | 100648 | 100652 | 100648–100904 |
| 3 graph-annotation-redefine-absent | 100872 | 100876 | 100872–101128 |
| 4 graph-include-same-uri-later-available | 171552 | 167436 | 167432–167688 |
| 5 graph-import-same-uri-later-available | 169992 | 169912 | 169904–170160 |
| 6 facade-mandatory-redefine-resolved | 401224 | 399675 | 399600–400936 |
| 7 mandatory-redefine-unavailable | 8240 | 8152 | 8152–8152 |
| 8 optional-shared-work-refusal | 4552 | 4552 | 4552–4552 |
| 9 optional-legacy-null-denial | 7328 | 7328 | 7328–7328 |
| 10 optional-explicit-denial | 7712 | 7712 | 7712–7712 |
| 11 optional-no-resolver-denial | 7456 | 7456 | 7456–7456 |
| 12 optional-resolved-malformed | 66232 | 66236 | 66232–66488 |
| 13 optional-resolved-wrong-namespace | 67704 | 67704 | 67704–67704 |
| 14 optional-missing-required-reference | 51648 | 51652 | 51648–51904 |
| 15 cancel-after-unavailable | 12336 | 7144 | 7144–7144 |
| 16 cancel-after-acquisition | 7328 | 7328 | 7328–7328 |
| 17 facade-original-anyURI_a002 | 428808 | 427083 | 427008–428600 |
| 18 facade-original-anyURI_a004-derived-valid | 466936 | 467036 | 466936–468528 |
| 19 facade-original-schD7 | 527824 | 527468 | 527368–528960 |
| 20 facade-original-schG8 | 531088 | 530764 | 530664–532256 |
| 21 facade-include-later-available | 389976 | 390051 | 389976–391312 |
| 22 validation-original-a004-derived-valid | 29320 | 29320 | 29320–29320 |
| 23 caller-read-primary-survives-disposal | 64744 | 64748 | 64744–65000 |

Graph routes0–5 own a fresh compilation, builtin/component registries, source and
namespace contexts, graph output and temporary Children lists;0–3 acquire no
resource/document for absence. Added empty/annotation redefine classification
uses an operation-owned temporary list. Include/import followup4–5 additionally
own one successfully returned wrapper/stream/child parse while shared source bytes
are setup-owned. These whole-workload samples include callback/disposal/semantic
checks; differences between routes do not isolate the cost of one added list.

Public routes6/17–21 own datatype/content/identity composition, schema graph and
results of the explicit instance controls. Original principal/source byte arrays
and instance DOMs are cached setup inputs; each original compile owns a new
caller principal wrapper and, for schD7/schG8, one new loader-owned child stream.
Loader cleanup transfers/disposes resolved ownership exactly once; principal
wrappers are kept open until their explicit caller using-scope exits.

Diagnostic7–14 own bounded compiler scratch and exceptions/diagnostic snapshots;
work/denial failures stop before publication. Cancellation15–16 own token sources,
scratch and cancellation exceptions; resolved resource cleanup precedes throw.
Route23 owns a per-call primary parser exception, read/resource state and cleanup
exception; exact primary object survives cleanup failure. Validation22 owns per-run
state/result; its precompiled schema and original DOM remain setup-owned.

Original a002/schD7/schG8 completeVALID matches authored metadata. a004 authored
instance INVALID remains frozen and disagrees with observedVALID; routes18/22
exercise only separately source-derived W3C4126 VALID on unchanged original bytes.
No original metadata disagreement is credited as matching validity.

Five categories retain8 outputs each with prior arrays/fixtures alive. Coarse
incremental full-GC live-heap deltas follow; these are not object sizes, attributed
allocation totals, peak ceilings or native memory. Negative GC deltas remain
unmodified and demonstrate why attribution cannot be inferred.

| Retained category | Count | Heap before B | Heap after B | Delta B |
| --- | ---: | ---: | ---: | ---: |
| compiled-graphs | 8 | 814488 | 1271672 | 457184 |
| public-schemas | 8 | 1271920 | 3272184 | 2000264 |
| schema-diagnostics | 8 | 3260864 | 3496224 | 235360 |
| cancelled-and-caller-failures | 8 | 3496480 | 3682304 | 185824 |
| validation-results | 8 | 3682512 | 3249624 | -432888 |

Measurement is bounded by24routes/64samples/40retained,120s total/110s work/10s
cleanup, one disabled control plus one measurement, no retry. The accepted finite
supervisor requires reason=completed AND exit0/reaping/group absence/log closure.
Preparation actually proved timeout-to-SIGTERM-exit0 remains failure/no-next.
Compact summary copied byte-for-byte from root; no raw distributions committed.
Raw logs/results remain ignored pending root cleanup after integrated verification.

Root separate combined candidate075b033958 correctness passed2044 distinct cases,
eight builds0warnings/errors,1430public/eightfrozen/additive46/emitted APIs and
normal/trueXMLonly consumers. Worker did not rerun these checks. A3 branch evidence
is complete; integration and verified status remain root-owned. Async/x64/AOT,
native memory, broader corpus/milestone gate, global-idle and timings are outside
this evidence. No numerical throughput target or allocation-free primitive is
inferred from these owned-result allocation samples.
