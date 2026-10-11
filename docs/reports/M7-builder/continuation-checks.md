# Continuation correctness and prepared measurements

Isolated checkout `/Users/sebastienros/.codex/worktrees/464f/xemele`, branch
`codex/m7-builder-continuation`; preserves checkpoint8d2d366, claim713f91d and
baseb5a449e. Both verified prerequisite integration revisions are ancestors.
Original checkout and main remain untouched.

The fragment constructor selects RAWTEXT for scripting-enabled noscript without
inventing a last start-tag token. True/false witnesses use `a</noscript><b>x`;
true returns one literal text node. Default false parses the b element.

Pinned host SDK10.0.400/runtime10.0.11 Release test builds: zero warnings/errors.
69builder tests passed, including full unchanged62.dat corpus (14316 public-lane
executions/zero mismatches),336 original static configurations/899 assertions,
all15 template support files and both original HTML5_ELEMENTS ownership loops,
2231 named references, exact foreign visual tree predicates/NUL/charset witnesses.
44 inherited tokenizer/model/PI/parsed-text tests passed. XML outputs retained in
ignored artifacts/M7-builder/{resume-correctness,template-correctness,
standalone-expanded,full-static,preservation}.xml.

All166 resources have individual hash-checked dispositions; mixed browser/DOM/
serialization assertions remain explicit. Root approved exact non-parser API
projections; one original noscript innerHTML serialization assertion is routed to
M7-serializer and is not claimed passing. No broad script/extension exclusion.
Root supplied pinned common.js blob a1822115 after worker's missing retained
input; its original bytes and SHA5099709377292cd60778f3db339a22487c83da37be78d20b0985435ec139047f are retained with provenance.

Official unchanged checker compares1235 compiled declarations:1230 predecessor
members unchanged, four public methods plus public Html5Parser type. Brief1234
was an off-by-one inference; root confirmed1235. All3388 inventory fixtures
SHA-verified from existing retained offline inputs; bootstrapFalse; missing/added/
changed controls reject. Four-entrypoint sample builds/runs, contextual tbody
fragment and caller stream ownership checked. Generator --check reproduces all
228 labels/91711 cells; planning169 and git diff --check pass.

Failures preserved: initial template harness tried to append a second synthetic
root (test harness only; corrected to reuse fragment-owner root). A new test
initially violated xUnit2031 (corrected filtered Assert.Single overload). First
API compare failed because the checkpoint lacked its owned builder baseline;
actual emitter identified precisely five added declarations, then full union
passed. An attempted emitter invocation used unsupported `extract`; rerun with
its documented `emit` verb succeeded. No failed check is reported passing.

Prepared probes under benchmarks/Xemele.Benchmarks/M7-builder: borrowed prescan
versus mechanically retained95b1952 allocating attribute baseline (same1024window),
linear ordinal ASCII byte/char label oracle versus generated static literals,
scalar/IndexOf/small-set/cached SearchValues scans; all cover six short/long/hit/
miss/dense/multilingual fixtures. Four public entrypoints exercise output-owning
DOM, selectedcontent/PI patches, foreign content and templates. Separate allocation
sequence covers cold parser/cache/oracle setup, positive control,96x1024 warm
borrowed operations, owned DOM/stream/fragment/clone boundaries, prepared foster/
adoption N256/512/1024/2048 and inclusive retained heap. No async claim.

Correctness-only --verify passes228byte/char label cases and6prescan/scan oracles;
--discover confirms72 MemoryDiagnoser/ShortRun NoEmit cases,3warmup/3measurement/
1launch, actual.NET10.0.11Arm64. Probe/sample builds zero warnings/errors.
No allocation counter, timing or benchmark has run.

Intended sole wrapper: `python3 -B docs/reports/M7-builder/run_measurements.py
--exclusive-grant`. Wrapper mechanically reuses verified model lifecycle;
identity closure extends verified tokenizer capture to complete relevant source,
all owned preparation/generated files, runtime/test/sample/checker/probe binaries,
actual retained fixtures/corpus, SDK/runtime/host and complete effective child
environment opaque SHA per key (SHLVL1 and CODEX_THREAD_ID retained).
Plan: initial45s; allocation150s;72caseBDN1950s; final45s; total2300s; exact process
handles/groups bounded and reaped on success/failure/timeout; final identity and
no-child closure mandatory. A130s per-run reserve protects45s final identity plus
up to30s initial cleanup and40s final cleanup with15s slack. Total2300s preserves
these bounds even after full workload timeouts. Identity-only controls skip every measurement run.
Root review/PARK/durable exclusive grant precede counters; root release precedes
raw-result inspection. Timing claims remain provisional one Arm64 host.
