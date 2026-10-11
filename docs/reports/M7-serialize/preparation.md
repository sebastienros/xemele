# Serializer source and measurement preparation

Current completed branch evidence is in `measurements.md` and
`final-measurement-summary.json`. Corrected source/test/probe revision
`a7022f44247f51d7201bf930e45856fcf2acadb3` passes1622 tests/API1253/publicsample;
measured revision `a80a097dc529e3f63a0d71ea8c5085fadfd4a86c` passes the full
allocation sequence and49 selected timings under exclusive grant, final identity
and cleanup0/nochild, durable RELEASE before inspection. First87 historical
timings and explicit750s timeout remain preserved. No lease or workload remains;
coordinator independently inspected all final raw artifacts; cleanup awaits integration
and durable final-report review. These newer results
supersede the ungranted/no-measurement statements in the historical section below.
Evidence-only handoff files change the closure after measurement; the successful
final freeze identifies the measured revision, not a subsequent metadata commit.

## Historical ungranted checkpoint

Runtime/test/sample/API implementation revision:
`52be6bd6f9558ed9385ba2cef842820c489347e0`.
Final prepared probe revision:
`e021177b87a92ba8805d27db72b32a43c2274883`.
The latter changes only the probe's explicit cold-first UTF8 transport allocation
domain. Runtime/test/sample/API/fixtures remain identical to the tested52be6bd.

Pinned SDK10.0.400/runtime10.0.11 offline Release builds pass with0warnings/errors.
The final combined test run1612/1612 passes (0errors/failures/skips/not-run),
including906serializer cases, earlier543legacy and113builder/tokenizer/model
cases, plus earlier XML serialization cases. Result XML/log are under
`artifacts/M7-serialize/final-52be6bd-tests.*`.
The640actual pinned native graph goldens plus direct native parse/serialize
vector-028 preserve template ordinary children; dual-graph legacy projection is
ordinary Children then TemplateContent. Html5 uses TemplateContent only. No
shared model, contract, build, other task or main file was edited.

Official1253API declarations match (1235unchanged predecessors+18exact frozen
Engine additions),3388fixtureSHA checks/bootstrapFalse and missing/added/changed
negative controls pass. Public sample passes all3APIs/bothmodes/outer node/template/
fragment/caller-owned UTF8 stream/earlier XML. Correctness-only probe validates
94prepared case semantics, Unicode/name negative controls and exact UTF8 byte
hash/length. Discovery94ShortRunNoEmit cases:60scanner/borrowedescape,
18borrowed/owned/stream composition,16fixed-name baseline/dispatch. Each has
3warmups/3actualiterations/1launch. No counter or BDN run has been executed.

On final prepared e021177, exact wrapper controls via
`python3 -B docs/reports/M7-serialize/prepare_identity.py` passed0/1/0:
positive initial/final0, no_child=true, runs=[];
unknown complete environment negative initial/final1, no_child=true, runs=[];
final positive initial/final0, no_child=true, runs=[].
The negative assertion is intentional control evidence, not a product failure.

Prepared manifest SHA256:
`8c3da026e8071cdefecf17cdfeb91c2c02f189ee6393aa5da5151926af148deb`.
It captures3941source/generated/input files,150binaries and3852SDK/runtime files,
actual host/hardware, actual prepared input JSON including raw UTF16 code units,
complete effective child environment digest/per-key opaque hashes, explicit
SHLVL1 and retained session identity. The manifest and actual inputs remain at
`artifacts/M7-serialize/{prepared,inputs}.json`. The full environment's secret
values are never printed or retained in plaintext; only approved runtime/path
settings are readable. Source plan/wrapper/freeze/controls are task-owned.

Source inspection uses indexed live child/attribute views, bounded1024-character
scans and fixed buffers; it does not establish0B allocation. The prepared probe
separates cold DOM/result, cold UTF8 stream/transport/runtime initialization,
cold cached candidates, warmed borrowed helpers/traversal, owned strings, owned
fixed UTF8 buffers, positive allocation control and retained whole-heap results.
No allocation/throughput result is claimed.

The coordinator's recorded review resolved the template correction. Worker used
bounded read_thread retrieval of that already recorded source review when the
app rejected the coordinator's outgoing correction message; local source work
remained within the original assignment. The rejected message was not retried
by the worker. At the handoff, the coordinator reports M7 coordination blocked
and explicitly states no measurement grant was issued. Worker cannot start
workloads without that grant, and cannot inspect raw outputs before durable
RELEASE. No lease is active, no measurement child exists, and no raw outputs have
been inspected.

Focused resumption: coordinator reviews/grants the committed source/probe,
worker recaptures exact controls after this evidence-only metadata commit,
coordinator records durable PARK+exclusive GRANT, worker runs the single prepared
wrapper, reports initial/allocation/94BDN/final/nochild lifecycle, coordinator
records durable RELEASE, then worker/root inspect raw results and record actual
performance evidence. No autonomous tuning or rerun. All source/probe code is
preserved; there is no outstanding correctness or shared-path request.
