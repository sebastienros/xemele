# Syntax executable checks

Tested source revision: `d4de9b394cec42fe8caa5c8deacc7c31ecc7b4b0`.
Runtime source remains independently reviewed `7ea67f77a7d878f510f2ea29e1d30bb187439c2a`;
d4de9b3 only corrects verifier reproducibility/approved integrated golden selection.
Command: SDK 10.0.400, DOTNET_CLI_USE_MSBUILD_SERVER=0,
`python3 docs/reports/M1-syntax/verify.py` with authorized NuGet/MTP local IPC.
Raw successful output: ignored `artifacts/M1-syntax/checks-d4de9b394cec-1.txt`.
The process exited 0 before any syntax latency benchmark started.

- Release solution build: zero warnings/errors.
- Focused syntax tests: 70/70. Full solution: 134/134. Foundation: 11/11.
  All have zero skipped tests; the full run includes the verified common tests.
- Foundation M1/M2 fixture projects explicitly build before approved model
  comparisons: 139/173 rows unchanged.
  Runtime public API: exactly the approved 81 common rows, nonempty discovery
  and compiler comparison; no public syntax family or empty API fragment.
- Six independent M1 declaration lanes compile and match their approved public
  baselines; the separate protocol model passes 38 assertions.
- Actual runtime internal syntax ABI: nine approved types, 80 declaration rows,
  compared against the independently compiled M1 syntax model. See internal-abi.txt.
- Build-tool Python checks: 16/16; bootstrap corpus verification passes.
- Independent character/sink source sample passes, pausing every callback through
  declaration, CRLF, split surrogate, references, namespaces and CDATA.
- Runtime pack contains net10.0/Xemele.dll and no Foundation/Contracts model files.
  Separate NuGet-only sample with isolated temporary package cache passes. It uses
  the actual named internal sample friend, not a public parser placeholder.
- Benchmark project builds; allocation mode runs without latency timing.
  Fresh setup: 8,608 B. Warm repeated setup: 6,616 B/op. Warm borrowed dispatch:
  0 B across 10,000 appends, consuming 50,500 text UTF-16 units. Retained
  one-open-root heap-minus-fragmentation estimate: 6,872 B/op, excludes the
  preallocated reference array and remains informational.
- Planning validator: all 101 tasks pass. Whitespace check passes.

## Focused coverage and oracle scope

Representative success and malformed vectors run at every character split and
with pauses at every callback. They cover source-scalars vs UTF-16 consumption,
CRLF/surrogate carry, namespace scopes and expanded duplicates, predefined/numeric
references, comment/CDATA/PI/declaration grammar, envelopes, DTD prohibition,
exact/one-over quotas, callback-thrown parse exception identity, cancellation,
reentrancy, sink/final identity, byte mappings and explicit EOF.

A 100,000-character mapped chunk with per-callback pauses verifies mapping work
between input length and input length + callback count + 1. Mutable unconsumed
mappings are rechecked and rejected when changed. Long text emits 524,288 units
before finalization while token scratch remains at its initial capacity.
A synchronous per-thread warm regression independently measures 0 B across 10,000
repeated tags/namespace/attribute/reference/CRLF/astral dispatch appends.

The generated BCL oracle compares only concatenated text/CDATA and element count
for 100 documents with randomized chunks. A separate every-split/pause BCL oracle
compares resolved names, namespace order/values, normalized ordinary attributes,
and PI target/body, including complete target separator whitespace. Test-only
intern probes cover 2,000 distinct long shared-prefix names plus repeats, ordinal
case/culture behavior, quota-before-key-allocation, and 140 colliding initial
runtime dictionary buckets that trigger its defensive comparer transition.
No BCL or native grammar is invoked by the runtime.

## Reviews and failures preserved

Coordinator reported two independent focused source reviews PASS at
`fe5ab4451206d2318c2eed7833fb462faad7da00`: PI adjacency/whitespace and structural
oracle; incremental map/mutable-tail validation and ordinal borrowed dictionary
collision defense. These are source reviews, not executable evidence. Later
runtime change 7ea67f7 only corrects complete PI separator exclusion; a third
independent source review PASS at that full revision covers the complete initial
XML S, later whitespace, split/pause/CRLF and quota behavior. Executable oracles
above cover it too. Prior tests/verifier changes are separate in Git history.

Known-revision failures remain raw under ignored artifacts/M1-syntax:

- `checks-fe5ab445-build-failed.txt`: xUnit2013 required Assert.Single for the
  one-key test. Fixed at dcd2f51; no failed build claimed as a test pass.
- `checks-1c22f6bb41ee-1.txt`: independent package consumer reused an older
  globally cached local version. It was not a runtime grammar failure; b21b13c
  adds an isolated restore cache and the successful final consumer proves it.
- `checks-b21b13c89583-1.txt`: expanded multiple-whitespace PI oracle passed 69/70
  and exposed a real separator/body error. Fixed at 7ea67f7 and rerun 70/70.

Earlier precommit development failures were xUnit cancellation/analyzer errors,
sandbox MTP-pipe denial, one zero-test launch against the earlier assembly, and
an incorrect PI expected column (58/59 pass, corrected expected 22/23/24). Tool
transcripts preserve those outputs; none is represented as passing evidence.
Automatic approval review also timed out once on a combined edit/test command;
the edit and approved test retry were separated and subsequently completed.
The original review blockers at 8dab4f4 (PI adjacency, mapped-tail quadratic work,
linear name interning) are corrected in 19c5440/fe5ab44 and covered above.

Latency performance evidence is a separate report after the coordinator's
exclusive ShortRun window. No DOM/decoder composition, public performance victory
or cross-architecture release confirmation is claimed by these checks.
