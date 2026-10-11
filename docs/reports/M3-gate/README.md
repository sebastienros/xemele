# Verified M3 core streaming preview

Source checkpoint: `75de168c4ebb80c140cdf5ecb7054ebf38e594d3`.
SDK 10.0.400, .NET 10.0.11, macOS 15.8.1 Arm64. The net10.0-only
solution, original-source grammar driver and standalone demo build successfully.
All 5,753 cumulative tests pass without errors, failures or skips: 5,172 unit,
268 conformance and 313 differential. XML results and command logs are retained.

The original M2 source-context lane separately checks every one of 2,585 frozen
cases against normative/independent models: 1,394 accepted, 1,191 rejected,
3,386 resources hash-verified, 1,394 bulk-decode/all-callback-pause models,
409 original-suite output models and 15 unchanged authored effective-XML10
output overrides. The 771 known reference differences are independently resolved
by the original frozen rules; none is a newly invented exclusion. Child exits
and cleanup pass, source/runtime/output provenance is frozen before/after.

The public no-primary-base lane remains distinct: all 2,585 original-byte inputs
agree across DOM/SAX/push/pull, with 1,169 accepted and 1,416 rejected. There are
316 no-primary-base denials and 218 differences against source-context
expectations; these are reported, not counted as conformance acceptance. All
97 deterministic fuzz operations and the five discovered decoder regressions
pass after the explicitly owned DOM repair. The cumulative suites repeat the
integrated original-byte and preservation behavior without changing inputs.

The official compiled inventory matches 720 public members. Local package
`Xemele.1.0.0-m3-preview.nupkg` contains the matching net10.0 assembly and no
contract/foundation/corpus artifacts. A temporary consumer restores only from
that local feed into an isolated package cache, builds without repository
project references, and produces exactly the same independently checked output
as the source demo. The package is retained in ignored `artifacts/M3-gate/packages`
on the coordination checkout; it was not published. Demo.cs documents lambda
and subclass SAX, chunked push, sync/async pull and forward output, genuinely
suspended async DOM input, DTD defaults, recovery, caller-stream ownership and
exact multilingual/CDATA/namespace output. No later milestone assembly is needed.

## Actual streaming memory and allocations

All five modes consume generated input of exactly 1 GiB, with depth two,
fixed vocabulary and bounded tokens, and deliver their first element before EOF.
Each has 64 active managed-heap checkpoints plus completion. The absolute whole
managed heap includes source/sink/runtime objects, so it conservatively bounds
parser-owned live memory without subtraction. Maximum measured live bytes:

| Surface | Whole managed heap upper bound |
| --- | ---: |
| SAX | 244,912 B |
| Async SAX | 276,280 B |
| Pull | 247,440 B |
| Async pull | 278,192 B |
| Push | 383,776 B |

Every observation is below the mandatory 8,388,608 B gate. Four direct runner
exits are 0. SAX completed all semantic/live assertions and wrote its report;
the optional outer RSS wrapper subsequently exited 1 on a restricted macOS
sysctl. Its runner exit was not separately captured and is not relabeled 0.
The completed memory/semantic observations are accepted under documented
coordinator review. A prelaunch wrapper-index failure and an insufficient
75-second pull timeout are preserved as failures. No successful workload was
repeated merely to repair reporting. RSS is unavailable, not measured zero.
The nine-mebibyte retained negative control trips the same live threshold.

Verified owner reports supply actual warm primitive allocation controls, with
cold setup, owned persistence/diagnostics, true async suspension and positive
controls recorded separately. The integrated cumulative tests preserve these
regressions. No new optimization or comparative performance claim is made.

| Boundary | Preserved actual evidence |
| --- | --- |
| Borrowed callback dispatch | 0 B warmed direct dispatch, escaping positive control; M3-callbacks |
| Bounded SAX/push | 0 B selected warmed borrowed controls; whole-operation setup owned; M3-sax/M3-push |
| Reader cursor copies | 0 B warmed direct copy, owned-string positive control; M3-reader |
| Resource mode/rebinding | 0 B warmed mode/rebinding/no-op controls with positives; M3-resource-mode/M3-async-token-binding |
| Sync/async writer primitives | 0 B warmed selected actual-call controls; capacity/setup and suspension separate; M3-writer/M3-async-writer |
| Async input | Buffered primitive/no-op 0 B; 1,024 actual suspended calls 327,680 B, 320 B/call; M3-async-input |
| Async DOM serialization | Borrowed scalar control 0 B; owned setup and process-wide suspended costs explicit; M3-async-serialization |
| DOM decoder repair | Inclusive process-wide owned setup/diagnostic/suspension costs with escaping positive; M3-dom-decoder-terminal |

Inclusive BDN observations of 2 B buffered reader/input and 7 B 64 KiB push
remain unexplained; they are not subtracted or relabeled zero. Historical async
writer batch/tiering and serializer wrapper failures remain honest owner
records. Source-closure proofs preserve applicable observations after the
DOM-only error-path repair. Timings on this shared host are informational;
no speedup, regression, repeatability, or dedicated-hardware qualification is
claimed. M9 still requires the planned release/platform/x64-and-Arm64 checks.

The gate introduces only verification/demo artifacts. All runtime feature,
join and repair prerequisites are verified. Task validation checks all 148
records. Previous milestone tasks and frozen specifications are preserved.
